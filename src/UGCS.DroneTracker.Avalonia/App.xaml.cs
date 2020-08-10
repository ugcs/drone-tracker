using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using Ninject;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Avalonia.Views;
using UGCS.DroneTracker.Core;
using UGCS.DroneTracker.Core.PTZ;
using UGCS.DroneTracker.Core.Services;
using UGCS.DroneTracker.Core.Settings;
using UGCS.DroneTracker.Core.UGCS;
using ugcs_at.UGCS;

namespace UGCS.DroneTracker.Avalonia
{
    public class App : Application
    {
        public IKernel Kernel => _kernel ??= new StandardKernel(new NinjectBindings(), new AutoMapperModule());
        public static App AppInstance => Current as App;

        public const string ApplicationDataFolderName = "UGCS-DroneTracker";
        private const string LogsFolder = "Logs";
        private const int DaysForLogsDelete = 5;

        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<App>();

        private StandardKernel _kernel;
        private MainWindow _mainWindow;
        private IApplicationSettingsManager _settingsManager;
        private UGCSConnection _ugcsConnection;
        private VehiclesManager _vehiclesManager;
        private IPTZDeviceMessagesTransport _ptzTransport;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            _settingsManager = Kernel.Get<IApplicationSettingsManager>();
            _ugcsConnection = Kernel.Get<UGCSConnection>();
            _vehiclesManager = Kernel.Get<VehiclesManager>();
            _ptzTransport = Kernel.Get<IPTZDeviceMessagesTransport>();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.LogException((Exception) e.ExceptionObject);
        }


        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var appDataFolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    ApplicationDataFolderName
                );

                if (!Directory.Exists(appDataFolderPath))
                {
                    Directory.CreateDirectory(appDataFolderPath);
                }

                setupLogConfig();
                cleanupLogs(appDataFolderPath);

                var settings = _settingsManager.GetAppSettings();

                desktop.Exit += Desktop_Exit;
                desktop.Startup += Desktop_Startup;

                _mainWindow = new MainWindow
                {
                    DataContext = Kernel.Get<MainWindowViewModel>(),
                    Width = settings.WindowWidth,
                    Height = settings.WindowHeight,
                };

                desktop.MainWindow = _mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async void Desktop_Startup(object sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            _logger.LogInfoMessage("App startup begin");
            var settings = _settingsManager.GetAppSettings();

            _logger.LogInfoMessage("Connect to UGCS");
            await _ugcsConnection.ConnectAsync(settings.UGCSHost, settings.UGCSPort, settings.UGCSLogin, settings.UGCSPassword);
            if (_ugcsConnection.ConnectionStatus == UgcsConnectionStatus.Connected)
            {
                _logger.LogInfoMessage("Connected to UGCS");

                _logger.LogInfoMessage("Initialize VehicleManager");
                await _vehiclesManager.Initialize();
                _logger.LogInfoMessage("VehicleManager initialized");
            }
            else
            {
                _logger.LogError("Not connected to UGCS");
            }

            _logger.LogInfoMessage("Initialize PTZ transport");
            // TODO
            //await _ptzTransport.Initialize();
            _ptzTransport.Initialize();
            _logger.LogInfoMessage("PTZ transport initialized");

            _logger.LogInfoMessage("App startup end");
        }

        private void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            var settings = _settingsManager.GetAppSettings();

            var trackVm = Kernel.Get<DroneTrackerViewModel>();
            // TODO do refactor
            settings.InitialPlatformLat = trackVm.InitialPlatformLatitude;
            settings.InitialPlatformLon = trackVm.InitialPlatformLongitude;
            settings.InitialPlatformAlt = trackVm.InitialPlatformAltitude;
            settings.InitialPlatformTilt = trackVm.InitialPlatformTilt;
            settings.InitialPlatformRoll = trackVm.InitialPlatformRoll;
            settings.InitialNorthDir = trackVm.InitialNorthDirection;

            settings.ZeroPTZPanAngle = trackVm.ZeroPTZPanAngle;
            settings.ZeroPTZTiltAngle = trackVm.ZeroPTZTiltAngle;

            _settingsManager.Save();

            _ptzTransport.Teardown();
            _ugcsConnection.CloseConnection();
        }

        private static void setupLogConfig()
        {
            var log4NetConfig = new XmlDocument();
            log4NetConfig.Load(File.OpenRead("log4net.config"));
            var repo = LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(Hierarchy));
            XmlConfigurator.Configure(repo, log4NetConfig["log4net"]);
        }

        private static void cleanupLogs(string appDataFolderPath)
        {
            var dirInfo = new DirectoryInfo(Path.Combine(appDataFolderPath, LogsFolder));
            if (!dirInfo.Exists)
                return;

            var files = dirInfo.GetFiles("log-*.txt");
            if (files.Length == 0)
                return;
            var date = DateTime.Now.AddDays(-DaysForLogsDelete);
            foreach (var file in files)
            {
                if (file.CreationTime < date)
                {
                    file.Delete();
                }
            }
        }

    }
}
