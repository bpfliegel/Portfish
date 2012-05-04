using System;
using System.Collections.Generic;
using System.Text;

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
using System.Runtime.CompilerServices;

namespace Portfish
{
    /// The History class stores statistics about how often different moves
    /// have been successful or unsuccessful during the current search. These
    /// statistics are used for reduction and move ordering decisions. History
    /// entries are stored according only to moving piece and destination square,
    /// in particular two moves with different origin but same destination and
    /// same piece will be considered identical.
    internal sealed class History
    {
        internal const Value MaxValue = 2000;

        internal readonly Value[][] history = new Value[16][];  // [piece][to_square] 16, 64
        internal readonly Value[][] maxGains = new Value[16][]; // [piece][to_square] 16, 64

        internal History()
        {
            for (int i = 0; i < 16; i++)
            {
                history[i] = new Value[64];
                maxGains[i] = new Value[64];
            }
        }

        internal void clear()
        {
            for (int i = 0; i < 16; i++)
            {
                Array.Clear(history[i], 0, 64);
                Array.Clear(maxGains[i], 0, 64);
            }
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Value value(Piece p, Square to)
        {
            return history[p][to];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void add(Piece p, Square to, Value bonus)
        {
            if (Math.Abs(history[p][to] + bonus) < MaxValue) history[p][to] += bonus;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Value gain(Piece p, Square to)
        {
            return maxGains[p][to];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void update_gain(Piece p, Square to, Value g)
        {
            maxGains[p][to] = Math.Max(g, maxGains[p][to] - 1);
        }
    }
}
