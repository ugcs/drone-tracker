using System;
using System.Collections.Generic;

namespace UGCS.DroneTracker.Core.PelcoD
{
    public class PelcoRequestBuilder
    {
        private readonly Dictionary<PelcoDEMessageType, byte> _commandsMapping;

        public PelcoRequestBuilder(Dictionary<PelcoDEMessageType, byte> commandsMapping)
        {
            _commandsMapping = commandsMapping ?? DefaultPelcoCodesMappingFactory.CodesMapping;
        }

        public PelcoDEMessage Stop(byte address)
        {
            return buildCommandRequest(address, PelcoDEMessageType.Stop);
        }

        public PelcoDEMessage Pan(byte address, PanDirection direction, byte speed)
        {
            var cmd = direction switch
            {
                PanDirection.Left => PelcoDEMessageType.PanLeft,
                PanDirection.Right => PelcoDEMessageType.PanRight,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
            return buildPanTiltContinuously(address, cmd, speed);
        }

        public PelcoDEMessage Tilt(byte address, TiltDirection direction, byte speed)
        {
            var cmd = direction switch
            {
                TiltDirection.Up => PelcoDEMessageType.TiltUp,
                TiltDirection.Down => PelcoDEMessageType.TiltDown,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
            return buildPanTiltContinuously(address, cmd, speed);
        }

        public PelcoDEMessage SetPan(byte address, int coordinate)
        {
            var angleBytes = BitConverter.GetBytes(coordinate);
            var message = buildCommandRequest(address, PelcoDEMessageType.SetPan, angleBytes[1], angleBytes[0]);
            return message;
        }
        public PelcoDEMessage SetTilt(byte address, int coordinate)
        {
            var angleBytes = BitConverter.GetBytes(coordinate);
            var message = buildCommandRequest(address, PelcoDEMessageType.SetTilt, angleBytes[1], angleBytes[0]);
            return message;
        }

        public PelcoDEMessage RequestPan(byte address)
        {
            return buildCommandRequest(address, PelcoDEMessageType.RequestPan);
        }

        public PelcoDEMessage RequestTilt(byte address)
        {
            return buildCommandRequest(address, PelcoDEMessageType.RequestTilt);
        }

        private PelcoDEMessage buildCommandRequest(byte deviceAddress, PelcoDEMessageType messageType, byte dataH = 0, byte dataL = 0)
        {
            if (!_commandsMapping.ContainsKey(messageType)) return null;
            var command2Code = _commandsMapping[messageType];
            return new PelcoDEMessage(deviceAddress, command2Code, dataH, dataL);
        }

        private PelcoDEMessage buildPanTiltContinuously(byte deviceAddress, PelcoDEMessageType messageType, byte speed)
        {
            byte data1 = 0x00;
            byte data2 = 0x00;

            switch (messageType)
            {
                case PelcoDEMessageType.PanLeft:
                case PelcoDEMessageType.PanRight:
                    data1 = speed;
                    break;
                case PelcoDEMessageType.TiltUp:
                case PelcoDEMessageType.TiltDown:
                    data2 = speed;
                    break;
            }

            return buildCommandRequest(deviceAddress, messageType, data1, data2);
        }
    }
}