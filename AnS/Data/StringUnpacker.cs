using System;
using System.Collections.Generic;
using System.Text;

namespace AnS.Data
{
    public class StringUnpacker
    {
        string data;
        public int Position
        {
            get; set;
        }

        public bool CanRead
        {
            get
            {
                return Position < data.Length;
            }
        }

        public StringUnpacker(string k)
        {
            data = string.IsNullOrEmpty(k) ? "" : k;
            Position = 0;
        }

        public uint NextUInt()
        {
            if (!CanRead)
            {
                return 0;
            }

            string sub = data.Substring(Position, 4);
            Position += 4;
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; ++i)
            {
                bytes[i] = (byte)sub[i];
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes);
        }

        public ushort NextUShort()
        {
            if (!CanRead)
            {
                return 0;
            }

            string sub = data.Substring(Position, 2);
            Position += 2;
            byte[] bytes = new byte[2];
            for (int i = 0; i < 2; ++i)
            {
                bytes[i] = (byte)sub[i];
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt16(bytes);
        }

        public byte NextByte()
        {
            if (!CanRead)
            {
                return 0;
            }
            return (byte)data[Position++];
        }
    }
}
