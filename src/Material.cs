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
    /// MaterialInfo is a class which contains various information about a
    /// material configuration. It contains a material balance evaluation,
    /// a function pointer to a special endgame evaluation function (which in
    /// most cases is NULL, meaning that the standard evaluation function will
    /// be used), and "scale factors" for black and white.
    ///
    /// The scale factors are used to scale the evaluation score up or down.
    /// For instance, in KRB vs KR endgames, the score is scaled down by a factor
    /// of 4, which will result in scores of absolute value less than one pawn.

    internal sealed class MaterialEntry
    {
        internal Key key;
        internal Int16 value;
        internal byte factorWHITE, factorBLACK;
        internal EndgameValue evaluationFunction;
        internal Color evaluationFunctionColor;
        internal EndgameScaleFactor scalingFunctionWHITE, scalingFunctionBLACK;
        internal int spaceWeight;
        internal Phase gamePhase;

        /// MaterialEntry::scale_factor takes a position and a color as input, and
        /// returns a scale factor for the given color. We have to provide the
        /// position in addition to the color, because the scale factor need not
        /// to be a constant: It can also be a function which should be applied to
        /// the position. For instance, in KBP vs K endgames, a scaling function
        /// which checks for draws with rook pawns and wrong-colored bishops.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal ScaleFactor scale_factor_WHITE(Position pos)
        {
            if (scalingFunctionWHITE == null)
                return factorWHITE;

            ScaleFactor sf = scalingFunctionWHITE(ColorC.WHITE, pos);
            return sf == ScaleFactorC.SCALE_FACTOR_NONE ? factorWHITE : sf;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal ScaleFactor scale_factor_BLACK(Position pos)
        {
            if (scalingFunctionBLACK == null)
                return (factorBLACK);

            ScaleFactor sf = scalingFunctionBLACK(ColorC.BLACK, pos);
            return sf == ScaleFactorC.SCALE_FACTOR_NONE ? factorBLACK : sf;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Value evaluate(Position pos)
        {
            return evaluationFunction(evaluationFunctionColor, pos);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Score material_value()
        {
            return Utils.make_score(value, value);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal int space_weight()
        {
            return spaceWeight;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Phase game_phase()
        {
            return gamePhase;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool specialized_eval_exists()
        {
            return evaluationFunction != null;
        }
    };

    internal sealed class MaterialTable
    {
        private readonly MaterialEntry[] entries = new MaterialEntry[Constants.MaterialTableSize];

        internal MaterialTable()
        {
            pieceCount[0] = new int[6];
            pieceCount[1] = new int[6];
            for (int i = 0; i < Constants.MaterialTableSize; i++)
            {
                entries[i] = new MaterialEntry();
            }
        }

        // Values modified by Joona Kiiski
        internal const Value MidgameLimit = (15581);
        internal const Value EndgameLimit = (3998);

        // Scale factors used when one side has no more pawns
        internal static readonly int[] NoPawnsSF = new int[] { 6, 12, 32, 0 };

        // Polynomial material balance parameters
        internal const Value RedundantQueenPenalty = (320);
        internal const Value RedundantRookPenalty = (554);

        internal static readonly int[] LinearCoefficients = new int[] { 1617, -162, -1172, -190, 105, 26 };

        internal static readonly int[][] QuadraticCoefficientsSameColor = new int[][] {
         new int[]{ 7, 7, 7, 7, 7, 7 }, new int[]{ 39, 2, 7, 7, 7, 7 }, new int[]{ 35, 271, -4, 7, 7, 7 },
         new int[]{ 7, 25, 4, 7, 7, 7 }, new int[]{ -27, -2, 46, 100, 56, 7 }, new int[]{ 58, 29, 83, 148, -3, -25 } };

        internal static readonly int[][] QuadraticCoefficientsOppositeColor = new int[][] {
         new int[]{ 41, 41, 41, 41, 41, 41 }, new int[]{ 37, 41, 41, 41, 41, 41 }, new int[]{ 10, 62, 41, 41, 41, 41 },
         new int[]{ 57, 64, 39, 41, 41, 41 }, new int[]{ 50, 40, 23, -22, 41, 41 }, new int[]{ 106, 101, 3, 151, 171, 41 } };

        private readonly int[][] pieceCount = new int[2][];

        // Helper templates used to detect a given material distribution
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_KXK(Color Us, Position pos)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);
            return pos.non_pawn_material(Them) == ValueC.VALUE_ZERO
                  && pos.piece_count(Them, PieceTypeC.PAWN) == 0
                  && pos.non_pawn_material(Us) >= Constants.RookValueMidgame;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_KBPsKs(Color Us, Position pos)
        {
            return pos.non_pawn_material(Us) == Constants.BishopValueMidgame
                && pos.piece_count(Us, PieceTypeC.BISHOP) == 1
                && pos.piece_count(Us, PieceTypeC.PAWN) >= 1;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_KQKRPs(Color Us, Position pos)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);
            return pos.piece_count(Us, PieceTypeC.PAWN) == 0
                  && pos.non_pawn_material(Us) == Constants.QueenValueMidgame
                  && pos.piece_count(Us, PieceTypeC.QUEEN) == 1
                  && pos.piece_count(Them, PieceTypeC.ROOK) == 1
                  && pos.piece_count(Them, PieceTypeC.PAWN) >= 1;
        }

        /// MaterialTable::imbalance() calculates imbalance comparing piece count of each
        /// piece type for both colors.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        int imbalance(Color Us)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);

            int pt1, pt2, pc, v;
            int value = 0;

            // Redundancy of major pieces, formula based on Kaufman's paper
            // "The Evaluation of Material Imbalances in Chess"
            if (pieceCount[Us][PieceTypeC.ROOK] > 0)
                value -= RedundantRookPenalty * (pieceCount[Us][PieceTypeC.ROOK] - 1)
                        + RedundantQueenPenalty * pieceCount[Us][PieceTypeC.QUEEN];

            // Second-degree polynomial material imbalance by Tord Romstad
            for (pt1 = PieceTypeC.NO_PIECE_TYPE; pt1 <= PieceTypeC.QUEEN; pt1++)
            {
                pc = pieceCount[Us][pt1];
                if (pc == 0)
                    continue;

                v = LinearCoefficients[pt1];

                for (pt2 = PieceTypeC.NO_PIECE_TYPE; pt2 <= pt1; pt2++)
                    v += QuadraticCoefficientsSameColor[pt1][pt2] * pieceCount[Us][pt2]
                        + QuadraticCoefficientsOppositeColor[pt1][pt2] * pieceCount[Them][pt2];

                value += pc * v;
            }
            return value;
        }

        /// MaterialTable::material_info() takes a position object as input,
        /// computes or looks up a MaterialInfo object, and returns a pointer to it.
        /// If the material configuration is not already present in the table, it
        /// is stored there, so we don't have to recompute everything when the
        /// same material configuration occurs again.
        internal void probe(Position pos, out MaterialEntry e)
        {
            Key key = pos.material_key();
            e = entries[((UInt32)key) & Constants.MaterialTableMask];

            // If mi->key matches the position's material hash key, it means that we
            // have analysed this material configuration before, and we can simply
            // return the information we found the last time instead of recomputing it.
            if (e.key == key) return;

            // Initialize MaterialInfo entry
            Value npm = pos.non_pawn_material(ColorC.WHITE) + pos.non_pawn_material(ColorC.BLACK);
            e.value = 0;
            e.scalingFunctionWHITE = null; e.scalingFunctionBLACK = null;
            e.spaceWeight = 0;
            e.key = key;
            e.factorWHITE = e.factorBLACK = ScaleFactorC.SCALE_FACTOR_NORMAL;
            e.gamePhase = npm >= MidgameLimit ? PhaseC.PHASE_MIDGAME
                  : npm <= EndgameLimit ? PhaseC.PHASE_ENDGAME
                  : (((npm - EndgameLimit) * 128) / (MidgameLimit - EndgameLimit));

            // Let's look if we have a specialized evaluation function for this
            // particular material configuration. First we look for a fixed
            // configuration one, then a generic one if previous search failed.
            if ((e.evaluationFunction = Endgame.probeValue(key, out e.evaluationFunctionColor)) != null)
                return;

            if (is_KXK(ColorC.WHITE, pos))
            {
                e.evaluationFunction = Endgame.Endgame_KXK;
                e.evaluationFunctionColor = ColorC.WHITE;
                return;
            }

            if (is_KXK(ColorC.BLACK, pos))
            {
                e.evaluationFunction = Endgame.Endgame_KXK;
                e.evaluationFunctionColor = ColorC.BLACK;
                return;
            }

            if ((pos.pieces_PT(PieceTypeC.PAWN) == 0) && (pos.pieces_PT(PieceTypeC.ROOK) == 0) && (pos.pieces_PT(PieceTypeC.QUEEN) == 0))
            {
                // Minor piece endgame with at least one minor piece per side and
                // no pawns. Note that the case KmmK is already handled by KXK.
                Debug.Assert((pos.pieces_PTC(PieceTypeC.KNIGHT, ColorC.WHITE) | pos.pieces_PTC(PieceTypeC.BISHOP, ColorC.WHITE)) != 0);
                Debug.Assert((pos.pieces_PTC(PieceTypeC.KNIGHT, ColorC.BLACK) | pos.pieces_PTC(PieceTypeC.BISHOP, ColorC.BLACK)) != 0);

                if (pos.piece_count(ColorC.WHITE, PieceTypeC.BISHOP) + pos.piece_count(ColorC.WHITE, PieceTypeC.KNIGHT) <= 2
                    && pos.piece_count(ColorC.BLACK, PieceTypeC.BISHOP) + pos.piece_count(ColorC.BLACK, PieceTypeC.KNIGHT) <= 2)
                {
                    e.evaluationFunction = Endgame.Endgame_KmmKm;
                    e.evaluationFunctionColor = pos.sideToMove;
                    return;
                }
            }

            // OK, we didn't find any special evaluation function for the current
            // material configuration. Is there a suitable scaling function?
            //
            // We face problems when there are several conflicting applicable
            // scaling functions and we need to decide which one to use.
            EndgameScaleFactor sf;
            Color c;
            if ((sf = Endgame.probeScaleFactor(key, out c)) != null)
            {
                if (c == ColorC.WHITE)
                {
                    e.scalingFunctionWHITE = sf;
                }
                else
                {
                    e.scalingFunctionBLACK = sf;
                }
                return;
            }

            // Generic scaling functions that refer to more then one material
            // distribution. Should be probed after the specialized ones.
            // Note that these ones don't return after setting the function.
            if (is_KBPsKs(ColorC.WHITE, pos))
                e.scalingFunctionWHITE = Endgame.Endgame_KBPsK;

            if (is_KBPsKs(ColorC.BLACK, pos))
                e.scalingFunctionBLACK = Endgame.Endgame_KBPsK;

            if (is_KQKRPs(ColorC.WHITE, pos))
                e.scalingFunctionWHITE = Endgame.Endgame_KQKRPs;

            else if (is_KQKRPs(ColorC.BLACK, pos))
                e.scalingFunctionBLACK = Endgame.Endgame_KQKRPs;

            Value npm_w = pos.non_pawn_material(ColorC.WHITE);
            Value npm_b = pos.non_pawn_material(ColorC.BLACK);

            if (npm_w + npm_b == ValueC.VALUE_ZERO)
            {
                if (pos.piece_count(ColorC.BLACK, PieceTypeC.PAWN) == 0)
                {
                    Debug.Assert(pos.piece_count(ColorC.WHITE, PieceTypeC.PAWN) >= 2);
                    e.scalingFunctionWHITE = Endgame.Endgame_KPsK;
                }
                else if (pos.piece_count(ColorC.WHITE, PieceTypeC.PAWN) == 0)
                {
                    Debug.Assert(pos.piece_count(ColorC.BLACK, PieceTypeC.PAWN) >= 2);
                    e.scalingFunctionBLACK = Endgame.Endgame_KPsK;
                }
                else if (pos.piece_count(ColorC.WHITE, PieceTypeC.PAWN) == 1 && pos.piece_count(ColorC.BLACK, PieceTypeC.PAWN) == 1)
                {
                    // This is a special case because we set scaling functions
                    // for both colors instead of only one.
                    e.scalingFunctionWHITE = Endgame.Endgame_KPKP;
                    e.scalingFunctionBLACK = Endgame.Endgame_KPKP;
                }
            }

            // No pawns makes it difficult to win, even with a material advantage
            if (pos.piece_count(ColorC.WHITE, PieceTypeC.PAWN) == 0 && npm_w - npm_b <= Constants.BishopValueMidgame)
            {
                e.factorWHITE = (byte)
                (npm_w == npm_b || npm_w < Constants.RookValueMidgame ? 0 : NoPawnsSF[Math.Min(pos.piece_count(ColorC.WHITE, PieceTypeC.BISHOP), 2)]);
            }

            if (pos.piece_count(ColorC.BLACK, PieceTypeC.PAWN) == 0 && npm_b - npm_w <= Constants.BishopValueMidgame)
            {
                e.factorBLACK = (byte)
                (npm_w == npm_b || npm_b < Constants.RookValueMidgame ? 0 : NoPawnsSF[Math.Min(pos.piece_count(ColorC.BLACK, PieceTypeC.BISHOP), 2)]);
            }

            // Compute the space weight
            if (npm_w + npm_b >= 2 * Constants.QueenValueMidgame + 4 * Constants.RookValueMidgame + 2 * Constants.KnightValueMidgame)
            {
                int minorPieceCount = pos.piece_count(ColorC.WHITE, PieceTypeC.KNIGHT) + pos.piece_count(ColorC.WHITE, PieceTypeC.BISHOP)
                                     + pos.piece_count(ColorC.BLACK, PieceTypeC.KNIGHT) + pos.piece_count(ColorC.BLACK, PieceTypeC.BISHOP);

                e.spaceWeight = minorPieceCount * minorPieceCount;
            }

            // Evaluate the material imbalance. We use PIECE_TYPE_NONE as a place holder
            // for the bishop pair "extended piece", this allow us to be more flexible
            // in defining bishop pair bonuses.
            pieceCount[0][0] = pos.piece_count(ColorC.WHITE, PieceTypeC.BISHOP) > 1 ? 1 : 0;
            pieceCount[0][1] = pos.piece_count(ColorC.WHITE, PieceTypeC.PAWN);
            pieceCount[0][2] = pos.piece_count(ColorC.WHITE, PieceTypeC.KNIGHT);
            pieceCount[0][3] = pos.piece_count(ColorC.WHITE, PieceTypeC.BISHOP);
            pieceCount[0][4] = pos.piece_count(ColorC.WHITE, PieceTypeC.ROOK);
            pieceCount[0][5] = pos.piece_count(ColorC.WHITE, PieceTypeC.QUEEN);

            pieceCount[1][0] = pos.piece_count(ColorC.BLACK, PieceTypeC.BISHOP) > 1 ? 1 : 0;
            pieceCount[1][1] = pos.piece_count(ColorC.BLACK, PieceTypeC.PAWN);
            pieceCount[1][2] = pos.piece_count(ColorC.BLACK, PieceTypeC.KNIGHT);
            pieceCount[1][3] = pos.piece_count(ColorC.BLACK, PieceTypeC.BISHOP);
            pieceCount[1][4] = pos.piece_count(ColorC.BLACK, PieceTypeC.ROOK);
            pieceCount[1][5] = pos.piece_count(ColorC.BLACK, PieceTypeC.QUEEN);

            e.value = (Int16)((imbalance(ColorC.WHITE) - imbalance(ColorC.BLACK)) / 16);

            return;
        }
    }
}
