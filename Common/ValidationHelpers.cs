using System;
using System.Collections.Generic;
using System.Text;

namespace Support
{
    public static class ValidationHelpers
    {
        public static bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out Guid guid);
        }
    }
}
