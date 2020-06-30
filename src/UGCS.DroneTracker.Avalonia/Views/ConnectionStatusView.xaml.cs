using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using UGCS.DroneTracker.Avalonia.ViewModels;
using UGCS.DroneTracker.Core.UGCS;
using ugcs_at.UGCS;

namespace UGCS.DroneTracker.Avalonia.Views
{
    public class UgcsConnectionStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var result = UIResourcesHelper.LightGrayColor;
            if (value == null) return result;
            var status = (UgcsConnectionStatus) value;

            var isOuterColor = true;
            if (parameter != null)
            {
                var strParam = parameter.ToString();
                isOuterColor = strParam switch
                {
                    "outer" => true,
                    "inner" => false,
                    _ => true
                };
            }

            result = status switch
            {
                UgcsConnectionStatus.NotConnected => isOuterColor ? UIResourcesHelper.SPHRed200 : UIResourcesHelper.SPHRed500,
                UgcsConnectionStatus.Connecting => isOuterColor ? UIResourcesHelper.LightYellowColor : UIResourcesHelper.DarkYellowColor,
                UgcsConnectionStatus.Connected => isOuterColor ? UIResourcesHelper.SPHGreen200 : UIResourcesHelper.SPHGreen500,
                _ => throw new ArgumentOutOfRangeException()
            };

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolConnectedStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return UIResourcesHelper.LightGrayColor;
            var isConnected = (bool) value;

            var isOuterColor = true;
            if (parameter != null)
            {
                var strParam = parameter.ToString();
                isOuterColor = strParam switch
                {
                    "outer" => true,
                    "inner" => false,
                    _ => true
                };
            }

            return isConnected ? 
                isOuterColor ? UIResourcesHelper.SPHGreen200 : UIResourcesHelper.SPHGreen500 :
                isOuterColor ? UIResourcesHelper.SPHRed200 : UIResourcesHelper.SPHRed500;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class ConnectionStatusView : ReactiveUserControl<ConnectionStatusViewModel>
    {
        public ConnectionStatusView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
