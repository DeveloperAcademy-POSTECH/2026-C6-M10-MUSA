using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace C6Lab
{
    /// <summary>
    /// #557: this device's IPv4 addresses, for a Host to show so a guest can use Direct IP when room search fails. Read
    /// with getifaddrs, the same way LabBonjourNative reaches the OS, because the managed interface list is unproven
    /// under iOS IL2CPP. Interface names come from general knowledge of iOS and macOS, so unknown names are kept: an extra
    /// line is better than hiding the only usable address.
    /// </summary>
    public static class LabLocalAddresses
    {
        public const uint InterfaceUp = 0x1, InterfaceLoopback = 0x8, InterfaceRunning = 0x40;
        private const int AddressFamilyIpv4 = 2;
        // Cellular, VPN and peer-to-peer links a guest on the same network cannot reach.
        private static readonly string[] HiddenInterfaces = { "pdp_ip", "utun", "ipsec", "ppp", "gif", "stf", "awdl", "llw", "anpi" };

        /// <summary>Whether a guest on the same network could reach this IPv4 address.</summary>
        public static bool IsShown(string interfaceName, uint flags, byte[] ipv4)
        {
            if (string.IsNullOrEmpty(interfaceName) || ipv4 == null || ipv4.Length != 4) return false;
            if ((flags & InterfaceUp) == 0 || (flags & InterfaceRunning) == 0 || (flags & InterfaceLoopback) != 0) return false;
            if (ipv4[0] == 0 || ipv4[0] == 127 || (ipv4[0] == 169 && ipv4[1] == 254)) return false;
            return !HiddenInterfaces.Any(name => interfaceName.StartsWith(name, StringComparison.Ordinal));
        }

        /// <summary>Wi-Fi and Ethernet first, then hotspot bridges, then anything else; each address once.</summary>
        public static string[] Arrange(IEnumerable<KeyValuePair<string, string>> interfaceAndAddress) => interfaceAndAddress
            .OrderBy(item => Rank(item.Key)).ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Value).Distinct().ToArray();

        private static int Rank(string name) => name.StartsWith("en", StringComparison.Ordinal) ? 0
            : name.StartsWith("bridge", StringComparison.Ordinal) || name.StartsWith("ap", StringComparison.Ordinal) ? 1 : 2;

        /// <summary>The addresses to show now. Empty when they cannot be read.</summary>
        public static string[] Read()
        {
            if (!LabBonjourDiscovery.IsSupported) return Array.Empty<string>();
            var found = new List<KeyValuePair<string, string>>();
            IntPtr head = IntPtr.Zero;
            try
            {
                if (LabBonjourNative.getifaddrs(out head) != 0) return Array.Empty<string>();
                // struct ifaddrs on 64-bit Darwin: next 0, name 8, flags 16, addr 24.
                for (IntPtr entry = head; entry != IntPtr.Zero; entry = Marshal.ReadIntPtr(entry, 0))
                {
                    IntPtr address = Marshal.ReadIntPtr(entry, 24);
                    if (address == IntPtr.Zero || Marshal.ReadByte(address, 1) != AddressFamilyIpv4) continue;
                    var ipv4 = new byte[4];
                    Marshal.Copy(address + 4, ipv4, 0, 4);
                    string name = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry, 8));
                    if (IsShown(name, unchecked((uint)Marshal.ReadInt32(entry, 16)), ipv4))
                        found.Add(new KeyValuePair<string, string>(name, string.Join(".", ipv4)));
                }
            }
            catch (DllNotFoundException) { return Array.Empty<string>(); }
            catch (EntryPointNotFoundException) { return Array.Empty<string>(); }
            finally { if (head != IntPtr.Zero) LabBonjourNative.freeifaddrs(head); }
            return Arrange(found);
        }
    }
}
