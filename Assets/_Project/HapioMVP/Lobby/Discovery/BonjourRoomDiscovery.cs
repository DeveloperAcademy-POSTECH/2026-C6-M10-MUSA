using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using UnityEngine;

namespace C6.Prototype.Lobby.Discovery
{
    /// <summary>
    /// Real Apple Bonjour registration, browsing, TXT monitoring and interface-specific IPv4 resolution.
    /// Own this object on one Unity thread, call Tick every frame, and Dispose on session teardown.
    /// No network callback enters gameplay and no advertised metadata authorizes a participant.
    /// </summary>
    public sealed class BonjourRoomDiscovery : IDisposable
    {
        public const string ServiceType = "_c6hapio._udp";
        public const string LocalDomain = "local.";
        // DEMO_TUNING_VALUE: metadata heartbeat 3 s, advertised-room expiry 12 s, resolution deadline 8 s.
        public const double HeartbeatSeconds = 3;
        public const double ResolveTimeoutSeconds = 8;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly DiscoveryRoomCatalog catalog = new DiscoveryRoomCatalog();
        private readonly List<Operation> operations = new List<Operation>();
        private readonly Dictionary<string, Service> services = new Dictionary<string, Service>(StringComparer.Ordinal);
        private readonly Queue<string> failures = new Queue<string>();
        private Operation browse;
        private Operation registration;
        private RoomAdvertisement advertisedRoom;
        private ulong heartbeat;
        private double now;
        private double nextHeartbeat;
        private bool changed;
        private bool disposed;
        private bool pumping;

