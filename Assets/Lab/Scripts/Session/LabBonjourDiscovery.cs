using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using UnityEngine;

namespace C6Lab
{
    public sealed class LabBonjourTarget
    {
        public string Address { get; }
        public ushort Port { get; }
        public uint InterfaceIndex { get; }
        public string ServiceName { get; }
        public string Key => ServiceName + "|" + InterfaceIndex + "|" + Address + "|" + Port;
        public LabBonjourTarget(string address, ushort port, uint interfaceIndex, string serviceName)
        { Address = address; Port = port; InterfaceIndex = interfaceIndex; ServiceName = serviceName; }
    }

    /// <summary>One visible room, possibly reachable through several network interfaces and IP families.</summary>
    public sealed class LabDiscoveredRoom
    {
        public string Key { get; }
        public string Name { get; }
        public ushort Port { get; }

        internal LabDiscoveredRoom(string name, ushort port)
        { Name = name; Port = port; Key = LabRoomCatalog.Key(name, port); }
    }

    /// <summary>Keep the room picker independent of the number of Bonjour A/AAAA records.</summary>
    public static class LabRoomCatalog
    {
        public static string Key(string name, ushort port) => port.ToString(CultureInfo.InvariantCulture) + ":" + name;

        public static IReadOnlyList<LabDiscoveredRoom> GroupTargets(IEnumerable<LabBonjourTarget> targets)
        {
            if (targets == null) return Array.Empty<LabDiscoveredRoom>();
            return targets.Where(Usable).GroupBy(target => Key(target.ServiceName, target.Port),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new LabDiscoveredRoom(group.First().ServiceName, group.First().Port))
                .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase).ThenBy(room => room.Port)
                .ToArray();
        }

