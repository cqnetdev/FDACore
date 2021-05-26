using System;
using System.Collections.Generic;
using System.Text;

namespace Support
{

    public static class IntHelpers
    {
        public static Byte GetHighNibble(Byte value)
        {
            return (Byte)((value & 0xF0) >> 4);
        }

        public static Byte GetLowNibble(Byte value)
        {
            return (Byte)(value & 0x0F);
        }

        public static Byte GetHighByte(ushort value)
        {
            return (Byte)((value & 0xFF00) >> 8);
        }

        public static Byte GetHighByte(int value)
        {
            return (Byte)((value & 0xFF00) >> 8);
        }

        public static Byte GetLowByte(int value)
        {
            return (Byte)(value & 0x00FF);
        }

        public static Byte GetLowByte(ushort value)
        {
            return (Byte)(value & 0x00FF);
        }

        public static bool GetBit(byte bytevalue, int bitnum)
        {
            return (bytevalue & (1 << bitnum)) != 0;
        }

        public static ushort ToUInt16(byte highByte, byte lowByte)
        {
            UInt16 value = (UInt16)((highByte << 8) | (lowByte));
            return value;
        }

        public static ushort SwapBytes(ushort data)
        {
            return (ushort)(((data & 0x00FF) << 8) | ((data & 0xFF00) >> 8));
        }

        public static byte[] SwapBytes(byte[] data)
        {
            if (data.Length == 2)
                return new byte[] { data[1], data[0] };  // A,B -> B,A

            if (data.Length == 4)
                return new byte[] { data[1], data[0], data[3], data[2] }; // A,B,C,D -> B,A,D,C

            // invalid data type length (can only swap 2 or 4 bytes) 
            return null;
        }

        public static byte[] SwapWords(byte[] data)
        {
            if (data.Length == 4)
                return new byte[] { data[2], data[3], data[0], data[1] };   // A,B,C,D -> C,D,A,B

            // bad data length for swapping words (only 4 bytes accepted)
            return null;
        }
    }
}
