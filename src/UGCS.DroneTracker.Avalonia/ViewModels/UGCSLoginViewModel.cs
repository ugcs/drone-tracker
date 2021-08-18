using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using SerialPortLib;
using UGCS.DroneTracker.Core;
using UGCS.DroneTracker.Core.Services;
using UGCS.DroneTracker.Core.Settings;
using UGCS.DroneTracker.Core.UGCS;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public class UGCSLoginViewModel : ViewModelBase, IRoutableViewModel, IActivatableViewModel
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<UGCSLoginViewModel>();

        private string _ugcsHost;
        private int _ugcsPort;
        private string _ugcsLogin;
        private string _ugcsPassword;
        private readonly IApplicationSettingsManager _settingsManager;
        private readonly UGCSConnection _ugcsConnection;
        private readonly VehiclesManager _vehiclesManager;
        private string _message;
        private bool _manualLogin;
        private bool _showLoginParams;

        public string UrlPathSegment => "UGCSLogin";
        public ViewModelActivator Activator { get; }
        public IScreen HostScreen { get; }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; set; }

        public string UGCSHost
        {
            get => _ugcsHost;
            set => this.RaiseAndSetIfChanged(ref _ugcsHost, value);
        }

        public int UGCSPort
        {
            get => _ugcsPort;
            set => this.RaiseAndSetIfChanged(ref _ugcsPort, value);
        }

        public string UGCSLogin
        {
            get => _ugcsLogin;
            set => this.RaiseAndSetIfChanged(ref _ugcsLogin, value);
        }

        public string UGCSPassword
        {
            get => _ugcsPassword;
            set => this.RaiseAndSetIfChanged(ref _ugcsPassword, value);
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public bool ShowLoginParams
        {
            get => _showLoginParams;
            set => this.RaiseAndSetIfChanged(ref _showLoginParams, value);
        }


        public UGCSLoginViewModel(IScreen hostScreen, UGCSConnection ugcsConnection, VehiclesManager vehiclesManager, IApplicationSettingsManager settingsManager)
        {
            Activator = new ViewModelActivator();
            HostScreen = hostScreen;
            _settingsManager = settingsManager;
            _ugcsConnection = ugcsConnection;
            _vehiclesManager = vehiclesManager;

            LoginCommand = ReactiveCommand.CreateFromTask(doLogin);

            var settings = (AppSettingsDto)_settingsManager.GetAppSettings();

            processConnectionStatus(ugcsConnection.ConnectionStatus);

            UGCSHost = settings.UGCSHost;
            UGCSPort = settings.UGCSPort;
            UGCSLogin = settings.UGCSLogin;
            UGCSPassword = settings.UGCSPassword;

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                this.WhenAnyValue(vm => vm._ugcsConnection.ConnectionStatus)
                    .Subscribe(connectionStatus => connectionStatusChanged(connectionStatus))
                    .DisposeWith(disposables);
            });
        }

        private void processConnectionStatus(UgcsConnectionStatus connectionStatus)
        {
            switch (connectionStatus)
            {
                case UgcsConnectionStatus.Connecting:
                    Message = "Connecting...";
                    ShowLoginParams = false;
                    break;
                case UgcsConnectionStatus.Connected:
                    Message = "Connected";
                    ShowLoginParams = false;
                    break;
                case UgcsConnectionStatus.NotConnected:
                    Message = "Not connected";
                    ShowLoginParams = true;
                    break;
            }
        }

        private void connectionStatusChanged(UgcsConnectionStatus connectionStatus)
        {
            processConnectionStatus(connectionStatus);
            if (connectionStatus == UgcsConnectionStatus.Connected)
            {
                if (!_manualLogin)
                {
                    _logger.LogInfoMessage("GoDroneTrackerViewCommand.Execute()");
                    (HostScreen as MainWindowViewModel)?.GoDroneTrackerViewCommand.Execute();
                }
            }
        }

        private async Task doLogin()
        {
            if (_ugcsConnection.ConnectionStatus == UgcsConnectionStatus.Connected) return;
            
            _logger.LogInfoMessage("Try to connect to UGCS");

            _manualLogin = true;

            Message = "Connecting to UGCS...";

            await _ugcsConnection.ConnectAsync(UGCSHost, UGCSPort, UGCSLogin, UGCSPassword);
            if (_ugcsConnection.ConnectionStatus == UgcsConnectionStatus.Connected)
            {
                _logger.LogInfoMessage("Connected to UGCS");

                _logger.LogInfoMessage("Initialize VehicleManager");
                await _vehiclesManager.Initialize();
                _logger.LogInfoMessage("VehicleManager initialized");

                var settings = (AppSettingsDto)_settingsManager.GetAppSettings();

                settings.UGCSHost = UGCSHost;
                settings.UGCSPort = UGCSPort;
                settings.UGCSLogin = UGCSLogin;
                settings.UGCSPassword = UGCSPassword;

                _settingsManager.Save(settings);

                (HostScreen as MainWindowViewModel)?.GoDroneTrackerViewCommand.Execute();
            }
            else
            {
                Message = "Not connected";
                _logger.LogError("Not connected to UGCS");
            }
            _manualLogin = false;
        }
    }
}
