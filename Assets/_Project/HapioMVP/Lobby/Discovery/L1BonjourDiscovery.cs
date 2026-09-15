using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using UnityEngine;

namespace C6.Prototype.Lobby.Discovery
{
    public sealed class L1BonjourTarget
    {
        public string Address { get; }
        public ushort Port { get; }
        public uint InterfaceIndex { get; }
        public string ServiceName { get; }
        public string Key => ServiceName + "|" + InterfaceIndex + "|" + Address + "|" + Port;
        internal L1BonjourTarget(string address, ushort port, uint interfaceIndex, string serviceName)
        { Address = address; Port = port; InterfaceIndex = interfaceIndex; ServiceName = serviceName; }
    }

    /// <summary>
    /// Isolated L1 probe: list every bounded A/AAAA route, preserving link-local scope. No game
    /// admission, production catalog, heartbeat, retry policy or transport settings are changed.
    /// Own on one Unity thread and Dispose when the probe leaves the foreground.
    /// </summary>
    public sealed class L1BonjourDiscovery : IDisposable
    {
        public const string ServiceType = "_c6l1._udp";
        public const string Protocol = "C6-L1-1";
        public const int MaximumServices = 32;
        public const int MaximumAddressesPerService = 32;
        public const double ResolveTimeoutSeconds = 8;
        private const string Domain = "local.";
        private static readonly byte[] Txt = Encoding.UTF8.GetBytes("\u0009p=" + Protocol);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly BonjourNative.RegisterReply RegisterCallback = Registered;
        private static readonly BonjourNative.BrowseReply BrowseCallback = Browsed;
        private static readonly BonjourNative.ResolveReply ResolveCallback = Resolved;
        private static readonly BonjourNative.AddressReply AddressCallback = Addressed;
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
        public IReadOnlyList<L1BonjourTarget> Targets => Array.AsReadOnly(services.Values.SelectMany(s => s.Addresses.Values)
            .Select(x => x.Target).OrderBy(x => x.ServiceName, StringComparer.Ordinal).ThenBy(x => x.InterfaceIndex)
            .ThenBy(x => x.Address, StringComparer.Ordinal).ToArray());
        public static bool IsSupported => BonjourRoomDiscovery.IsSupported;

