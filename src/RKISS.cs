using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace Portfish
{
    /// RKISS is our pseudo random number generator (PRNG) used to compute hash keys.
    /// George Marsaglia invented the RNG-Kiss-family in the early 90's. This is a
    /// specific version that Heinz van Saanen derived from some internal domain code
    /// by Bob Jenkins. Following the feature list, as tested by Heinz.
    ///
    /// - Quite platform independent
    /// - Passes ALL dieharder tests! Here *nix sys-rand() e.g. fails miserably:-)
    /// - ~12 times faster than my *nix sys-rand()
    /// - ~4 times faster than SSE2-version of Mersenne twister
    /// - Average cycle length: ~2^126
    /// - 64 bit seed
    /// - Return doubles with a full 53 bit mantissa
    /// - Thread safe
    internal sealed class RKISS
    {
        UInt64 a, b, c, d, e;

        // Init seed and scramble a few rounds
        internal RKISS()
        {
            a = 0xf1ea5eed;
            b = c = d = 0xd4e12c77;
            for (int i = 0; i < 73; i++)
            {
                rand();
            }
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal UInt64 rand()
        {
            e = a - ((b << 7) | (b >> 57));
            a = b ^ ((c << 13) | (c >> 51));
            b = c + ((d << 37) | (d >> 27));
            c = d + e;
            return d = e + a;
        }
    }
}
