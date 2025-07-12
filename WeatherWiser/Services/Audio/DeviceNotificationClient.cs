using System;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace WeatherWiser.Services.Audio
{
    public class DeviceNotificationClient : IMMNotificationClient
    {
        public event Action<string> DefaultDeviceChanged;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                DefaultDeviceChanged?.Invoke(defaultDeviceId);
            }
        }

        public void OnDeviceRemoved(string deviceId) {}

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
