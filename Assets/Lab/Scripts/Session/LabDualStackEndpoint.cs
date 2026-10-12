using System;
using System.Net;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;
using UnityEngine;

namespace C6Lab
{
    /// <summary>One version-pinned conversion boundary; IPv6 scope is part of endpoint identity.</summary>
    public static class LabDualStackEndpoint
    {
        public const string SupportedUnityVersion = "6000.6.5f1";

        public static void ValidateEnvironment()
        {
            if (Application.unityVersion != SupportedUnityVersion)
                throw new NotSupportedException("Revalidate the local transport adapter before changing Unity versions.");
            if (UnsafeUtility.SizeOf<NetworkEndpoint.TransferrableData>() != 64)
                throw new NotSupportedException("The installed native endpoint layout differs from the reviewed Unity/UTP baseline.");
        }

        public static bool TryParse(string address, ushort port, out NetworkEndpoint endpoint, out string error)
        {
            ValidateEnvironment();
            return LabTransportEndpoint.TryParse(address, port, out endpoint, out error);
        }

        public static uint GetScopeId(NetworkEndpoint endpoint) => LabTransportEndpoint.GetScopeId(endpoint);
        public static string GetAddress(NetworkEndpoint endpoint) => LabTransportEndpoint.GetAddress(endpoint);
        public static string GetTransportAddress(string address) => address.Split('%')[0];

        public static IPEndPoint ToIPEndPoint(NetworkEndpoint endpoint) =>
            new IPEndPoint(IPAddress.Parse(GetAddress(endpoint)), endpoint.Port);

        public static NetworkEndpoint FromIPEndPoint(IPEndPoint address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (!TryParse(address.Address.ToString(), (ushort)address.Port, out var endpoint, out var error))
                throw new ArgumentException(error, nameof(address));
            return endpoint;
        }
    }
}
