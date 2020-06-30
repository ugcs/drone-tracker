using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using UGCS.DroneTracker.Avalonia.ViewModels;

namespace UGCS.DroneTracker.Avalonia.Views
{
    public class UGCSLoginView : ReactiveUserControl<UGCSLoginViewModel>
    {
        public UGCSLoginView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.WhenActivated(disposables => { });
            AvaloniaXamlLoader.Load(this);
        }
    }
}
