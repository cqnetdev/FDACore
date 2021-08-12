namespace Support
{
    public static class CRCHelpers
    {
        public static ushort CalcCRC16(byte[] data, ushort initial, ushort poly, bool swapBytes)
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
                CRC = IntHelpers.SwapBytes(CRC);

            return CRC;
        }
    }
}