using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WeatherWiser.ViewModels;

namespace WeatherWiser.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;
            // CompositionTarget.Rendering イベントを使用してカーソル位置を監視
            CompositionTarget.Rendering += OnRendering;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (extendedStyle == 0)
            {
                throw new InvalidOperationException("Failed to get window long.");
            }
            int result = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED);
            if (result == 0)
            {
                throw new InvalidOperationException("Failed to set window long.");
            }

            var viewModel = this.DataContext as MainWindowViewModel;
            if (viewModel != null)
            {
                viewModel.Initialize();
            }
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            // カーソルの位置を取得（ディスプレイの絶対座標）
            var cursorPosition = System.Windows.Forms.Cursor.Position;
            // ウィンドウのスクリーン座標を取得
            var windowPosition = this.PointToScreen(new Point(0, 0));
            // ウィンドウの範囲内にカーソルがあるかどうかを判定
            if (cursorPosition.X >= windowPosition.X && cursorPosition.X <= windowPosition.X + this.ActualWidth &&
                cursorPosition.Y >= windowPosition.Y && cursorPosition.Y <= windowPosition.Y + this.ActualHeight)
            {
                this.Opacity = 0.2; // 背景の透過率を変更
            }
            else
            {
                this.Opacity = 1.0; // 背景の透過率を元に戻す
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
