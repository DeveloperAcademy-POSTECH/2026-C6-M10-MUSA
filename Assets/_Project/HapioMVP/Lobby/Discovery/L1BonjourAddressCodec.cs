using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace C6.Prototype.Lobby.Discovery
{
    /// <summary>Probe-only Darwin sockaddr decoder. Does not change production address selection.</summary>
    public static class L1BonjourAddressCodec
    {
        public static bool TryDecode(IntPtr socketAddress, uint callbackInterface, out string address)
        {
            address = null;
            if (socketAddress == IntPtr.Zero) return false;
            int length = Marshal.ReadByte(socketAddress, 0);
            int family = Marshal.ReadByte(socketAddress, 1);
            IPAddress parsed;
            if (family == 2 && length >= 16)
            {
                var bytes = new byte[4];
                Marshal.Copy(IntPtr.Add(socketAddress, 4), bytes, 0, bytes.Length);
                if (bytes[0] == 0 || bytes[0] == 127 || bytes[0] >= 224) return false;
                parsed = new IPAddress(bytes);
            }
            else if (family == 30 && length >= 28)
            {
                // Darwin sockaddr_in6: len/family/port/flowinfo, 16 address bytes, native uint scope.
                var bytes = new byte[16];
                Marshal.Copy(IntPtr.Add(socketAddress, 8), bytes, 0, bytes.Length);
                parsed = new IPAddress(bytes);
                if (parsed.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(parsed) || parsed.IsIPv6Multicast || parsed.IsIPv4MappedToIPv6) return false;
                if (parsed.IsIPv6LinkLocal)
                {
                    uint scope = unchecked((uint)Marshal.ReadInt32(socketAddress, 24));
                    if (scope == 0) scope = callbackInterface;
                    if (scope == 0 || scope >= 0xffffff00U) return false;
                    parsed.ScopeId = scope;
                }
            }
            else return false;
            address = parsed.ToString();
            return true;
        }

        public static string Family(string address)
            => IPAddress.TryParse(address, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4";
    }
}
