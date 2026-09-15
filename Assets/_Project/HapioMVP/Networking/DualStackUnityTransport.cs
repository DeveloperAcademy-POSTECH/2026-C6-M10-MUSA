using System;
using Unity.Collections;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;

namespace C6.Prototype.Networking
{
    /// <summary>
    /// Instance-local NGO driver factory. Reimplementing the interface selects this factory
    /// through UnityTransport.DriverConstructor without changing its global static hook.
    /// Clients retain stock UTP UDP; only a Host needs the two-family socket interface.
    /// </summary>
    public sealed class DualStackUnityTransport : UnityTransport, INetworkStreamDriverConstructor
    {
        bool configured, hostMode;
        NetworkEndpoint target;
        DualStackUdpNetworkInterface hostInterface;
        public string LastFailureCode { get; private set; } = string.Empty;
        public bool HasIpv4Listener => hostMode && m_Driver.IsCreated && hostInterface.HasIpv4Socket;
        public bool HasIpv6Listener => hostMode && m_Driver.IsCreated && hostInterface.HasIpv6Socket;

        public void ConfigureHost(ushort port)
        {
            EnsureConfigurable(port);
            hostMode = true; configured = true; target = default; LastFailureCode = string.Empty;
            SetConnectionData(true, "127.0.0.1", port, "0.0.0.0");
        }

        public void ConfigureClient(string canonicalAddress, ushort port)
        {
            EnsureConfigurable(port);
            if (!DualStackEndpoint.TryParse(canonicalAddress, port, out var parsed, out var error))
                throw new ArgumentException(error, nameof(canonicalAddress));
            hostMode = false; configured = true; target = parsed; LastFailureCode = string.Empty;
            SetConnectionData(true, DualStackEndpoint.GetTransportAddress(canonicalAddress), port);
        }

        void EnsureConfigurable(ushort port)
        {
            if (m_Driver.IsCreated) throw new InvalidOperationException("Shut down the transport before changing endpoints.");
            if (port == 0) throw new ArgumentOutOfRangeException(nameof(port));
            DualStackEndpoint.ValidateEnvironment();
        }

        public override bool StartClient() => StartConfigured(false);
        public override bool StartServer() => StartConfigured(true);
        bool StartConfigured(bool server)
        {
            if (m_Driver.IsCreated) return false;
            LastFailureCode = string.Empty;
            if (!configured || hostMode != server) return Reject("ENDPOINT_NOT_CONFIGURED");
            if (s_DriverConstructor != null) return Reject("DRIVER_CONSTRUCTOR_CONFLICT");
            if (UseWebSockets || UseEncryption || Protocol != ProtocolType.UnityTransport)
                return Reject("LOCAL_UDP_CONFIGURATION_REQUIRED");
            try
            {
                bool started = server ? base.StartServer() : base.StartClient();
                if (!started) LastFailureCode = server ? "HOST_BIND_FAILED" : "CLIENT_START_FAILED";
                return started;
            }
            catch (Exception error)
            {
                // The base factory may have assigned a driver before a later bind step failed.
                if (m_Driver.IsCreated) base.Shutdown();
                hostInterface = default;
                Debug.LogWarning("C6_L2_TRANSPORT_START exception=" + error.GetType().Name);
                return Reject(server ? "HOST_START_FAILED" : "CLIENT_START_FAILED");
            }
        }

        bool Reject(string code)
        {
            LastFailureCode = code;
            Debug.LogWarning("C6_L2_TRANSPORT_START code=" + code);
            return false;
        }

        protected override NetworkConnection Connect(NetworkEndpoint ignoredEndpoint) => base.Connect(target);

        public override void Shutdown()
        {
            try { base.Shutdown(); }
            finally { hostInterface = default; }
        }

        public new void CreateDriver(UnityTransport transport, out NetworkDriver driver,
            out NetworkPipeline unreliableFragmentedPipeline,
            out NetworkPipeline unreliableSequencedFragmentedPipeline,
            out NetworkPipeline reliableSequencedPipeline)
        {
            if (transport != this || !configured)
                throw new InvalidOperationException("The local driver factory must use its configured transport instance.");
            DualStackEndpoint.ValidateEnvironment();
            var settings = GetDefaultNetworkSettings();
            NativeArray<NetworkPipelineStageId> unreliable = default, sequenced = default, reliable = default;
            driver = default;
            try
            {
                if (hostMode)
                {
                    hostInterface = new DualStackUdpNetworkInterface(true);
                    driver = NetworkDriver.Create(hostInterface.WrapToUnmanaged(), settings);
                }
                else driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
                // NGO retains fragmentation, sequencing, metrics and configured simulation stages.
                GetDefaultPipelineConfigurations(ref driver, out unreliable, out sequenced, out reliable);
                unreliableFragmentedPipeline = driver.CreatePipeline(unreliable);
                unreliableSequencedFragmentedPipeline = driver.CreatePipeline(sequenced);
                reliableSequencedPipeline = driver.CreatePipeline(reliable);
            }
            catch
            {
                if (driver.IsCreated) driver.Dispose();
                hostInterface.Dispose();
                hostInterface = default;
                throw;
            }
            finally
            {
                if (unreliable.IsCreated) unreliable.Dispose();
                if (sequenced.IsCreated) sequenced.Dispose();
                if (reliable.IsCreated) reliable.Dispose();
                settings.Dispose();
            }
        }
    }
}
