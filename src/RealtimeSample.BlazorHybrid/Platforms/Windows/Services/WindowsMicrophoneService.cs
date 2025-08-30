using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealtimeSample.BlazorHybrid.Services.Contracts;


namespace RealtimeSample.BlazorHybrid.Platforms.Windows.Services
{
    public class WindowsMicrophoneService : IMicrophoneService
    {
        public Stream GetStream()
        {
            return new MicrophoneStream();
        }
    }
}
