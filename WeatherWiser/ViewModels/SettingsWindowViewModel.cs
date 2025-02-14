using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace WeatherWiser.ViewModels
{
    public partial class SettingsWindowViewModel : BaseSettingsViewModel
    {
        public ObservableCollection<string> Displays { get; }

        public event Action SettingsChanged;

        public SettingsWindowViewModel()
        {
            LoadSettings();
            Displays = new ObservableCollection<string>(Screen.AllScreens.Select(s => s.DeviceName));
        }

        public new void SaveSettings()
        {
            base.SaveSettings();
            SettingsChanged?.Invoke();
        }
    }
}
