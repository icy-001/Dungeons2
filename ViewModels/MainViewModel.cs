using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dungeons2.ViewModels
{
    public class HomeViewModel : ObservableObject { }
    public class SettingsViewModel : ObservableObject { }

    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial object CurrentView { get; set; }

        public MainViewModel()
        {
            CurrentView = new HomeViewModel();
        }

        [RelayCommand]
        private void Navigate(string viewName)
        {
            CurrentView = viewName switch
            {
                "Home" => new HomeViewModel(),
                "Settings" => new SettingsViewModel(),
                _ => CurrentView
            };
        }
    }
}