using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Oggfile
{
#pragma warning disable CS0169 // Field is never used
    internal class OpusHeader
    {
        byte _version;
        byte _channel_count;
        ushort _pre_skip;
        uint _input_sample_rate;
        short _output_gain;
        byte _mapping_family;
        byte _stream_count;
        byte _coupled_count;
    }
#pragma warning restore CS0169 // Field is never used
}
