using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AsyncNet.Udp.Client;
using log4net;
using log4net.Core;
using UGCS.DroneTracker.Core.PelcoD;

namespace UGCS.DroneTracker.Core.PTZ
{
    public class UdpPTZMessagesTransport : IPTZDeviceMessagesTransport
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<UdpPTZMessagesTransport>();

        private readonly AsyncNetUdpClient _udpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly string _host;
        private readonly int _port;

        public event EventHandler<byte[]> MessageReceived;
        public event EventHandler<byte[]> MessageSending;
        public event EventHandler<bool> ConnectionStatusChanged;


        public UdpPTZMessagesTransport(string host, int port)
        {
            _host = host;
            _port = port;
            _udpClient = new AsyncNetUdpClient(host, port);
            _udpClient.UdpPacketArrived += _udpClient_UdpPacketArrived;
            _udpClient.ClientStarted += _udpClient_ClientStarted;
            _udpClient.ClientReady += _udpClient_ClientReady;
            _udpClient.ClientStopped += _udpClient_ClientStopped;
        }

        private void _udpClient_ClientStopped(object sender, AsyncNet.Udp.Client.Events.UdpClientStoppedEventArgs e)
        {
            _logger.LogInfoMessage("UDP client stopped");
            OnConnectionStatusChanged(isConnected: false);
        }

        private void _udpClient_ClientStarted(object sender, AsyncNet.Udp.Client.Events.UdpClientStartedEventArgs e)
        {
            _logger.LogInfoMessage($"UDP client started on => host:{e.TargetHostname}, port:{e.TargetPort}");
        }

        private void _udpClient_ClientReady(object sender, AsyncNet.Udp.Client.Events.UdpClientReadyEventArgs e)
        {
            _logger.LogInfoMessage($"UDP client ready on => host:{_host}, port:{_port}");
            OnConnectionStatusChanged(isConnected: true);
        }

        protected void OnConnectionStatusChanged(bool isConnected)
        {
            _logger.LogInfoMessage($"UDP client connection status changed. isConnected={isConnected}");
            ConnectionStatusChanged?.Invoke(this, isConnected);
        }

        private void _udpClient_UdpPacketArrived(object sender, AsyncNet.Udp.Remote.Events.UdpPacketArrivedEventArgs e)
        {
            _logger.LogInfoMessage($"UDP packet arrived => host:{e.RemoteEndPoint.Address.ToString()}, port:{e.RemoteEndPoint.Port} ({e.RemoteEndPoint}) / data: {BitConverter.ToString(e.PacketData)}");
            MessageReceived?.Invoke(this, e.PacketData);
        }

        public Task Initialize()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger.LogInfoMessage($"UDP client initialize...");
            return _udpClient.StartAsync(_cancellationTokenSource.Token);
        }

        public void SendMessage(IPTZMessage message)
        {
            MessageSending?.Invoke(this, message.DataBytes);
            _logger.LogInfoMessage($"Post message. Data: {BitConverter.ToString(message.DataBytes)}");


            // for debug
            //if (message.DataBytes[3] == 0x51)
            //{
            //    Task.Factory.StartNew(async () =>
            //    {
            //        await Task.Delay(1000);
            //        byte[] sendBuffer = new byte[] { 0xff, 0x01, 0x00, 0x59, 0x08, 0x9c, 0xfe };
            //        MessageReceived?.Invoke(this, sendBuffer);
            //    });
            //}

            var postResult = _udpClient.Post(message.DataBytes);
            if (!postResult)
            {
                _logger.LogError($"Post message fail.");
            }
        }

        public void Teardown()
        {
            _logger.LogInfoMessage($"UDP client teardown...");
            _cancellationTokenSource?.Cancel();
        }
    }
}