using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Ninject;
using ReactiveUI;
using UGCS.DroneTracker.Avalonia.Models;
using UGCS.DroneTracker.Core;
using UGCS.DroneTracker.Core.PTZ;
using UGCS.DroneTracker.Core.Services;
using UGCS.DroneTracker.Core.Settings;
using UGCS.Sdk.Protocol.Encoding;
using ugcs_at;

// ReSharper disable ConvertClosureToMethodGroup

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public enum RemoteControlActionType
    {
        Stop = 0,
        SetZero,
        PanLeft,
        PanRight,
        TiltUp,
        TiltDown
    }

    public partial class DroneTrackerViewModel : ViewModelBase, IRoutableViewModel, IActivatableViewModel
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<DroneTrackerViewModel>();

        public IDroneTracker DroneTracker { get; }

        public ConnectionStatusViewModel ConnectionStatusViewModel
        {
            get => _connectionStatusViewModel;
            set => this.RaiseAndSetIfChanged(ref _connectionStatusViewModel, value);
        }


        private readonly IApplicationSettingsManager _settingsManager;

        private readonly VehiclesManager _vehiclesManager;
        private Vehicle _selectedVehicle;
        private ConnectionStatusViewModel _connectionStatusViewModel;
        private TrackedVehicle _trackedVehicle;
        
        private readonly IQueryablePTZDeviceController _ptzController;

        private double _initialPlatformLatitude;
        private double _initialPlatformLongitude;
        private double _initialPlatformTilt;
        private double _initialPlatformRoll;
        private double _initialNorthDirection;
        
        private double _initialPlatformAltitude;

        public string UrlPathSegment => "DroneTracker";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; }


        public DroneTrackerViewModel(IScreen hostScreen,
            VehiclesManager vehiclesManager,
            IDroneTracker droneTracker,
            IQueryablePTZDeviceController ptzController,
            IApplicationSettingsManager settingsManager,
            ConnectionStatusViewModel connectionStatusViewModel)
        {
            HostScreen = hostScreen;

            Activator = new ViewModelActivator();

            _settingsManager = settingsManager;
            _vehiclesManager = vehiclesManager;
            DroneTracker = droneTracker;
            _ptzController = ptzController;

            _vehiclesManager.VehicleListChanged += _vehiclesManager_VehicleListChanged;
            _vehiclesManager.SelectedVehicleChanged += _vehiclesManager_SelectedVehicleChanged;
            _vehiclesManager.SelectedVehicleLocationTelemetryChanged +=
                _vehiclesManager_SelectedVehicleLocationTelemetryChanged;

            ConnectionStatusViewModel = connectionStatusViewModel;

            createCommands();


            this.WhenActivated((CompositeDisposable disposables) =>
            {
                initViewModel();

                this.WhenAnyValue(vm => vm.SelectedVehicle)
                    .Subscribe(v => updateSelectedVehicle(v))
                    .DisposeWith(disposables);

                this.WhenAnyValue(vm => vm.ConnectionStatusViewModel.IsPTZConnected)
                    .Subscribe(isPtzConnected => onPtzConnectedStatusChanged(isPtzConnected: isPtzConnected))
                    .DisposeWith(disposables);
            });
        }

        private void onPtzConnectedStatusChanged(bool isPtzConnected)
        {
            if (isPtzConnected)
            {
                Task.Factory.StartNew(async () =>
                {
                    var updatePositionSuccess = await this.DroneTracker.UpdateCurrentPosition(_settingsManager.GetAppSettings().PTZDeviceAddress);
                    if (updatePositionSuccess)
                    {
                        this.DroneTracker.ResetTotalRotation();
                    }
                    else
                    {
                        ConnectionStatusViewModel.IsPTZConnected = false;
                    }
                });
            }
        }

        private AppSettingsDto getAppSettings => _settingsManager.GetAppSettings();


        public void DoStopPositioning()
        {
            doStopPositioning();
        }

        public void DoStartPositioning(RemoteControlActionType remoteControlAction)
        {
            doStartPositioning(remoteControlAction);
        }

        private void initViewModel()
        {
            _logger.LogInfoMessage($"initViewModel requested");
            Vehicles.Clear();
            _vehiclesManager.Vehicles.ForEach(v => Vehicles.Add(v));
            
            SelectedVehicle = _vehiclesManager.SelectedVehicle;

            var settings = getAppSettings;
            InitialPlatformLatitude = settings.InitialPlatformLat;
            InitialPlatformLongitude = settings.InitialPlatformLon;
            InitialPlatformAltitude = settings.InitialPlatformAlt;

            InitialPlatformTilt = settings.InitialPlatformTilt;
            InitialPlatformRoll = settings.InitialPlatformRoll;

            InitialNorthDirection = settings.InitialNorthDir;

            ZeroPTZPanAngle = settings.ZeroPTZPanAngle;
            ZeroPTZTiltAngle = settings.ZeroPTZTiltAngle;
        }

        private void updateSelectedVehicle(Vehicle vehicle)
        {
            _logger.LogInfoMessage($"updateSelectedVehicle {vehicle?.Name}");
            SelectedVehicle = vehicle;
            _vehiclesManager.SelectedVehicle = vehicle;

            if (TrackedVehicle != null && vehicle != null && TrackedVehicle.Vehicle?.VehicleId == vehicle.VehicleId)
            {
                var tmpTrackedVehicle = TrackedVehicle;
                TrackedVehicle = new TrackedVehicle(SelectedVehicle)
                {
                    IsConnected = _vehiclesManager.IsConnected(SelectedVehicle),
                    Latitude = tmpTrackedVehicle.Latitude,
                    Longitude = tmpTrackedVehicle.Longitude,
                    Altitude = tmpTrackedVehicle.Altitude
                };
            }
            else
            {
                TrackedVehicle = new TrackedVehicle(SelectedVehicle)
                {
                    IsConnected = _vehiclesManager.IsConnected(SelectedVehicle)
                };
            }
        }

        private void _vehiclesManager_SelectedVehicleChanged(object sender, Vehicle vehicle)
        {
            updateSelectedVehicle(vehicle);
        }

        private void _vehiclesManager_VehicleListChanged(object sender, List<Vehicle> vehicles)
        {
            _logger.LogInfoMessage($"_vehiclesManager_VehicleListChanged => {vehicles.Count}");
            Vehicles.Clear();
            vehicles.ForEach(v => Vehicles.Add(v));
        }

        private void _vehiclesManager_SelectedVehicleLocationTelemetryChanged(object sender, LocationTelemetryDto e)
        {
            if (TrackedVehicle == null) return;
            if (e.Altitude.HasValue)
                TrackedVehicle.Altitude = e.Altitude;
            if (e.Latitude.HasValue)
                TrackedVehicle.Latitude = e.Latitude;
            if (e.Longitude.HasValue)
                TrackedVehicle.Longitude = e.Longitude;
        }
    }
}