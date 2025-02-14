using System;
using System.Configuration;

namespace WeatherWiser.Helpers
{
    public static class SettingsHelper
    {
        public static T GetSetting<T>(string key, T defaultValue = default)
        {
            try
            {
                var value = ConfigurationManager.AppSettings[key];
                if (value != null)
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception)
            {
                // TODO:ログを追加するか、必要に応じてエラーハンドリングを行います
            }
            return defaultValue;
        }

        public static void SaveSetting<T>(string key, T value)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings[key].Value = value.ToString();
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception)
            {
                // TODO:ログを追加するか、必要に応じてエラーハンドリングを行います
            }
        }
    }
}
