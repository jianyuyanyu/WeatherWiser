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
        private readonly SoundService _soundService;
        private readonly WeatherService _weatherService;
        private DispatcherTimer _dateTimeTimer;
        private DispatcherTimer _weatherTimer;
        public event Action<int[]> SpectrumUpdated;
        public event Action<int[]> LevelUpdated;

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

        public double WindowLeft
        {
            get => _windowLeft;
            set => SetProperty(ref _windowLeft, value);
        }
        private double _windowLeft;

        public double WindowTop
        {
            get => _windowTop;
            set => SetProperty(ref _windowTop, value);
        }
        private double _windowTop;

        public MainWindowViewModel()
        {
            _soundService = new SoundService();
            _soundService.SpectrumUpdated += OnSpectrumUpdated;
            _soundService.LevelUpdated += OnLevelUpdated;
            _weatherService = new WeatherService();
            Initialize();
        }

        public void Initialize()
        {
            LoadSettings();
            InitializeDateTimeTimer();
            InitializeWeatherTimer();
            SetupWindowPosition();
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
            WeatherInfo = await _weatherService.GetWeatherAsync(City);
        }

        private TimeSpan GetNextWeatherUpdateInterval()
        {
            DateTime now = DateTime.Now;
            DateTime nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
            return nextHour - now;
        }

        public void StartSoundService()
        {
            _soundService.Init();
            _soundService.Start();
        }

        public void StopSoundService()
        {
            _soundService.Stop();
            _soundService.Free();
        }

        public void RefreshSoundService()
        {
            _soundService.Stop();
            _soundService.Free();
            _soundService.Init();
            _soundService.Start();
        }

        private void OnSpectrumUpdated(int[] spectrums)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                SpectrumUpdated?.Invoke(spectrums);
            });
        }

        private void OnLevelUpdated(int[] levels)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                LevelUpdated?.Invoke(levels);
            });
        }

        private void SetupWindowPosition()
        {
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == SelectedDisplay) ?? Screen.PrimaryScreen;
            var workingArea = screen.WorkingArea;

            WindowLeft = WindowPosition switch
            {
                "TopLeft" => workingArea.Left + HorizontalOffset,
                "TopRight" => workingArea.Right - 600 - HorizontalOffset,
                "BottomLeft" => workingArea.Left + HorizontalOffset,
                "BottomRight" => workingArea.Right - 600 - HorizontalOffset,
                _ => workingArea.Left + HorizontalOffset
            };

            WindowTop = WindowPosition switch
            {
                "TopLeft" => workingArea.Top + VerticalOffset,
                "TopRight" => workingArea.Top + VerticalOffset,
                "BottomLeft" => workingArea.Bottom - 600 - VerticalOffset,
                "BottomRight" => workingArea.Bottom - 600 - VerticalOffset,
                _ => workingArea.Top + VerticalOffset
            };
        }
    }
}
