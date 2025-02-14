using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Threading;
using WeatherWiser.Models;
using WeatherWiser.Services;

namespace WeatherWiser.ViewModels
{
    public partial class MainWindowViewModel : BaseSettingsViewModel
    {
        private readonly WeatherService weatherService;
        private DispatcherTimer _dateTimeTimer;
        private DispatcherTimer _weatherTimer;

        public DateTimeInfo DateTimeInfo
        {
            get => _dateTimeInfo;
            set => SetProperty(ref _dateTimeInfo, value);
        }
        private DateTimeInfo _dateTimeInfo;

        public WeatherInfo WeatherInfo
        {
            get => _weatherInfo;
            set => SetProperty(ref _weatherInfo, value);
        }
        private WeatherInfo _weatherInfo;

        public MainWindowViewModel()
        {
            weatherService = new WeatherService();
        }

        public void Initialize()
        {
            LoadSettings();
            InitializeTimers();
            UpdateWindowTopmost();
            UpdateWindowPosition();
        }

        private void InitializeTimers()
        {
            InitializeDateTimeTimer();
            InitializeWeatherTimer();
        }

        private void InitializeDateTimeTimer()
        {
            _dateTimeTimer?.Stop();
            UpdateDateTimeInfo();
            _dateTimeTimer = CreateTimer(OnDateTimedEvent, TimeSpan.FromMilliseconds(ClockUpdateInterval));
        }

        private void InitializeWeatherTimer()
        {
            _weatherTimer?.Stop();
            UpdateWeatherInfo();
            _weatherTimer = CreateTimer(OnWeatherTimedEvent, GetNextWeatherUpdateInterval());
        }

        private DispatcherTimer CreateTimer(EventHandler tickHandler, TimeSpan interval)
        {
            var timer = new DispatcherTimer { Interval = interval };
            timer.Tick += tickHandler;
            timer.Start();
            return timer;
        }

        private void OnDateTimedEvent(object sender, EventArgs e)
        {
            UpdateDateTimeInfo();
            UpdateWindowPosition();
        }

        private void OnWeatherTimedEvent(object sender, EventArgs e)
        {
            UpdateWeatherInfo();
            _weatherTimer.Interval = GetNextWeatherUpdateInterval();
        }

        private void UpdateDateTimeInfo()
        {
            DateTimeInfo = new DateTimeInfo();
        }

        private async void UpdateWeatherInfo()
        {
            WeatherInfo = await weatherService.GetWeatherAsync(City);
        }

        private TimeSpan GetNextWeatherUpdateInterval()
        {
            DateTime now = DateTime.Now;
            DateTime nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
            return nextHour - now;
        }

        private void UpdateWindowTopmost()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Topmost = AlwaysOnTop;
            }
        }

        private void UpdateWindowPosition()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == SelectedDisplay) ?? Screen.PrimaryScreen;
                var workingArea = screen.WorkingArea;

                mainWindow.Left = WindowPosition switch
                {
                    "TopLeft" => workingArea.Left + HorizontalOffset,
                    "TopRight" => workingArea.Right - mainWindow.Width - HorizontalOffset,
                    "BottomLeft" => workingArea.Left + HorizontalOffset,
                    "BottomRight" => workingArea.Right - mainWindow.Width - HorizontalOffset,
                    _ => mainWindow.Left
                };

                mainWindow.Top = WindowPosition switch
                {
                    "TopLeft" => workingArea.Top + VerticalOffset,
                    "TopRight" => workingArea.Top + VerticalOffset,
                    "BottomLeft" => workingArea.Bottom - mainWindow.Height - VerticalOffset,
                    "BottomRight" => workingArea.Bottom - mainWindow.Height - VerticalOffset,
                    _ => mainWindow.Top
                };
            }
        }
    }
}
