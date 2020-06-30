using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UGCS.DroneTracker.Core.Annotations;
using UGCS.DroneTracker.Core.Helpers;
using UGCS.DroneTracker.Core.PTZ;
using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.Core.Services
{
    public interface IDroneTracker
    {
        double CurrentPlatformTilt { get; }
        double CurrentPlatformPan { get; }
        double TwistedWiresAngle { get; }

        void StartTrack(DroneTrackerSettings trackSettings);
        void StopTrack();

        void ResetWiresProtection();
        void UpdateCurrentPosition();
    }
    
    public class DroneTracker : INotifyPropertyChanged, IDroneTracker
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<DroneTracker>();

        private const int MAX_ANGLE_TO_TWIST_WIRES_CORRECTION = 360;
        private const int TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE = 179;
        private readonly IQueryablePTZDeviceController _ptzController;

        private double? _altitude;
        private double? _latitude;
        private double? _longitude;

        private double _initialPlatformTilt;
        private double _initialPlatformRoll;

        private double _initialNorthDirection;
        
        private double _initialPlatformLatitude;
        private double _initialPlatformLongitude;
        private double _initialPlatformAltitude;


        private bool _isTrackStarted;
        private WiresProtectionMode _isWiresProtectionMode;

        private double _twistedWiresAngle;
        private double _currentPlatformTilt;
        private double _currentPlatformPan;
        private Task _twistedWirePanCorrectionTask;
        private byte _ptzDeviceAddress;

        private double _minPanChangedThreshold;
        private double _minTiltChangedThreshold;
        private double _targetAzimuth;


        private double TwistedWiresZeroAngle { get; set; }

        public double TwistedWiresAngle
        {
            get => _twistedWiresAngle;
            private set { _twistedWiresAngle = value; OnPropertyChanged(); }
        }

        public double CurrentPlatformTilt
        {
            get => _currentPlatformTilt;
            private set { _currentPlatformTilt = value; OnPropertyChanged(); }
        }

        public double CurrentPlatformPan
        {
            get => _currentPlatformPan;
            private set { _currentPlatformPan = value; OnPropertyChanged(); }
        }

        public double TargetAzimuth
        {
            get => _targetAzimuth;
            private set { _targetAzimuth = value; OnPropertyChanged(); }
        }


        public DroneTracker(VehiclesManager vehiclesManager, IQueryablePTZDeviceController ptzController)
        {
            _ptzController = ptzController;

            vehiclesManager.SelectedVehicleLocationTelemetryChanged += _vehiclesManager_SelectedVehicleLocationTelemetryChanged;
        }

        private void _vehiclesManager_SelectedVehicleLocationTelemetryChanged(object sender, LocationTelemetryDto e)
        {
            if (e.Altitude.HasValue && !Nullable.Equals(_altitude, e.Altitude))
            {
                _altitude = e.Altitude;
            }
            if (e.Latitude.HasValue && !Nullable.Equals(_latitude, e.Latitude))
            {
                _latitude = e.Latitude;
            }
            if (e.Longitude.HasValue && !Nullable.Equals(_longitude, e.Longitude))
            {
                _longitude = e.Longitude;
            }

            //_logger.LogDebugMessage($"Selected vehicle telemetry changed: lat:{_latitude}, lon:{_longitude}, alt:{_altitude}");

            if (_isTrackStarted)
            {
                processTelemetry();
            }
        }

        private void processTelemetry()
        {
            if (_twistedWirePanCorrectionTask != null) return;

            if (!_latitude.HasValue || !_longitude.HasValue || !_altitude.HasValue) return;

            var ptzLatRad = _initialPlatformLatitude * LocationUtils.DEGREES_TO_RADIANS;
            var ptzLonRad = _initialPlatformLongitude * LocationUtils.DEGREES_TO_RADIANS;

            var vehicleLatRad = _latitude.Value;
            var vehicleLonRad = _longitude.Value;

            var modelAzimuth = LocationUtils.GetAzimuthBetweenCoordinate(ptzLatRad, ptzLonRad, vehicleLatRad, vehicleLonRad);
            modelAzimuth = Math.Round(modelAzimuth, 2);
            TargetAzimuth = modelAzimuth;

            var newPlatformPan = Math.Round(((modelAzimuth + _initialNorthDirection + 360) % 360), 2);

            var distance = LocationUtils.GetDistance(
                _initialPlatformLatitude * LocationUtils.DEGREES_TO_RADIANS, 
                _initialPlatformLongitude * LocationUtils.DEGREES_TO_RADIANS, 
                vehicleLatRad, vehicleLonRad);

            var deltaHeight = _altitude.Value - _initialPlatformAltitude;
            var tiltAngleRad = Math.Atan2(deltaHeight, distance);
            var modelPitch = Math.Round(tiltAngleRad * LocationUtils.RADIANS_TO_DEGREES, 2);
            var newPlatformTilt = 90 - modelPitch - _initialPlatformTilt;


            //_logger.LogDebugMessage($"Selected vehicle telemetry changed: lat:{_latitude}, lon:{_longitude}, alt:{_altitude}");
            //_logger.LogDebugMessage($"Calculated ptz pan:{newPlatformPan}, tilt:{newPlatformTilt}");

            if (Math.Abs(CurrentPlatformPan - newPlatformPan) > _minPanChangedThreshold)
            {
                _logger.LogDebugMessage($"processTelemetry => Selected vehicle current telemetry: lat={_latitude}, lon={_longitude}, alt={_altitude}");
                _logger.LogDebugMessage($"processTelemetry => PTZ need to change Pan. New pan={newPlatformPan}, old pan={CurrentPlatformPan}");


                switch (_isWiresProtectionMode)
                {
                    case WiresProtectionMode.Disabled:

                        CurrentPlatformPan = newPlatformPan;
                        _ptzController.PanTo(_ptzDeviceAddress, CurrentPlatformPan);

                        break;
                    case WiresProtectionMode.AllRound:

                        TwistedWiresAngle = calcNewTwistedWiresAngle(CurrentPlatformPan, newPlatformPan, TwistedWiresAngle);
                        _logger.LogInfoMessage($"processTelemetry => twisted wires protection: twisted angle={TwistedWiresAngle}");

                        if (Math.Abs(TwistedWiresAngle) < MAX_ANGLE_TO_TWIST_WIRES_CORRECTION)
                        {
                            CurrentPlatformPan = newPlatformPan;
                            _ptzController?.PanTo(_ptzDeviceAddress, CurrentPlatformPan);
                        }
                        else
                        {
                            _logger.LogInfoMessage($"processTelemetry => start twisted wires protection task");
                            _twistedWirePanCorrectionTask = Task.Factory.StartNew(() => doTwistWiresPanCorrection(newPlatformPan))
                                .ContinueWith(task =>
                                {
                                    _logger.LogInfoMessage($"processTelemetry => twisted wires protection task completed");
                                    _twistedWirePanCorrectionTask = null;
                                });
                        }


                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (Math.Abs(CurrentPlatformTilt - newPlatformTilt) > _minTiltChangedThreshold)
            {
                _logger.LogDebugMessage($"processTelemetry => Selected vehicle current telemetry: lat={_latitude}, lon={_longitude}, alt={_altitude}");
                _logger.LogDebugMessage($"processTelemetry => PTZ need to change Tilt. New tilt={newPlatformTilt}, old tilt={CurrentPlatformTilt}");
                
                CurrentPlatformTilt = newPlatformTilt;
                _ptzController?.TiltTo(_ptzDeviceAddress, CurrentPlatformTilt);
            }
        }

        private void doTwistWiresPanCorrection(double newPlatformPan)
        {
            _logger.LogInfoMessage($"doTwistWiresPanCorrection requested");

            var wiresCorrectionIntermediateAngle = CurrentPlatformPan;
            if (TwistedWiresAngle > 0)
            {
                wiresCorrectionIntermediateAngle -= TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE;
                if (wiresCorrectionIntermediateAngle < 0)
                {
                    wiresCorrectionIntermediateAngle = 360 + wiresCorrectionIntermediateAngle;
                }
            }
            else
            {
                wiresCorrectionIntermediateAngle += TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE;
                if (wiresCorrectionIntermediateAngle >= 360)
                {
                    wiresCorrectionIntermediateAngle = 360 - wiresCorrectionIntermediateAngle;
                }
            }
            _logger.LogDebugMessage($"doTwistWiresPanCorrection: Intermediate rotation => currentPan={CurrentPlatformPan}, interAngle={wiresCorrectionIntermediateAngle}");
            CurrentPlatformPan = wiresCorrectionIntermediateAngle;

            _ptzController?.PanToAsync(_ptzDeviceAddress, CurrentPlatformPan)
                .GetAwaiter()
                .GetResult();
            
            _logger.LogDebugMessage($"doTwistWiresPanCorrection: Intermediate rotation completed");

            TwistedWiresAngle = calcNewTwistedWiresAngle(TwistedWiresZeroAngle, newPlatformPan, currentTwistAngle: 0);

            _logger.LogDebugMessage($"doTwistWiresPanCorrection: finishing rotation => currentPan={CurrentPlatformPan}, interAngle={newPlatformPan}");
            CurrentPlatformPan = newPlatformPan;

            _ptzController?.PanToAsync(_ptzDeviceAddress, newPlatformPan)
                .GetAwaiter()
                .GetResult();

            _logger.LogDebugMessage($"doTwistWiresPanCorrection: finishing rotation completed");
        }


        public void StartTrack(DroneTrackerSettings trackSettings)
        {
            _logger.LogInfoMessage($"StartTrack requested => trackSettings:\n{JsonConvert.SerializeObject(trackSettings, Formatting.Indented)}");

            _initialPlatformLatitude = trackSettings.InitialPlatformLatitude;
            _initialPlatformLongitude = trackSettings.InitialPlatformLongitude;
            _initialPlatformAltitude = trackSettings.InitialPlatformAltitude;

            _initialPlatformTilt = trackSettings.InitialPlatformTilt;
            _initialPlatformRoll = trackSettings.InitialPlatformRoll;

            _initialNorthDirection = trackSettings.InitialNorthDirection;

            _isWiresProtectionMode = trackSettings.WiresProtectionMode;

            _ptzDeviceAddress = trackSettings.PTZDeviceAddress;

            _minPanChangedThreshold = trackSettings.MinimalPanChangedThreshold;
            _minTiltChangedThreshold = trackSettings.MinimalTiltChangedThreshold;

            _isTrackStarted = true;
            
            processTelemetry();
        }

        public void StopTrack()
        {
            _logger.LogInfoMessage("StopTrack requested");
            _isTrackStarted = false;
        }

        public void ResetWiresProtection()
        {
            TwistedWiresAngle = 0;
        }

        public async void UpdateCurrentPosition()
        {
            try
            {
                var ptzPan = await _ptzController.RequestPanAngleAsync(_ptzDeviceAddress);
                CurrentPlatformPan = ptzPan;
                var ptzTilt = await _ptzController.RequestTiltAngleAsync(_ptzDeviceAddress);
                CurrentPlatformTilt = ptzTilt;
            }
            catch (TaskCanceledException)
            {
                _logger.LogInfoMessage("UpdateCurrentPosition => request pan/tilt was cancelled by timeout");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }


        private double calcNewTwistedWiresAngle(double startRotationAngle, double endRotationAngle, double currentTwistAngle)
        {
            var startAngleRad = startRotationAngle * LocationUtils.DEGREES_TO_RADIANS;
            var startSin = Math.Sin(startAngleRad);
            var startCos = Math.Cos(startAngleRad);

            var endAngle = endRotationAngle;
            var endAngleRad = endAngle * LocationUtils.DEGREES_TO_RADIANS;
            var endSin = Math.Sin(endAngleRad);
            var endCos = Math.Cos(endAngleRad);

            var angleDirectionRad = getRotationDirection(startSin, startCos, endSin, endCos);
            angleDirectionRad = Math.Round(angleDirectionRad, 2);

            var rotationAngle = getRotationAngle(startSin, startCos, endSin, endCos) * LocationUtils.RADIANS_TO_DEGREES;
            rotationAngle = Math.Round(rotationAngle, 2);

            var newTwistedWiresAngle = currentTwistAngle;

            var directionSign = !double.IsNaN(angleDirectionRad) ? Math.Sign(angleDirectionRad) : 0;

            switch (directionSign)
            {
                case -1:
                    newTwistedWiresAngle -= rotationAngle;
                    break;
                case 1:
                    newTwistedWiresAngle += rotationAngle;
                    break;

                case 0:
                    // by 180
                    // my ptz turn from 0 to 180 counter clockwise, but from 180 to 0 clockwise
                    // and in some variations rotation by 180 sometimes going clockwise and sometime counter clockwise
                    newTwistedWiresAngle += rotationAngle;
                    break;
            }

            return newTwistedWiresAngle;
        }

        private double getRotationDirection(double startAngleSin, double startAngleCos, double endAngleSin, double endAngleCos)
        {
            var x1 = endAngleSin;
            var y1 = endAngleCos;
            var x2 = startAngleSin;
            var y2 = startAngleCos;
            var d1 = Math.Sqrt(x1 * x1 + y1 * y1);
            var d2 = Math.Sqrt(x2 * x2 + y2 * y2);

            return Math.Asin((x1 / d1) * (y2 / d2) - (y1 / d1) * (x2 / d2));
        }

        private double getRotationAngle(double startAngleSin, double startAngleCos, double endAngleSin, double endAngleCos)
        {
            var cosPhi = endAngleSin * startAngleSin + endAngleCos * startAngleCos;
            return (cosPhi >= -1 && cosPhi <= 1) ? Math.Acos(cosPhi) : Math.PI;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
