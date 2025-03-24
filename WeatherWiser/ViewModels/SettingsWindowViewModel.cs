using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using WeatherWiser.Services;

namespace WeatherWiser.ViewModels
{
    public partial class SettingsWindowViewModel : BaseSettingsViewModel
    {
        public bool AutoStartup
        {
            get => _autoStartup;
            set => SetProperty(ref _autoStartup, value);
        }
        private bool _autoStartup;

        private readonly string _currentVersionRunBasePath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private readonly RegistryService _registryService;

        public ObservableCollection<string> Displays { get; }

        public event Action SettingsChanged;

        public SettingsWindowViewModel()
        {
            _registryService = new RegistryService();
            base.LoadSettings();
            AutoStartup = !string.IsNullOrEmpty(_registryService.Read(_currentVersionRunBasePath, "WeatherWiser", string.Empty));
            Displays = [.. Screen.AllScreens.Select(s => s.DeviceName)];
        }

        public new void SaveSettings()
        {
            base.SaveSettings();
            if (AutoStartup)
            {
                _registryService.Write(_currentVersionRunBasePath, "WeatherWiser", Application.ExecutablePath);
            }
            else
            {
                _registryService.Delete(_currentVersionRunBasePath, "WeatherWiser");
            }
            SettingsChanged?.Invoke();
        }
    }
}
