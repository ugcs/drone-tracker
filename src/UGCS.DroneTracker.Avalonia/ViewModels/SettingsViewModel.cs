using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;
using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public class SettingsViewModel : ViewModelBase, IRoutableViewModel
    {
        private string _ugcsHost;
        private int _ugcsPort;
        private string _ugcsLogin;
        private string _ugcsPassword;
        private string _ptzUdpHost;
        private int _ptzUdpPort;
        private readonly IApplicationSettingsManager _settingsManager;

        private string _ptzSerialPortName;
        private int _ptzSerialPortSpeed;
        private byte _ptzDeviceAddress;
        private PTZDeviceTransportType _ptzDeviceTransportType;
        private double _ptzMaxSpeed;
        private double _ptzPanAngleToCoordinateFactor;
        private double _ptzTiltAngleToCoordinateFactor;
        private double _ptzMaxTiltCoordinate;
        private double _ptzMaxPanCoordinate;
        private double _ptzMaxTiltAngle;
        private double _ptzMinTiltAngle;
        private double _ptzMaxPanAngle;
        private double _ptzMinPanAngle;
        private WiresProtectionMode _wiresProtection;
        private double _panSpeed;

        public string UrlPathSegment => "Settings";

        public IScreen HostScreen { get; }

        public List<PTZDeviceTransportType> PtzDeviceTransportTypes { get; set; } = new List<PTZDeviceTransportType>()
        {
            PTZDeviceTransportType.Serial,
            PTZDeviceTransportType.Udp
        };

        public List<WiresProtectionMode> WiresProtectionModes { get; set; } = new List<WiresProtectionMode>()
        {
            WiresProtectionMode.Disabled,
            WiresProtectionMode.AllRound
        };


        public ReactiveCommand<Unit, Unit> GoBackCommand => HostScreen.Router.NavigateBack;
        public ReactiveCommand<Unit, Unit> ApplyAndGoBackCommand { get; set; }

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


        public string PTZUdpHost
        {
            get => _ptzUdpHost;
            set => this.RaiseAndSetIfChanged(ref _ptzUdpHost, value);
        }

        public int PTZUdpPort
        {
            get => _ptzUdpPort;
            set => this.RaiseAndSetIfChanged(ref _ptzUdpPort, value);
        }

        public string PTZSerialPortName
        {
            get => _ptzSerialPortName;
            set => this.RaiseAndSetIfChanged(ref _ptzSerialPortName, value);
        }

        public int PTZSerialPortSpeed
        {
            get => _ptzSerialPortSpeed;
            set => this.RaiseAndSetIfChanged(ref _ptzSerialPortSpeed, value);
        }

        public byte PTZDeviceAddress
        {
            get => _ptzDeviceAddress;
            set => this.RaiseAndSetIfChanged(ref _ptzDeviceAddress, value);
        }

        public PTZDeviceTransportType PtzDeviceTransportType
        {
            get => _ptzDeviceTransportType;
            set => this.RaiseAndSetIfChanged(ref _ptzDeviceTransportType, value);
        }

        public double PTZMaxSpeed
        {
            get => _ptzMaxSpeed;
            set => this.RaiseAndSetIfChanged(ref _ptzMaxSpeed, value);
        }

        public double PTZPanAngleToCoordinateFactor
        {
            get => _ptzPanAngleToCoordinateFactor;
            set => this.RaiseAndSetIfChanged(ref _ptzPanAngleToCoordinateFactor, value);
        }

        public double PTZTiltAngleToCoordinateFactor
        {
            get => _ptzTiltAngleToCoordinateFactor;
            set => this.RaiseAndSetIfChanged(ref _ptzTiltAngleToCoordinateFactor, value);
        }

        public double PTZMaxTiltCoordinate
        {
            get => _ptzMaxTiltCoordinate;
            set => this.RaiseAndSetIfChanged(ref _ptzMaxTiltCoordinate, value);
        }

        public double PTZMaxPanCoordinate
        {
            get => _ptzMaxPanCoordinate;
            set => this.RaiseAndSetIfChanged(ref _ptzMaxPanCoordinate, value);
        }

        public double PTZMaxTiltAngle
        {
            get => _ptzMaxTiltAngle;
            set => this.RaiseAndSetIfChanged(ref _ptzMaxTiltAngle, value);
        }

        public double PTZMinTiltAngle
        {
            get => _ptzMinTiltAngle;
            set => this.RaiseAndSetIfChanged(ref _ptzMinTiltAngle, value);
        }

        public double PTZMaxPanAngle
        {
            get => _ptzMaxPanAngle;
            set => this.RaiseAndSetIfChanged(ref _ptzMaxPanAngle, value);
        }

        public double PTZMinPanAngle
        {
            get => _ptzMinPanAngle;
            set => this.RaiseAndSetIfChanged(ref _ptzMinPanAngle, value);
        }

        public WiresProtectionMode WiresProtection
        {
            get => _wiresProtection;
            set => this.RaiseAndSetIfChanged(ref _wiresProtection, value);
        }

        public double PanSpeed
        {
            get => _panSpeed;
            set => this.RaiseAndSetIfChanged(ref _panSpeed, value);
        }

        public SettingsViewModel(IScreen hostScreen, IApplicationSettingsManager settingsManager)
        {
            HostScreen = hostScreen;
            _settingsManager = settingsManager;

            ApplyAndGoBackCommand = ReactiveCommand.Create(doApplyAndGoBack);

            var settings = (AppSettingsDto)_settingsManager.GetAppSettings();

            UGCSHost = settings.UGCSHost;
            UGCSPort = settings.UGCSPort;
            UGCSLogin = settings.UGCSLogin;
            UGCSPassword = settings.UGCSPassword;

            PTZDeviceAddress = settings.PTZDeviceAddress;

            PtzDeviceTransportType = settings.PTZTransportType;

            PTZUdpHost = settings.PTZUdpHost;
            PTZUdpPort = settings.PTZUdpPort;

            PTZSerialPortName = settings.PTZSerialPortName;
            PTZSerialPortSpeed = settings.PTZSerialPortSpeed;

            PTZMaxSpeed = settings.PTZMaxSpeed;

            PTZMinPanAngle = settings.PTZMinPanAngle;
            PTZMaxPanAngle = settings.PTZMaxPanAngle;

            PTZMinTiltAngle = settings.PTZMinTiltAngle;
            PTZMaxTiltAngle = settings.PTZMaxTiltAngle;

            PTZMaxPanCoordinate = settings.PTZMaxPanCoordinate;
            PTZMaxTiltCoordinate = settings.PTZMaxTiltCoordinate;

            PTZPanAngleToCoordinateFactor = settings.PTZPanAngleToCoordinatesFactor;
            PTZTiltAngleToCoordinateFactor = settings.PTZTiltAngleToCoordinatesFactor;

            WiresProtection = settings.WiresProtection;
            PanSpeed = settings.PanSpeed;
        }



        private void doApplyAndGoBack()
        {
            var settings = (AppSettingsDto)_settingsManager.GetAppSettings();

            settings.UGCSHost = UGCSHost;
            settings.UGCSPort = UGCSPort;
            settings.UGCSLogin = UGCSLogin;
            settings.UGCSPassword = UGCSPassword;

            settings.PTZDeviceAddress = PTZDeviceAddress;
            settings.PTZTransportType = PtzDeviceTransportType;

            settings.PTZUdpHost = PTZUdpHost;
            settings.PTZUdpPort = PTZUdpPort;

            settings.PTZSerialPortSpeed = PTZSerialPortSpeed;
            settings.PTZSerialPortName = PTZSerialPortName;

            settings.PTZMaxSpeed = PTZMaxSpeed;

            settings.PTZMinPanAngle = PTZMinPanAngle;
            settings.PTZMaxPanAngle = PTZMaxPanAngle;

            settings.PTZMinTiltAngle = PTZMinTiltAngle;
            settings.PTZMaxTiltAngle = PTZMaxTiltAngle;

            settings.PTZMaxPanCoordinate = PTZMaxPanCoordinate;
            settings.PTZMaxTiltCoordinate = PTZMaxTiltCoordinate;


            settings.PTZPanAngleToCoordinatesFactor = PTZPanAngleToCoordinateFactor;
            settings.PTZTiltAngleToCoordinatesFactor = PTZTiltAngleToCoordinateFactor;

            settings.WiresProtection = WiresProtection;
            settings.PanSpeed = PanSpeed;

            _settingsManager.Save(settings);

            GoBackCommand.Execute();
        }
    }
}
