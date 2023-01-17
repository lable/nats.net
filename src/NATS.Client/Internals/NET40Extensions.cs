using System;
using System.Collections.Generic;
using System.Text;

namespace NATS.Client.Internals
{
#if NET40
    internal static class Extensions
    {
        public static long ToUnixTimeMilliseconds(this DateTimeOffset UtcDateTime)
        {
            long num = UtcDateTime.Ticks / 10000;
            return num - 62135596800000L;
        }
    }

    internal static class EmptyArray<T>
    {
        public static readonly T[] Value = new T[0];
    }
#endif
}
