using System;
using System.Collections.Generic;
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
        // 画面描画の最終時間
        private DateTime _lastRenderTime = DateTime.MinValue;
        // 画面描画の間隔(100ms)
        private readonly TimeSpan _renderInterval = TimeSpan.FromMilliseconds(100);
        // デバイス番号
        private int _devicenumber = -1;
        // WASAPIプロセス
        private readonly WASAPIPROC _process;
        // コードページ
        private readonly bool UNICODE = true;
        // 音量レベルの減衰値
        private readonly short _levelDecay = 500;
        // 画面表示更新用タイマー
        private readonly DispatcherTimer _timer;
        // 音量レベルのピーク値
        private readonly int _levelPeek = 13;
        // 音量レベル
        private readonly int[] _levels = [0, 0];
        // 音量レベル（ピーク時）
        private readonly int[] _peekLevels = [0, 0];
        // 音量レベルの矩形
        private readonly System.Windows.Shapes.Rectangle[,] _levelRects = new System.Windows.Shapes.Rectangle[2, 13];
        // スペクトラムの減衰値
        private readonly short _spectrumDecay = 10;
        // スペクトラムのバー数
        private readonly int _numberOfBar = 16;
        // スペクトラムの高さ
        private readonly int _spectrumHeight = 10;
        // スペクトラム（ピーク時）
        private readonly int[] _peekSpectrums = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        // スペクトラムデータ
        private readonly List<byte> _spectrumdata = [];
        // スペクトラムの矩形
        private readonly System.Windows.Shapes.Rectangle[,] _spectrumRects = new System.Windows.Shapes.Rectangle[16, 10];
        // チャンネル数(1:mono or mix 2:stereo)
        private readonly int _channel = 1;
        // ミックス周波数
        private int _mixfreq;
        // ミックス周波数の倍率
        private float _mixfreqMultiplyer;
        // FFTデータ(2ch)
        private readonly float[] _fft = new float[16384 * 2];
        // FFTデータ取得フラグ
        private BASSData _DATAFLAG;
        // 可聴域の周波数倍率
        // 約20Hz～20KHzの対数スケールとするため、log(20,2)≒4.32～log(20000,2)≒14.29の範囲をもとに14.29-10=4.29としている）
        private readonly float _freqShift = (float)Math.Round(Math.Log(20000, 2) - 10, 2); // = 4.29

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

            InitBass();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ウィンドウのスタイルを変更してクリックの透過処理を有効にする
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (extendedStyle == 0)
            {
                MessageBox.Show("Window属性の取得に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // WS_EX_LAYERED と WS_EX_TOOLWINDOW を追加
            int windowResult = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
            if (windowResult == 0)
            {
                MessageBox.Show("Window属性の設定に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // BASS WASAPIの初期化(コールバック関数を指定)
            bool initResult = BassWasapi.BASS_WASAPI_Init(_devicenumber, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, _process, IntPtr.Zero);
            if (!initResult)
            {
                var error = Bass.BASS_ErrorGetCode();
                MessageBox.Show($"BASS WASAPI初期化エラーコード: {error}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // BASS WASAPIの開始
            bool startResult = BassWasapi.BASS_WASAPI_Start();
            if (!startResult)
            {
                var error = Bass.BASS_ErrorGetCode();
                MessageBox.Show($"BASS WASAPI開始時エラーコード: {error}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // スペクトラムの矩形を初期化
            InitializeSpectrumRectangles();

            // 音量レベルの矩形を初期化
            InitializeLevelRectangles();

            // タイマーを開始
            System.Threading.Thread.Sleep(500);
            _timer.Start();
        }

        private void InitializeSpectrumRectangles()
        {
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    System.Windows.Shapes.Rectangle rect = new()
                    {
                        Width = 28,
                        Height = 6,
                        Opacity = 0.8,
                        Fill = Brushes.DimGray
                    };
                    Canvas.SetLeft(rect, x * 34 + 3);
                    Canvas.SetTop(rect, 5 + (9 - y) * 10);
                    SpectrumCanvas.Children.Add(rect);
                    _spectrumRects[x, y] = rect;
                }
            }
        }

        private void InitializeLevelRectangles()
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 13; j++)
                {
                    System.Windows.Shapes.Rectangle rect = new()
                    {
                        Width = 34,
                        Height = 10,
                        Opacity = 0.8,
                        Fill = Brushes.DimGray,
                    };
                    Canvas.SetLeft(rect, j * 40 + 23);
                    Canvas.SetTop(rect, 5 + (15 * i));
                    LevelCanvas.Children.Add(rect);
                    _levelRects[i, j] = rect;
                }
            }
        }

        private int WasapiProcess(IntPtr buffer, int length, IntPtr user)
        {
            //Debug.WriteLine($"Process called with length: {length}");
            return length;
        }

        private void InitBass()
        {
            // デバイス情報に Unicode 文字セットを使用する
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UNICODE, UNICODE);
            // 既定のデバイスを特定
            int deviceCount = BassWasapi.BASS_WASAPI_GetDeviceCount();
            BASS_WASAPI_DEVICEINFO defaultDevice = null;
            for (int i = 0; i < deviceCount; i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device != null)
                {
                    // 既定のサウンドデバイスと同名でループバックに対応したデバイスを選択
                    if ((device.IsDefault && device.IsEnabled && device.IsLoopback) ||
                        (defaultDevice != null && device.IsLoopback))
                    {
                        Debug.WriteLine($"Device {i}: {device.name}");
                        defaultDevice = device;
                        _devicenumber = i;
                        break;
                    }
                    else if (device.IsDefault && device.IsEnabled)
                    {
                        Debug.WriteLine($"Device {i}: {device.name}");
                        defaultDevice = device;
                        _devicenumber = i;
                    }
                }
            }

            if (defaultDevice == null)
            {
                MessageBox.Show("音声出力デバイスが見つかりません。", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // デバイス情報からミックス周波数を取得
            _mixfreq = defaultDevice.mixfreq;
            // ミックス周波数に応じてFFTデータのサンプル数とFFTバッファ倍率を設定
            SetParamFromFreq(_mixfreq);
            // 再生バッファの更新に使用するスレッド数を設定
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            // BASS デバイスの初期化
            bool initResult = Bass.BASS_Init(0, defaultDevice.mixfreq, 
                    BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (!initResult)
            {
                var error = Bass.BASS_ErrorGetCode();
                MessageBox.Show($"BASS 音声出力デバイス初期化時エラーコード: {error}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetParamFromFreq(int freq)
        {
            // デバイスが 48000Hz の場合、音声の 16000Hz は FFT2048 で 2048 * 16000 / 48000 = 682 要素目あたりになる
            // 音声の 16000Hz が可変となるため、事前に FFTサンプル数 / ミックス周波数 を計算して保持しておく

            switch (freq)
            {
                case <= 48000:  // ~48khz
                    _DATAFLAG = BASSData.BASS_DATA_FFT2048;  // 2048 サンプル FFT
                    _mixfreqMultiplyer = 2048f / freq;
                    break;
                case <= 96000:  // ~96khz
                    _DATAFLAG = BASSData.BASS_DATA_FFT4096;  // 4096 サンプル FFT
                    _mixfreqMultiplyer = 4096f / freq;
                    break;
                case <= 192000: // ~192khz
                    _DATAFLAG = BASSData.BASS_DATA_FFT8192;  // 8192 サンプル FFT
                    _mixfreqMultiplyer = 8192f / freq;
                    break;
                default:        // ~
                    _DATAFLAG = BASSData.BASS_DATA_FFT16384; // 16384 サンプル FFT
                    _mixfreqMultiplyer = 16384f / freq;
                    break;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // スペクトラムの描画
            UpdateSpectrumCanvas();
            // 音量レベルの描画
            UpdateLevelCanvas();
        }

        private void UpdateSpectrumCanvas()
        {
            // FFTデータの取得
            int ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)_DATAFLAG);
            if (ret < -1)
            {
                return;
            }

            // 走査するFFTバッファの位置
            int freqPos = 0;
            // 走査する周波数範囲の上限
            int freqValue = 1;
            // スペクトラムデータのクリア
            _spectrumdata.Clear();

            float[] peeks = new float[_numberOfBar];

            // バンドごとのピーク値を取得
            for (int bandX = 0; bandX < _numberOfBar; bandX++)
            {
                peeks[bandX] = 0;

                // Math.Pow(...) で 20hz~20khz の対数スケールの近似値を取得し、ミックス周波数とFFTサンプル数に応じた倍率を掛ける
                freqValue = (int)(Math.Pow(2, (bandX * 10.0 / (_numberOfBar - 1)) + _freqShift) * _mixfreqMultiplyer);
                if (freqValue <= freqPos)
                    freqValue = freqPos + 1;

                // ミックス周波数に応じてFFTバッファから取得する周波数範囲の上限を調整
                freqValue = _mixfreq switch
                {
                    <= 48000 => Math.Min(freqValue, 2048),
                    <= 96000 => Math.Min(freqValue, 4096),
                    <= 192000 => Math.Min(freqValue, 8192),
                    _ => Math.Min(freqValue, 16384),
                };

                // 周波数範囲の上限までのFFTバッファを走査してピーク値を取得
                for (; freqPos < freqValue; freqPos++)
                {
                    peeks[bandX] = Math.Max(peeks[bandX], _fft[1 + freqPos]);
                }

                // ピーク値の平方根を増幅して0～255の範囲の値に変換（*3と-4は調整値）
                int powerY = (int)(Math.Sqrt(peeks[bandX]) * 3 * 255 - 4);
                powerY = Math.Max(Math.Min(powerY, byte.MaxValue), byte.MinValue);
                _spectrumdata.Add((byte)powerY);
            }

            Debug.WriteLine(string.Join(",", peeks));
            //Debug.WriteLine(string.Join(",", _spectrumdata));

            // 周波数スペクトラムの描画
            for (int x = 0; x < _numberOfBar; x++)
            {
                // バンド値の正規化
                int spectrum = NormalizeValue(this._spectrumdata[x], byte.MaxValue, _spectrumHeight);
                // 最大バンド値の更新（パラパラ降ってくる表現のため）
                this._peekSpectrums[x] = UpdatePeekValue(this._spectrumdata[x], this._peekSpectrums[x], this._spectrumDecay);
                // 最大バンド値の正規化
                int peekSpectrum = NormalizeValue(this._peekSpectrums[x], byte.MaxValue, _spectrumHeight);
                // バンド値に応じて矩形の色を変更
                for (int y = 0; y < _spectrumHeight; y++)
                {
                    _spectrumRects[x, y].Fill = y + 1 == peekSpectrum ? Brushes.Lime :
                        y < spectrum ? Brushes.LimeGreen : Brushes.DimGray;
                }
            }
        }

        private void UpdateLevelCanvas()
        {
            // BASS 音量レベルを取得して下位 16bit を L、上位 16bit を Rとする
            int level = BassWasapi.BASS_WASAPI_GetLevel();
            this._levels[0] = Utils.LowWord32(level);
            this._levels[1] = Utils.HighWord32(level);

            for (int i = 0; i < _levels.Length; i++)
            {
                // 音量レベルの正規化
                int normalizedLevel = NormalizeValue(this._levels[i], short.MaxValue, _levelPeek);
                // 最大音量レベルの更新（パラパラ降ってくる表現のため）
                this._peekLevels[i] = UpdatePeekValue(this._levels[i], this._peekLevels[i], this._levelDecay);
                // 最大音量レベルの正規化
                int normalizedPeekLevel = NormalizeValue(this._peekLevels[i], short.MaxValue, _levelPeek);
                // 音量レベルに応じて矩形の色を変更
                for (int j = 0; j < _levelPeek; j++)
                {
                    _levelRects[i, j].Fill = (j + 1 == normalizedPeekLevel) ? 
                        j >= 8 ? Brushes.Red : Brushes.Lime :
                        j < normalizedLevel ? 
                        j >= 8 ? Brushes.Crimson : Brushes.LimeGreen : Brushes.DimGray;
                }
            }
        }

        private int NormalizeValue(int value, int maxValue, int count)
        {
            // 正規化したうえで平方をとることで音量レベルの変化を強調
            return (int)Math.Ceiling(Math.Sqrt((double)value / (double)maxValue) * count);
        }

        private int UpdatePeekValue(int value, int peekValue, int decay)
        {
            // 音量レベルのピーク値を更新
            peekValue -= decay;
            peekValue = Math.Max(Math.Max(peekValue, 0), value);
            return peekValue;
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            _timer.Stop();
            BassWasapi.BASS_WASAPI_Stop(true);
            FreeBass();
        }

        public void FreeBass()
        {
            if (_devicenumber != -1)
            {
                BassWasapi.BASS_WASAPI_Free();
                Bass.BASS_Free();
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            // デバウンス処理
            if (DateTime.Now - _lastRenderTime < _renderInterval)
            {
                return;
            }
            _lastRenderTime = DateTime.Now;

            // ウィンドウ表示判定
            if (!IsWindowVisible())
            {
                return;
            }

            // カーソルの位置に応じてウィンドウの透過率を変更
            var cursorPosition = System.Windows.Forms.Cursor.Position;
            var windowPosition = PointToScreen(new Point(0, 0));
            double newOpacity = IsCursorInsideWindow(cursorPosition, windowPosition) ? 0.1 : 1.0;
            SetWindowOpacity(newOpacity);
        }

        private bool IsWindowVisible()
        {
            // ウィンドウが表示されているかを判定
            return this.IsVisible && this.WindowState != WindowState.Minimized;
        }

        private bool IsCursorInsideWindow(System.Drawing.Point cursorPosition, Point windowPosition)
        {
            // カーソル位置とウィンドウ位置およびサイズの比較
            return cursorPosition.X >= windowPosition.X && cursorPosition.X <= windowPosition.X + this.ActualWidth &&
                   cursorPosition.Y >= windowPosition.Y && cursorPosition.Y <= windowPosition.Y + this.ActualHeight;
        }

        private void SetWindowOpacity(double opacity)
        {
            // 透過率が変更される場合のみ処理を行う
            if (this.Opacity != opacity)
            {
                this.Opacity = opacity;
            }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
