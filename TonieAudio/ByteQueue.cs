using System;

#region Copyright & License
/*************************************************************************
    * 
    * The MIT License (MIT)
    * 
    * Copyright (c) 2014 Roman Atachiants (kelindar@gmail.com)
    * Permission is hereby granted, free of charge, to any person obtaining a copy
    * of this software and associated documentation files (the "Software"), to deal
    * in the Software without restriction, including without limitation the rights
    * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    * copies of the Software, and to permit persons to whom the Software is
    * furnished to do so, subject to the following conditions:
    * 
    * The above copyright notice and this permission notice shall be included in
    * all copies or substantial portions of the Software.
    * 
    * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    * THE SOFTWARE.
*************************************************************************/
#endregion


namespace TonieFile
{
    /// <summary>
    /// Defines a class that represents a resizable circular byte queue.
    /// </summary>
    public sealed class ByteQueue
    {
        // Private fields
        private int fHead;
        private int fTail;
        private int fSize;
        private int fSizeUntilCut;
        private byte[] fInternalBuffer;

        /// <summary>
        /// Gets the length of the byte queue
        /// </summary>
        public int Length
        {
            get { return fSize; }
        }

        /// <summary>
        /// Constructs a new instance of a byte queue.
        /// </summary>
        public ByteQueue()
        {
            fInternalBuffer = new byte[2048];
        }

        /// <summary>
        /// Clears the byte queue
        /// </summary>
        internal void Clear()
        {
            fHead = 0;
            fTail = 0;
            fSize = 0;
            fSizeUntilCut = fInternalBuffer.Length;
        }

        /// <summary>
        /// Clears the byte queue
        /// </summary>
        internal void Clear(int size)
        {
            lock (this)
            {
                if (size > fSize)
                    size = fSize;

                if (size == 0)
                    return;

                fHead = (fHead + size) % fInternalBuffer.Length;
                fSize -= size;

                if (fSize == 0)
                {
                    fHead = 0;
                    fTail = 0;
                }

                fSizeUntilCut = fInternalBuffer.Length - fHead;
                return;
            }
        }

        /// <summary>
        /// Extends the capacity of the bytequeue
        /// </summary>
        private void SetCapacity(int capacity)
        {
            byte[] newBuffer = new byte[capacity];

            if (fSize > 0)
            {
                if (fHead < fTail)
                {
                    Buffer.BlockCopy(fInternalBuffer, fHead, newBuffer, 0, fSize);
                }
                else
                {
                    Buffer.BlockCopy(fInternalBuffer, fHead, newBuffer, 0, fInternalBuffer.Length - fHead);
                    Buffer.BlockCopy(fInternalBuffer, 0, newBuffer, fInternalBuffer.Length - fHead, fTail);
                }
            }

            fHead = 0;
            fTail = fSize;
            fInternalBuffer = newBuffer;
        }


        /// <summary>
        /// Enqueues a buffer to the queue and inserts it to a correct position
        /// </summary>
        /// <param name="buffer">Buffer to enqueue</param>
        /// <param name="offset">The zero-based byte offset in the buffer</param>
        /// <param name="size">The number of bytes to enqueue</param>
        internal void Enqueue(byte[] buffer, int offset, int size)
        {
            if (size == 0)
                return;

            lock (this)
            {
                if ((fSize + size) > fInternalBuffer.Length)
                    SetCapacity((fSize + size + 2047) & ~2047);

                if (fHead < fTail)
                {
                    int rightLength = (fInternalBuffer.Length - fTail);

                    if (rightLength >= size)
                    {
                        Buffer.BlockCopy(buffer, offset, fInternalBuffer, fTail, size);
                    }
                    else
                    {
                        Buffer.BlockCopy(buffer, offset, fInternalBuffer, fTail, rightLength);
                        Buffer.BlockCopy(buffer, offset + rightLength, fInternalBuffer, 0, size - rightLength);
                    }
                }
                else
                {
                    Buffer.BlockCopy(buffer, offset, fInternalBuffer, fTail, size);
                }

                fTail = (fTail + size) % fInternalBuffer.Length;
                fSize += size;
                fSizeUntilCut = fInternalBuffer.Length - fHead;
            }
        }

        /// <summary>
        /// Dequeues a buffer from the queue
        /// </summary>
        /// <param name="buffer">Buffer to enqueue</param>
        /// <param name="offset">The zero-based byte offset in the buffer</param>
        /// <param name="size">The number of bytes to dequeue</param>
        /// <returns>Number of bytes dequeued</returns>
        internal int Dequeue(byte[] buffer, int offset, int size)
        {
            lock (this)
            {
                if (size > fSize)
                    size = fSize;

                if (size == 0)
                    return 0;

                if (fHead < fTail)
                {
                    Buffer.BlockCopy(fInternalBuffer, fHead, buffer, offset, size);
                }
                else
                {
                    int rightLength = (fInternalBuffer.Length - fHead);

                    if (rightLength >= size)
                    {
                        Buffer.BlockCopy(fInternalBuffer, fHead, buffer, offset, size);
                    }
                    else
                    {
                        Buffer.BlockCopy(fInternalBuffer, fHead, buffer, offset, rightLength);
                        Buffer.BlockCopy(fInternalBuffer, 0, buffer, offset + rightLength, size - rightLength);
                    }
                }

                fHead = (fHead + size) % fInternalBuffer.Length;
                fSize -= size;

                if (fSize == 0)
                {
                    fHead = 0;
                    fTail = 0;
                }

                fSizeUntilCut = fInternalBuffer.Length - fHead;
                return size;
            }
        }


        /// <summary>
        /// Peeks a byte with a relative index to the fHead
        /// Note: should be used for special cases only, as it is rather slow
        /// </summary>
        /// <param name="index">A relative index</param>
        /// <returns>The byte peeked</returns>
        private byte PeekOne(int index)
        {
            return index >= fSizeUntilCut
                ? fInternalBuffer[index - fSizeUntilCut]
                : fInternalBuffer[fHead + index];
        }


    }

}
