using ReactiveUI;
using UGCS.Sdk.Protocol.Encoding;

namespace UGCS.DroneTracker.Avalonia.Models
{
    public class TrackedVehicle : ReactiveObject
    {
        private bool _isConnected;
        private double? _altitude;
        private double? _latitude;
        private double? _longitude;

        public TrackedVehicle(Vehicle vehicle)
        {
            Vehicle = vehicle;
        }

        public Vehicle Vehicle { get; }

        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }

        public double? Altitude
        {
            get => _altitude;
            set => this.RaiseAndSetIfChanged(ref _altitude, value);
        }

        public double? Latitude
        {
            get => _latitude;
            set => this.RaiseAndSetIfChanged(ref _latitude, value);
        }

        public double? Longitude
        {
            get => _longitude;
            set => this.RaiseAndSetIfChanged(ref _longitude, value);
        }
    }
}