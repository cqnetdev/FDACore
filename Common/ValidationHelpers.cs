using System;

namespace Support
{
    public static class ValidationHelpers
    {
        public static bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out _);
        }
    }
}