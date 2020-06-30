namespace UGCS.DroneTracker.Core.PelcoD
{
    public interface IPTZMessage
    {
        byte[] DataBytes { get; }
    }

    public enum PanDirection 
    {
        Left = 0,
        Right = 1
    }

    public enum TiltDirection
    {
        Down = 0,
        Up = 1
    }

    public enum PelcoDEMessageType
    {
        Stop = 0,
        PanLeft,
        PanRight,
        TiltUp,
        TiltDown,
        SetPan,
        SetTilt,
        RequestPan,
        RequestTilt,
        SetPanCompleteResponse,
        SetTiltCompleteResponse,
        RequestPanResponse,
        RequestTiltResponse
    }


    public class PelcoDEMessage : IPTZMessage
    {
        private const byte SyncByte = 0xFF;

        public byte DeviceAddress { get; }

        private byte[] _dataBytes;

        public byte[] DataBytes => _dataBytes ??= calcBytes(DeviceAddress, Command1, Command2, DataH, DataL);


        public byte Command1 { get; } = 0x00;

        public byte Command2 { get; }

        public byte DataH { get; }

        public byte DataL { get; }


        public PelcoDEMessage(byte deviceAddress, byte command1, byte command2, byte dataH, byte dataL)
        {
            DeviceAddress = deviceAddress;
            Command1 = command1;
            Command2 = command2;
            DataH = dataH;
            DataL = dataL;
        }

        public PelcoDEMessage(byte deviceAddress, byte command, byte dataH = 0, byte dataL = 0)
        {
            DeviceAddress = deviceAddress;
            Command2 = command;
            DataH = dataH;
            DataL = dataL;
        }

        private PelcoDEMessage(byte[] dataBytes)
        {
            DeviceAddress = dataBytes[1];
            Command1 = dataBytes[2];
            Command2 = dataBytes[3];
            DataH = dataBytes[4];
            DataL = dataBytes[5];
        }

        public static PelcoDEMessage FromBytes(byte[] dataBytes)
        {
            if (dataBytes == null) return null;
            if (dataBytes.Length != 7) return null;
            if (dataBytes[0] != 0xFF) return null;

            var message = new PelcoDEMessage(dataBytes);
            var checkSum = PelcoDEMessage.calcCheckSum(
                message.DeviceAddress, 
                message.Command1, message.Command2,
                message.DataH, message.DataL
            );

            return checkSum != dataBytes[6] ? null : message;
        }

        private byte[] calcBytes(byte address, byte command1, byte command2, byte data1, byte data2)
        {
            byte checkSum = calcCheckSum(address, command1, command2, data1, data2);

            return new[] { SyncByte, address, command1, command2, data1, data2, checkSum };
        }

        private static byte calcCheckSum(byte address, byte command1, byte command2, byte data1, byte data2)
        {
            byte checkSum;
            unchecked
            {
                checkSum = (byte) (address + command1 + command2 + data1 + data2);
            }

            return checkSum;
        }

        public override string ToString()
        {
            return $"PelcoD message: [address:{DeviceAddress}, cmd1:{Command1:X2}, cmd2:{Command2:X2}, d1(H):{DataH:X2}, d2(L):{DataL:X2}]"; ;
        }
    }
}
