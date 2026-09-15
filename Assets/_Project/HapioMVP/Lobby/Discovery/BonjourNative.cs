using System;
using System.Runtime.InteropServices;

namespace C6.Prototype.Lobby.Discovery
{
    // Signatures are from Xcode 26.6 / macOS & iPhoneOS SDK dns_sd.h.
    // No worker thread or dispatch queue: callbacks run inside ProcessResult on the calling Unity thread.
    internal static class BonjourNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string DnsLibrary = "__Internal";
        private const string SystemLibrary = "__Internal";
#else
        private const string DnsLibrary = "/usr/lib/libSystem.B.dylib";
        private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
#endif
        internal const uint Add = 2;
        internal const uint IPv4 = 1;
        internal const ushort TxtType = 16;
        internal const ushort InternetClass = 1;
        internal const short PollInput = 1;
        internal const short PollError = 8 | 16 | 32;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void BrowseReply(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr serviceName, IntPtr type, IntPtr domain, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void RegisterReply(IntPtr reference, uint flags, int error,
            IntPtr serviceName, IntPtr type, IntPtr domain, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ResolveReply(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr fullName, IntPtr hostName, ushort networkPort, ushort txtLength, IntPtr txt, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void QueryReply(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr fullName, ushort recordType, ushort recordClass, ushort length, IntPtr bytes, uint ttl, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AddressReply(IntPtr reference, uint flags, uint interfaceIndex, int error,
            IntPtr hostName, IntPtr address, uint ttl, IntPtr context);

        [StructLayout(LayoutKind.Sequential)]
        internal struct PollDescriptor { public int FileDescriptor; public short Events; public short ReturnedEvents; }

        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceBrowse(out IntPtr reference, uint flags, uint interfaceIndex,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string type, [MarshalAs(UnmanagedType.LPUTF8Str)] string domain,
            BrowseReply callback, IntPtr context);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceRegister(out IntPtr reference, uint flags, uint interfaceIndex,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string type,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string domain, [MarshalAs(UnmanagedType.LPUTF8Str)] string host,
            ushort networkPort, ushort txtLength, byte[] txt, RegisterReply callback, IntPtr context);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceResolve(out IntPtr reference, uint flags, uint interfaceIndex,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string type,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string domain, ResolveReply callback, IntPtr context);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceQueryRecord(out IntPtr reference, uint flags, uint interfaceIndex,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name, ushort type, ushort recordClass, QueryReply callback, IntPtr context);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceGetAddrInfo(out IntPtr reference, uint flags, uint interfaceIndex,
            uint protocol, [MarshalAs(UnmanagedType.LPUTF8Str)] string host, AddressReply callback, IntPtr context);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceUpdateRecord(IntPtr reference, IntPtr record, uint flags,
            ushort length, byte[] bytes, uint ttl);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceRefSockFD(IntPtr reference);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int DNSServiceProcessResult(IntPtr reference);
        [DllImport(DnsLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void DNSServiceRefDeallocate(IntPtr reference);
        [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int poll(ref PollDescriptor descriptor, uint count, int milliseconds);

        internal static ushort NetworkPort(ushort port) => BitConverter.IsLittleEndian ? (ushort)((port << 8) | (port >> 8)) : port;
    }
}
