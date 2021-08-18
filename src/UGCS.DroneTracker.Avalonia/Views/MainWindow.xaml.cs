using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Ninject;
using ReactiveUI;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Core.Settings;
using UGCS.DroneTracker.Core.UGCS;

namespace UGCS.DroneTracker.Avalonia.Views
{
    public class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        private readonly IApplicationSettingsManager _settingManager;
        private readonly UGCSConnection _ugcsConnection;

        public MainWindow()
        {
            _settingManager = App.AppInstance.Kernel.Get<IApplicationSettingsManager>();
            _ugcsConnection = App.AppInstance.Kernel.Get<UGCSConnection>();

            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.WhenActivated(disposables =>
            {
                ViewModel.GoDroneTrackerViewCommand.Execute();

                //this.WhenAnyValue(vm => _ugcsConnection)
                //    .Select(uc => uc.ConnectionStatus)
                //    .Subscribe(connectionStatus => connectionStatusChanged(connectionStatus))
                //    .DisposeWith(disposables);
            });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var settings = _settingManager.GetAppSettings();

                if (settings.WindowPositionLeft == -1 || settings.WindowPositionTop == -1)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Position = new PixelPoint(settings.WindowPositionLeft, settings.WindowPositionTop);
                }
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void connectionStatusChanged(UgcsConnectionStatus connectionStatus)
        {
            if (connectionStatus == UgcsConnectionStatus.Connected)
            {
                ViewModel.GoDroneTrackerViewCommand.Execute();
            }
            else if (connectionStatus == UgcsConnectionStatus.NotConnected)
            {
                ViewModel.GoLoginViewCommand.Execute();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var settings = _settingManager.GetAppSettings();

            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;


            settings.WindowPositionLeft = this.Position.X;
            settings.WindowPositionTop = this.Position.Y;

            _settingManager.Save();

            base.OnClosing(e);
        }
    }
}
