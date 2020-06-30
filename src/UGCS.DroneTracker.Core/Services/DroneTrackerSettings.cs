using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.Core.Services
{
    public class DroneTrackerSettings
    {
        public byte PTZDeviceAddress { get; set; }
        public double InitialPlatformLatitude { get; set; }
        public double InitialPlatformLongitude { get; set; }
        public double InitialPlatformTilt { get; set; }
        public double InitialPlatformRoll { get; set; }
        public double InitialNorthDirection { get; set; }
        public double InitialPlatformAltitude { get; set; }

        public double MinimalPanChangedThreshold { get; set; }
        public double MinimalTiltChangedThreshold { get; set; }
        public WiresProtectionMode WiresProtectionMode { get; set; }
    }
}