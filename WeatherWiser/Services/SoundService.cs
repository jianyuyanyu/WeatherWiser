using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;
using WeatherWiser.Models;
using WeatherWiser.Services.Audio;

namespace WeatherWiser.Services
{
    public class SoundService
    {
        // スペクトラム更新イベント
        public event Action<int[]> SpectrumUpdated;
        // 音量レベル更新イベント
        public event Action<int[]> LevelUpdated;
        // アクティブデバイス情報変更イベント
        public event Action ActiveDeviceInfoChanged;

        // WASAPIデバイス情報
        private BASS_WASAPI_DEVICEINFO _activeDeviceInfo;
        public BASS_WASAPI_DEVICEINFO ActiveDeviceInfo
        {
            get => _activeDeviceInfo;
            private set
            {
                if (_activeDeviceInfo != value)
                {
                    _activeDeviceInfo = value;
                    ActiveDeviceInfoChanged?.Invoke();
                }
            }
        }

        // デバイス列挙子
        private MMDeviceEnumerator _deviceEnumerator;
        // デバイス通知クライアント
        private DeviceNotificationClient _notificationClient;
        // デバイスが破棄されたかどうかのフラグ
        private bool _isDisposed = false;
        // 保留中のデフォルトデバイスID
        private string _pendingDefaultDeviceId;
        // WASAPIプロセス
        private readonly WASAPIPROC _process;
        // 更新用タイマー
        private readonly DispatcherTimer _timer;
        // コードページ
        private readonly bool UNICODE = true;
        // デバイス番号
        private int _devicenumber = -1;
        // 周波数関連
        private FreqParams _freqParams;
        // FFTデータ(2ch)
        private readonly float[] _fft = new float[16384 * 2];
        // スペクトラムデータ
        private readonly int[] _spectrums = new int[16];
        // 音量レベル
        private readonly int[] _levels = new int[2];

        public SoundService()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _notificationClient = new DeviceNotificationClient();
            _notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
            _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);

