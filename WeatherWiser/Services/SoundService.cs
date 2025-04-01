using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;
using WeatherWiser.Models;

namespace WeatherWiser.Services
{
    public class SoundService
    {
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
        // スペクトラム更新イベント
        public event Action<int[]> SpectrumUpdated;
        // 音量レベル
        private readonly int[] _levels = new int[2];
        // 音量レベル更新イベント
        public event Action<int[]> LevelUpdated;

        public SoundService()
        {
            _process = new WASAPIPROC(WasapiProcess);
            _timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(25),
                IsEnabled = false,
            };
            _timer.Tick += Timer_Tick;
        }

        public void Init()
        {
            // デバイス情報に Unicode 文字セットを使用する
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UNICODE, UNICODE);
            // 既定のデバイスを特定
            BASS_WASAPI_DEVICEINFO defaultDevice = null;
            int deviceCount = BassWasapi.BASS_WASAPI_GetDeviceCount();
            for (int i = 0; i < deviceCount; i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device == null)
                {
                    continue;
                }

                // 既定のサウンドデバイスと同名でループバックに対応したデバイスを選択
                if ((device.IsDefault && device.IsEnabled && device.IsLoopback) ||
                    (defaultDevice != null && defaultDevice.name == device.name && device.IsLoopback))
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

            if (defaultDevice == null || !defaultDevice.IsLoopback)
            {
                throw new Exception("ループバックに対応した音声出力デバイスが見つかりません。");
            }

            _freqParams = new FreqParams(defaultDevice.mixfreq);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);

            if (!Bass.BASS_Init(0, defaultDevice.mixfreq, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero))
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

        public void Start()
        {
            if (!BassWasapi.BASS_WASAPI_Start())
            {
                throw new Exception($"BASS WASAPI開始時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            System.Threading.Thread.Sleep(500);
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            if (!BassWasapi.BASS_WASAPI_Stop(true))
            {
                throw new Exception($"BASS WASAPI停止時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }
        }

        public void Free()
        {
            if (!BassWasapi.BASS_WASAPI_Free())
            {
                throw new Exception($"BASS WASAPI解放時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            if (Bass.BASS_Stop())
            {
                throw new Exception($"BASS 音声出力デバイス停止時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }

            if (Bass.BASS_Free())
            {
                throw new Exception($"BASS 音声出力デバイス解放時エラーコード: {Bass.BASS_ErrorGetCode()}");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateSpectrum();
            UpdateLevel();
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
