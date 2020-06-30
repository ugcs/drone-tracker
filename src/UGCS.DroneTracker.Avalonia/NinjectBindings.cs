using System;
using Ninject;
using Ninject.Modules;
using ReactiveUI;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Core.PelcoD;
using UGCS.DroneTracker.Core.PTZ;
using UGCS.DroneTracker.Core.Services;
using UGCS.DroneTracker.Core.Settings;
using UGCS.DroneTracker.Core.UGCS;
using ugcs_at;
using ugcs_at.UGCS;

namespace UGCS.DroneTracker.Avalonia
{
    public class NinjectBindings : NinjectModule
    {
        private MainWindowViewModel _mainWindowVM;

        public override void Load()
        {
            Bind<IApplicationSettingsStorage<AppSettingsDto>>()
                .To<JsonFileSettingsStorage<AppSettingsDto>>()
                .InSingletonScope()
                .WithConstructorArgument("directory", App.ApplicationDataFolderName)
                .WithConstructorArgument("fileName", "user-settings.json");

            Bind<IApplicationSettingsManager>().To<ApplicationSettingsManager>().InSingletonScope();

            //Bind<MainWindowViewModel>().ToSelf().InSingletonScope();

            _mainWindowVM = new MainWindowViewModel();
            
            Bind<IScreen>().ToConstant(_mainWindowVM).InSingletonScope();
            Bind<MainWindowViewModel>().ToConstant(_mainWindowVM).InSingletonScope();

            Bind<DroneTrackerViewModel>().ToSelf().InSingletonScope();
            Bind<SettingsViewModel>().ToSelf().InSingletonScope();
            Bind<ConnectionStatusViewModel>().ToSelf().InSingletonScope();

            //Bind<TcpClient>().ToSelf().InSingletonScope();
            //Bind<MessageSender>().ToSelf().InSingletonScope();
            //Bind<MessageReceiver>().ToSelf().InSingletonScope();
            //Bind<MessageExecutor>().ToSelf().InSingletonScope()
            //    .OnActivation((executor) => executor.Configuration.DefaultTimeout = 10 * 1000);
            //Bind<TelemetrySubscription>().ToSelf().InSingletonScope();
            //Bind<ObjectModificationSubscription>().ToSelf().InSingletonScope();
            //Bind<EventSubscriptionWrapper>().ToSelf().InSingletonScope();

            Bind<UGCSConnection>().ToSelf().InSingletonScope();
            Bind<UGCSFacade>().ToSelf().InSingletonScope();

            Bind<VehiclesManager>().ToSelf().InSingletonScope();


            Bind<IPTZDeviceMessagesTransport>()
                .ToMethod(ctx => 
                    PTZDeviceTransportFactory.CreateInstance(ctx.Kernel.Get<IApplicationSettingsManager>()))
                .InSingletonScope();

            Bind<PelcoRequestBuilder>()
                .ToMethod(ctx =>
                    {
                        var settings = ctx.Kernel.Get<IApplicationSettingsManager>().GetAppSettings();
                        return new PelcoRequestBuilder(settings.PelcoCodesMapping);
                    })
                .InSingletonScope();
            Bind<PelcoResponseDecoder>()
                .ToMethod(ctx =>
                {
                    var settings = ctx.Kernel.Get<IApplicationSettingsManager>().GetAppSettings();
                    return new PelcoResponseDecoder(settings.PelcoCodesMapping);
                })
                .InSingletonScope();

            Bind<IQueryablePTZDeviceController, IPTZDeviceController>().To<PelcoDeviceController>().InSingletonScope();

            Bind<IDroneTracker>().To<Core.Services.DroneTracker>().InSingletonScope();


        }
    }

    public static class PTZDeviceTransportFactory
    {
        public static IPTZDeviceMessagesTransport CreateInstance(IApplicationSettingsManager settingsManager)
        {
            var settings = settingsManager.GetAppSettings();
            return settings.PTZTransportType switch
            {
                PTZDeviceTransportType.Serial => new SerialPortPTZMessagesTransport(settings.PTZSerialPortName, settings.PTZSerialPortSpeed),
                PTZDeviceTransportType.Udp => new UdpPTZMessagesTransport(settings.PTZUdpHost, settings.PTZUdpPort),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

    }
}
