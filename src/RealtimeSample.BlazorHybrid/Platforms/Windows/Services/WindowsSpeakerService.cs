using NAudio.Wave;
using RealtimeSample.BlazorHybrid.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeSample.BlazorHybrid.Platforms.Windows.Services
{
    public class WindowsSpeakerService : ISpeakerService
    {
        BufferedWaveProvider _waveProvider;
        WaveOutEvent _waveOutEvent;

        public WindowsSpeakerService()
        {
            WaveFormat outputAudioFormat = new(
                rate: 24000,
                bits: 16,
                channels: 1);
            _waveProvider = new(outputAudioFormat)
            {
                BufferDuration = TimeSpan.FromMinutes(2),
            };
            _waveOutEvent = new();
            _waveOutEvent.Init(_waveProvider);
            _waveOutEvent.Play();
        }

        //public void EnqueueForPlayback(BinaryData audioData)
        //{
        //    var buffer = audioData.ToArray();
        //    _waveProvider.AddSamples(buffer, 0, buffer.Length);
        //}

        //public void ClearPlayback()
        //{
        //    _waveProvider.ClearBuffer();
        //}

        public void Dispose()
        {
            _waveOutEvent?.Dispose();
        }

        public void Init(string connectionId)
        {
        }

        public async Task EnqueueAsync(byte[] audioData, string message)
        {
            _waveProvider.AddSamples(audioData, 0, audioData.Length);
        }

        public async Task ClearPlaybackAsync()
        {
            _waveProvider.ClearBuffer();
        }
    }
}
