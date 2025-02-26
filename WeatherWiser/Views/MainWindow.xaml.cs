using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Channels;
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
        // デバイス番号
        private int _devicenumber = -1;
        // WASAPIプロセス
        private readonly WASAPIPROC _process;
        // コードページ
        private readonly bool UNICODE = true;
        // 音量レベルの減衰値
        private readonly short _spectrumDecay = 10;
        // 音量レベルの減衰値
        private readonly short _levelDecay = 500;
        // 画面表示更新用タイマー
        private readonly DispatcherTimer _timer;
        // 音量レベルのピーク値
        private int _levelPeek = 13;
        // 音量レベル
        private readonly int[] _levels = [0, 0];
        // 音量レベル（ピーク時）
        private readonly int[] _peekLevels = [0, 0];
        // 音量レベルの矩形
        private System.Windows.Shapes.Rectangle[,] _levelRects = new System.Windows.Shapes.Rectangle[2, 13];
        // スペクトラムのバー数
        private int _numberOfBar = 16;
        // スペクトラムの高さ
        private int _spectrumHeight = 10;
        // スペクトラム（ピーク時）
        private readonly int[] _peekSpectrums = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        // スペクトラムの矩形
        private System.Windows.Shapes.Rectangle[,] _spectrumRects = new System.Windows.Shapes.Rectangle[16, 10];

        private int _channel = 1;                            // 1: "mixed-data"(mono) 2: L+R
        private int _mixfreq;                            // devide frequency
        private float _mixfreqMultiplyer;                // frequency multiply value
        private float[] _fft = new float[16384 * 2/*channel*/];     // buffer for fft data
        private BASSData _DATAFLAG;                      // for "interreave" format
        private readonly float _freqShift = (float)Math.Round(Math.Log(20000/*hz*/, 2) - 10/*difference to 20hz*/, 2);    // constant 4.29
        public List<byte> _spectrumdata = new List<byte>();                 // spectrum data buffer

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
            int ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)_DATAFLAG);
            if (ret < -1)
            {
                return;
            }

            int bandX, powerY;
            int fftPos = 0;
            int freqValue = 1;

            _spectrumdata.Clear();

            for (bandX = 0; bandX < _numberOfBar; bandX++)
            {
                float[] peak = { 0f, 0f };

                freqValue = (int)(Math.Pow(2, (bandX * 10.0 / _numberOfBar) + _freqShift) * _mixfreqMultiplyer);
                if (freqValue <= fftPos)
                    freqValue = fftPos + 1;

                if (_mixfreq <= 48000)
                    // 44.1khz, 48khz
                    if (freqValue > 4096 * _channel - _channel)
                        freqValue = 4096 * _channel - _channel;
                else if (_mixfreq <= 88200)
                    // 88.2khz
                    if (freqValue > 8192 * _channel - _channel)
                        freqValue = 8192 * _channel - _channel;
                else
                    // 96khz, 176.4khz, 192khz, 384khz ~
                    if (freqValue > 16384 * _channel - _channel)
                        freqValue = 16384 * _channel - _channel;

                for (; fftPos < freqValue; fftPos += _channel)
                {
                    for (int i = 0; i < _channel; i++)
                    {
                        if (peak[0] < _fft[1 + fftPos])
                            peak[0] = _fft[1 + fftPos];
                        if (peak[1] < _fft[1 + fftPos + (_channel - 1)])
                            peak[1] = _fft[1 + fftPos + (_channel - 1)];
                    }
                }

                for (int i = 0; i < _channel; i++)
                {
                    powerY = (int)(Math.Sqrt(peak[i]) * 2 * 255);
                    if (powerY > 255)
                        powerY = 255;
                    if (powerY < 0)
                        powerY = 0;
                    _spectrumdata.Add((byte)powerY);
                }
            }

            //Debug.WriteLine(string.Join(", ", _spectrumdata));
            UpdateSpectrumCanvas();
            UpdateLevelCanvas();
        }


        private void UpdateSpectrumCanvas()
        {
            for (int x = 0; x < _numberOfBar; x++)
            {
                int spectrum = NormalizeValue(this._spectrumdata[x], byte.MaxValue, _spectrumHeight);
                this._peekSpectrums[x] = UpdatePeekValue(this._spectrumdata[x], this._peekSpectrums[x], this._spectrumDecay);
                int peekSpectrum = NormalizeValue(this._peekSpectrums[x], byte.MaxValue, _spectrumHeight);

                for (int y = 0; y < _spectrumHeight; y++)
                {
                    _spectrumRects[x, y].Fill = y + 1 == peekSpectrum ? y >= 7 ? Brushes.Lime : Brushes.Lime :
                        y < spectrum ? y >= 7 ? Brushes.LimeGreen : Brushes.LimeGreen : Brushes.DimGray;
                }
            }
        }

        private void UpdateLevelCanvas()
        {
            int level = BassWasapi.BASS_WASAPI_GetLevel();
            this._levels[0] = Utils.LowWord32(level);
            this._levels[1] = Utils.HighWord32(level);

            for (int i = 0; i < _levels.Length; i++)
            {
                int normalizedLevel = NormalizeValue(this._levels[i], short.MaxValue, _levelPeek);
                this._peekLevels[i] = UpdatePeekValue(this._levels[i], this._peekLevels[i], this._levelDecay);
                int normalizedPeekLevel = NormalizeValue(this._peekLevels[i], short.MaxValue, _levelPeek);

                for (int j = 0; j < _levelPeek; j++)
                {
                    _levelRects[i, j].Fill = (j + 1 == normalizedPeekLevel) ? j >= 8 ? Brushes.Red : Brushes.Lime :
                        j < normalizedLevel ? j >= 8 ? Brushes.Crimson : Brushes.LimeGreen : Brushes.DimGray;
                }
            }
        }

        private int NormalizeValue(int value, int maxValue, int count)
        {
            return (int)Math.Ceiling(Math.Sqrt((double)value / (double)maxValue) * count);
        }

        private int UpdatePeekValue(int value, int peekValue, int decay )
        {
            peekValue -= decay;
            if (peekValue < 0)
                peekValue = 0;
            if (peekValue < value)
                peekValue = value;
            return peekValue;
        }

        private void InitBass()
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

            _mixfreq = defaultDevice.mixfreq;
            SetParamFromFreq(_mixfreq);
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

        // helper function
        private void SetParamFromFreq(int freq)
        {
            if (freq <= 48000)            // 44.1khz, 48khz
            {
                _DATAFLAG = _channel > 1 ? BASSData.BASS_DATA_FFT4096 | BASSData.BASS_DATA_FFT_INDIVIDUAL : BASSData.BASS_DATA_FFT2048;
                _mixfreqMultiplyer = 44100f / freq * 0.25f;
            }
            else if (freq <= 96000)       // 88.2khz, 96khz
            {
                _DATAFLAG = _channel > 1 ? BASSData.BASS_DATA_FFT8192 | BASSData.BASS_DATA_FFT_INDIVIDUAL : BASSData.BASS_DATA_FFT4096;
                _mixfreqMultiplyer = 44100f / freq * 0.5f;
            }
            else if (freq <= 192000)      // 176.4khz, 192khz
            {
                _DATAFLAG = _channel > 1 ? BASSData.BASS_DATA_FFT16384 | BASSData.BASS_DATA_FFT_INDIVIDUAL : BASSData.BASS_DATA_FFT8192;
                _mixfreqMultiplyer = 44100f / freq;
            }
            else                                        // 384khz and above?
            {
                _DATAFLAG = _channel > 1 ? BASSData.BASS_DATA_FFT32768 | BASSData.BASS_DATA_FFT_INDIVIDUAL : BASSData.BASS_DATA_FFT16384;
                _mixfreqMultiplyer = 44100f / freq * 2f;
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

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    System.Windows.Shapes.Rectangle rect = new()
                    {
                        Width = 28,
                        Height = 6,
                        Fill = Brushes.DimGray
                    };
                    Canvas.SetLeft(rect, x * 34 + 3);
                    Canvas.SetTop(rect, 5 + (9 - y) * 10);
                    SpectrumCanvas.Children.Add(rect);
                    _spectrumRects[x, y] = rect;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 13; j++)
                {
                    System.Windows.Shapes.Rectangle rect = new()
                    {
                        Width = 34,
                        Height = 10,
                        Fill = Brushes.DimGray
                    };
                    Canvas.SetLeft(rect, j * 40 + 23);
                    Canvas.SetTop(rect, 5 + (15 * i));
                    LevelCanvas.Children.Add(rect);
                    _levelRects[i, j] = rect;
                }
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
            if (!IsWindowVisible())
            {
                return;
            }

            // カーソルの位置を取得（ディスプレイの絶対座標）
            var cursorPosition = System.Windows.Forms.Cursor.Position;
            // ウィンドウのスクリーン座標を取得
            var windowPosition = PointToScreen(new Point(0, 0));
            // ウィンドウの範囲内にカーソルがあるかどうかを判定
            if (IsCursorInsideWindow(cursorPosition, windowPosition))
            {
                // 背景の透過率を変更
                SetWindowOpacity(0.2);
            }
            else
            {
                // 背景の透過率を元に戻す
                SetWindowOpacity(1.0);
            }
        }

        private bool IsWindowVisible()
        {
            return this.IsVisible && this.WindowState != WindowState.Minimized;
        }

        private bool IsCursorInsideWindow(System.Drawing.Point cursorPosition, Point windowPosition)
        {
            return cursorPosition.X >= windowPosition.X && cursorPosition.X <= windowPosition.X + this.ActualWidth &&
                   cursorPosition.Y >= windowPosition.Y && cursorPosition.Y <= windowPosition.Y + this.ActualHeight;
        }

        private void SetWindowOpacity(double opacity)
        {
            if (this.Opacity != opacity)
            {
                this.Opacity = opacity;
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