        public bool StartAdvertise(string serviceName, ushort port)
        {
            CheckOwner();
            if (!IsSupported) return Unavailable();
            if (string.IsNullOrWhiteSpace(serviceName) || Encoding.UTF8.GetByteCount(serviceName) > 63 ||
                serviceName.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-') || port == 0)
                throw new ArgumentException("Use an ASCII probe service label (letters, digits, hyphens; max63bytes) and a nonzero port.");
            StopAdvertisingInternal(); LastError = null;
            var operation = Create("register");
            advertisement = operation;
            int error = Native(() => BonjourNative.DNSServiceRegister(out operation.Reference, 0, 0, serviceName,
                ServiceType, Domain, null, BonjourNative.NetworkPort(port), (ushort)Txt.Length, Txt, RegisterCallback, operation.Context));
            bool accepted = Created(operation, error);
            Report("stage=register result=" + (accepted ? "started" : "failed")); Flush(); return accepted;
        }

        public bool StartBrowse()
        {
            CheckOwner();
            if (!IsSupported) return Unavailable();
            StopBrowsingInternal(); LastError = null;
            var operation = Create("browse"); browse = operation;
            int error = Native(() => BonjourNative.DNSServiceBrowse(out operation.Reference, 0, 0, ServiceType,
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
            if (pumping) throw new InvalidOperationException("L1 Bonjour Tick cannot be nested.");
            now = monotonicSeconds; pumping = true;
            try
            {
                foreach (Operation operation in operations.ToArray())
                {
                    if (operation.Done) continue;
                    if ((operation.Service != null || operation.Stage == "register") && !operation.HasAddressOrReply && now >= operation.Deadline)
                    { Fail(operation, "Bonjour " + operation.Stage + " timed out."); continue; }
                    int descriptor = BonjourNative.DNSServiceRefSockFD(operation.Reference);
                    if (descriptor < 0) { Fail(operation, "Bonjour invalid socket."); continue; }
                    var poll = new BonjourNative.PollDescriptor { FileDescriptor = descriptor, Events = BonjourNative.PollInput };
                    int ready = BonjourNative.poll(ref poll, 1, 0);
                    if (ready < 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != 4) Fail(operation, "Bonjour polling error " + error + ".");
                    }
                    else if (ready > 0)
                    {
                        if ((poll.ReturnedEvents & BonjourNative.PollError) != 0) Fail(operation, "Bonjour socket closed.");
                        else if ((poll.ReturnedEvents & BonjourNative.PollInput) != 0)
                        {
                            int error = BonjourNative.DNSServiceProcessResult(operation.Reference);
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
            int error = Native(() => BonjourNative.DNSServiceResolve(out operation.Reference, 0, service.InterfaceIndex,
                service.Name, ServiceType, Domain, ResolveCallback, operation.Context));
            Created(operation, error); Report("stage=resolve result=started");
        }

        private void GetAddresses(Service service, string host)
        {
            var operation = Create("address", service); service.Address = operation;
            int error = Native(() => BonjourNative.DNSServiceGetAddrInfo(out operation.Reference, 0, service.InterfaceIndex,
                BonjourNative.IPv4 | BonjourNative.IPv6, host, AddressCallback, operation.Context));
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
                if (operation.Reference != IntPtr.Zero) BonjourNative.DNSServiceRefDeallocate(operation.Reference);
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
        { LastError = "L1 Bonjour probe is available on macOS and iOS."; changed = true; Report("stage=platform result=unsupported"); Flush(); return false; }

        private void CheckOwner()
        {
            if (disposed) throw new ObjectDisposedException(nameof(L1BonjourDiscovery));
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("L1 Bonjour must run on its owning Unity thread.");
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

        [MonoPInvokeCallback(typeof(BonjourNative.RegisterReply))]
        private static void Registered(IntPtr reference, uint flags, int error, IntPtr name, IntPtr type, IntPtr domain, IntPtr context)
        {
            Guard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.Fail(operation, Error("register", error)); return; }
                operation.HasAddressOrReply = true; owner.RegisteredServiceName = Utf8(name, 256); owner.AdvertisingConfirmed = (flags & BonjourNative.Add) != 0;
                owner.changed = true; owner.Report("stage=register result=" + (owner.AdvertisingConfirmed ? "confirmed" : "removed"));
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.BrowseReply))]
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
                if ((flags & BonjourNative.Add) == 0) { owner.Remove(key); owner.Report("stage=browse result=removed"); return; }
                if (owner.services.ContainsKey(key)) return;
                if (owner.services.Count >= MaximumServices) { owner.Report("stage=browse result=capacity_reached"); return; }
                var service = new Service { Name = serviceName, InterfaceIndex = interfaceIndex, Key = key };
                owner.services.Add(key, service); owner.Report("stage=browse result=found"); owner.Resolve(service);
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.ResolveReply))]
        private static void Resolved(IntPtr reference, uint flags, uint interfaceIndex, int error, IntPtr fullName, IntPtr host,
            ushort port, ushort length, IntPtr txt, IntPtr context)
        {
            Guard(context, operation =>
            {
                var owner = operation.Owner;
                if (error != 0) { owner.Fail(operation, Error("resolve", error)); return; }
                string hostName = Utf8(host); Service service = operation.Service;
                if (string.IsNullOrEmpty(hostName) || txt == IntPtr.Zero || length != Txt.Length || BonjourNative.NetworkPort(port) == 0)
                { owner.Fail(operation, "Bonjour probe metadata invalid."); return; }
                var record = new byte[length]; Marshal.Copy(txt, record, 0, length);
                if (!record.SequenceEqual(Txt)) { owner.Fail(operation, "Bonjour probe protocol mismatch."); return; }
                service.Port = BonjourNative.NetworkPort(port); operation.HasAddressOrReply = true; operation.Done = true;
                owner.Report("stage=resolve result=completed"); owner.GetAddresses(service, hostName);
            });
        }

        [MonoPInvokeCallback(typeof(BonjourNative.AddressReply))]
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
                if (!L1BonjourAddressCodec.TryDecode(address, routeInterface, out string candidate))
                { owner.Report("stage=address result=unusable_record"); return; }
                string key = routeInterface + "|" + candidate;
                if ((flags & BonjourNative.Add) == 0 || ttl == 0)
                { if (service.Addresses.Remove(key)) owner.changed = true; owner.Report("stage=address result=removed"); return; }
                if (!service.Addresses.ContainsKey(key) && service.Addresses.Count >= MaximumAddressesPerService)
                { owner.Report("stage=address result=capacity_reached"); return; }
                service.Addresses[key] = new AddressRecord(new L1BonjourTarget(candidate, service.Port, routeInterface, service.Name), owner.now + ttl);
                operation.HasAddressOrReply = true; owner.changed = true;
                owner.Report("stage=address result=ready family=" + L1BonjourAddressCodec.Family(candidate));
            });
        }

        private sealed class Operation
        {
            internal readonly L1BonjourDiscovery Owner; internal readonly string Stage; internal readonly Service Service;
            internal readonly double Deadline; internal GCHandle Handle; internal IntPtr Reference; internal bool Done, HasAddressOrReply;
            internal IntPtr Context => GCHandle.ToIntPtr(Handle);
            internal Operation(L1BonjourDiscovery owner, string stage, Service service, double deadline)
            { Owner = owner; Stage = stage; Service = service; Deadline = deadline; Handle = GCHandle.Alloc(this); }
        }

        private sealed class Service
        {
            internal string Key, Name; internal uint InterfaceIndex; internal ushort Port; internal Operation Resolve, Address;
            internal readonly Dictionary<string, AddressRecord> Addresses = new Dictionary<string, AddressRecord>(StringComparer.Ordinal);
        }
        private sealed class AddressRecord
        {
            internal readonly L1BonjourTarget Target; internal readonly double ExpiresAt;
            internal AddressRecord(L1BonjourTarget target, double expiresAt) { Target = target; ExpiresAt = expiresAt; }
        }
    }
}
