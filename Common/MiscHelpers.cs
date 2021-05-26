using System;
using System.Collections.Generic;
using System.Text;

namespace Support
{
    public static class MiscHelpers
    {

        public static int AddCircular(int value, int offset, int min, int max)
        {
            int newValue = value + offset;
            if (newValue > max)
                newValue = min;

            if (newValue < min)
                newValue = max;

            return newValue;
        }


    }
}
