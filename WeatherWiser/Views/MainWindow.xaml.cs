using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WeatherWiser.ViewModels;

namespace WeatherWiser.Views
{
    public partial class MainWindow : Window
    {
        // 画面描画の最終時間
        private DateTime _lastRenderTime = DateTime.MinValue;
        // 画面描画の間隔(100ms)
        private readonly TimeSpan _renderInterval = TimeSpan.FromMilliseconds(100);
        // 音量レベルの減衰値
        private readonly short _levelDecay = 500;
        // 音量レベルのピーク値
        private readonly int _levelPeek = 13;
        // 音量レベル（ピーク時）
        private readonly int[] _peekLevels = new int[2];
        // 音量レベルの矩形
        private readonly System.Windows.Shapes.Rectangle[,] _levelRects = new System.Windows.Shapes.Rectangle[2, 13];
        // スペクトラムの減衰値
        private readonly short _spectrumDecay = 10;
        // スペクトラムのピーク値
        private readonly int _spectrumPeek = 10;
        // スペクトラム（ピーク時）
        private readonly int[] _peekSpectrums = new int[16];
        // スペクトラムの矩形
        private readonly System.Windows.Shapes.Rectangle[,] _spectrumRects = new System.Windows.Shapes.Rectangle[16, 10];

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SpectrumUpdated += OnSpectrumUpdated;
                viewModel.LevelUpdated += OnLevelUpdated;
            }
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

            // ウィンドウの透過率を設定
            CompositionTarget.Rendering += OnRendering;
            // スペクトラムの矩形を初期化
            InitializeSpectrumRectangles();
            // 音量レベルの矩形を初期化
            InitializeLevelRectangles();

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.StartSoundService();
            }
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

        private void OnSpectrumUpdated(int[] spectrums)
        {
            Debug.WriteLine(string.Join(",", spectrums));

            // スペクトラムの更新処理
            for (int x = 0; x < spectrums.Length; x++)
            {
                // バンド値の正規化
                int spectrum = NormalizeValue(spectrums[x], byte.MaxValue, _spectrumPeek);
                // 最大バンド値の更新（パラパラ降ってくる表現のため）
                this._peekSpectrums[x] = UpdatePeekValue(spectrums[x], this._peekSpectrums[x], this._spectrumDecay);
                // 最大バンド値の正規化
                int peekSpectrum = NormalizeValue(this._peekSpectrums[x], byte.MaxValue, _spectrumPeek);
                // バンド値に応じて矩形の色を変更
                for (int y = 0; y < _spectrumPeek; y++)
                {
                    _spectrumRects[x, y].Fill = y + 1 == peekSpectrum ? Brushes.Lime :
                        y < spectrum ? Brushes.LimeGreen : Brushes.DimGray;
                }
            }
        }

        private void OnLevelUpdated(int[] levels)
        {
            // 音量レベルの更新処理
            for (int i = 0; i < levels.Length; i++)
            {
                // 音量レベルの正規化
                int normalizedLevel = NormalizeValue(levels[i], short.MaxValue, _levelPeek);
                // 最大音量レベルの更新（パラパラ降ってくる表現のため）
                this._peekLevels[i] = UpdatePeekValue(levels[i], this._peekLevels[i], this._levelDecay);
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

        public void RefreshSoundService()
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.RefreshSoundService();
            }
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.StopSoundService();
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
