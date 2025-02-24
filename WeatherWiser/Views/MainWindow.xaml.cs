using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace WeatherWiser.Views
{
    public partial class MainWindow : Window
    {
        // WASAPIプロセス
        private readonly WASAPIPROC _process;
        // コードページ
        private readonly bool UNICODE = true;
        // 音量レベルの減衰値
        private readonly short _decay = 300;
        // 画面表示更新用タイマー
        private readonly DispatcherTimer _timer;
        // 音量レベル
        private readonly int[] _levels = [0, 0];
        // 音量レベル（ピーク時）
        private readonly int[] _peekLevels = [0, 0];
        // デバイス番号
        private int _devicenumber = -1;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;
            CompositionTarget.Rendering += OnRendering;

            _process = new WASAPIPROC(WasapiProcess);
            _timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(25/*40Hz*/),
                IsEnabled = false,
            };
            _timer.Tick += Timer_Tick;
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UNICODE, UNICODE);
            InitBass();
        }

        private int WasapiProcess(IntPtr buffer, int length, IntPtr user)
        {
            //Debug.WriteLine($"Process called with length: {length}");
            return length;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            int level = BassWasapi.BASS_WASAPI_GetLevel();
            this._levels[0] = Utils.LowWord32(level);
            this._levels[1] = Utils.HighWord32(level);

            int levelL = (int)Math.Ceiling((double)this._levels[0] / (double)short.MaxValue * 13);
            int levelR = (int)Math.Ceiling((double)this._levels[1] / (double)short.MaxValue * 13);

            this._peekLevels[0] = this._peekLevels[0] - _decay;
            if (this._peekLevels[0] < 0)
                this._peekLevels[0] = 0;
            if (this._peekLevels[0] < this._levels[0])
                this._peekLevels[0] = this._levels[0];

            this._peekLevels[1] = this._peekLevels[1] - _decay;
            if (this._peekLevels[1] < 0)
                this._peekLevels[1] = 0;
            if (this._peekLevels[1] < this._levels[0])
                this._peekLevels[1] = this._levels[0];

            double peekLevelL = Math.Ceiling((double)this._peekLevels[0] / (double)short.MaxValue * 13);
            double peekLevelR = Math.Ceiling((double)this._peekLevels[1] / (double)short.MaxValue * 13);

            Debug.WriteLine($"{levelL},{peekLevelL},{levelR},{peekLevelR}");

            // Canvasの内容をクリア
            LevelCanvas.Children.Clear();

            // 左チャンネルの音量メーターを描画
            for (int i = 0; i < 13; i++)
            {
                System.Windows.Shapes.Rectangle rect = new()
                {
                    Width = 34,
                    Height = 11,
                    Fill = (0 < this._peekLevels[0] && i == peekLevelL) ? i >= 8 ? Brushes.Red : Brushes.Lime :
                                i < levelL ? i >= 8 ? Brushes.Crimson : Brushes.LimeGreen : Brushes.DimGray
                };
                Canvas.SetLeft(rect, i * 40 + 13);
                Canvas.SetTop(rect, 10);
                LevelCanvas.Children.Add(rect);
            }

            // 右チャンネルの音量メーターを描画
            for (int i = 0; i < 13; i++)
            {
                System.Windows.Shapes.Rectangle rect = new()
                {
                    Width = 34,
                    Height = 11,
                    Fill = (0 < this._peekLevels[1] && i == peekLevelR) ? i >= 8 ? Brushes.Red : Brushes.Lime :
                                i < levelR ? i >= 8 ? Brushes.Crimson : Brushes.LimeGreen : Brushes.DimGray
                };
                Canvas.SetLeft(rect, i * 40 + 13);
                Canvas.SetTop(rect, 29);
                LevelCanvas.Children.Add(rect);
            }
        }

        private void InitBass()     // Analyzer class initialization. DO NOT call twice or more.
        {
            // デバイス情報を取得
            int deviceCount = BassWasapi.BASS_WASAPI_GetDeviceCount();
            BASS_WASAPI_DEVICEINFO defaultDevice = null;
            for (int i = 0; i < deviceCount; i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device != null)
                {
                    if ((device.IsDefault && device.IsEnabled && device.IsLoopback) ||
                        (defaultDevice != null && device.IsLoopback))
                    {
                        Debug.WriteLine($"Device {i}: {device.name}");
                        defaultDevice = device;
                        _devicenumber = i;
                        break;
                    }
                    else if(device.IsDefault && device.IsEnabled)
                    {
                        Debug.WriteLine($"Device {i}: {device.name}");
                        defaultDevice = device;
                        _devicenumber = i;
                    }
                }
            }

            if (defaultDevice == null)
            {
                MessageBox.Show($"音声出力デバイスが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            bool initResult = Bass.BASS_Init(0, defaultDevice.mixfreq, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (!initResult)
            {
                var error = Bass.BASS_ErrorGetCode();
                MessageBox.Show($"BASS 音声出力デバイス初期化時エラーコード: {error}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void FreeBass()
        {
            if (_devicenumber != -1)
            {
                BassWasapi.BASS_WASAPI_Free();
                Bass.BASS_Free();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ウィンドウのスタイルを変更してクリックの透過処理を有効にする
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (extendedStyle == 0)
            {
                MessageBox.Show($"Window属性の取得に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int windowResult = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED);
            if (windowResult == 0)
            {
                MessageBox.Show($"Window属性の設定に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // BASS WASAPIの初期化
            bool initResult = BassWasapi.BASS_WASAPI_Init(_devicenumber, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, _process, IntPtr.Zero);
            if (!initResult)
            {
                var error = Bass.BASS_ErrorGetCode();
                MessageBox.Show($"BASS WASAPI初期化エラーコード: {error}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool startResult = BassWasapi.BASS_WASAPI_Start();
            if (!startResult)
            {
                var error = Bass.BASS_ErrorGetCode();
                MessageBox.Show($"BASS WASAPI開始時エラーコード: {error}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            System.Threading.Thread.Sleep(500);
            _timer.Start();
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;

            _timer.Stop();
            BassWasapi.BASS_WASAPI_Stop(true);

            FreeBass();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            // ウィンドウが有効かどうかを確認
            if (!this.IsVisible || this.WindowState == WindowState.Minimized)
            {
                return;
            }

            // カーソルの位置を取得（ディスプレイの絶対座標）
            var cursorPosition = System.Windows.Forms.Cursor.Position;
            // ウィンドウのスクリーン座標を取得
            var windowPosition = this.PointToScreen(new Point(0, 0));
            // ウィンドウの範囲内にカーソルがあるかどうかを判定
            if (cursorPosition.X >= windowPosition.X && cursorPosition.X <= windowPosition.X + this.ActualWidth &&
                cursorPosition.Y >= windowPosition.Y && cursorPosition.Y <= windowPosition.Y + this.ActualHeight)
            {
                // 背景の透過率を変更
                this.Opacity = 0.2;
            }
            else
            {
                // 背景の透過率を元に戻す
                this.Opacity = 1.0;
            }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
