using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Toast
{
    internal static class TimeUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
        {
            var timestampFrequency = Stopwatch.Frequency;
            if (timestampFrequency <= 0)
            {
                throw new InvalidOperationException("The operation cannot be performed when Stopwatch.Frequency is zero or negative.");
            }

            return new TimeSpan((long)((endingTimestamp - startingTimestamp) * ((double)TimeSpan.TicksPerSecond / timestampFrequency)));
        }
    }
}