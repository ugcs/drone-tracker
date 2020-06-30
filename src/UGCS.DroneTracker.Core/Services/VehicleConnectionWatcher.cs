using System;
using System.Collections.Generic;
using System.Linq;
using UGCS.DroneTracker.Core.Services;
using UGCS.Sdk.Protocol.Encoding;

namespace ugcs_at.Services
{
    public class ConnectedStatusChangedEventArgs
    {
        public int? VehicleId { get; set; }
        public bool IsConnected { get; set; }
    }


    public class VehicleConnectionWatcher : IVehicleTelemetryWatcher
    {
        private const string DownlinkPresentTelemetryCode = "downlink_present";

        private readonly HashSet<int> _connectedVehicleIds = new HashSet<int>();

        public event EventHandler<ConnectedStatusChangedEventArgs> ConnectedStatusChanged;

        public bool IsConnected(int vehicleId)
        {
            return _connectedVehicleIds.Contains(vehicleId);
        }

        public void OnTelemetryNotification(int vehicleId, List<Telemetry> telemetryList)
        {
            var linkTelemetry = telemetryList.Where(t => t.TelemetryField.Code == DownlinkPresentTelemetryCode);
            foreach (var telemetry in linkTelemetry)
            {
                var connected = telemetry.Value?.BoolValue ?? false;

                if (connected)
                {
                    _connectedVehicleIds.Add(vehicleId);
                }
                else
                {
                    _connectedVehicleIds.Remove(vehicleId);
                }
                ConnectedStatusChanged?.Invoke(this, new ConnectedStatusChangedEventArgs() {VehicleId = vehicleId, IsConnected = connected});
            }
        }
    }
}