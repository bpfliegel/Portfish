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
using TracedType = System.Int32;
using System.Runtime.CompilerServices;

namespace Portfish
{
    /// The TTEntry is the class of transposition table entries
    ///
    /// A TTEntry needs 128 bits to be stored
    ///
    /// bit  0-31: key
    /// bit 32-63: data
    /// bit 64-79: value
    /// bit 80-95: depth
    /// bit 96-111: static value
    /// bit 112-127: margin of static value
    ///
    /// the 32 bits of the data field are so defined
    ///
    /// bit  0-15: move
    /// bit 16-20: not used
    /// bit 21-22: value type
    /// bit 23-31: generation
    internal struct TTEntry
    {
        internal UInt32 key;
        internal UInt16 move16;
        internal byte bound, generation8;
        internal Int16 value16, depth16, staticValue, staticMargin;

        internal void save(UInt32 k, Value v, Bound t, Depth d, Move m, int g, Value statV, Value statM)
        {
            key = k;
            move16 = (UInt16)m;
            bound = (byte)t;
            generation8 = (byte)g;
            value16 = (Int16)v;
            depth16 = (Int16)d;
            staticValue = (Int16)statV;
            staticMargin = (Int16)statM;
        }

        internal void set_generation(int g) { generation8 = (byte)g; }

        //internal UInt32 key() { return key32; }
        internal Depth depth() { return (Depth)depth16; }
        internal Move move() { return (Move)move16; }
        internal Value value() { return (Value)value16; }
        internal Bound type() { return (Bound)bound; }
        internal int generation() { return (int)generation8; }
        internal Value static_value() { return (Value)staticValue; }
        internal Value static_value_margin() { return (Value)staticMargin; }
    };

    /// The transposition table class. This is basically just a huge array containing
    /// TTCluster objects, and a few methods for writing and reading entries.
    internal static class TT
    {
        internal static TTEntry StaticEntry = new TTEntry();

        internal static UInt32 size = 0;
        internal static UInt32 sizeMask = 0;
        internal static TTEntry[] entries = null;
        internal static byte generation = 0; // Size must be not bigger then TTEntry::generation8

        /// TranspositionTable::refresh() updates the 'generation' value of the TTEntry
        /// to avoid aging. Normally called after a TT hit.

        /// TranspositionTable::set_size() sets the size of the transposition table,
        /// measured in megabytes.
        internal static void set_size(UInt32 mbSize)
        {
            UInt32 newSize = 1024;

            // Transposition table consists of clusters and each cluster consists
            // of ClusterSize number of TTEntries. Each non-empty entry contains
            // information of exactly one position and newSize is the number of
            // clusters we are going to allocate.
            while (2UL * newSize * 64 <= (mbSize << 20))
                newSize *= 2;

            if (newSize == size)
                return;

            size = newSize;
            sizeMask = size - 1;

            entries = new TTEntry[size * 4];

            clear();
        }

        /// TranspositionTable::clear() overwrites the entire transposition table
        /// with zeroes. It is called whenever the table is resized, or when the
        /// user asks the program to clear the table (from the UCI interface).
        internal static void clear()
        {
            if (entries != null)
            {
                Array.Clear(entries, 0, entries.Length);
            }
        }

        /// TranspositionTable::store() writes a new entry containing position key and
        /// valuable information of current position. The lowest order bits of position
        /// key are used to decide on which cluster the position will be placed.
        /// When a new entry is written and there are no empty entries available in cluster,
        /// it replaces the least valuable of entries. A TTEntry t1 is considered to be
        /// more valuable than a TTEntry t2 if t1 is from the current search and t2 is from
        /// a previous search, or if the depth of t1 is bigger than the depth of t2.
        internal static void store(Key posKey, Value v, Bound t, Depth d, Move m, Value statV, Value kingD)
        {
            UInt32 posKey32 = (UInt32)(posKey >> 32); // Use the high 32 bits as key inside the cluster
            UInt32 ttePos = 0;
            UInt32 replacePos = 0;
            ttePos = replacePos = (((UInt32)posKey) & sizeMask) << 2;

            for (int i = 0; i < Constants.ClusterSize; i++)
            {
                if ((entries[ttePos].key == 0) || entries[ttePos].key == posKey32) // Empty or overwrite old
                {
                    // Preserve any existing ttMove
                    if (m == MoveC.MOVE_NONE)
                        m = (Move)entries[ttePos].move16;

                    entries[ttePos].save(posKey32, v, t, d, m, generation, statV, kingD);
                    return;
                }

                // Implement replace strategy
                if ((entries[replacePos].generation8 == generation ? 2 : 0) + (entries[ttePos].generation8 == generation || entries[ttePos].bound == 3/*Bound.BOUND_EXACT*/ ? -2 : 0) + (entries[ttePos].depth16 < entries[replacePos].depth16 ? 1 : 0) > 0)
                {
                    replacePos = ttePos;
                }

                ttePos++;
            }
            entries[replacePos].save(posKey32, v, t, d, m, generation, statV, kingD);
        }

        /// TranspositionTable::probe() looks up the current position in the
        /// transposition table. Returns a pointer to the TTEntry or NULL if
        /// position is not found.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool probe(Key posKey, ref UInt32 ttePos, out TTEntry entry)
        {
            UInt32 posKey32 = (UInt32)(posKey >> 32);
            UInt32 offset = (((UInt32)posKey) & sizeMask) << 2;

            for (UInt32 i = offset; i < (Constants.ClusterSize + offset); i++)
            {
                if (entries[i].key == posKey32)
                {
                    ttePos = i;
                    entry = entries[i];
                    return true;
                }
            }

            entry = StaticEntry;
            return false;
        }

        /// TranspositionTable::new_search() is called at the beginning of every new
        /// search. It increments the "generation" variable, which is used to
        /// distinguish transposition table entries from previous searches from
        /// entries from the current search.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void new_search()
        {
            generation++;
        }
    };
}
