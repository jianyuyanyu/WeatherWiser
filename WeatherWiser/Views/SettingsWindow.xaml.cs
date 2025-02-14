using System;
using System.Windows;
using WeatherWiser.ViewModels;

namespace WeatherWiser.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            if (DataContext is SettingsWindowViewModel viewModel)
            {
                viewModel.SettingsChanged += OnSettingsChanged;
            }

            this.Closed += OnWindowClosed;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                viewModel.SaveSettings();
                MessageBox.Show("Settings saved.");
                Close();
            }
            else
            {
                MessageBox.Show("Error saving settings.");
            }
        }

        private void OnSettingsChanged()
        {
            if (Application.Current.MainWindow.DataContext is MainWindowViewModel mainWindowViewModel)
            {
                mainWindowViewModel.Initialize();
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                viewModel.SettingsChanged -= OnSettingsChanged;
            }
        }
    }
}
