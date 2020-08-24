using System.Reactive;
using System.Reflection;
using Ninject;
using ReactiveUI;
using ugcs_at;

namespace UGCS.DroneTracker.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IScreen
    {
        public string WindowTitle { get; set; }

        public RoutingState Router { get; } = new RoutingState();

        public ReactiveCommand<Unit, IRoutableViewModel> GoSettingsViewCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoLoginViewCommand { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoDroneTrackerViewCommand { get; }

        public MainWindowViewModel()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            WindowTitle = $"UGCS Drone Tracker {version}";

            GoSettingsViewCommand = ReactiveCommand.CreateFromObservable(
                () => Router.Navigate.Execute(App.AppInstance.Kernel.Get<SettingsViewModel>())
            );

            GoDroneTrackerViewCommand = ReactiveCommand.CreateFromObservable(
                () => Router.Navigate.Execute(App.AppInstance.Kernel.Get<DroneTrackerViewModel>())
            );

            GoLoginViewCommand = ReactiveCommand.CreateFromObservable(
                () => Router.Navigate.Execute(App.AppInstance.Kernel.Get<UGCSLoginViewModel>())
            );
        }
    }
}
