using ReactiveUI;
using UGCS.DroneTracker.Core.PTZ;
using UGCS.DroneTracker.Core.Services;
using UGCS.DroneTracker.Core.UGCS;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public class ConnectionStatusViewModel : ViewModelBase
    {
        private UgcsConnectionStatus _ugcsStatus;
        private readonly UGCSConnection _ugcsConnection;
        private readonly VehiclesManager _vehiclesManager;
        private bool _isSelectedVehicleConnected;
        private bool _isPtzConnected;
        private IPTZDeviceMessagesTransport _ptzTransport;

        public UgcsConnectionStatus UgcsStatus
        {
            get => _ugcsStatus;
            set => this.RaiseAndSetIfChanged(ref _ugcsStatus, value);
        }

        public bool IsSelectedVehicleConnected
        {
            get => _isSelectedVehicleConnected;
            set => this.RaiseAndSetIfChanged(ref _isSelectedVehicleConnected, value);
        }

        public bool IsPTZConnected
        {
            get => _isPtzConnected;
            set => this.RaiseAndSetIfChanged(ref _isPtzConnected, value);
        }

        public ConnectionStatusViewModel(UGCSConnection ugcsConnection, 
            VehiclesManager vehiclesManager, 
            IPTZDeviceMessagesTransport ptzTransport)
        {
            _ugcsConnection = ugcsConnection;
            UgcsStatus = _ugcsConnection.ConnectionStatus;
            _ugcsConnection.ConnectionStatusChanged += _ugcsConnection_ConnectionStatusChanged;

            _vehiclesManager = vehiclesManager;
            _vehiclesManager.SelectedVehicleConnectedChanged += _vehiclesManager_SelectedVehicleConnectedChanged;

            _ptzTransport = ptzTransport;
            _ptzTransport.ConnectionStatusChanged += _ptzTransport_ConnectionStatusChanged;
        }

        private void _ptzTransport_ConnectionStatusChanged(object sender, bool ptzConnectionStatus)
        {
            IsPTZConnected = ptzConnectionStatus;
        }

        private void _ugcsConnection_ConnectionStatusChanged(object sender, UgcsConnectionStatus newStatus)
        {
            UgcsStatus = newStatus;
        }

        private void _vehiclesManager_SelectedVehicleConnectedChanged(object sender, ConnectedStatusChangedEventArgs e)
        {
            IsSelectedVehicleConnected = e.VehicleId.HasValue && e.IsConnected;
        }
    }
}