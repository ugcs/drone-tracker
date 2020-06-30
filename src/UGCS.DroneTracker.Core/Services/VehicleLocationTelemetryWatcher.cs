using System;
using System.Collections.Generic;
using System.Linq;
using UGCS.Sdk.Protocol.Encoding;

namespace UGCS.DroneTracker.Core.Services
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class VehicleLocationTelemetryWatcher : IVehicleTelemetryWatcher
    {
        private readonly VehiclesManager _vehiclesManager;

        private const string ALT_AMSL_TELE_FIELD_CODE = "altitude_amsl";
        private const string ALT_AGL_TELE_FIELD_CODE = "altitude_agl";
        private const string LAT_TELE_FIELD_CODE = "latitude";
        private const string LON_TELE_FIELD_CODE = "longitude";

        private readonly string[] _knownCodes =
            {ALT_AGL_TELE_FIELD_CODE, ALT_AMSL_TELE_FIELD_CODE, LAT_TELE_FIELD_CODE, LON_TELE_FIELD_CODE};


        public event EventHandler<LocationTelemetryDto> LocationTelemetryChanged;
        
        public VehicleLocationTelemetryWatcher(VehiclesManager vehiclesManager)
        {
            _vehiclesManager = vehiclesManager;
        }

        public void OnTelemetryNotification(int vehicleId, List<Telemetry> telemetryList)
        {
            if (_vehiclesManager.SelectedVehicle.Id != vehicleId) return;

            var knownTelemetry = telemetryList.Where(t => Array.IndexOf(_knownCodes, t.TelemetryField.Code) != -1).ToArray();

            if (knownTelemetry.Length == 0) return;

            var telemetryDto = new LocationTelemetryDto(vehicleId);
            
            foreach (var telemetry in knownTelemetry)
            {
                var telemetryValue = telemetry.Value;
                if (telemetryValue == null) continue;
                var value = telemetryValue.DoubleValueSpecified ? telemetryValue.DoubleValue : telemetryValue.FloatValue;

                switch (telemetry.TelemetryField.Code)
                {
                    case ALT_AMSL_TELE_FIELD_CODE:
                        telemetryDto.Altitude = value;
                        break;
                    case LAT_TELE_FIELD_CODE:
                        telemetryDto.Latitude = value;
                        break;
                    case LON_TELE_FIELD_CODE:
                        telemetryDto.Longitude = value;
                        break;
                }
            }
            onLocationTelemetryChanged(telemetryDto);
        }

        private void onLocationTelemetryChanged(LocationTelemetryDto telemetryDto)
        {
            LocationTelemetryChanged?.Invoke(this, telemetryDto);
        }
    }
}