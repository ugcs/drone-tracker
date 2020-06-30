using System.Collections.ObjectModel;
using ReactiveUI;
using UGCS.DroneTracker.Avalonia.Models;
using UGCS.DroneTracker.Core.Services;
using UGCS.Sdk.Protocol.Encoding;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public partial class DroneTrackerViewModel
    {
        private bool _isTrackingEnabled;
        private bool _isManualControl;
        public ObservableCollection<Vehicle> Vehicles { get; private set; } = new ObservableCollection<Vehicle>();

        public Vehicle SelectedVehicle
        {
            get => _selectedVehicle;
            set => this.RaiseAndSetIfChanged(ref _selectedVehicle, value);
        }

        public TrackedVehicle TrackedVehicle
        {
            get => _trackedVehicle;
            set => this.RaiseAndSetIfChanged(ref _trackedVehicle, value);
        }

        public double InitialPlatformLatitude
        {
            get => _initialPlatformLatitude;
            set => this.RaiseAndSetIfChanged(ref _initialPlatformLatitude, value);
        }

        public double InitialPlatformLongitude
        {
            get => _initialPlatformLongitude;
            set => this.RaiseAndSetIfChanged(ref _initialPlatformLongitude, value);
        }

        public double InitialPlatformAltitude
        {
            get => _initialPlatformAltitude;
            set => this.RaiseAndSetIfChanged(ref _initialPlatformAltitude, value);
        }

        public double InitialPlatformTilt
        {
            get => _initialPlatformTilt;
            set => this.RaiseAndSetIfChanged(ref _initialPlatformTilt, value);
        }

        public double InitialPlatformRoll
        {
            get => _initialPlatformRoll;
            set => this.RaiseAndSetIfChanged(ref _initialPlatformRoll, value);
        }

        public double InitialNorthDirection
        {
            get => _initialNorthDirection;
            set => this.RaiseAndSetIfChanged(ref _initialNorthDirection, value);
        }

        public double ZeroPTZPanAngle { get; set; }
        public double ZeroPTZTiltAngle { get; set; }

        public bool IsTrackingEnabled
        {
            get => _isTrackingEnabled;
            set => this.RaiseAndSetIfChanged(ref _isTrackingEnabled, value);
        }

        public bool IsManualControl
        {
            get => _isManualControl;
            set => this.RaiseAndSetIfChanged(ref _isManualControl, value);
        }
    }
}