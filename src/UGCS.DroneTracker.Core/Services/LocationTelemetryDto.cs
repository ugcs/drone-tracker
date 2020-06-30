namespace UGCS.DroneTracker.Core.Services
{
    public class LocationTelemetryDto
    {
        public int VehicleId { get; set; }
        public double? Altitude { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public LocationTelemetryDto(int vehicleId)
        {
            this.VehicleId = vehicleId;
        }

        public override string ToString()
        {
            return $"LocationTelemetryDto => vid: {VehicleId}, lat:{Latitude}, lon:{Longitude}, alt:{Altitude}";
        }
    }
}