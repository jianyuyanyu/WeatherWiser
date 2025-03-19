using Microsoft.Win32;
using System;

namespace WeatherWiser.Services
{
    public class RegistryService
    {
        public void Write(string basePath, string key, object value)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(basePath))
                {
                    registryKey.SetValue(key, value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to registry: {ex.Message}");
            }
        }

        public T Read<T>(string basePath, string key, T defaultValue = default)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(basePath))
                {
                    object value = registryKey?.GetValue(key, defaultValue);
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                    return defaultValue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from registry: {ex.Message}");
                return defaultValue;
            }
        }

        public void Delete(string basePath, string key)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(basePath, true))
                {
                    registryKey?.DeleteValue(key);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting from registry: {ex.Message}");
            }
        }

        public bool KeyExists(string basePath, string key)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(basePath))
                {
                    return registryKey?.GetValue(key) != null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking registry key: {ex.Message}");
                return false;
            }
        }

        public void Clear(string basePath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(basePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing registry: {ex.Message}");
            }
        }

        public void ClearValue(string basePath, string key)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(basePath, true))
                {
                    registryKey?.SetValue(key, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing registry value: {ex.Message}");
            }
        }

        public void ClearAllValues(string basePath)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(basePath, true))
                {
                    foreach (string valueName in registryKey.GetValueNames())
                    {
                        registryKey.DeleteValue(valueName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing all registry values: {ex.Message}");
            }
        }

        public void ClearAllKeys(string basePath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(basePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing all registry keys: {ex.Message}");
            }
        }

        public void ClearAll(string basePath)
        {
            try
            {
                ClearAllValues(basePath);
                ClearAllKeys(basePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing all registry: {ex.Message}");
            }
        }
    }
}
