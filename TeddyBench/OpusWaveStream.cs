using Concentus.Oggfile;
using NAudio.Wave;
using System;
using System.IO;

namespace TeddyBench
{
    public class OpusWaveStream : WaveStream
    {
        private readonly int Rate;
        private readonly int Channels;
        private readonly WaveFormat _Format;
        public readonly OpusOggReadStream OpusDecoder;
        private readonly ByteQueue ByteBuffer = new ByteQueue();
        private byte[] ConversionBuffer = new byte[0];
        private int ConversionBufferLength = 0;

        public Stream BaseStream { get; }

        public override WaveFormat WaveFormat => _Format;

        public override long Length => OpusDecoder.TotalTime.Ticks;

        public override long Position { get => OpusDecoder.CurrentTime.Ticks; set => OpusDecoder.SeekTo(new TimeSpan(value)); }



        public OpusWaveStream(Stream stream, int rate, int channels)
        {
            Rate = rate;
            Channels = channels;
            BaseStream = stream;
            _Format = new WaveFormat(Rate, Channels);

            OpusDecoder = new OpusOggReadStream(Concentus.Structs.OpusDecoder.Create(Rate, Channels), stream);
        }

        internal void SeekTo(TimeSpan newPos)
        {
            lock(OpusDecoder)
            {
                OpusDecoder.SeekTo(newPos);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (ByteBuffer.Length < count)
            {
                short[] shortBuf;

                lock (OpusDecoder)
                {
                    shortBuf = OpusDecoder.DecodeNextPacket();
                }

                if (shortBuf == null || shortBuf.Length == 0)
                {
                    break;
                }
                FillConversionBuffer(shortBuf);

                ByteBuffer.Enqueue(ConversionBuffer, 0, ConversionBufferLength);
            }

            int copySize = Math.Min(count, (int)ByteBuffer.Length);
            ByteBuffer.Dequeue(buffer, offset, copySize);

            return copySize;
        }

        private void FillConversionBuffer(short[] shortBuf)
        {
            int newLength = shortBuf.Length * 2;
            if (ConversionBuffer.Length < newLength)
            {
                ConversionBuffer = new byte[newLength];
            }

            ConversionBufferLength = newLength;

            for (int pos = 0; pos < shortBuf.Length; pos++)
            {
                ConversionBuffer[2 * pos + 0] = (byte)shortBuf[pos];
                ConversionBuffer[2 * pos + 1] = (byte)(shortBuf[pos] >> 8);
            }
        }
    }
}
