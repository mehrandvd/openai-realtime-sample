using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeSample.BlazorHybrid.Services.Contracts
{
    public interface IMicrophoneService
    {
        Stream GetStream();
    }
}
