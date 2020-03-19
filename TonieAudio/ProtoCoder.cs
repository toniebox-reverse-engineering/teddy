/* Copyright (c) 2020 g3gg0.de

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TonieFile
{
    public class ProtoCoder
    {
        public class SkipEncodeAttribute : Attribute
        {
        }

        private List<byte> Payload = new List<byte>();
        private int PayloadPos = 0;

        public ProtoCoder(byte[] data)
        {
            Payload.AddRange(data);
        }

        public ProtoCoder()
        {
        }

        private bool HasPayload()
        {
            return PayloadPos < Payload.Count();
        }

        /* https://developers.google.com/protocol-buffers/docs/encoding */
        private enum eFieldType
        {
            Variant = 0,
            Fixed64 = 1,
            String = 2,
            Fixed32 = 5
        }

        private void AddField(ulong field, eFieldType type)
        {
            ulong value = (field << 3) | (ulong)type;
            AddVariant(value);
        }

        private void GetField(out ulong field, out eFieldType type)
        {
            ulong value = GetVariant();

            field = value >> 3;
            type = (eFieldType)(value & 7);
        }

        private void AddVariant(uint value)
        {
            AddVariant((ulong)value);
        }

        private void AddVariant(int value)
        {
            AddVariant((ulong)value);
        }

        private void AddVariant(long value)
        {
            AddVariant((ulong)value);
        }

        private void AddVariant(ulong value)
        {
            do
            {
                byte val = (byte)(value & 0x7F);

                value >>= 7;
                if (value > 0)
                {
                    val |= 0x80;
                }
                Payload.Add(val);
            } while (value > 0);
        }

        private void AddVariantField(ulong field, long value)
        {
            AddVariantField(field, (ulong)value);
        }

        private void AddVariantField(ulong field, ulong value)
        {
            AddField(field, eFieldType.Variant);
            AddVariant(value);
        }

        public void AddStringField(ulong field, byte[] value)
        {
            if (value == null)
            {
                return;
            }
            AddField(field, eFieldType.String);
            AddVariant(value.Length);
            Payload.AddRange(value);
        }

        private byte GetByte()
        {
            return Payload[PayloadPos++];
        }

        private void GetBytes(byte[] data)
        {
            for (int pos = 0; pos < data.Length; pos++)
            {
                data[pos] = GetByte();
            }
        }

        private ulong GetVariant()
        {
            ulong ret = 0;
            int bytePos = 0;
            do
            {
                byte data = GetByte();

                ret |= ((ulong)(data & 0x7F)) << (7 * bytePos);

                if ((data & 0x80) == 0)
                {
                    break;
                }
                bytePos++;
            } while (true);

            return ret;
        }

        public void FillObject(object obj)
        {
            Type objType = obj.GetType();
            ulong lastField = 1;
            FieldInfo[] objFields = objType.GetFields();

            while (HasPayload())
            {
                GetField(out ulong field, out eFieldType type);

                if (field < lastField)
                {
                    throw new FormatException("Field " + field + " unexpected after field " + lastField);
                }

                if ((int)field - 1 >= objFields.Length)
                {
                    throw new FormatException("Next field " + field + " unexpected in structure.");
                }

                var objField = objFields[field - 1];
                var objFieldType = objField.FieldType;

                switch (type)
                {
                    case eFieldType.Variant:
                        {
                            ulong value = GetVariant();

                            switch (objFieldType.Name)
                            {
                                case "Bool":
                                    objField.SetValue(obj, value != 0);
                                    break;
                                case "Byte":
                                    objField.SetValue(obj, (Byte)value);
                                    break;
                                case "Int16":
                                    objField.SetValue(obj, (Int16)value);
                                    break;
                                case "Int32":
                                    objField.SetValue(obj, (Int32)value);
                                    break;
                                case "Int64":
                                    objField.SetValue(obj, (Int64)value);
                                    break;
                                case "UInt16":
                                    objField.SetValue(obj, (UInt16)value);
                                    break;
                                case "UInt32":
                                    objField.SetValue(obj, (UInt32)value);
                                    break;
                                case "UInt64":
                                    objField.SetValue(obj, (UInt64)value);
                                    break;
                                default:
                                    Console.WriteLine("Unexpected type '" + objFieldType.Name + "'");
                                    break;
                            }
                            break;
                        }
                    case eFieldType.String:
                        {
                            ulong length = GetVariant();

                            switch (objFieldType.Name)
                            {
                                case "Byte[]":
                                    {
                                        byte[] data = new byte[length];
                                        GetBytes(data);

                                        objField.SetValue(obj, data);
                                        break;
                                    }
                                case "UInt16[]":
                                case "UInt32[]":
                                case "UInt64[]":
                                    {
                                        int curPos = PayloadPos;
                                        List<ulong> values = new List<ulong>();

                                        while (PayloadPos < curPos + (int)length)
                                        {
                                            values.Add(GetVariant());
                                        }

                                        switch (objFieldType.Name)
                                        {
                                            case "Int16[]":
                                                {
                                                    Int16[] data = values.Select(v => (Int16)v).ToArray();
                                                    objField.SetValue(obj, data);
                                                    break;
                                                }
                                            case "Int32[]":
                                                {
                                                    Int32[] data = values.Select(v => (Int32)v).ToArray();
                                                    objField.SetValue(obj, data);
                                                    break;
                                                }
                                            case "Int64[]":
                                                {
                                                    Int64[] data = values.Select(v => (Int64)v).ToArray();
                                                    objField.SetValue(obj, data);
                                                    break;
                                                }
                                            case "UInt16[]":
                                                {
                                                    UInt16[] data = values.Select(v => (UInt16)v).ToArray();
                                                    objField.SetValue(obj, data);
                                                    break;
                                                }
                                            case "UInt32[]":
                                                {
                                                    UInt32[] data = values.Select(v => (UInt32)v).ToArray();
                                                    objField.SetValue(obj, data);
                                                    break;
                                                }
                                            case "UInt64[]":
                                                {
                                                    UInt64[] data = values.Select(v => (UInt64)v).ToArray();
                                                    objField.SetValue(obj, data);
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                default:
                                    Console.WriteLine("Unexpected type '" + objFieldType.Name + "'");
                                    break;

                            }
                            break;
                        }
                }
            }
        }

        public T Deserialize<T>(byte[] protoBuf) where T : class, new()
        {
            T obj = new T();

            Payload.Clear();
            Payload.AddRange(protoBuf);

            FillObject(obj);

            return obj;
        }

        internal byte[] Serialize<T>(T obj) where T : class
        {
            Payload.Clear();
            AddObject(obj);
            return Payload.ToArray();
        }

        private void AddObject<T>(T obj) where T : class
        {
            Type destObjectType = obj.GetType();
            var objFields = destObjectType.GetFields();
            ulong field = 1;

            for (int pos = 0; pos < objFields.Length; pos++)
            {
                var objField = objFields[pos];
                var objFieldType = objField.FieldType;

                if (objField.CustomAttributes.Where(a => a.AttributeType.Name == "SkipEncodeAttribute").Count() != 0)
                {
                    continue;
                }

                if (objFieldType.IsArray)
                {
                    if (objFieldType.Name == "Byte[]")
                    {
                        AddStringField(field, (byte[])objField.GetValue(obj));
                    }
                    else
                    {
                        ProtoCoder subCoder = new ProtoCoder();

                        switch (objFieldType.Name)
                        {
                            case "Int16[]":
                                ((Int16[])objField.GetValue(obj)).ToList().ForEach(v => subCoder.AddVariant((ulong)v));
                                break;
                            case "Int32[]":
                                ((Int32[])objField.GetValue(obj)).ToList().ForEach(v => subCoder.AddVariant((ulong)v));
                                break;
                            case "Int64[]":
                                ((Int64[])objField.GetValue(obj)).ToList().ForEach(v => subCoder.AddVariant((ulong)v));
                                break;
                            case "UInt16[]":
                                ((UInt16[])objField.GetValue(obj)).ToList().ForEach(v => subCoder.AddVariant((ulong)v));
                                break;
                            case "UInt32[]":
                                ((UInt32[])objField.GetValue(obj)).ToList().ForEach(v => subCoder.AddVariant((ulong)v));
                                break;
                            case "UInt64[]":
                                ((UInt64[])objField.GetValue(obj)).ToList().ForEach(v => subCoder.AddVariant((ulong)v));
                                break;
                        }

                        byte[] data = subCoder.Payload.ToArray();
                        AddStringField(field, data);
                    }
                }
                else
                {
                    switch (objFieldType.Name)
                    {
                        case "Boolean":
                            AddVariantField(field, ((Boolean)objField.GetValue(obj)) ? 1 : 0);
                            break;
                        case "Byte":
                            AddVariantField(field, (Byte)objField.GetValue(obj));
                            break;
                        case "Int16":
                            AddVariantField(field, (Int16)objField.GetValue(obj));
                            break;
                        case "Int32":
                            AddVariantField(field, (Int32)objField.GetValue(obj));
                            break;
                        case "Int64":
                            AddVariantField(field, (Int64)objField.GetValue(obj));
                            break;
                        case "UInt16":
                            AddVariantField(field, (UInt16)objField.GetValue(obj));
                            break;
                        case "UInt32":
                            AddVariantField(field, (UInt32)objField.GetValue(obj));
                            break;
                        case "UInt64":
                            AddVariantField(field, (UInt64)objField.GetValue(obj));
                            break;
                        default:
                            Console.WriteLine("Unexpected type '" + objFieldType.Name + "'");
                            break;
                    }
                }
                field++;

            }
        }
    }
}
