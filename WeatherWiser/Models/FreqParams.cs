using System;
using Un4seen.Bass;

namespace WeatherWiser.Models
{
    public class FreqParams
    {
        // 可聴域の周波数倍率
        // 約20Hz～20KHzの対数スケールとするため、log(20,2)≒4.32～log(20000,2)≒14.29の範囲をもとに14.29-10=4.29としている）
        public float FreqShift => _freqShift;
        private readonly float _freqShift = (float)Math.Round(Math.Log(20000, 2) - 10, 2); // = 4.29

        public int MixFreq => _mixFreq;
        private readonly int _mixFreq;

        public BASSData BassData => _bassData;
        private readonly BASSData _bassData;

        public float MixFreqMultiplyer => _mixFreqMultiplyer;
        private readonly float _mixFreqMultiplyer;

        public int MaxFftLength => _maxFftLength;
        private readonly int _maxFftLength;

        public FreqParams(int mixFreq)
        {
            _mixFreq = mixFreq;
            _bassData = GetBassData(_mixFreq);
            _mixFreqMultiplyer = GetMixFreqMultiplyer(_mixFreq);
            _maxFftLength = GetMaxFftLength(_bassData);
        }

        private BASSData GetBassData(int mixFreq)
        {
            return mixFreq switch
            {
                <= 48000 => BASSData.BASS_DATA_FFT2048,     // ~48khz
                <= 96000 => BASSData.BASS_DATA_FFT4096,     // ~96khz
                <= 192000 => BASSData.BASS_DATA_FFT8192,    // ~192khz
                _ => BASSData.BASS_DATA_FFT16384            // ~384khz
            };
        }

        private float GetMixFreqMultiplyer(int mixFreq)
        {
            return mixFreq switch
            {
                <= 48000 => 2048f / mixFreq,
                <= 96000 => 4096f / mixFreq,
                <= 192000 => 8192f / mixFreq,
                _ => 16384f / mixFreq
            };
        }

        private int GetMaxFftLength(BASSData bassData)
        {
            return bassData switch
            {
                BASSData.BASS_DATA_FFT2048 => 2048,
                BASSData.BASS_DATA_FFT4096 => 4096,
                BASSData.BASS_DATA_FFT8192 => 8192,
                _ => 16384
            };
        }
    }
}
