using System.Collections.Generic;
using UGCS.DroneTracker.Core.PelcoD;

namespace UGCS.DroneTracker.Core.Settings
{
    public enum PTZDeviceTransportType
    {
        Serial = 0,
        Udp = 1
    }

    public enum WiresProtectionMode
    {
        Disabled = 0,
        AllRound = 1,
        DeadZone = 2
    }

    public interface IAppSettings {}

    public class AppSettingsDto : IAppSettings
    {
        public string UGCSHost { get; set; } = "localhost";
        public int UGCSPort { get; set; } = 3334;
        public string UGCSLogin { get; set; } = "admin";
        
        // TODO do not store in plain text
        public string UGCSPassword { get; set; } = "admin";

        public PTZDeviceTransportType PTZTransportType { get; set; } = PTZDeviceTransportType.Serial;

        public string PTZSerialPortName { get; set; } = "COM1";
        public int PTZSerialPortSpeed { get; set; } = 9600;
        public byte PTZDeviceAddress { get; set; } = 0x01;


        public string PTZUdpHost { get; set; } = "192.168.0.93";
        public int PTZUdpPort { get; set; } = 6000;


        public double WindowWidth { get; set; } = 400;
        public double WindowHeight { get; set; } = 1050;

        public int WindowPositionLeft { get; set; } = -1;
        public int WindowPositionTop { get; set; } = -1;

        public double InitialPlatformLat { get; set; }
        public double InitialPlatformLon { get; set; }
        public double InitialPlatformAlt { get; set; }
        public double InitialPlatformTilt { get; set; }
        public double InitialPlatformRoll { get; set; }
        public double InitialNorthDir { get; set; } = 0d;


        public double MinimalPanChangedThreshold { get; set; } = 1d;
        public double MinimalTiltChangedThreshold { get; set; } = 1d;

        public double PTZPanAngleToCoordinateFactor { get; set; } = 1; //1.12d;
        public double PTZTiltAngleToCoordinateFactor { get; set; } = 1; //1.75d;

        public double PTZMaxSpeed { get; set; } = 0x3F;

        public double PTZMinPanAngle { get; set; } = 0;

        public double PTZMaxPanAngle { get; set; } = 360;

        public double PTZMinTiltAngle { get; set; } = 45;

        public double PTZMaxTiltAngle { get; set; } = -90;

        public double PTZMaxPanCoordinate { get; set; } = 400.19;

        public double PTZMaxTiltCoordinate { get; set; } = 236.25;

        public WiresProtectionMode WiresProtection { get; set; } = WiresProtectionMode.AllRound;

        public double PanSpeed { get; set; } = 30.0;

        public Dictionary<PelcoDEMessageType, byte> PelcoCodesMapping { get; set; } =
            DefaultPelcoCodesMappingFactory.CodesMapping;

        public double ZeroPTZTiltAngle { get; set; }
        public double ZeroPTZPanAngle { get; set; }
    }

}
