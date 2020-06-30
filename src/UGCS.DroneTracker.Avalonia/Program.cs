using Avalonia;
using Avalonia.Logging.Serilog;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Splat;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Avalonia.Views;


namespace UGCS.DroneTracker.Avalonia
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            Locator.CurrentMutable.Register(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
            Locator.CurrentMutable.Register(() => new DroneTrackerView(), typeof(IViewFor<DroneTrackerViewModel>));
            Locator.CurrentMutable.Register(() => new UGCSLoginView(), typeof(IViewFor<UGCSLoginViewModel>));

            return AppBuilder.Configure<App>()
                           .UsePlatformDetect()
                           .LogToDebug()
                           .UseReactiveUI();
        }
    }
}
