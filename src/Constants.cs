using System;
using System.Net;

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
    internal static class Constants
    {
        // Set to true to force running with one thread. Used for debugging
        internal const bool FakeSplit = false;

        // Material and pawn table sizes
        internal const int MaterialTableSize = 8192;
        internal const int PawnTableSize = 16384;

        // Number of CPUs
        internal const int NumberOfCPUs = 4;

        internal const int BrokerCapacity = 4;

        internal const int MaterialTableMask = MaterialTableSize-1;
        internal const int PawnTableMask = PawnTableSize-1;

        internal const UInt32 UInt32One = 1;

        // This is the number of TTEntry slots for each cluster
        internal const int ClusterSize = 4;

        internal const string endl = "\n";

        internal const int BROKER_SLOTS = 128;
        internal const int BROKER_SLOT_MASK = BROKER_SLOTS - 1;

        internal const int MAX_THREADS = 32;
        internal const int MAX_SPLITPOINTS_PER_THREAD = 8;

        internal const int MAX_MOVES = 192;
        internal const int MAX_PLY = 100;
        internal const int MAX_PLY_PLUS_2 = MAX_PLY + 2;

        internal const int INT_MIN = (-2147483647 - 1); /* minimum (signed) int value */
        internal const int INT_MAX = 2147483647;    /* maximum (signed) int value */

        internal const UInt64 FileABB = 0x0101010101010101UL;
        internal const UInt64 FileBBB = FileABB << 1;
        internal const UInt64 FileCBB = FileABB << 2;
        internal const UInt64 FileDBB = FileABB << 3;
        internal const UInt64 FileEBB = FileABB << 4;
        internal const UInt64 FileFBB = FileABB << 5;
        internal const UInt64 FileGBB = FileABB << 6;
        internal const UInt64 FileHBB = FileABB << 7;

        internal const UInt64 Rank1BB = 0xFF;
        internal const UInt64 Rank2BB = Rank1BB << (8 * 1);
        internal const UInt64 Rank3BB = Rank1BB << (8 * 2);
        internal const UInt64 Rank4BB = Rank1BB << (8 * 3);
        internal const UInt64 Rank5BB = Rank1BB << (8 * 4);
        internal const UInt64 Rank6BB = Rank1BB << (8 * 5);
        internal const UInt64 Rank7BB = Rank1BB << (8 * 6);
        internal const UInt64 Rank8BB = Rank1BB << (8 * 7);

        internal const Value PawnValueMidgame = (Value)(0x0C6);
        internal const Value PawnValueEndgame = (Value)(0x102);
        internal const Value KnightValueMidgame = (Value)(0x331);
        internal const Value KnightValueEndgame = (Value)(0x34E);
        internal const Value BishopValueMidgame = (Value)(0x344);
        internal const Value BishopValueEndgame = (Value)(0x359);
        internal const Value RookValueMidgame = (Value)(0x4F6);
        internal const Value RookValueEndgame = (Value)(0x4FE);
        internal const Value QueenValueMidgame = (Value)(0x9D9);
        internal const Value QueenValueEndgame = (Value)(0x9FE);
    }
}
