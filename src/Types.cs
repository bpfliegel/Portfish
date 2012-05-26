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
using TracedType = System.Int32;
using NodeType = System.Int32;
using TimeType = System.Int32;
using System.Runtime.CompilerServices;

namespace Portfish
{
    internal sealed class StateInfoArray
    {
        internal readonly StateInfo[] state = new StateInfo[Constants.MAX_PLY_PLUS_2];

        public StateInfoArray()
        {
            for (int i = 0; i < (Constants.MAX_PLY_PLUS_2); i++) { state[i] = new StateInfo(); }
        }
    }

    internal sealed class MList
    {
        internal readonly MoveStack[] moves = new MoveStack[Constants.MAX_MOVES + 2]; // 2 additional for the killers at the end
        internal int pos = 0;

        public void Recycle()
        {
            pos = 0;
        }
    }

    internal sealed class LoopStack
    {
        internal readonly Stack[] ss = new Stack[Constants.MAX_PLY_PLUS_2];

        public void Recycle()
        {
            Array.Clear(ss, 0, Constants.MAX_PLY_PLUS_2);
        }
    }

    internal sealed class SwapList
    {
        internal readonly int[] list = new int[32];
    }

    internal sealed class MovesSearched
    {
        internal readonly Move[] movesSearched = new Move[Constants.MAX_MOVES];
    }

    // Different node types, used as template parameter
    internal static class NodeTypeC { internal const int Root = 0, PV = 1, NonPV = 2, SplitPointRoot = 3, SplitPointPV = 4, SplitPointNonPV = 5; };

    /// A move needs 16 bits to be stored
    ///
    /// bit  0- 5: destination square (from 0 to 63)
    /// bit  6-11: origin square (from 0 to 63)
    /// bit 12-13: promotion piece type - 2 (from KNIGHT-2 to QUEEN-2)
    /// bit 14-15: special move flag: promotion (1), en passant (2), castle (3)
    ///
    /// Special cases are MOVE_NONE and MOVE_NULL. We can sneak these in because in
    /// any normal move destination square is always different from origin square
    /// while MOVE_NONE and MOVE_NULL have the same origin and destination square.
    /// 
    internal static class MoveC
    {
        internal const int MOVE_NONE = 0;
        internal const int MOVE_NULL = 65;
    };

    internal static class TimeTypeC { internal const int OptimumTime = 0, MaxTime = 1; };

    internal struct MoveStack
    {
        internal Move move;
        internal int score;
    };

    internal static class TracedTypeC
    {
        internal const int PST = 8, IMBALANCE = 9, MOBILITY = 10, THREAT = 11,
        PASSED = 12, UNSTOPPABLE = 13, SPACE = 14, TOTAL = 15;
    };

    // Evaluation weights, initialized from UCI options
    internal static class EvalWeightC
    {
        internal const int Mobility = 0, PassedPawns = 1, Space = 2, KingDangerUs = 3, KingDangerThem = 4;
    }

    /// Game phase
    internal static class PhaseC
    {
        internal const int PHASE_ENDGAME = 0,
        PHASE_MIDGAME = 128;
    };

    internal static class CastleRightC
    {
        internal const int // Defined as in PolyGlot book hash key
            CASTLES_NONE = 0,
            WHITE_OO = 1,
            WHITE_OOO = 2,
            BLACK_OO = 4,
            BLACK_OOO = 8,
            ALL_CASTLES = 15;
    };

    internal static class CastlingSideC
    {
        internal const int KING_SIDE = 0,
        QUEEN_SIDE = 1;
    };

    internal static class ScaleFactorC
    {
        internal const int
        SCALE_FACTOR_DRAW = 0,
        SCALE_FACTOR_NORMAL = 64,
        SCALE_FACTOR_MAX = 128,
        SCALE_FACTOR_NONE = 255;
    };

    internal enum Bound
    {
        BOUND_NONE = 0,
        BOUND_UPPER = 1,
        BOUND_LOWER = 2,
        BOUND_EXACT = BOUND_UPPER | BOUND_LOWER
    };

    internal static class ValueC
    {
        internal const int
            VALUE_ZERO = 0,
            VALUE_DRAW = 0,
            VALUE_KNOWN_WIN = 15000,
            VALUE_MATE = 30000,
            VALUE_INFINITE = 30001,
            VALUE_NONE = 30002,

            VALUE_MATE_IN_MAX_PLY = VALUE_MATE - Constants.MAX_PLY,
            VALUE_MATED_IN_MAX_PLY = -VALUE_MATE + Constants.MAX_PLY,

            VALUE_ENSURE_INTEGER_SIZE_P = Constants.INT_MAX,
            VALUE_ENSURE_INTEGER_SIZE_N = Constants.INT_MIN;
    };

