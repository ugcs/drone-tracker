using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UGCS.DroneTracker.Core.UGCS;
using UGCS.Sdk.Protocol.Encoding;
using ugcs_at.Services;
using ugcs_at.UGCS;

namespace UGCS.DroneTracker.Core.Services
{
    public interface IVehicleTelemetryWatcher
    {
        void OnTelemetryNotification(int vehicleId, List<Telemetry> telemetryList);
    }

    public class VehiclesManager
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<VehiclesManager>();

        private readonly UGCSFacade _ugcsFacade;
        private readonly VehicleConnectionWatcher _connectedTelemetryWatcher;
        private Vehicle _selectedVehicle;
        private VehicleLocationTelemetryWatcher _locationTelemetryWatcher;

        public event EventHandler<List<Vehicle>> VehicleListChanged;
        public event EventHandler<Vehicle> SelectedVehicleChanged;
        public event EventHandler<ConnectedStatusChangedEventArgs> SelectedVehicleConnectedChanged;
        public event EventHandler<LocationTelemetryDto> SelectedVehicleLocationTelemetryChanged;

        public Vehicle SelectedVehicle
        {
            get => _selectedVehicle;
            set { _selectedVehicle = value; onSelectedVehicleChanged(raiseEvent: false); }
        }

        public List<Vehicle> Vehicles { get; private set; } = new List<Vehicle>();

        public List<IVehicleTelemetryWatcher> TelemetryWatchers { get; } = new List<IVehicleTelemetryWatcher>(); 

        public VehiclesManager(UGCSFacade ugcsFacade)
        {
            _ugcsFacade = ugcsFacade;

            _connectedTelemetryWatcher = new VehicleConnectionWatcher();
            _connectedTelemetryWatcher.ConnectedStatusChanged += connectedTelemetryWatcher_OnConnectedStatusChanged;
            TelemetryWatchers.Add(_connectedTelemetryWatcher);

            _locationTelemetryWatcher = new VehicleLocationTelemetryWatcher(this);
            _locationTelemetryWatcher.LocationTelemetryChanged += locationTelemetryWatcher_LocationTelemetryChanged;
            TelemetryWatchers.Add(_locationTelemetryWatcher);
        }

        private void locationTelemetryWatcher_LocationTelemetryChanged(object? sender, LocationTelemetryDto e)
        {
            onSelectedVehicleLocationTelemetryChanged(e);
        }

        private void onSelectedVehicleLocationTelemetryChanged(LocationTelemetryDto locationTelemetryDto)
        {
            SelectedVehicleLocationTelemetryChanged?.Invoke(this, locationTelemetryDto);
        }

        private void connectedTelemetryWatcher_OnConnectedStatusChanged(object? sender, ConnectedStatusChangedEventArgs e)
        {
            var vehicleId = e.VehicleId;
            if (SelectedVehicle != null && SelectedVehicle.Id == vehicleId)
            {
                onSelectedVehicleConnectedChanged(e.IsConnected);
            }
        }

        public async Task Initialize()
        {
            _logger.LogInfoMessage("Initializing");
            SelectedVehicle = null;
            onVehicleListChanged();
            onSelectedVehicleChanged();

            await getVehicleListAsync();
            _logger.LogInfoMessage("Get vehicles list - OK");

            _ugcsFacade.SubscribeToVehicleModification(facade_VehicleChanged);
            _logger.LogInfoMessage("Subscribe to vehicle modification - OK");
            await getSelectedVehicle();
            _logger.LogInfoMessage("Get selected vehicle - OK");

            _ugcsFacade.SubscribeToMissionPreferences(facade_SelectedVehicleChanged);
            _logger.LogInfoMessage("Subscribe to Mission Preferences - OK");

            _ugcsFacade.SubscribeToTelemetry(facade_TelemetryNotificationHandler);
            _logger.LogInfoMessage("Subscribe to telemetry - OK");
        }

        public bool IsConnected(Vehicle vehicle)
        {
            return vehicle != null && _connectedTelemetryWatcher.IsConnected(vehicle.Id);
        }

        private void facade_VehicleChanged(Vehicle vehicle, ModificationType modificationType)
        {
            _logger.LogDebugMessage($"facade_VehicleListChanged enter {vehicle.Name} / {modificationType}");

            switch (modificationType)
            {
                case ModificationType.MT_CREATE:
                    Vehicles.Add(vehicle);
                    onVehicleListChanged();
                    break;
                case ModificationType.MT_UPDATE:
                    break;
                case ModificationType.MT_DELETE:
                    var vehicleToRemove = Vehicles.FirstOrDefault(v => v.Id == vehicle.Id);
                    if (vehicleToRemove != null)
                    {
                        Vehicles.Remove(vehicleToRemove);
                        onVehicleListChanged();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(modificationType), modificationType, null);
            }
        }

        private void facade_TelemetryNotificationHandler(int vehicleId, List<Telemetry> telemetryList)
        {
            TelemetryWatchers.ForEach(tw => tw.OnTelemetryNotification(vehicleId, telemetryList));
        }

        private void facade_SelectedVehicleChanged(int? vehicleId)
        {
            _logger.LogDebugMessage($"facade_SelectedVehicleChanged: selectedID={vehicleId}");
            updateSelectedVehicle(vehicleId);
        }

        private void updateSelectedVehicle(int? vehicleId)
        {
            _logger.LogDebugMessage($"updateSelectedVehicle: selectedID={vehicleId}");
            if (vehicleId.HasValue)
            {
                var vehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId);
                SelectedVehicle = vehicle ?? null;
            }
            else
            {
                SelectedVehicle = null;
            }

            onSelectedVehicleChanged();
        }


        private void onVehicleListChanged()
        {
            VehicleListChanged?.Invoke(this, Vehicles);
        }

        private void onSelectedVehicleChanged(bool raiseEvent = true)
        {
            _logger.LogDebugMessage($"onSelectedVehicleChanged: raiseEvent={raiseEvent}, selectedVehicle={SelectedVehicle?.Name} ({SelectedVehicle?.Id})");
            if (raiseEvent) SelectedVehicleChanged?.Invoke(this, SelectedVehicle);
            var isConnected = false;
            if (SelectedVehicle != null)
            {
                isConnected = _connectedTelemetryWatcher.IsConnected(SelectedVehicle.Id);
            }
            onSelectedVehicleConnectedChanged(isConnected);
        }

        private void onSelectedVehicleConnectedChanged(bool connected)
        {
            SelectedVehicleConnectedChanged?.Invoke(this, new ConnectedStatusChangedEventArgs() {VehicleId = SelectedVehicle?.Id, IsConnected = connected});
        }

        private async Task getVehicleListAsync()
        {
            try
            {
                var vehicles = await _ugcsFacade.GetVehiclesAsync();
                Vehicles = vehicles;
                onVehicleListChanged();
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                throw;
            }
        }

        private async Task getSelectedVehicle()
        {
            var selectedVehicleId = await _ugcsFacade.GetSelectedVehicleIdAsync();
            updateSelectedVehicle(selectedVehicleId);
        }
    }
}
