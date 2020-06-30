using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using RJCP.IO.Ports;
using SerialPortLib;
using UGCS.DroneTracker.Core.PelcoD;

namespace UGCS.DroneTracker.Core.PTZ
{
    public class SerialPortPTZMessagesTransport : IPTZDeviceMessagesTransport
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<SerialPortPTZMessagesTransport>();

        private readonly SerialPortInput _serialPort;
        private readonly string _portName;
        private readonly int _portSpeed;

        public SerialPortPTZMessagesTransport(string portName, int portSpeed)
        {
            _portName = portName;
            _portSpeed = portSpeed;

            _serialPort = new SerialPortInput(new NullLogger<SerialPortInput>());

            _serialPort.ConnectionStatusChanged += serialPort_ConnectionStatusChanged;
            _serialPort.MessageReceived += serialPort_MessageReceived;
        }

        private void serialPort_MessageReceived(object sender, MessageReceivedEventArgs args)
        {
            var message = BitConverter.ToString(args.Data);
            _logger.LogInfoMessage($"Serial port data arrived => {message}");
            MessageReceived?.Invoke(this, args.Data);
        }

        private void serialPort_ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
        {
            _logger.LogInfoMessage($"Serial port connection status changed. isConnected={args.Connected}");
            OnConnectionStatusChanged(isConnected: args.Connected);
        }

        protected void OnConnectionStatusChanged(bool isConnected)
        {
            ConnectionStatusChanged?.Invoke(this, isConnected);
        }


        public Task Initialize()
        {
            _logger.LogInfoMessage($"Serial port initializing on => portName:{_portName}, portSpeed:{_portSpeed}");
            _serialPort.SetPort(_portName, _portSpeed, StopBits.One, Parity.None, DataBits.Eight);
            var connectResult = _serialPort.Connect();
            if (!connectResult)
            {
                _logger.LogError($"Initializing: Serial port Connect - fail");
            }
            return Task.CompletedTask;
        }

        public void SendMessage(IPTZMessage message)
        {
            MessageSending?.Invoke(this, message.DataBytes);
            _logger.LogInfoMessage($"Send message. Data: {BitConverter.ToString(message.DataBytes)}");
            var sendResult = _serialPort.SendMessage(message.DataBytes);
            if (!sendResult)
            {
                _logger.LogError($"Send message fail.");
            }
        }

        public void Teardown()
        {
            _logger.LogInfoMessage($"Teardown: Serial port disconnecting");
            _serialPort.Disconnect();
        }

        public event EventHandler<byte[]> MessageReceived;
        public event EventHandler<byte[]> MessageSending;
        public event EventHandler<bool> ConnectionStatusChanged;
    }
}