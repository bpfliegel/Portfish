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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Portfish
{
    internal static class ResultC
    {
        internal const int
        INVALID = 0,
        UNKNOWN = 1,
        DRAW = 2,
        WIN = 4;
    };

    internal struct KPKPosition
    {

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static UInt32 probe_kpk_bitbase(Square wksq, Square wpsq, Square bksq, Color stm)
        {
            int idx = stm + (bksq << 1) + (wksq << 7) + ((wpsq & 7) << 13) + (((wpsq >> 3) - 1) << 15);
            return KPKBitbase[idx / 32] & (Constants.UInt32One << (idx & 31));
        }

        internal static void kpk_bitbase_init()
        {
            Result[] db = new Result[IndexMax];
            KPKPosition pos = new KPKPosition();
            int idx, bit, repeat = 1;

            // Initialize table with known win / draw positions
            for (idx = 0; idx < IndexMax; idx++)
                db[idx] = pos.classify_leaf(idx);

            // Iterate until all positions are classified (30 cycles needed)
            while (repeat != 0)
                for (repeat = idx = 0; idx < IndexMax; idx++)
                    if (db[idx] == ResultC.UNKNOWN && (db[idx] = pos.classify_index(idx, db)) != ResultC.UNKNOWN)
                        repeat = 1;

            // Map 32 position results into one KPKBitbase[] entry
            UInt32 one = 1;
            for (idx = 0; idx < IndexMax / 32; idx++)
                for (bit = 0; bit < 32; bit++)
                    if (db[32 * idx + bit] == ResultC.WIN)
                        KPKBitbase[idx] |= (one << bit);
        }

        private Square wksq, bksq, psq;
        private Color stm;

        // The possible pawns squares are 24, the first 4 files and ranks from 2 to 7
        private const int IndexMax = 2 * 24 * 64 * 64; // stm * wp_sq * wk_sq * bk_sq = 196608

        // Each uint32_t stores results of 32 positions, one per bit
        private static readonly UInt32[] KPKBitbase = new UInt32[IndexMax / 32];

        private Bitboard k_attacks(Color Us) { return Us == ColorC.WHITE ? Utils.StepAttacksBB[PieceC.W_KING][wksq] : Utils.StepAttacksBB[PieceC.B_KING][bksq]; }

        private Bitboard p_attacks() { return Utils.StepAttacksBB[PieceC.W_PAWN][psq]; }

        // A KPK bitbase index is an integer in [0, IndexMax] range
        //
        // Information is mapped in this way
        //
        // bit     0: side to move (WHITE or BLACK)
        // bit  1- 6: black king square (from SQ_A1 to SQ_H8)
        // bit  7-12: white king square (from SQ_A1 to SQ_H8)
        // bit 13-14: white pawn file (from FILE_A to FILE_D)
        // bit 15-17: white pawn rank - 1 (from RANK_2 - 1 to RANK_7 - 1)
        private static int index(Square w, Square b, Square p, Color c)
        {
            Debug.Assert(Utils.file_of(p) <= FileC.FILE_D);
            return c + (b << 1) + (w << 7) + (Utils.file_of(p) << 13) + ((Utils.rank_of(p) - 1) << 15);
        }

        private void decode_index(int idx)
        {
            stm = (idx & 1);
            bksq = ((idx >> 1) & 63);
            wksq = ((idx >> 7) & 63);
            psq = Utils.make_square(((idx >> 13) & 3), ((idx >> 15) + 1));
        }

        private Result classify_leaf(int idx)
        {
            decode_index(idx);

            // Check if two pieces are on the same square or if a king can be captured
            if (wksq == psq || wksq == bksq || bksq == psq
                || (Utils.bit_is_set(k_attacks(ColorC.WHITE),bksq)!=0)
                || (stm == ColorC.WHITE && (Utils.bit_is_set(p_attacks(),bksq)!=0)))
                return ResultC.INVALID;

            // The position is an immediate win if it is white to move and the white
            // pawn can be promoted without getting captured.
            if (Utils.rank_of(psq) == RankC.RANK_7
                && stm == ColorC.WHITE
                && wksq != psq + SquareC.DELTA_N
                && (Utils.square_distance(bksq, psq + SquareC.DELTA_N) > 1
                    || (Utils.bit_is_set(k_attacks(ColorC.WHITE),(psq + SquareC.DELTA_N))!=0) ))
                return ResultC.WIN;

            // Check for known draw positions

            // Case 1: Stalemate
            if (stm == ColorC.BLACK
                &&
                ((k_attacks(ColorC.BLACK) & ~(k_attacks(ColorC.WHITE) | p_attacks())) == 0)
                )
                return ResultC.DRAW;

            // Case 2: King can capture undefended pawn
            if (stm == ColorC.BLACK
                &&
                ((Utils.bit_is_set(k_attacks(ColorC.BLACK), psq) & ~k_attacks(ColorC.WHITE)) != 0)
                )
                return ResultC.DRAW;

            // Case 3: Black king in front of white pawn
            if (bksq == psq + SquareC.DELTA_N
                && Utils.rank_of(psq) < RankC.RANK_7)
                return ResultC.DRAW;

            //  Case 4: White king in front of pawn and black has opposition
            if (stm == ColorC.WHITE
                && wksq == psq + SquareC.DELTA_N
                && bksq == wksq + SquareC.DELTA_N + SquareC.DELTA_N
                && Utils.rank_of(psq) < RankC.RANK_5)
                return ResultC.DRAW;

            // Case 5: Stalemate with rook pawn
            if (bksq == SquareC.SQ_A8
                && Utils.file_of(psq) == FileC.FILE_A)
                return ResultC.DRAW;

            // Case 6: White king trapped on the rook file
            if (Utils.file_of(wksq) == FileC.FILE_A
                && Utils.file_of(psq) == FileC.FILE_A
                && Utils.rank_of(wksq) > Utils.rank_of(psq)
                && bksq == wksq + 2)
                return ResultC.DRAW;

            return ResultC.UNKNOWN;
        }

        private Result classify(Color Us, Result[] db)
        {
            // White to Move: If one move leads to a position classified as RESULT_WIN,
            // the result of the current position is RESULT_WIN. If all moves lead to
            // positions classified as RESULT_DRAW, the current position is classified
            // RESULT_DRAW otherwise the current position is classified as RESULT_UNKNOWN.
            //
            // Black to Move: If one move leads to a position classified as RESULT_DRAW,
            // the result of the current position is RESULT_DRAW. If all moves lead to
            // positions classified as RESULT_WIN, the position is classified RESULT_WIN.
            // Otherwise, the current position is classified as RESULT_UNKNOWN.

            Result r = ResultC.INVALID;
            Bitboard b = k_attacks(Us);

            while (b != 0)
            {
                r |= Us == ColorC.WHITE ? db[index(Utils.pop_1st_bit(ref b), bksq, psq, ColorC.BLACK)]
                                 : db[index(wksq, Utils.pop_1st_bit(ref b), psq, ColorC.WHITE)];

                if (Us == ColorC.WHITE && ((r & ResultC.WIN) != 0))
                    return ResultC.WIN;

                if (Us == ColorC.BLACK && ((r & ResultC.DRAW) != 0))
                    return ResultC.DRAW;
            }

            if (Us == ColorC.WHITE && Utils.rank_of(psq) < RankC.RANK_7)
            {
                Square s = psq + SquareC.DELTA_N;
                r |= db[index(wksq, bksq, s, ColorC.BLACK)]; // Single push

                if (Utils.rank_of(s) == RankC.RANK_3 && s != wksq && s != bksq)
                    r |= db[index(wksq, bksq, s + SquareC.DELTA_N, ColorC.BLACK)]; // Double push

                if ((r & ResultC.WIN) != 0)
                    return ResultC.WIN;
            }

            return ((r & ResultC.UNKNOWN) != 0) ? ResultC.UNKNOWN : Us == ColorC.WHITE ? ResultC.DRAW : ResultC.WIN;
        }

        private Result classify_index(int idx, Result[] db)
        {
            decode_index(idx);
            return stm == ColorC.WHITE ? classify(ColorC.WHITE, db) : classify(ColorC.BLACK, db);
        }
    };
}
