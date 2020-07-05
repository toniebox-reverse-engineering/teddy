using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeddyBench
{
    public class ISO15693
    {
        [Flags]
        public enum Flag
        {
            SUBCARRIER_TWO = (1 << 0),
            DATARATE_HIGH = (1 << 1),
            INVENTORY = (1 << 2),
            PROTOCOL_EXT = (1 << 3),
            OPTION = (1 << 6),
            SELECT = (1 << 4),
            ADDRESS = (1 << 5),
            INV_AFI = (1 << 4),
            INV_SLOT1 = (1 << 5)
        }

        public enum Command
        {
            INVENTORY = 0x01,
            STAYQUIET = 0x02,
            READBLOCK = 0x20,
            WRITEBLOCK = 0x21,
            LOCKBLOCK = 0x22,
            READ_MULTI_BLOCK = 0x23,
            WRITE_MULTI_BLOCK = 0x24,
            SELECT = 0x25,
            RESET_TO_READY = 0x26,
            WRITE_AFI = 0x27,
            LOCK_AFI = 0x28,
            WRITE_DSFID = 0x29,
            LOCK_DSFID = 0x2A,
            GET_SYSTEM_INFO = 0x2B,
            READ_MULTI_SECSTATUS = 0x2C,
            NXP_SET_EAS = 0xA2,
            NXP_RESET_EAS = 0xA3,
            NXP_LOCK_EAS = 0xA4,
            NXP_EAS_ALARM = 0xA5,
            NXP_PASSWORD_PROTECT_EAS_AFI = 0xA6,
            NXP_WRITE_EAS_ID = 0xA7,
            NXP_INVENTORY_PAGE_READ = 0xB0,
            NXP_INVENTORY_PAGE_READ_FAST = 0xB1,
            NXP_GET_RANDOM_NUMBER = 0xB2,
            NXP_SET_PASSWORD = 0xB3,
            NXP_WRITE_PASSWORD = 0xB4,
            NXP_DESTROY = 0xB9,
            NXP_ENABLE_PRIVACY = 0xBA
        }

        private const byte MANUFACTURER_NXP = 0x04;

        public static byte[] BuildCommand(Command command, byte[] payload)
        {
            return BuildCommand(command, null, payload);
        }

        public static byte[] BuildCommand(Command command, ulong uid, byte[] payload = null)
        {
            if (uid != 0)
            {
                return BuildCommand(command, BitConverter.GetBytes(uid), payload);
            }
            else
            {
                return BuildCommand(command, null, payload);
            }
        }

        private static ushort CalcChecksum(byte[] buf, int start, int length)
        {
            uint crc = 0xFFFF;

            for (int i = start; i < start+length; i++)
            {
                crc ^= buf[i];

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ 0x8408;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            crc = (crc & 0xffff) ^ 0xFFFF;

            return (ushort)crc;
        }

        private static void SetChecksum(byte[] buf)
        {
            int crcPos = buf.Length - 2;
            ushort crc = CalcChecksum(buf, 0, crcPos);

            buf[crcPos + 0] = (byte)(crc & 0xff);
            buf[crcPos + 1] = (byte)(crc >> 8);
        }

        public static bool CheckChecksum(byte[] buf, int start = 0)
        {
            int crcPos = buf.Length - 2 - start;
            ushort crc = CalcChecksum(buf, start, crcPos);

            if(buf[crcPos + 0] != (byte)(crc & 0xff) ||  buf[crcPos + 1] != (byte)(crc >> 8))
            {
                return false;
            }

            return true;
        }

        public static byte[] BuildCommand(Command command, byte[] uid = null, byte[] payload = null)
        {
            Flag flags = Flag.DATARATE_HIGH;
            bool hasUid = uid != null;
            bool hasPayload = payload != null;
            bool advanced = (int)command >= 0xA0;
            byte[] buf = new byte[1 + 1 + (advanced ? 1 : 0) + (hasUid ? uid.Length : 0) + (hasPayload ? payload.Length : 0) + 2];
            int pos = 0;

            if (command == Command.INVENTORY)
            {
                flags |= Flag.INVENTORY;
                flags |= Flag.INV_SLOT1;
            }
            else
            {
                if (hasUid)
                {
                    flags |= Flag.ADDRESS;
                }
            }

            buf[pos++] = (byte)flags;
            buf[pos++] = (byte)command;

            if (advanced)
            {
                buf[pos++] = MANUFACTURER_NXP;
            }

            bool addressed = !(flags.HasFlag(Flag.INVENTORY)) && (flags.HasFlag(Flag.ADDRESS));

            if (addressed)
            {
                Array.Copy(uid, 0, buf, pos, uid.Length);
                pos += uid.Length;
            }

            if (payload != null)
            {
                Array.Copy(payload, 0, buf, pos, payload.Length);
            }

            SetChecksum(buf);

            return buf;
        }
    }
}
