using Prism.Mvvm;
using System.Windows.Forms;
using WeatherWiser.Helpers;

namespace WeatherWiser.ViewModels
{
    public abstract class BaseSettingsViewModel : BindableBase
    {
        private static class SettingKeys
        {
            public const string ClockUpdateInterval = "ClockUpdateInterval";
            public const string AlwaysOnTop = "AlwaysOnTop";
            public const string WindowPosition = "WindowPosition";
            public const string HorizontalOffset = "HorizontalOffset";
            public const string VerticalOffset = "VerticalOffset";
            public const string FontSize = "FontSize";
            public const string Display = "Display";
            public const string City = "City";
        }

        public int ClockUpdateInterval
        {
            get => _clockUpdateInterval;
            set => SetProperty(ref _clockUpdateInterval, value);
        }
        private int _clockUpdateInterval;

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set => SetProperty(ref _alwaysOnTop, value);
        }
        private bool _alwaysOnTop;

        public string WindowPosition
        {
            get => _windowPosition;
            set => SetProperty(ref _windowPosition, value);
        }
        private string _windowPosition;

        public int HorizontalOffset
        {
            get => _horizontalOffset;
            set => SetProperty(ref _horizontalOffset, value);
        }
        private int _horizontalOffset;

        public int VerticalOffset
        {
            get => _verticalOffset;
            set => SetProperty(ref _verticalOffset, value);
        }
        private int _verticalOffset;

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }
        private int _fontSize;

        public int MediumFontSize
        {
            get => _mediumFontSize;
            set => SetProperty(ref _mediumFontSize, value);
        }
        private int _mediumFontSize;

        public int SmallFontSize
        {
            get => _smallFontSize;
            set => SetProperty(ref _smallFontSize, value);
        }
        private int _smallFontSize;

        public string SelectedDisplay
        {
            get => _selectedDisplay;
            set => SetProperty(ref _selectedDisplay, value);
        }
        private string _selectedDisplay;

        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }
        private string _city;

        protected void LoadSettings()
        {
            ClockUpdateInterval = SettingsHelper.GetSetting(SettingKeys.ClockUpdateInterval, 1000);
            AlwaysOnTop = SettingsHelper.GetSetting(SettingKeys.AlwaysOnTop, false);
            WindowPosition = SettingsHelper.GetSetting(SettingKeys.WindowPosition, "TopLeft");
            HorizontalOffset = SettingsHelper.GetSetting(SettingKeys.HorizontalOffset, 0);
            VerticalOffset = SettingsHelper.GetSetting(SettingKeys.VerticalOffset, 0);
            FontSize = SettingsHelper.GetSetting(SettingKeys.FontSize, 36);
            MediumFontSize = FontSize / 3;
            SmallFontSize = FontSize / 5;
            SelectedDisplay = SettingsHelper.GetSetting(SettingKeys.Display, Screen.PrimaryScreen.DeviceName);
            City = SettingsHelper.GetSetting(SettingKeys.City, "Tokyo, JP");
        }

        public void SaveSettings()
        {
            SettingsHelper.SaveSetting(SettingKeys.ClockUpdateInterval, ClockUpdateInterval);
            SettingsHelper.SaveSetting(SettingKeys.AlwaysOnTop, AlwaysOnTop);
            SettingsHelper.SaveSetting(SettingKeys.WindowPosition, WindowPosition);
            SettingsHelper.SaveSetting(SettingKeys.HorizontalOffset, HorizontalOffset);
            SettingsHelper.SaveSetting(SettingKeys.VerticalOffset, VerticalOffset);
            SettingsHelper.SaveSetting(SettingKeys.FontSize, FontSize);
            SettingsHelper.SaveSetting(SettingKeys.Display, SelectedDisplay);
            SettingsHelper.SaveSetting(SettingKeys.City, City);
        }
    }
}