            _process = new WASAPIPROC(WasapiProcess);
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(25),
                IsEnabled = false,
            };
            _timer.Tick += Timer_Tick;
        }

        public string GetDeviceInfo()
        {
            if (ActiveDeviceInfo == null)
            {
                return "Device not initialised";
            }

            var name = ActiveDeviceInfo?.name ?? "No active device";
            var freq = ActiveDeviceInfo?.mixfreq ?? 0;
            var chans = ActiveDeviceInfo?.mixchans switch
            {
                1 => "Mono",
                2 => "Stereo",
                >= 3 => "Multi",
                _ => "Unknown"
            };

            return $"{name} ({freq}Hz, {chans})";
        }

        public void StartSoundService()
        {
            try
            {
                Init();
                Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"デバイス初期化エラー: {ex.Message}");
            }
        }

        public void StopSoundService()
        {
            try
            {
                Stop();
                Free();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"デバイス停止エラー: {ex.Message}");
            }
        }

        public void RestartSoundService()
        {
            try
            {
                Stop();
                Free();
                Init();
                Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"デバイス再初期化エラー: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _notificationClient.DefaultDeviceChanged -= OnDefaultDeviceChanged;
                _deviceEnumerator.UnregisterEndpointNotificationCallback(_notificationClient);
                _deviceEnumerator.Dispose();
                _isDisposed = true;
            }
        }

        private void OnDefaultDeviceChanged(string defaultDeviceId)
        {
            Debug.WriteLine("デフォルトデバイスが変更されました。再初期化します。");
            _pendingDefaultDeviceId = defaultDeviceId;
            RestartSoundService();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateSpectrum();
            UpdateLevel();
        }

        private void Init()
        {
            ActiveDeviceInfo = null;
            // デバイス情報に Unicode 文字セットを使用する
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UNICODE, UNICODE);
            // デバイス数を取得
            int deviceCount = BassWasapi.BASS_WASAPI_GetDeviceCount();

            if (!string.IsNullOrEmpty(_pendingDefaultDeviceId))
            {
                // 1. defaultDeviceIdが指定されていればそれを優先
                for (int i = 0; i < deviceCount; i++)
                {
                    var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                    if (device != null && device.id == _pendingDefaultDeviceId && device.IsLoopback && device.IsEnabled)
                    {
                        Debug.WriteLine($"[切替] Device {i}: {device.name}");
                        ActiveDeviceInfo = device;
                        _devicenumber = i;
                        _pendingDefaultDeviceId = null;
                        break;
                    }
                }
            }
            else
            {
                // 2. デフォルトデバイスを探す
                for (int i = 0; i < deviceCount; i++)
                {
                    var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                    if (device == null) continue;

                    if (device.IsDefault && device.IsEnabled)
                    {
                        // OS が既定として選択しているサウンドデバイスを特定
                        Debug.WriteLine($"[既定] Device {i}: {device.name}");
                        ActiveDeviceInfo = device;
                        _devicenumber = i;
                    }
                    else if (ActiveDeviceInfo != null && ActiveDeviceInfo.name == device.name &&
                        device.IsLoopback && device.IsInput)
                    {
                        // 特定したサウンドデバイスと同名でループバックに対応したデバイスを特定
                        Debug.WriteLine($"[特定] Device {i}: {device.name}");
                        ActiveDeviceInfo = device;
                        _devicenumber = i;
                        break;
                    }
                }
            }

            if (ActiveDeviceInfo == null || !ActiveDeviceInfo.IsLoopback)
            {
                throw new Exception("ループバックに対応した音声出力デバイスが見つかりません。");
            }

            _freqParams = new FreqParams(ActiveDeviceInfo.mixfreq);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);

            if (!Bass.BASS_Init(0, ActiveDeviceInfo.mixfreq, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero))
            {
                throw new Exception($"BASS 音声出力デバイス初期化時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            if (!Bass.BASS_Start())
            {
                throw new Exception($"BASS 音声出力デバイス開始時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            if (!BassWasapi.BASS_WASAPI_Init(_devicenumber, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, _process, IntPtr.Zero))
            {
                throw new Exception($"BASS WASAPI初期化時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }
        }

        private void Start()
        {
            if (!BassWasapi.BASS_WASAPI_Start())
            {
                throw new Exception($"BASS WASAPI開始時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            System.Threading.Thread.Sleep(500);
            _timer.Start();
        }

        private void Stop()
        {
            _timer.Stop();
            if (!BassWasapi.BASS_WASAPI_Stop(true))
            {
                throw new Exception($"BASS WASAPI停止時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }
        }

        private void Free()
        {
            if (!BassWasapi.BASS_WASAPI_Free())
            {
                throw new Exception($"BASS WASAPI解放時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            if (!Bass.BASS_Stop())
            {
                throw new Exception($"BASS 音声出力デバイス停止時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            if (!Bass.BASS_Free())
            {
                throw new Exception($"BASS 音声出力デバイス解放時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            ActiveDeviceInfo = null;
        }

        private void UpdateSpectrum()
        {
            int ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)_freqParams.BassData);
            if (ret < -1) return;

            int freqPos = 0;
            for (int bandX = 0; bandX < _spectrums.Length; bandX++)
            {
                int freqValue = (int)(Math.Pow(2, (bandX * 10.0 / (_spectrums.Length - 1)) + _freqParams.FreqShift) * _freqParams.MixFreqMultiplyer);
                freqValue = freqValue <= freqPos ? freqPos + 1 : Math.Min(freqValue, _freqParams.MaxFftLength);
                float peek = _fft.Skip(freqPos).Take(freqValue - freqPos).Max();
                freqPos = freqValue;
                int powerY = (int)(Math.Sqrt(peek) * 3 * 255 - 4);
                _spectrums[bandX] = Math.Max(Math.Min(powerY, byte.MaxValue), byte.MinValue);
            }

            SpectrumUpdated?.Invoke(_spectrums);
        }

        private void UpdateLevel()
        {
            int level = BassWasapi.BASS_WASAPI_GetLevel();
            _levels[0] = Utils.LowWord32(level);
            _levels[1] = Utils.HighWord32(level);

            LevelUpdated?.Invoke(_levels);
        }

        private int WasapiProcess(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }
    }
}
