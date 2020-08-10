using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Ninject;
using ReactiveUI;
using UGCS.DroneTracker.Core.Helpers;
using UGCS.DroneTracker.Core.PelcoD;
using UGCS.DroneTracker.Core.Services;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public partial class DroneTrackerViewModel
    {
        public ReactiveCommand<Unit, Unit> GoSettings { get; set; }
        public ReactiveCommand<Unit, Unit> StartTrackCommand { get; set; }
        public ReactiveCommand<Unit, Unit> StopTrackCommand { get; set; }
        public ReactiveCommand<RemoteControlActionType, Unit> StartPositioningCommand { get; set; }
        public ReactiveCommand<Unit, Unit> StopPositioningCommand { get; set; }
        public ReactiveCommand<Unit, Unit> SetZeroPositionCommand { get; set; }
        public ReactiveCommand<Unit, Unit> PositionToZeroCommand { get; set; }
        public ReactiveCommand<Unit, Unit> SetNorthFromCurrentCommand { get; set; }
        public ReactiveCommand<Unit, Unit> SetPTZInitialLocationFromDroneCommand { get; set; }
        public ReactiveCommand<Unit, Unit> ToggleManualControl { get; set; }
        public ReactiveCommand<Unit, Unit> ResetTotalRotationCommand { get; set; }

        private void createCommands()
        {
            GoSettings = ReactiveCommand.Create(doGotoSettings);

            var canStartTrackExecute = this.WhenAnyValue(
                vm => vm.IsTrackingEnabled,
                (trackingEnabled) => (bool) !trackingEnabled
            );

            StartTrackCommand = ReactiveCommand.Create(doStartTrack, canStartTrackExecute);

            var canStopTrackExecute = this.WhenAnyValue(
                vm => vm.IsTrackingEnabled,
                (trackingEnabled) => (bool) trackingEnabled);

            StopTrackCommand = ReactiveCommand.Create(doStopTrack, canStopTrackExecute);


            var canCommandsBlockedByTrackingExecuted = this.WhenAnyValue(
                vm => vm.IsTrackingEnabled,
                (trackingEnabled) => (bool) !trackingEnabled);

            var canPositioningCommandsExecuted = this.WhenAnyValue(
                vm => vm.IsTrackingEnabled,
                vm => vm.IsManualControl,
                (trackingEnabled, manualControl) => (bool) !trackingEnabled && manualControl);

            StartPositioningCommand =
                ReactiveCommand.Create<RemoteControlActionType>(this.doStartPositioning,
                    canPositioningCommandsExecuted);

            StopPositioningCommand =
                ReactiveCommand.Create(this.doStopPositioning, canPositioningCommandsExecuted);

            SetZeroPositionCommand =
                ReactiveCommand.CreateFromTask(this.doSetZeroPositionAsync, canPositioningCommandsExecuted);
            PositionToZeroCommand =
                ReactiveCommand.CreateFromTask(this.doPositionToZeroAsync, canPositioningCommandsExecuted);

            SetNorthFromCurrentCommand =
                ReactiveCommand.CreateFromTask(this.doSetNorthAsync, canCommandsBlockedByTrackingExecuted);

            SetPTZInitialLocationFromDroneCommand = 
                ReactiveCommand.CreateFromTask(this.doSetPTZInitialLocationAsync, canCommandsBlockedByTrackingExecuted);

            ToggleManualControl =
                ReactiveCommand.Create(this.doToggleManualControl, canCommandsBlockedByTrackingExecuted);

            ResetTotalRotationCommand =
                ReactiveCommand.Create(this.doResetTotalRotation, canCommandsBlockedByTrackingExecuted);
        }

        private void doResetTotalRotation()
        {
            this.DroneTracker.ResetTotalRotation();
        }


        private void doGotoSettings()
        {
            // TODO do refactor
            var settings = _settingsManager.GetAppSettings();
            settings.InitialPlatformLat = InitialPlatformLatitude;
            settings.InitialPlatformLon = InitialPlatformLongitude;
            settings.InitialPlatformAlt = InitialPlatformAltitude;
            settings.InitialPlatformTilt = InitialPlatformTilt;
            settings.InitialPlatformRoll = InitialPlatformRoll;
            settings.InitialNorthDir = InitialNorthDirection;

            settings.ZeroPTZPanAngle = ZeroPTZPanAngle;
            settings.ZeroPTZTiltAngle = ZeroPTZTiltAngle;

            _settingsManager.Save();

            HostScreen.Router.Navigate.Execute(App.AppInstance.Kernel.Get<SettingsViewModel>());
        }

        private void doToggleManualControl()
        {
            _logger.LogInfoMessage($"doToggleManualControl requested. IsTrackEnabled={IsTrackingEnabled}");

            if (IsTrackingEnabled) return;
            IsManualControl = !IsManualControl;
        }

        private async Task doSetNorthAsync()
        {
            _logger.LogInfoMessage($"doSetNorthAsync requested. InitialNorthDirection={InitialNorthDirection}");
            try
            {
                var currentPTZPan = await _ptzController.RequestPanAngleAsync(getAppSettings.PTZDeviceAddress);
                _logger.LogInfoMessage($"doSetNorthAsync => ptzController response: {currentPTZPan}");
                InitialNorthDirection = Math.Round(currentPTZPan, 2);
            }
            catch (TaskCanceledException)
            {
                InitialNorthDirection = 0;
                _logger.LogInfoMessage($"doSetNorthAsync => pan request was cancelled by timeout");
            }
            catch(Exception ex)
            {
                _logger.LogException(ex);
            }
        }

        private Task doSetPTZInitialLocationAsync()
        {
            InitialPlatformLatitude = Math.Round(TrackedVehicle.Latitude.GetValueOrDefault() * LocationUtils.RADIANS_TO_DEGREES, 7);
            InitialPlatformLongitude = Math.Round(TrackedVehicle.Longitude.GetValueOrDefault() * LocationUtils.RADIANS_TO_DEGREES, 7);

            InitialPlatformAltitude = Math.Round(TrackedVehicle.Altitude.GetValueOrDefault(), 2);

            return Task.CompletedTask;
        }


        private async Task doSetZeroPositionAsync()
        {
            var settings = getAppSettings;
            ZeroPTZPanAngle = await _ptzController.RequestPanAngleAsync(settings.PTZDeviceAddress);
            ZeroPTZTiltAngle = await _ptzController.RequestTiltAngleAsync(settings.PTZDeviceAddress);
        }

        private Task doPositionToZeroAsync()
        {
            var settings = getAppSettings;
            return Task.WhenAll(
                _ptzController.PanToAsync(settings.PTZDeviceAddress, ZeroPTZPanAngle),
                _ptzController.TiltToAsync(settings.PTZDeviceAddress, ZeroPTZTiltAngle)
            );
        }

        private void doStopPositioning()
        {
            _logger.LogInfoMessage("doStopPositioning requested");

            var deviceAddress = getAppSettings.PTZDeviceAddress;

            _ptzController.Stop(deviceAddress);

            DroneTracker.UpdateCurrentPosition(deviceAddress);
        }


        private void doStartPositioning(RemoteControlActionType remoteControlAction)
        {
            _logger.LogInfoMessage($"doStopPositioning doStartPositioning => rcAction={remoteControlAction}, IsManualControl={IsManualControl}");

            if (!IsManualControl) return;

            var deviceAddress = _settingsManager.GetAppSettings().PTZDeviceAddress;

            switch (remoteControlAction)
            {
                case RemoteControlActionType.PanLeft:
                    _ptzController.Pan(deviceAddress, PanDirection.Left, 100);
                    break;
                case RemoteControlActionType.PanRight:
                    _ptzController.Pan(deviceAddress, PanDirection.Right, 100);
                    break;
                case RemoteControlActionType.TiltUp:
                    _ptzController.Tilt(deviceAddress, TiltDirection.Up, 100);
                    break;
                case RemoteControlActionType.TiltDown:
                    _ptzController.Tilt(deviceAddress, TiltDirection.Down, 100);
                    break;
                default:
                    break;
            }
        }

        private void doStartTrack()
        {
            _logger.LogInfoMessage($"doStartTrack requested => IsTrackingEnabled={IsTrackingEnabled}");

            if (IsTrackingEnabled) return;

            IsManualControl = false;
            IsTrackingEnabled = true;

            var settings = _settingsManager.GetAppSettings();

            var trackSettings = new DroneTrackerSettings
            {
                PTZDeviceAddress = settings.PTZDeviceAddress,

                InitialPlatformLatitude = InitialPlatformLatitude,
                InitialPlatformLongitude = InitialPlatformLongitude,
                InitialPlatformAltitude = InitialPlatformAltitude,

                InitialPlatformTilt = InitialPlatformTilt,
                InitialPlatformRoll = InitialPlatformRoll,

                InitialNorthDirection = InitialNorthDirection,

                MinimalPanChangedThreshold = settings.MinimalPanChangedThreshold,
                MinimalTiltChangedThreshold = settings.MinimalTiltChangedThreshold,

                WiresProtectionMode = settings.WiresProtection,
                PanSpeed = settings.PanSpeed
            };

            DroneTracker.StartTrack(trackSettings);
        }

        private void doStopTrack()
        {
            _logger.LogInfoMessage($"doStopTrack requested => IsTrackingEnabled={IsTrackingEnabled}");
            DroneTracker.StopTrack();
            IsTrackingEnabled = false;
        }

    }
}