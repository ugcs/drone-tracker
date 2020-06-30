using System;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Core.Helpers;

namespace UGCS.DroneTracker.Avalonia.Views
{
    public class GeodeticLocationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? null : 
                LocationUtils.DegreesToString(LocationUtils.RadiansToFullDegrees((double) value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RadiansToDegreesLocationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? null : Math.Round((double)value * LocationUtils.RADIANS_TO_DEGREES, 7).ToString(CultureInfo.CurrentCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }




    public class DroneTrackerView : ReactiveUserControl<DroneTrackerViewModel>
    {
        public DroneTrackerView()
        {
            this.InitializeComponent();
        }

        private Grid _remoteControlButtonsGrid => this.FindControl<Grid>("RemoteButtons");

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.WhenActivated(disposables =>
            {
                _remoteControlButtonsGrid
                    .AddHandler(InputElement.PointerPressedEvent, (sender, args) =>
                        {
                            if (!getRemoteControlButtonAction(args, out var rcActionType)) return;

                            if (!isRemoteControlPositioningAction(rcActionType)) return;

                            var vm = this.DataContext as DroneTrackerViewModel;
                            // TODO i dunno why command is not executed here
                            //vm?.StartPositioningCommand.Execute(rcActionType);
                            vm?.DoStartPositioning(rcActionType);
                        }
                        ,
                        RoutingStrategies.Tunnel
                    )
                    .DisposeWith(disposables);

                _remoteControlButtonsGrid
                    .AddHandler(InputElement.PointerReleasedEvent, (sender, args) =>
                    {
                        if (!getRemoteControlButtonAction(args, out var rcActionType)) return;

                        if (!isRemoteControlPositioningAction(rcActionType)) return;

                        var vm = this.DataContext as DroneTrackerViewModel;
                        // TODO i dunno why command is not executed here
                        //vm?.StopPositioningCommand.Execute();
                        vm?.DoStopPositioning();
                    },
                        RoutingStrategies.Tunnel
                    )
                    .DisposeWith(disposables);

            });
        }

        private static bool isRemoteControlPositioningAction(RemoteControlActionType rcActionType)
        {
            return rcActionType == RemoteControlActionType.PanLeft ||
                     rcActionType == RemoteControlActionType.PanRight ||
                     rcActionType == RemoteControlActionType.TiltUp ||
                     rcActionType == RemoteControlActionType.TiltDown;
        }


        private static bool getRemoteControlButtonAction(RoutedEventArgs args, out RemoteControlActionType rcActionType)
        {
            rcActionType = RemoteControlActionType.Stop;
            var sourceButton = args.Source as Button;
            if (sourceButton == null && args.Source is ILogical logicalSender)
            {
                sourceButton = logicalSender.GetLogicalParent<Button>();
            }

            if (sourceButton == null) return false;

            rcActionType = (RemoteControlActionType)
                (sourceButton.CommandParameter ?? RemoteControlActionType.Stop);
            return true;
        }

    }

}
