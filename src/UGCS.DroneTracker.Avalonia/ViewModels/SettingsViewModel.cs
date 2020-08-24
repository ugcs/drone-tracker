using System.Collections.Generic;
using System.Reactive;
using AutoMapper;
using Newtonsoft.Json;
using ReactiveUI;
using UGCS.DroneTracker.Core;
using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public class SettingsViewModel : ViewModelBase, IRoutableViewModel
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<SettingsViewModel>();

        private readonly IApplicationSettingsManager _settingsManager;
        private readonly MapperConfiguration _mapperConfig;


        private string _ugcsHost;
        private int _ugcsPort;
        private string _ugcsLogin;
        private string _ugcsPassword;
        private string _ptzUdpHost;
        private int _ptzUdpPort;

        private string _ptzSerialPortName;
        private int _ptzSerialPortSpeed;
        private byte _ptzDeviceAddress;
        private PTZDeviceTransportType _ptzTransportType;
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
            WiresProtectionMode.AllRound,
            WiresProtectionMode.DeadZone
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

        public PTZDeviceTransportType PTZTransportType
        {
            get => _ptzTransportType;
            set => this.RaiseAndSetIfChanged(ref _ptzTransportType, value);
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

        public SettingsViewModel(IScreen hostScreen, IApplicationSettingsManager settingsManager, MapperConfiguration mapperConfig)
        {
            HostScreen = hostScreen;
            _settingsManager = settingsManager;
            _mapperConfig = mapperConfig;

            ApplyAndGoBackCommand = ReactiveCommand.Create(doApplyAndGoBack);

            var settings = (AppSettingsDto)_settingsManager.GetAppSettings();

            _mapperConfig.CreateMapper().Map<AppSettingsDto, SettingsViewModel>(settings, this);

            _logger.LogDebugMessage($"SettingsViewModel ctor => app settings :\n{JsonConvert.SerializeObject(settings, Formatting.Indented)}");
        }



        private void doApplyAndGoBack()
        {
            var appSettingsDto = (AppSettingsDto)_settingsManager.GetAppSettings();

            _mapperConfig.CreateMapper().Map<SettingsViewModel, AppSettingsDto>(this, appSettingsDto);

            _logger.LogDebugMessage($"SettingsViewModel doApplyAndGoBack => app settings :\n{JsonConvert.SerializeObject(appSettingsDto, Formatting.Indented)}");
            
            _settingsManager.Save(appSettingsDto);

            GoBackCommand.Execute();
        }
    }
}
