using CommunityToolkit.Mvvm.ComponentModel;
using Dungeons2.Services;

namespace Dungeons2.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public bool LogSheetsEnabled { get; } = GoogleSheetsService.Instance.Enabled;
    }
}
