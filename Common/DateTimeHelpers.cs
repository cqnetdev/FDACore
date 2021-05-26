using System;
using System.Collections.Generic;
using System.Text;

namespace Support
{
    public static class DateTimeHelpers
    {
        public static bool IsValidDayOfMonth(int day, int month)
        {
            if (day < 1)
                return false;

            if (month == 2 && day > 29)
                return false;

            if ((month == 4 || month == 6 || month == 9 || month == 11) && day > 30)
            {
                return false;
            }

            if (day > 31)
                return false;

            return true;
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

        private static void Write4Chars(char[] chars, int offset, int value)
        {
            chars[offset] = Digit(value / 1000);
            value -= (value / 1000) * 1000;
            chars[offset + 1] = Digit(value / 100);
            value -= (value / 100) * 100;
            chars[offset + 2] = Digit(value / 10);
            chars[offset + 3] = Digit(value % 10);
        }

        private static char Digit(int value)
        {
            return (char)(value + '0');
        }
    }

  

}
