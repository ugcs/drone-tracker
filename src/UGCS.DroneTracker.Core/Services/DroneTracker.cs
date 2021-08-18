﻿using System;
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
        double TotalRotationAngle { get; }

        void StartTrack(DroneTrackerSettings trackSettings);
        void StopTrack();

        void ResetTotalRotation();
        Task<bool> UpdateCurrentPosition(byte deviceAddress);
    }
    
    public class DroneTracker : INotifyPropertyChanged, IDroneTracker
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<DroneTracker>();

        private const int MAX_ANGLE_TO_ALLROUND_TWIST_WIRES_CORRECTION = 360;
        private const int ALLROUND_TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE = 179;

        private const int MAX_ANGLE_TO_DEADZONE_WIRES_PROTECTION = 179;

        private readonly IQueryablePTZDeviceController _ptzController;

        private double? _altitude;
        private double? _latitude;
        private double? _longitude;
        
        private bool _isTrackStarted;

        private double _totalRotationAngle;
        private double _currentPlatformTilt;
        private double _currentPlatformPan;
        private Task _twistedWirePanCorrectionTask;

        private double _targetAzimuth;


        private DroneTrackerSettings _trackSettings;

        private double TotalRotationZeroAngle { get; set; }

        public double TotalRotationAngle
        {
            get => _totalRotationAngle;
            private set { _totalRotationAngle = value; OnPropertyChanged(); }
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

            var deviceAddress = _trackSettings.PTZDeviceAddress;

            var initialPlatformLatitude = _trackSettings.InitialPlatformLatitude;
            var initialPlatformLongitude = _trackSettings.InitialPlatformLongitude;
            var initialNorthDirection = _trackSettings.InitialNorthDirection;

            var ptzLatRad = initialPlatformLatitude * LocationUtils.DEGREES_TO_RADIANS;
            var ptzLonRad = initialPlatformLongitude * LocationUtils.DEGREES_TO_RADIANS;

            var vehicleLatRad = _latitude.Value;
            var vehicleLonRad = _longitude.Value;

            var modelAzimuth = LocationUtils.GetAzimuthBetweenCoordinate(ptzLatRad, ptzLonRad, vehicleLatRad, vehicleLonRad);
            modelAzimuth = Math.Round(modelAzimuth, 2);
            TargetAzimuth = modelAzimuth;

            var newPlatformPan = Math.Round(((modelAzimuth + initialNorthDirection + 360) % 360), 2);

            var distance = LocationUtils.GetDistance(
                initialPlatformLatitude * LocationUtils.DEGREES_TO_RADIANS, 
                initialPlatformLongitude * LocationUtils.DEGREES_TO_RADIANS, 
                vehicleLatRad, vehicleLonRad);

            var deltaHeight = _altitude.Value - _trackSettings.InitialPlatformAltitude;
            var tiltAngleRad = Math.Atan2(deltaHeight, distance);
            var modelPitch = Math.Round(tiltAngleRad * LocationUtils.RADIANS_TO_DEGREES, 2);
            
            var newPlatformTilt = modelPitch - _trackSettings.InitialPlatformTilt;


            //_logger.LogDebugMessage($"Selected vehicle telemetry changed: lat:{_latitude}, lon:{_longitude}, alt:{_altitude}");
            //_logger.LogDebugMessage($"Calculated ptz pan:{newPlatformPan}, tilt:{newPlatformTilt}");

            if (Math.Abs(CurrentPlatformPan - newPlatformPan) > _trackSettings.MinimalPanChangedThreshold)
            {
                _logger.LogDebugMessage($"processTelemetry => Selected vehicle current telemetry: lat={_latitude}, lon={_longitude}, alt={_altitude}");
                _logger.LogDebugMessage($"processTelemetry => PTZ need to change Pan. New pan={newPlatformPan}, old pan={CurrentPlatformPan}");


                switch (_trackSettings.WiresProtectionMode)
                {
                    case WiresProtectionMode.Disabled:

                        CurrentPlatformPan = newPlatformPan;
                        _ptzController.PanTo(deviceAddress, CurrentPlatformPan);

                        break;
                    case WiresProtectionMode.AllRound:

                        TotalRotationAngle = calcOverallTwistedWiresAngle(CurrentPlatformPan, newPlatformPan, TotalRotationAngle);
                        _logger.LogInfoMessage($"processTelemetry => (AllRound) twisted wires protection: twisted angle={TotalRotationAngle}");

                        if (Math.Abs(TotalRotationAngle) < MAX_ANGLE_TO_ALLROUND_TWIST_WIRES_CORRECTION)
                        {
                            CurrentPlatformPan = newPlatformPan;
                            _ptzController?.PanTo(deviceAddress, CurrentPlatformPan);
                        }
                        else
                        {
                            _logger.LogInfoMessage($"processTelemetry => (AllRound) start twisted wires protection task");
                            _twistedWirePanCorrectionTask = Task.Factory.StartNew(() => doAllRoundTwistWiresPanCorrection(newPlatformPan))
                                .ContinueWith(task =>
                                {
                                    _logger.LogInfoMessage($"processTelemetry => (AllRound) twisted wires protection task completed");
                                    _twistedWirePanCorrectionTask = null;
                                });
                        }


                        break;

                    case WiresProtectionMode.DeadZone:

                        TotalRotationAngle = calcTwistedWiresAngle(TotalRotationZeroAngle, newPlatformPan);

                        if (Math.Abs(TotalRotationAngle) < MAX_ANGLE_TO_DEADZONE_WIRES_PROTECTION / 2.0)
                        {
                            CurrentPlatformPan = newPlatformPan;
                            _ptzController?.PanTo(deviceAddress, CurrentPlatformPan);
                        }
                        else
                        {
                            _logger.LogInfoMessage($"processTelemetry => (DeadZone) Current calculated pan out of deadzone range. Pan was ignored: twistedWiresAngle={TotalRotationAngle} Requested pan={newPlatformPan}");
                        }

                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (Math.Abs(CurrentPlatformTilt - newPlatformTilt) > _trackSettings.MinimalTiltChangedThreshold)
            {
                _logger.LogDebugMessage($"processTelemetry => Selected vehicle current telemetry: lat={_latitude}, lon={_longitude}, alt={_altitude}");
                _logger.LogDebugMessage($"processTelemetry => PTZ need to change Tilt. New tilt={newPlatformTilt}, old tilt={CurrentPlatformTilt}");
                
                CurrentPlatformTilt = newPlatformTilt;
                _ptzController?.TiltTo(deviceAddress, CurrentPlatformTilt);
            }
        }

        private void doAllRoundTwistWiresPanCorrection(double newPlatformPan)
        {
            _logger.LogInfoMessage($"doAllRoundTwistWiresPanCorrection requested");

            var wiresCorrectionIntermediateAngle = CurrentPlatformPan;
            if (TotalRotationAngle > 0)
            {
                wiresCorrectionIntermediateAngle -= ALLROUND_TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE;
                if (wiresCorrectionIntermediateAngle < 0)
                {
                    wiresCorrectionIntermediateAngle = 360 + wiresCorrectionIntermediateAngle;
                }
            }
            else
            {
                wiresCorrectionIntermediateAngle += ALLROUND_TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE;
                if (wiresCorrectionIntermediateAngle >= 360)
                {
                    wiresCorrectionIntermediateAngle = 360 - wiresCorrectionIntermediateAngle;
                }
            }
            _logger.LogDebugMessage($"doAllRoundTwistWiresPanCorrection: Intermediate rotation => currentPan={CurrentPlatformPan}, interAngle={wiresCorrectionIntermediateAngle}");
            
            CurrentPlatformPan = wiresCorrectionIntermediateAngle;

            // TODO make setting for devices supported set pan complete response
            //_ptzController?.PanToAsync(_ptzDeviceAddress, CurrentPlatformPan)
            //    .GetAwaiter()
            //    .GetResult();

            _ptzController?.PanTo(_trackSettings.PTZDeviceAddress, CurrentPlatformPan);

            var waitingDelay = (int) (ALLROUND_TWIST_WIRES_CORRECTION_INTERMEDIATE_ANGLE / _trackSettings.PanSpeed * 1000);
            _logger.LogDebugMessage($"doAllRoundTwistWiresPanCorrection: Waiting to Intermediate rotation complete: delay={waitingDelay} ms");
            Task.Delay(waitingDelay).Wait();

            
            _logger.LogDebugMessage($"doAllRoundTwistWiresPanCorrection: Intermediate rotation completed");

            TotalRotationAngle = calcOverallTwistedWiresAngle(TotalRotationZeroAngle, newPlatformPan, currentTwistAngle: 0);

            var anglesDelta = getAnglesDelta(CurrentPlatformPan, newPlatformPan);

            _logger.LogDebugMessage($"doAllRoundTwistWiresPanCorrection: finishing rotation => currentPan={CurrentPlatformPan}, interAngle={newPlatformPan}, anglesDelta={anglesDelta}");


            //_ptzController?.PanToAsync(_ptzDeviceAddress, newPlatformPan)
            //    .GetAwaiter()
            //    .GetResult();


            _ptzController?.PanTo(_trackSettings.PTZDeviceAddress, newPlatformPan);
            waitingDelay = (int)(anglesDelta / _trackSettings.PanSpeed * 1000);
            _logger.LogDebugMessage($"doAllRoundTwistWiresPanCorrection: Waiting to finishing rotation complete: delay={waitingDelay} ms");
            Task.Delay(waitingDelay).Wait();

            CurrentPlatformPan = newPlatformPan;

            _logger.LogDebugMessage($"doAllRoundTwistWiresPanCorrection: finishing rotation completed");
        }


        public void StartTrack(DroneTrackerSettings trackSettings)
        {
            _logger.LogInfoMessage($"StartTrack requested => trackSettings:\n{JsonConvert.SerializeObject(trackSettings, Formatting.Indented)}");

            _trackSettings = trackSettings;

            _isTrackStarted = true;
            
            processTelemetry();
        }

        public void StopTrack()
        {
            _logger.LogInfoMessage("StopTrack requested");
            _isTrackStarted = false;
        }

        public void ResetTotalRotation()
        {
            TotalRotationAngle = 0;
            TotalRotationZeroAngle = CurrentPlatformPan;
        }

        public async Task<bool> UpdateCurrentPosition(byte deviceAddress)
        {
            try
            {
                var ptzPan = await _ptzController.RequestPanAngleAsync(deviceAddress);
                CurrentPlatformPan = ptzPan;
                var ptzTilt = await _ptzController.RequestTiltAngleAsync(deviceAddress);
                CurrentPlatformTilt = ptzTilt;
                return true;
            }
            catch (TaskCanceledException)
            {
                _logger.LogInfoMessage("UpdateCurrentPosition => request pan/tilt was cancelled by timeout");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }

            return false;
        }

        private double getAnglesDelta(double startAngle, double endAngle)
        {
            var startAngleRad = startAngle * LocationUtils.DEGREES_TO_RADIANS;
            var startSin = Math.Sin(startAngleRad);
            var startCos = Math.Cos(startAngleRad);

            var endAngleRad = endAngle * LocationUtils.DEGREES_TO_RADIANS;
            var endSin = Math.Sin(endAngleRad);
            var endCos = Math.Cos(endAngleRad);

            var rotationAngle = getRotationAngle(startSin, startCos, endSin, endCos) * LocationUtils.RADIANS_TO_DEGREES;
            rotationAngle = Math.Round(rotationAngle, 2);

            return rotationAngle;
        }

        private double calcTwistedWiresAngle(double startRotationAngle, double endRotationAngle)
        {
            return calcOverallTwistedWiresAngle(startRotationAngle, endRotationAngle, currentTwistAngle: 0);
        }

        private double calcOverallTwistedWiresAngle(double startRotationAngle, double endRotationAngle, double currentTwistAngle)
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
