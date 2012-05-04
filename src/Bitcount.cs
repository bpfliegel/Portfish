using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

using Key = System.UInt64;
using Bitboard = System.UInt64;
using Move = System.Int32;
using File = System.Int32;
using Rank = System.Int32;
using Score = System.Int32;
using Square = System.Int32;
using Color = System.Int32;
using Value = System.Int32;
using PieceType = System.Int32;
using Piece = System.Int32;
using CastleRight = System.Int32;
using Depth = System.Int32;
using Result = System.Int32;
using ScaleFactor = System.Int32;
using Phase = System.Int32;

namespace Portfish
{
    internal static class Bitcount
    {
        /// count_1s() counts the number of nonzero bits in a bitboard.
        /// We have different optimized versions according if platform
        /// is 32 or 64 bits, and to the maximum number of nonzero bits.
        /// We also support hardware popcnt instruction. See Readme.txt
        /// on how to pgo compile with popcnt support.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static int popcount_1s_Full(Bitboard b)
        {
#if X64
            b -= ((b >> 1) & 0x5555555555555555UL);
            b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
            b = ((b >> 4) + b) & 0x0F0F0F0F0F0F0F0FUL;
            b *= 0x0101010101010101UL;
            return (int)(b >> 56);
#else
            UInt32 w = (UInt32)(b >> 32), v = (UInt32)(b);
            v -= (v >> 1) & 0x55555555; // 0-2 in 2 bits
            w -= (w >> 1) & 0x55555555;
            v = ((v >> 2) & 0x33333333) + (v & 0x33333333); // 0-4 in 4 bits
            w = ((w >> 2) & 0x33333333) + (w & 0x33333333);
            v = ((v >> 4) + v) & 0x0F0F0F0F; // 0-8 in 8 bits
            v += (((w >> 4) + w) & 0x0F0F0F0F);  // 0-16 in 8 bits
            v *= 0x01010101; // mul is fast on amd procs
            return (int)(v >> 24);
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static int popcount_1s_Max15(Bitboard b)
        {
#if X64
            b -= (b >> 1) & 0x5555555555555555UL;
            b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
            b *= 0x1111111111111111UL;
            return (int)(b >> 60);
#else
            UInt32 w = (UInt32)(b >> 32), v = (UInt32)(b);
            v -= (v >> 1) & 0x55555555; // 0-2 in 2 bits
            w -= (w >> 1) & 0x55555555;
            v = ((v >> 2) & 0x33333333) + (v & 0x33333333); // 0-4 in 4 bits
            w = ((w >> 2) & 0x33333333) + (w & 0x33333333);
            v += w; // 0-8 in 4 bits
            v *= 0x11111111;
            return (int)(v >> 28);
#endif
        }
    }
}
