using Prism.Ioc;
using Prism.Unity;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using WeatherWiser.Views;

namespace WeatherWiser
{
    public partial class App : PrismApplication
    {
        private NotifyIcon _notifyIcon;

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<MainWindow>();
            containerRegistry.RegisterForNavigation<SettingsWindow>();
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InitializeNotifyIcon();
        }

        private void InitializeNotifyIcon()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("WeatherWiser.Resources.WeatherWiser.ico"))
            {
                if (stream != null)
                {
                    _notifyIcon = new NotifyIcon
                    {
                        Icon = new System.Drawing.Icon(stream),
                        Visible = true,
                        Text = "Weather Wiser"
                    };
                }
                else
                {
                    throw new Exception("アイコンリソースの読み込みに失敗しました。");
                }
            }

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, OnSettingsClicked);
            contextMenu.Items.Add("Refresh", null, OnRefreshClicked);
            contextMenu.Items.Add("Exit", null, OnExitClicked);
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OnSettingsClicked(object sender, EventArgs e)
        {
            var settingsWindow = Container.Resolve<SettingsWindow>();
            settingsWindow.ShowDialog();
        }

        private void OnRefreshClicked(object sender, EventArgs e)
        {
            var mainWindow = Container.Resolve<MainWindow>();
            mainWindow.RestartSoundService();
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon.Dispose();
            base.OnExit(e);
        }
    }
}
