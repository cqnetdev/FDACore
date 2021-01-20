using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public class Helpers
    {
        public static bool IsValidDayOfMonth(int day,int month)
        {
            if (day < 1)
                return false;

            if (month == 2 && day > 29)
                return false;

            if ( (month == 4 || month == 6 || month == 9 || month == 11) && day > 30)
            {
                return false;
            }

            if (day > 31)
                return false;

            return true;
        }

        public static int AddCircular(int value,int offset,int min,int max)
        {
            int newValue = value + offset;
            if (newValue > max)
                newValue = min;

            if (newValue < min)
                newValue = max;

            return newValue;
        }

        public static bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out Guid guid);
        }

        // this way is more efficient than DateTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
        public static string FormatDateTime(DateTime dt)
        {
            //yyyy-MM-dd HH:mm.ss.fff
            char[] chars = new char[23];
            Write4Chars(chars, 0, dt.Year);
            chars[4] = '-';
            Write2Chars(chars, 5, dt.Month);
            chars[7] = '-';
            Write2Chars(chars, 8, dt.Day);   
            chars[10] = ' ';
            Write2Chars(chars, 11, dt.Hour);
            chars[13] = ':';
            Write2Chars(chars, 14, dt.Minute);
            chars[16] = ':';
            Write2Chars(chars, 17, dt.Second);
            chars[19] = '.';
            Write2Chars(chars, 20, dt.Millisecond / 10);
            chars[22] = Digit(dt.Millisecond % 10);

            return new string(chars);
        }

        private static void Write2Chars(char[] chars, int offset, int value)
        {
            chars[offset] = Digit(value / 10);
            chars[offset + 1] = Digit(value % 10);
        }

        private static void Write4Chars(char[] chars,int offset, int value)
        {
            chars[offset] = Digit(value / 1000);
            value -= (value / 1000)*1000;
            chars[offset + 1] = Digit(value / 100);
            value -= (value / 100)*100;
            chars[offset + 2] = Digit(value / 10);
            chars[offset + 3] = Digit(value % 10);
        }

        private static char Digit(int value)
        {
            return (char)(value + '0');
        }



        /* this seemed to be taking a lot of CPU time, replaced with above
        public static string ConsoleFormatDateTime(DateTime datetime)
        {
            return datetime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        */

        /*
        public static string SQLFormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") ;
        }
        */

        public static string xorString(string key, string input)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
                sb.Append((char)(input[i] ^ key[(i % key.Length)]));
            String result = sb.ToString();

            return result;
        }

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

        public static bool GetBit(byte bytevalue,int bitnum)
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
                return new byte[] { data[2], data[3], data[0],data[1]};   // A,B,C,D -> C,D,A,B

            // bad data length for swapping words (only 4 bytes accepted)
            return null;
        }

        public static byte[] ByteToBits(byte byteValue)
        {
            byte[] bits = new byte[8];
            byte mask;
            for (byte i = 0; i < 8; i++)
            {
                mask = (byte)Math.Pow(2, i);
                bits[i] = (byte)((byteValue & mask) >> i);              
            }
            return bits;
        }

        public static byte ByteFromBits(byte[] bits)
        {
            byte byteValue = 0;
            for (int i = 0; i < 8; i++)
                byteValue += (byte)Math.Pow(2, i);

            return byteValue;
        }


        public static ushort CalcCRC16(byte[] data,ushort initial,ushort poly,bool swapBytes)
        {
            ushort CRC = initial;
            byte databyte;
            byte flag;
            int length = data.Length;

            for (int bytenum = 0; bytenum < length; bytenum++)
            {
                databyte = data[bytenum];
                CRC = (ushort)(CRC ^ databyte);

                for (byte i = 0; i < 8; i++)
                {
                    flag = (byte)(CRC & 0x01);
                    CRC = (ushort)(CRC >> 1);
                    if (flag != 0)
                        CRC = (ushort)(CRC ^ poly);
               }
            }

            if (swapBytes)
                CRC = SwapBytes(CRC);

            return CRC; 
        }
    }
}
