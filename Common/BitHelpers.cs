using System;

namespace Support
{
    public static class BitHelpers
    {
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

        // not used
        //public static byte ByteFromBits(byte[] bits)
        //{
        //    byte byteValue = 0;
        //    for (int i = 0; i < 8; i++)
        //        byteValue += (byte)Math.Pow(2, i);

        //    return byteValue;
        //}
    }
}