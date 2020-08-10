using System;
using System.Collections.Generic;
using System.Text;
using AutoMapper;
using Ninject;
using Ninject.Modules;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.Avalonia
{
    public class AutoMapperModule : NinjectModule
    {
        public override void Load()
        {
            var mapperConfiguration = createMapperConfiguration();
            Bind<MapperConfiguration>().ToConstant(mapperConfiguration).InSingletonScope();

            // This teaches Ninject how to create automapper instances say if for instance
            // MyResolver has a constructor with a parameter that needs to be injected
            Bind<IMapper>().ToMethod(ctx =>
                new Mapper(mapperConfiguration, type => ctx.Kernel.Get(type)));
        }

        private MapperConfiguration createMapperConfiguration()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<AppSettingsDto, SettingsViewModel>();
                cfg.CreateMap<SettingsViewModel, AppSettingsDto>();
            });

            // TODO remove on production
            //config.AssertConfigurationIsValid();

            return config;
        }
    }
}