        public static LabBonjourTarget[] RoutesForRoom(IEnumerable<LabBonjourTarget> targets, string roomKey)
        {
            if (targets == null || string.IsNullOrEmpty(roomKey)) return Array.Empty<LabBonjourTarget>();
            return targets.Where(target => Usable(target)
                    && string.Equals(Key(target.ServiceName, target.Port), roomKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static bool Usable(LabBonjourTarget target) => target != null
            && !string.IsNullOrWhiteSpace(target.ServiceName) && target.Port != 0
            && LabDualStackEndpoint.TryParse(target.Address, target.Port, out _, out _);
    }

    /// <summary>
    /// Local Lab room advertisement and bounded A/AAAA discovery. The result is only a
    /// connection candidate; Host approval and protocol checks still decide admission.
    /// Own on one Unity thread and Dispose when the room closes or search ends.
    /// </summary>
    public sealed class LabBonjourDiscovery : IDisposable
    {
        public const string ServiceType = "_c6lab._udp";
        public const string Protocol = "C6-LAB-5606";
        public const int MaximumServices = 32;
        public const int MaximumAddressesPerService = 32;
        public const double ResolveTimeoutSeconds = 8;
        private const string Domain = "local.";
        private const string TxtPrefix = "p=" + Protocol + ";f=";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly LabBonjourNative.RegisterReply RegisterCallback = Registered;
        private static readonly LabBonjourNative.BrowseReply BrowseCallback = Browsed;
        private static readonly LabBonjourNative.ResolveReply ResolveCallback = Resolved;
        private static readonly LabBonjourNative.AddressReply AddressCallback = Addressed;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<string, Service> services = new Dictionary<string, Service>(StringComparer.Ordinal);
        private readonly List<Operation> operations = new List<Operation>();
        private readonly Queue<string> diagnostics = new Queue<string>();
        private Operation advertisement;
        private Operation browse;
        private double now;
        private bool pumping, changed, disposed;

        public event Action Changed;
        public event Action<string> Diagnostic;
        public string LastError { get; private set; }
        public string RegisteredServiceName { get; private set; }
        public bool AdvertisingConfirmed { get; private set; }
        public bool IsAdvertising => advertisement != null && !advertisement.Done;
        public bool IsBrowsing => browse != null && !browse.Done;
        public int ActiveNativeOperations => operations.Count(x => !x.Done);
        public IReadOnlyList<LabBonjourTarget> Targets => Array.AsReadOnly(services.Values.SelectMany(s => s.Addresses.Values)
            .Select(x => x.Target).OrderBy(x => x.ServiceName, StringComparer.Ordinal).ThenBy(x => x.InterfaceIndex)
            .ThenBy(x => x.Address, StringComparer.Ordinal).ToArray());
        public static bool IsSupported => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.IPhonePlayer;

        private static byte[] BuildTxt(bool ipv4, bool ipv6)
        {
            byte[] payload = Encoding.UTF8.GetBytes(TxtPrefix + (ipv4 ? "4" : "") + (ipv6 ? "6" : ""));
            var record = new byte[payload.Length + 1];
            record[0] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, record, 1, payload.Length);
            return record;
        }

        public bool StartAdvertise(string serviceName, ushort port, bool ipv4, bool ipv6)
        {
            CheckOwner();
            // A room can be opened long after Play began. Start each native deadline at
            // registration time, before the first Update/Tick has run on this instance.
            now = Time.realtimeSinceStartupAsDouble;
            if (!IsSupported) return Unavailable();
            if (!ipv4 && !ipv6) return Unavailable();
            if (string.IsNullOrWhiteSpace(serviceName) || Encoding.UTF8.GetByteCount(serviceName) > 63 ||
                serviceName.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-') || port == 0)
                throw new ArgumentException("Use an ASCII room service label (letters, digits, hyphens; max63bytes) and a nonzero port.");
            StopAdvertisingInternal(); LastError = null;
            var operation = Create("register");
            advertisement = operation;
            byte[] txt = BuildTxt(ipv4, ipv6);
            int error = Native(() => LabBonjourNative.DNSServiceRegister(out operation.Reference, 0, 0, serviceName,
                ServiceType, Domain, null, LabBonjourNative.NetworkPort(port), (ushort)txt.Length, txt, RegisterCallback, operation.Context));
            bool accepted = Created(operation, error);
            Report("stage=register result=" + (accepted ? "started" : "failed")); Flush(); return accepted;
        }

        public bool StartBrowse()
        {
            CheckOwner();
            now = Time.realtimeSinceStartupAsDouble;
            if (!IsSupported) return Unavailable();
            StopBrowsingInternal(); LastError = null;
            var operation = Create("browse"); browse = operation;
            int error = Native(() => LabBonjourNative.DNSServiceBrowse(out operation.Reference, 0, 0, ServiceType,
                Domain, BrowseCallback, operation.Context));
            bool accepted = Created(operation, error);
            Report("stage=browse result=" + (accepted ? "started" : "failed")); Flush(); return accepted;
        }

        public void Tick() => Tick(Time.realtimeSinceStartupAsDouble);
        public void Tick(double monotonicSeconds)
        {
            CheckOwner();
            if (double.IsNaN(monotonicSeconds) || double.IsInfinity(monotonicSeconds) || monotonicSeconds < now)
                throw new ArgumentOutOfRangeException(nameof(monotonicSeconds));
            if (pumping) throw new InvalidOperationException("Lab Bonjour Tick cannot be nested.");
            now = monotonicSeconds; pumping = true;
            try
            {
                foreach (Operation operation in operations.ToArray())
                {
                    if (operation.Done) continue;
                    if ((operation.Service != null || operation.Stage == "register") && !operation.HasAddressOrReply && now >= operation.Deadline)
                    { Fail(operation, "Bonjour " + operation.Stage + " timed out."); continue; }
                    int descriptor = LabBonjourNative.DNSServiceRefSockFD(operation.Reference);
                    if (descriptor < 0) { Fail(operation, "Bonjour invalid socket."); continue; }
                    var poll = new LabBonjourNative.PollDescriptor { FileDescriptor = descriptor, Events = LabBonjourNative.PollInput };
                    int ready = LabBonjourNative.poll(ref poll, 1, 0);
                    if (ready < 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != 4) Fail(operation, "Bonjour polling error " + error + ".");
                    }
                    else if (ready > 0)
                    {
                        if ((poll.ReturnedEvents & LabBonjourNative.PollError) != 0) Fail(operation, "Bonjour socket closed.");
                        else if ((poll.ReturnedEvents & LabBonjourNative.PollInput) != 0)
                        {
                            int error = LabBonjourNative.DNSServiceProcessResult(operation.Reference);
                            if (error != 0 && !operation.Done) Fail(operation, Error(operation.Stage, error));
                        }
                    }
                }
                foreach (Service service in services.Values.ToArray())
                {
                    foreach (string key in service.Addresses.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray())
                    { service.Addresses.Remove(key); changed = true; Report("stage=address result=ttl_expired"); }
                }
            }
            finally { pumping = false; Flush(); }
        }

        public void Stop()
        {
            CheckOwner(); StopBrowsingInternal(); StopAdvertisingInternal(); Report("stage=lifecycle result=stopped"); Flush();
        }

        public void Dispose()
        {
            if (disposed) return;
            CheckOwner(); StopBrowsingInternal(); StopAdvertisingInternal();
            foreach (Operation operation in operations) operation.Done = true;
            Cleanup(); diagnostics.Clear(); Changed = null; Diagnostic = null; disposed = true;
        }

        private void StopBrowsingInternal()
        {
            if (browse != null) browse.Done = true; browse = null;
            foreach (Service service in services.Values) Cancel(service);
            services.Clear(); changed = true;
        }

        private void StopAdvertisingInternal()
        {
            if (advertisement != null) advertisement.Done = true; advertisement = null;
            AdvertisingConfirmed = false; RegisteredServiceName = null; changed = true;
        }

        private void Resolve(Service service)
        {
            var operation = Create("resolve", service); service.Resolve = operation;
            int error = Native(() => LabBonjourNative.DNSServiceResolve(out operation.Reference, 0, service.InterfaceIndex,
                service.Name, ServiceType, Domain, ResolveCallback, operation.Context));
            Created(operation, error); Report("stage=resolve result=started");
        }

        private void GetAddresses(Service service, string host)
        {
            var operation = Create("address", service); service.Address = operation;
            int error = Native(() => LabBonjourNative.DNSServiceGetAddrInfo(out operation.Reference, 0, service.InterfaceIndex,
                LabBonjourNative.IPv4 | LabBonjourNative.IPv6, host, AddressCallback, operation.Context));
            Created(operation, error); Report("stage=address result=started families=IPv4,IPv6");
        }

        private Operation Create(string stage, Service service = null)
        { var operation = new Operation(this, stage, service, now + ResolveTimeoutSeconds); operations.Add(operation); return operation; }

        private bool Created(Operation operation, int error)
        {
            if (error == 0 && operation.Reference != IntPtr.Zero) return true;
            Fail(operation, Error(operation.Stage, error)); return false;
        }

        private void Fail(Operation operation, string reason)
        {
            if (operation.Done) return;
            operation.Done = true;
            if (operation == browse) StopBrowsingInternal();
            if (operation == advertisement) StopAdvertisingInternal();
            if (operation.Service != null) Remove(operation.Service.Key);
            LastError = reason; changed = true; Report("stage=" + operation.Stage + " result=failed reason=" + reason);
        }

        private void Remove(string key)
        {
            if (!services.TryGetValue(key, out Service service)) return;
            Cancel(service); services.Remove(key); changed = true;
        }

        private static void Cancel(Service service)
        { if (service.Resolve != null) service.Resolve.Done = true; if (service.Address != null) service.Address.Done = true; }

        private void Cleanup()
        {
            if (pumping) return;
            for (int i = operations.Count - 1; i >= 0; i--)
            {
                Operation operation = operations[i]; if (!operation.Done) continue;
                if (operation.Reference != IntPtr.Zero) LabBonjourNative.DNSServiceRefDeallocate(operation.Reference);
                operation.Reference = IntPtr.Zero; if (operation.Handle.IsAllocated) operation.Handle.Free(); operations.RemoveAt(i);
            }
        }

        private void Report(string text) { if (diagnostics.Count < 128) diagnostics.Enqueue(text); }
        private void Flush()
        {
            if (pumping) return;
            Cleanup(); bool notify = changed; changed = false;
            string[] pending = diagnostics.ToArray(); diagnostics.Clear();
            if (notify) Changed?.Invoke(); foreach (string entry in pending) Diagnostic?.Invoke(entry);
        }

        private bool Unavailable()
        { LastError = "Lab Bonjour room is available on macOS and iOS."; changed = true; Report("stage=platform result=unsupported"); Flush(); return false; }

        private void CheckOwner()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabBonjourDiscovery));
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Lab Bonjour must run on its owning Unity thread.");
        }

        private static int Native(Func<int> call)
        {
            try { return call(); }
            catch (DllNotFoundException) { return -65563; }
            catch (EntryPointNotFoundException) { return -65544; }
        }

        private static string Error(string stage, int error)
            => "Bonjour " + stage + " error " + error + (error == -65570 ? ": Local Network access denied." : ".");

        private static string Utf8(IntPtr pointer, int bound = 1024)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0; while (length < bound && Marshal.ReadByte(pointer, length) != 0) length++;
            if (length >= bound) return null;
            var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length); return StrictUtf8.GetString(bytes);
        }

        private static void Guard(IntPtr context, Action<Operation> action)
        {
            if (context == IntPtr.Zero) return;
            var operation = GCHandle.FromIntPtr(context).Target as Operation;
            if (operation == null || operation.Done || operation.Owner.disposed) return;
            try { action(operation); }
            catch (Exception error) { operation.Owner.Fail(operation, "Bonjour callback error " + error.GetType().Name + "."); }
        }

        [MonoPInvokeCallback(typeof(LabBonjourNative.RegisterReply))]
        private static void Registered(IntPtr reference, uint flags, int error, IntPtr name, IntPtr type, IntPtr domain, IntPtr context)
        {
            Guard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.Fail(operation, Error("register", error)); return; }
                operation.HasAddressOrReply = true; owner.RegisteredServiceName = Utf8(name, 256); owner.AdvertisingConfirmed = (flags & LabBonjourNative.Add) != 0;
                owner.changed = true; owner.Report("stage=register result=" + (owner.AdvertisingConfirmed ? "confirmed" : "removed"));
            });
        }

        [MonoPInvokeCallback(typeof(LabBonjourNative.BrowseReply))]
        private static void Browsed(IntPtr reference, uint flags, uint interfaceIndex, int error, IntPtr name, IntPtr type, IntPtr domain, IntPtr context)
        {
            Guard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.Fail(operation, Error("browse", error)); return; }
                string serviceName = Utf8(name, 256), serviceType = Utf8(type, 128), serviceDomain = Utf8(domain, 256);
                if (string.IsNullOrEmpty(serviceName) || serviceType?.TrimEnd('.') != ServiceType ||
                    !string.Equals(serviceDomain, Domain, StringComparison.OrdinalIgnoreCase)) return;
                string key = interfaceIndex + "|" + serviceName;
                if ((flags & LabBonjourNative.Add) == 0) { owner.Remove(key); owner.Report("stage=browse result=removed"); return; }
                if (owner.services.ContainsKey(key)) return;
                if (owner.services.Count >= MaximumServices) { owner.Report("stage=browse result=capacity_reached"); return; }
                var service = new Service { Name = serviceName, InterfaceIndex = interfaceIndex, Key = key };
                owner.services.Add(key, service); owner.Report("stage=browse result=found"); owner.Resolve(service);
            });
        }

        [MonoPInvokeCallback(typeof(LabBonjourNative.ResolveReply))]
        private static void Resolved(IntPtr reference, uint flags, uint interfaceIndex, int error, IntPtr fullName, IntPtr host,
            ushort port, ushort length, IntPtr txt, IntPtr context)
        {
            Guard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.Fail(operation, Error("resolve", error)); return; }
                string hostName = Utf8(host); Service service = operation.Service;
                if (string.IsNullOrEmpty(hostName) || txt == IntPtr.Zero || length <= TxtPrefix.Length + 1 || length > 64
                    || LabBonjourNative.NetworkPort(port) == 0)
                { owner.Fail(operation, "Bonjour room metadata invalid."); return; }
                var record = new byte[length]; Marshal.Copy(txt, record, 0, length);
                if (record[0] != length - 1 || !StrictUtf8.GetString(record, 1, length - 1).StartsWith(TxtPrefix, StringComparison.Ordinal))
                { owner.Fail(operation, "Bonjour room protocol mismatch."); return; }
                string families = StrictUtf8.GetString(record, 1 + TxtPrefix.Length, length - 1 - TxtPrefix.Length);
                if (families != "4" && families != "6" && families != "46")
                { owner.Fail(operation, "Bonjour room family metadata invalid."); return; }
                service.Ipv4 = families.Contains("4");
                service.Ipv6 = families.Contains("6");
                service.Port = LabBonjourNative.NetworkPort(port); operation.HasAddressOrReply = true; operation.Done = true;
                owner.Report("stage=resolve result=completed"); owner.GetAddresses(service, hostName);
            });
        }

        [MonoPInvokeCallback(typeof(LabBonjourNative.AddressReply))]
        private static void Addressed(IntPtr reference, uint flags, uint interfaceIndex, int error, IntPtr host, IntPtr address, uint ttl, IntPtr context)
        {
            Guard(context, operation =>
            {
                var owner = operation.Owner;
                // A missing record in one family is not failure of the other family.
                if (error == -65554) { owner.Report("stage=address result=one_family_no_record"); return; }
                if (error != 0) { owner.Fail(operation, Error("address", error)); return; }
                Service service = operation.Service;
                uint routeInterface = interfaceIndex == 0 ? service.InterfaceIndex : interfaceIndex;
                if (!LabBonjourAddressCodec.TryDecode(address, routeInterface, out string candidate))
                { owner.Report("stage=address result=unusable_record"); return; }
                bool ipv6 = LabBonjourAddressCodec.Family(candidate) == "IPv6";
                if (ipv6 ? !service.Ipv6 : !service.Ipv4) return;
                string key = routeInterface + "|" + candidate;
                if ((flags & LabBonjourNative.Add) == 0 || ttl == 0)
                { if (service.Addresses.Remove(key)) owner.changed = true; owner.Report("stage=address result=removed"); return; }
                if (!service.Addresses.ContainsKey(key) && service.Addresses.Count >= MaximumAddressesPerService)
                { owner.Report("stage=address result=capacity_reached"); return; }
                service.Addresses[key] = new AddressRecord(new LabBonjourTarget(candidate, service.Port, routeInterface, service.Name), owner.now + ttl);
                operation.HasAddressOrReply = true; owner.changed = true;
                owner.Report("stage=address result=ready family=" + LabBonjourAddressCodec.Family(candidate));
            });
        }

        private sealed class Operation
        {
            internal readonly LabBonjourDiscovery Owner; internal readonly string Stage; internal readonly Service Service;
            internal readonly double Deadline; internal GCHandle Handle; internal IntPtr Reference; internal bool Done, HasAddressOrReply;
            internal IntPtr Context => GCHandle.ToIntPtr(Handle);
            internal Operation(LabBonjourDiscovery owner, string stage, Service service, double deadline)
            { Owner = owner; Stage = stage; Service = service; Deadline = deadline; Handle = GCHandle.Alloc(this); }
        }

        private sealed class Service
        {
            internal string Key, Name; internal uint InterfaceIndex; internal ushort Port; internal bool Ipv4, Ipv6; internal Operation Resolve, Address;
            internal readonly Dictionary<string, AddressRecord> Addresses = new Dictionary<string, AddressRecord>(StringComparer.Ordinal);
        }
        private sealed class AddressRecord
        {
            internal readonly LabBonjourTarget Target; internal readonly double ExpiresAt;
            internal AddressRecord(LabBonjourTarget target, double expiresAt) { Target = target; ExpiresAt = expiresAt; }
        }
    }
}