    internal static class PieceTypeC
    {
        internal const int NO_PIECE_TYPE = 0,
        PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6;
    };

    internal static class PieceC
    {
        internal const int NO_PIECE = 16,
        W_PAWN = 1, W_KNIGHT = 2, W_BISHOP = 3, W_ROOK = 4, W_QUEEN = 5, W_KING = 6,
        B_PAWN = 9, B_KNIGHT = 10, B_BISHOP = 11, B_ROOK = 12, B_QUEEN = 13, B_KING = 14;
    };

    internal static class ColorC
    {
        // Constants
        internal const int WHITE = 0, BLACK = 1, NO_COLOR = 2;
    };

    internal static class DepthC
    {
        internal const int
        ONE_PLY = 2,

        DEPTH_ZERO = 0 * ONE_PLY,
        DEPTH_QS_CHECKS = -1 * ONE_PLY,
        DEPTH_QS_NO_CHECKS = -2 * ONE_PLY,
        DEPTH_QS_RECAPTURES = -5 * ONE_PLY,

        DEPTH_NONE = -127 * ONE_PLY;
    };

    internal static class SquareC
    {
        internal const int
            SQ_A1 = 0, SQ_B1 = 1, SQ_C1 = 2, SQ_D1 = 3, SQ_E1 = 4, SQ_F1 = 5, SQ_G1 = 6, SQ_H1 = 7,
            SQ_A2 = 8, SQ_B2 = 9, SQ_C2 = 10, SQ_D2 = 11, SQ_E2 = 12, SQ_F2 = 13, SQ_G2 = 14, SQ_H2 = 15,
            SQ_A3 = 16, SQ_B3 = 17, SQ_C3 = 18, SQ_D3 = 19, SQ_E3 = 20, SQ_F3 = 21, SQ_G3 = 22, SQ_H3 = 23,
            SQ_A4 = 24, SQ_B4 = 25, SQ_C4 = 26, SQ_D4 = 27, SQ_E4 = 28, SQ_F4 = 29, SQ_G4 = 30, SQ_H4 = 31,
            SQ_A5 = 32, SQ_B5 = 33, SQ_C5 = 34, SQ_D5 = 35, SQ_E5 = 36, SQ_F5 = 37, SQ_G5 = 38, SQ_H5 = 39,
            SQ_A6 = 40, SQ_B6 = 41, SQ_C6 = 42, SQ_D6 = 43, SQ_E6 = 44, SQ_F6 = 45, SQ_G6 = 46, SQ_H6 = 47,
            SQ_A7 = 48, SQ_B7 = 49, SQ_C7 = 50, SQ_D7 = 51, SQ_E7 = 52, SQ_F7 = 53, SQ_G7 = 54, SQ_H7 = 55,
            SQ_A8 = 56, SQ_B8 = 57, SQ_C8 = 58, SQ_D8 = 59, SQ_E8 = 60, SQ_F8 = 61, SQ_G8 = 62, SQ_H8 = 63;

        internal const int SQ_NONE = 64;

        internal const int
            DELTA_N = 8, DELTA_E = 1, DELTA_S = -8, DELTA_W = -1;

        internal const int
            DELTA_NN = DELTA_N + DELTA_N,
            DELTA_NE = DELTA_N + DELTA_E,
            DELTA_SE = DELTA_S + DELTA_E,
            DELTA_SS = DELTA_S + DELTA_S,
            DELTA_SW = DELTA_S + DELTA_W,
            DELTA_NW = DELTA_N + DELTA_W;
    };

    internal static class FileC
    {
        internal const int FILE_A = 0, FILE_B = 1, FILE_C = 2, FILE_D = 3, FILE_E = 4, FILE_F = 5, FILE_G = 6, FILE_H = 7;
    };

    internal static class RankC
    {
        internal const int RANK_1 = 0, RANK_2 = 1, RANK_3 = 2, RANK_4 = 3, RANK_5 = 4, RANK_6 = 5, RANK_7 = 6, RANK_8 = 7;
    };

    /// Score enum keeps a midgame and an endgame value in a single integer (enum),
    /// first LSB 16 bits are used to store endgame value, while upper bits are used
    /// for midgame value. Compiler is free to choose the enum type as long as can
    /// keep its data, so ensure Score to be an integer type.
    internal static class ScoreC
    {
        internal const int SCORE_ZERO = 0;
        internal const int SCORE_ENSURE_INTEGER_SIZE_P = Constants.INT_MAX;
        internal const int SCORE_ENSURE_INTEGER_SIZE_N = Constants.INT_MIN;
    };
}