        private static readonly BonjourNative.BrowseReply BrowseCallback = OnBrowse;
        private static readonly BonjourNative.RegisterReply RegisterCallback = OnRegistered;
        private static readonly BonjourNative.ResolveReply ResolveCallback = OnResolved;
        private static readonly BonjourNative.QueryReply QueryCallback = OnTxt;
        private static readonly BonjourNative.AddressReply AddressCallback = OnAddress;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public event Action Changed;
        public event Action<string> Failed;
        public IReadOnlyList<DiscoveredRoom> Rooms => catalog.Rooms;
        public bool IsBrowsing => browse != null && !browse.Done;
        public bool IsAdvertising => registration != null && !registration.Done;
        public bool AdvertisingConfirmed { get; private set; }
        public string RegisteredServiceName { get; private set; }
        public string LastError { get; private set; }
        public int ActiveNativeOperations => operations.Count(x => !x.Done);
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR)
                return true;
#else
                return false;
#endif
            }
        }

        public bool StartBrowse()
        {
            CheckOwner();
            if (!Available()) return false;
            if (IsBrowsing) return true;
            LastError = null;
            var operation = NewOperation("browse");
            int error = SafeCall(() => BonjourNative.DNSServiceBrowse(out operation.Reference, 0, 0,
                ServiceType, LocalDomain, BrowseCallback, operation.Context));
            if (!CompleteCreation(operation, error)) { Flush(); return false; }
            browse = operation;
            changed = true;
            Flush();
            return true;
        }

        public void StopBrowse()
        {
            CheckOwner();
            StopBrowseInternal();
            Flush();
        }

        public bool Refresh()
        {
            CheckOwner();
            StopBrowseInternal();
            return StartBrowse();
        }

        public bool Advertise(RoomAdvertisement room)
        {
            CheckOwner();
            if (!Available()) return false;
            if (!RoomAdvertisementCodec.Validate(room, out string reason)) { Fail(reason); Flush(); return false; }
            StopAdvertisingInternal();
            LastError = null;
            advertisedRoom = room.Copy();
            heartbeat = 0;
            byte[] txt = RoomAdvertisementCodec.Encode(advertisedRoom, heartbeat);
            var operation = NewOperation("register");
            // RoomId is used as the DNS instance label; the user-facing Name is carried in bounded TXT.
            string instance = "C6-" + room.RoomId;
            if (Encoding.UTF8.GetByteCount(instance) > 63) instance = "C6-" + room.RoomId.Substring(0, 60);
            int error = SafeCall(() => BonjourNative.DNSServiceRegister(out operation.Reference, 0, 0,
                instance, ServiceType, LocalDomain, null, BonjourNative.NetworkPort(room.Port),
                (ushort)txt.Length, txt, RegisterCallback, operation.Context));
            if (!CompleteCreation(operation, error)) { advertisedRoom = null; Flush(); return false; }
            registration = operation;
            nextHeartbeat = now + HeartbeatSeconds;
            changed = true;
            Flush();
            return true;
        }

        public bool UpdateAdvertisement(RoomAdvertisement room)
        {
            CheckOwner();
            if (!RoomAdvertisementCodec.Validate(room, out string reason)) { Fail(reason); Flush(); return false; }
            if (!IsAdvertising) return Advertise(room);
            if (room.RoomId != advertisedRoom.RoomId || room.Port != advertisedRoom.Port) return Advertise(room);
            advertisedRoom = room.Copy();
            bool ok = SendHeartbeat();
            Flush();
            return ok;
        }

        public void StopAdvertising()
        {
            CheckOwner();
            StopAdvertisingInternal();
            Flush();
        }

        public void Tick() => Tick(Time.realtimeSinceStartupAsDouble);

        public void Tick(double monotonicSeconds)
        {
            CheckOwner();
            if (double.IsNaN(monotonicSeconds) || double.IsInfinity(monotonicSeconds) || monotonicSeconds < now)
                throw new ArgumentOutOfRangeException(nameof(monotonicSeconds), "Discovery requires a nondecreasing monotonic clock.");
            if (pumping) throw new InvalidOperationException("Bonjour Tick cannot be nested.");
            now = monotonicSeconds;
            pumping = true;
            try
            {
                foreach (Operation operation in operations.ToArray())
                {
                    if (operation.Done) continue;
                    if (operation.Kind == "resolve" && now - operation.CreatedAt >= ResolveTimeoutSeconds)
                    {
                        FailOperation(operation, "Bonjour resolution timed out. Refresh the room list and check local network permission.");
                        continue;
                    }
                    int descriptor = BonjourNative.DNSServiceRefSockFD(operation.Reference);
                    if (descriptor < 0) { FailOperation(operation, "Bonjour returned an invalid socket."); continue; }
                    var poll = new BonjourNative.PollDescriptor { FileDescriptor = descriptor, Events = BonjourNative.PollInput };
                    int ready = BonjourNative.poll(ref poll, 1, 0);
                    if (ready < 0)
                    {
                        // EINTR is harmless; the next frame checks the socket again.
                        int error = Marshal.GetLastWin32Error();
                        if (error != 4) FailOperation(operation, "Bonjour socket polling failed (" + error + ").");
                    }
                    else if (ready > 0)
                    {
                        if ((poll.ReturnedEvents & BonjourNative.PollError) != 0)
                            FailOperation(operation, "Bonjour service connection closed. Refresh or create the room again.");
                        else if ((poll.ReturnedEvents & BonjourNative.PollInput) != 0)
                        {
                            int error = BonjourNative.DNSServiceProcessResult(operation.Reference);
                            if (error != 0) FailOperation(operation, DescribeError(operation.Kind, error));
                        }
                    }
                }
                foreach (Service service in services.Values.ToArray())
                    if (service.Advertisement != null && now - service.LastTxtAt >= DiscoveryRoomCatalog.DefaultLifetimeSeconds)
                        changed |= catalog.Remove(service.Key);
                changed |= catalog.Expire(now) > 0;
                if (IsAdvertising && now >= nextHeartbeat) SendHeartbeat();
            }
            finally { pumping = false; Flush(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            CheckOwner();
            StopBrowseInternal();
            StopAdvertisingInternal();
            foreach (Operation operation in operations) operation.Done = true;
            Cleanup();
            failures.Clear();
            Changed = null;
            Failed = null;
            disposed = true;
        }

        private bool SendHeartbeat()
        {
            if (!IsAdvertising || advertisedRoom == null) return false;
            byte[] txt = RoomAdvertisementCodec.Encode(advertisedRoom, ++heartbeat);
            int error = SafeCall(() => BonjourNative.DNSServiceUpdateRecord(registration.Reference, IntPtr.Zero, 0,
                (ushort)txt.Length, txt, 0));
            nextHeartbeat = now + HeartbeatSeconds;
            if (error == 0) return true;
            FailOperation(registration, DescribeError("update", error));
            return false;
        }

        private void StopBrowseInternal()
        {
            if (browse != null) browse.Done = true;
            browse = null;
            foreach (Service service in services.Values) CancelService(service);
            services.Clear();
            changed |= catalog.Clear();
            changed = true;
        }

        private void StopAdvertisingInternal()
        {
            if (registration != null) registration.Done = true;
            registration = null;
            advertisedRoom = null;
            AdvertisingConfirmed = false;
            RegisteredServiceName = null;
            changed = true;
        }

        private void StartResolve(Service service)
        {
            var operation = NewOperation("resolve", service);
            service.Resolve = operation;
            int error = SafeCall(() => BonjourNative.DNSServiceResolve(out operation.Reference, 0, service.InterfaceIndex,
                service.Name, ServiceType, service.Domain, ResolveCallback, operation.Context));
            if (!CompleteCreation(operation, error)) RemoveService(service.Key);
        }

        private void StartAddressAndTxt(Service service, string fullName, string hostName)
        {
            var address = NewOperation("address", service);
            service.AddressOperation = address;
            int error = SafeCall(() => BonjourNative.DNSServiceGetAddrInfo(out address.Reference, 0, service.InterfaceIndex,
                BonjourNative.IPv4, hostName, AddressCallback, address.Context));
            if (!CompleteCreation(address, error)) { RemoveService(service.Key); return; }
            var query = NewOperation("TXT query", service);
            service.Query = query;
            error = SafeCall(() => BonjourNative.DNSServiceQueryRecord(out query.Reference, 0, service.InterfaceIndex,
                fullName, BonjourNative.TxtType, BonjourNative.InternetClass, QueryCallback, query.Context));
            if (!CompleteCreation(query, error)) RemoveService(service.Key);
        }

        private void AcceptTxt(Service service, ushort length, IntPtr bytes)
        {
            if (bytes == IntPtr.Zero || length == 0 || length > RoomAdvertisementCodec.MaximumRecordBytes)
            { changed |= catalog.Remove(service.Key); return; }
            var record = new byte[length];
            Marshal.Copy(bytes, record, 0, length);
            if (!RoomAdvertisementCodec.TryDecode(record, service.Port, out RoomAdvertisement room, out _))
            { service.Advertisement = null; changed |= catalog.Remove(service.Key); return; }
            service.Advertisement = room;
            service.LastTxtAt = now;
            Publish(service);
        }

        private void Publish(Service service)
        {
            if (service.Advertisement == null || service.Addresses.Count == 0 ||
                now - service.LastTxtAt >= DiscoveryRoomCatalog.DefaultLifetimeSeconds) return;
            changed |= catalog.Upsert(service.Key, service.Advertisement, service.Addresses.Min, now);
        }

        private void RemoveService(string key)
        {
            if (services.TryGetValue(key, out Service service)) { CancelService(service); services.Remove(key); }
            changed |= catalog.Remove(key);
        }

        private static void CancelService(Service service)
        {
            service.Active = false;
            if (service.Resolve != null) service.Resolve.Done = true;
            if (service.Query != null) service.Query.Done = true;
            if (service.AddressOperation != null) service.AddressOperation.Done = true;
        }

        private Operation NewOperation(string kind, Service service = null)
        {
            var operation = new Operation(this, kind, service, now);
            operations.Add(operation);
            return operation;
        }

        private bool CompleteCreation(Operation operation, int error)
        {
            if (error == 0 && operation.Reference != IntPtr.Zero) return true;
            FailOperation(operation, DescribeError(operation.Kind, error));
            return false;
        }

        private void FailOperation(Operation operation, string message)
        {
            operation.Done = true;
            if (operation == browse) StopBrowseInternal();
            if (operation == registration) StopAdvertisingInternal();
            if (operation.Service != null) RemoveService(operation.Service.Key);
            Fail(message);
        }

        private void Fail(string message)
        {
            LastError = message;
            if (failures.Count < 16) failures.Enqueue(message);
            changed = true;
        }

        private void Cleanup()
        {
            if (pumping) return;
            for (int i = operations.Count - 1; i >= 0; i--)
            {
                Operation operation = operations[i];
                if (!operation.Done) continue;
                if (operation.Reference != IntPtr.Zero)
                { BonjourNative.DNSServiceRefDeallocate(operation.Reference); operation.Reference = IntPtr.Zero; }
                if (operation.Handle.IsAllocated) operation.Handle.Free();
                operations.RemoveAt(i);
            }
        }

        private void Flush()
        {
            if (pumping) return;
            Cleanup();
            bool notify = changed;
            changed = false;
            string[] errors = failures.ToArray();
            failures.Clear();
            if (notify) Changed?.Invoke();
            foreach (string error in errors) Failed?.Invoke(error);
        }

        private bool Available()
        {
            if (IsSupported) return true;
            Fail("Bonjour room discovery is available on macOS and iOS in this prototype.");
            Flush();
            return false;
        }

        private void CheckOwner()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BonjourRoomDiscovery));
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Bonjour operations must run on their owning Unity thread.");
        }

        private static int SafeCall(Func<int> action)
        {
            try { return action(); }
            catch (DllNotFoundException) { return -65563; }
            catch (EntryPointNotFoundException) { return -65544; }
        }

        private static string DescribeError(string operation, int error)
        {
            string hint = error == -65570 ? " Local network access was denied; allow access in Settings and refresh."
                : error == -65563 ? " Bonjour system service or native library is unavailable."
                : error == -65548 ? " The room service name is already in use."
                : " Check local network access and refresh the room list.";
            return "Bonjour " + operation + " failed (" + error + ")." + hint;
        }

        private static Operation Context(IntPtr context)
        {
            if (context == IntPtr.Zero) return null;
            return GCHandle.FromIntPtr(context).Target as Operation;
        }

        private static string ReadUtf8(IntPtr pointer, int maximum = 1024)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0;
            while (length < maximum && Marshal.ReadByte(pointer, length) != 0) length++;
            if (length == maximum) return null;
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return StrictUtf8.GetString(bytes);
        }

        private static void CallbackGuard(IntPtr context, Action<Operation> action)
        {
            Operation operation = Context(context);
            if (operation == null || operation.Done || operation.Owner.disposed) return;
            try { action(operation); }
            catch (Exception exception)
            {
                // No managed exception may cross a native callback / IL2CPP reverse P/Invoke boundary.
                operation.Owner.FailOperation(operation, "Bonjour response could not be processed: " + exception.GetType().Name + ".");
            }
        }

        [MonoPInvokeCallback(typeof(BonjourNative.BrowseReply))]
        private static void OnBrowse(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr serviceName, IntPtr type, IntPtr domain, IntPtr context)
        {
            CallbackGuard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.FailOperation(operation, DescribeError("browse", error)); return; }
                string name = ReadUtf8(serviceName, 256);
                string returnedType = ReadUtf8(type, 128);
                string returnedDomain = ReadUtf8(domain, 256);
                if (string.IsNullOrEmpty(name) || returnedType?.TrimEnd('.') != ServiceType ||
                    !string.Equals(returnedDomain, LocalDomain, StringComparison.OrdinalIgnoreCase)) return;
                string key = interfaceIndex + "|" + name + "|" + returnedDomain;
                if ((flags & BonjourNative.Add) == 0) { owner.RemoveService(key); return; }
                if (owner.services.ContainsKey(key) || owner.services.Count >= DiscoveryRoomCatalog.MaximumServices) return;
                var service = new Service { Key = key, Name = name, Domain = returnedDomain, InterfaceIndex = interfaceIndex };
                owner.services.Add(key, service);
                owner.StartResolve(service);
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.RegisterReply))]
        private static void OnRegistered(IntPtr reference, uint flags, int error,
            IntPtr serviceName, IntPtr type, IntPtr domain, IntPtr context)
        {
            CallbackGuard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.FailOperation(operation, DescribeError("register", error)); return; }
                owner.AdvertisingConfirmed = (flags & BonjourNative.Add) != 0;
                owner.RegisteredServiceName = ReadUtf8(serviceName, 256);
                owner.changed = true;
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.ResolveReply))]
        private static void OnResolved(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr fullName, IntPtr hostName, ushort networkPort, ushort txtLength, IntPtr txt, IntPtr context)
        {
            CallbackGuard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.FailOperation(operation, DescribeError("resolve", error)); return; }
                Service service = operation.Service;
                if (!service.Active) return;
                string resolvedName = ReadUtf8(fullName);
                string resolvedHost = ReadUtf8(hostName);
                if (string.IsNullOrEmpty(resolvedName) || string.IsNullOrEmpty(resolvedHost))
                { owner.RemoveService(service.Key); return; }
                service.Port = BonjourNative.NetworkPort(networkPort);
                owner.AcceptTxt(service, txtLength, txt);
                operation.Done = true;
                owner.StartAddressAndTxt(service, resolvedName, resolvedHost);
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.QueryReply))]
        private static void OnTxt(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr fullName, ushort recordType, ushort recordClass, ushort length, IntPtr bytes, uint ttl, IntPtr context)
        {
            CallbackGuard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.FailOperation(operation, DescribeError("TXT query", error)); return; }
                Service service = operation.Service;
                if (!service.Active || recordType != BonjourNative.TxtType || recordClass != BonjourNative.InternetClass) return;
                // TXT updates can emit an old-record removal after a new-record add. Do not erase the
                // fresh room during replacement; PTR goodbye removes it immediately, otherwise the
                // last received TXT heartbeat expires after 12 s. Removed TXT never extends that lease.
                if ((flags & BonjourNative.Add) == 0 || ttl == 0) return;
                owner.AcceptTxt(service, length, bytes);
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.AddressReply))]
        private static void OnAddress(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr hostName, IntPtr address, uint ttl, IntPtr context)
        {
            CallbackGuard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.FailOperation(operation, DescribeError("IPv4 resolve", error)); return; }
                Service service = operation.Service;
                if (!service.Active || address == IntPtr.Zero) return;
                // Darwin sockaddr_in: byte length, byte AF_INET (2), ushort port, 4 network-order address bytes.
                if (Marshal.ReadByte(address, 0) < 8 || Marshal.ReadByte(address, 1) != 2) return;
                byte[] bytes = new byte[4];
                Marshal.Copy(IntPtr.Add(address, 4), bytes, 0, 4);
                string ipv4 = new IPAddress(bytes).ToString();
                if (!RoomAdvertisementCodec.IsUsableIPv4(ipv4)) return;
                if ((flags & BonjourNative.Add) != 0 && ttl != 0) service.Addresses.Add(ipv4);
                else service.Addresses.Remove(ipv4);
                if (service.Addresses.Count == 0) owner.changed |= owner.catalog.Remove(service.Key);
                else owner.Publish(service);
            });
        }

        private sealed class Operation
        {
            internal readonly BonjourRoomDiscovery Owner;
            internal readonly string Kind;
            internal readonly Service Service;
            internal readonly double CreatedAt;
            internal GCHandle Handle;
            internal IntPtr Reference;
            internal bool Done;
            internal IntPtr Context => GCHandle.ToIntPtr(Handle);
            internal Operation(BonjourRoomDiscovery owner, string kind, Service service, double createdAt)
            {
                Owner = owner; Kind = kind; Service = service; CreatedAt = createdAt;
                Handle = GCHandle.Alloc(this);
            }
        }

        private sealed class Service
        {
            internal string Key, Name, Domain;
            internal uint InterfaceIndex;
            internal ushort Port;
            internal bool Active = true;
            internal double LastTxtAt;
            internal RoomAdvertisement Advertisement;
            internal readonly SortedSet<string> Addresses = new SortedSet<string>(StringComparer.Ordinal);
            internal Operation Resolve, Query, AddressOperation;
        }
    }
}
