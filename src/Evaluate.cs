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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Portfish
{
    internal sealed class EvalInfo
    {
        // Pointers to material and pawn hash table entries
        internal MaterialEntry mi = null;
        internal PawnEntry pi = null;

        // attackedBy[color][piece type] is a bitboard representing all squares
        // attacked by a given color and piece type, attackedBy[color][0] contains
        // all squares attacked by the given color.
        internal readonly Bitboard[][] attackedBy = new Bitboard[2][]; // 2, 8

        // kingRing[color] is the zone around the king which is considered
        // by the king safety evaluation. This consists of the squares directly
        // adjacent to the king, and the three (or two, for a king on an edge file)
        // squares two ranks in front of the king. For instance, if black's king
        // is on g8, kingRing[BLACK] is a bitboard containing the squares f8, h8,
        // f7, g7, h7, f6, g6 and h6.
        internal readonly Bitboard[] kingRing = new Bitboard[2];

        // kingAttackersCount[color] is the number of pieces of the given color
        // which attack a square in the kingRing of the enemy king.
        internal readonly int[] kingAttackersCount = new int[2];

        // kingAttackersWeight[color] is the sum of the "weight" of the pieces of the
        // given color which attack a square in the kingRing of the enemy king. The
        // weights of the individual piece types are given by the variables
        // QueenAttackWeight, RookAttackWeight, BishopAttackWeight and
        // KnightAttackWeight in evaluate.cpp
        internal readonly int[] kingAttackersWeight = new int[2];

        // kingAdjacentZoneAttacksCount[color] is the number of attacks to squares
        // directly adjacent to the king of the given color. Pieces which attack
        // more than one square are counted multiple times. For instance, if black's
        // king is on g8 and there's a white knight on g5, this knight adds
        // 2 to kingAdjacentZoneAttacksCount[BLACK].
        internal readonly int[] kingAdjacentZoneAttacksCount = new int[2];

        public EvalInfo()
        {
            for (int i = 0; i < 2; i++)
            {
                attackedBy[i] = new Bitboard[8];
            }
        }
    };

    internal static class Evaluate
    {
        // Evaluation grain size, must be a power of 2
        internal const int GrainSize = 8;

        // Evaluation weights, initialized from UCI options
        //enum { Mobility, PassedPawns, Space, KingDangerUs, KingDangerThem };
        internal static readonly Score[] Weights = new Score[6];

        // Internal evaluation weights. These are applied on top of the evaluation
        // weights read from UCI parameters. The purpose is to be able to change
        // the evaluation weights while keeping the default values of the UCI
        // parameters at 100, which looks prettier.
        //
        // Values modified by Joona Kiiski
        internal static readonly Score[] WeightsInternal = {
              Utils.make_score(252, 344), Utils.make_score(216, 266), Utils.make_score(46, 0), Utils.make_score(247, 0), Utils.make_score(259, 0)
          };

        // MobilityBonus[PieceType][attacked] contains mobility bonuses for middle and
        // end game, indexed by piece type and number of attacked squares not occupied
        // by friendly pieces.
        internal static readonly Score[][] MobilityBonus = new Score[][] {
             new Score[]{}, new Score[]{},
             new Score[]{ Utils.make_score(-38,-33), Utils.make_score(-25,-23), Utils.make_score(-12,-13), Utils.make_score( 0, -3), Utils.make_score(12,  7), Utils.make_score(25, 17), // Knights
               Utils.make_score( 31, 22), Utils.make_score( 38, 27), Utils.make_score( 38, 27) },
             new Score[]{ Utils.make_score(-25,-30), Utils.make_score(-11,-16), Utils.make_score(  3, -2), Utils.make_score(17, 12), Utils.make_score(31, 26), Utils.make_score(45, 40), // Bishops
               Utils.make_score( 57, 52), Utils.make_score( 65, 60), Utils.make_score( 71, 65), Utils.make_score(74, 69), Utils.make_score(76, 71), Utils.make_score(78, 73),
               Utils.make_score( 79, 74), Utils.make_score( 80, 75), Utils.make_score( 81, 76), Utils.make_score(81, 76) },
             new Score[]{ Utils.make_score(-20,-36), Utils.make_score(-14,-19), Utils.make_score( -8, -3), Utils.make_score(-2, 13), Utils.make_score( 4, 29), Utils.make_score(10, 46), // Rooks
               Utils.make_score( 14, 62), Utils.make_score( 19, 79), Utils.make_score( 23, 95), Utils.make_score(26,106), Utils.make_score(27,111), Utils.make_score(28,114),
               Utils.make_score( 29,116), Utils.make_score( 30,117), Utils.make_score( 31,118), Utils.make_score(32,118) },
             new Score[]{ Utils.make_score(-10,-18), Utils.make_score( -8,-13), Utils.make_score( -6, -7), Utils.make_score(-3, -2), Utils.make_score(-1,  3), Utils.make_score( 1,  8), // Queens
               Utils.make_score(  3, 13), Utils.make_score(  5, 19), Utils.make_score(  8, 23), Utils.make_score(10, 27), Utils.make_score(12, 32), Utils.make_score(15, 34),
               Utils.make_score( 16, 35), Utils.make_score( 17, 35), Utils.make_score( 18, 35), Utils.make_score(20, 35), Utils.make_score(20, 35), Utils.make_score(20, 35),
               Utils.make_score( 20, 35), Utils.make_score( 20, 35), Utils.make_score( 20, 35), Utils.make_score(20, 35), Utils.make_score(20, 35), Utils.make_score(20, 35),
               Utils.make_score( 20, 35), Utils.make_score( 20, 35), Utils.make_score( 20, 35), Utils.make_score(20, 35), Utils.make_score(20, 35), Utils.make_score(20, 35),
               Utils.make_score( 20, 35), Utils.make_score( 20, 35) }
          };

        // OutpostBonus[PieceType][Square] contains outpost bonuses of knights and
        // bishops, indexed by piece type and square (from white's point of view.
        internal static readonly Value[][] OutpostBonus = new Value[][] {
          new Value[]{
          //  A     B     C     D     E     F     G     H
            0, 0, 0, 0, 0, 0, 0, 0, // Knights
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 4, 8, 8, 4, 0, 0,
            0, 4,17,26,26,17, 4, 0,
            0, 8,26,35,35,26, 8, 0,
            0, 4,17,17,17,17, 4, 0,
          0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
          new Value[]{
            0, 0, 0, 0, 0, 0, 0, 0, // Bishops
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 5, 5, 5, 5, 0, 0,
            0, 5,10,10,10,10, 5, 0,
            0,10,21,21,21,21,10, 0,
            0, 5, 8, 8, 8, 8, 5, 0,
          0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}
          };

        // ThreatBonus[attacking][attacked] contains threat bonuses according to
        // which piece type attacks which one.
        internal static readonly Score[][] ThreatBonus = new Score[][] {
            new Score[]{}, new Score[]{},
            new Score[]{ Utils.make_score(0, 0), Utils.make_score( 7, 39), Utils.make_score( 0,  0), Utils.make_score(24, 49), Utils.make_score(41,100), Utils.make_score(41,100) }, // KNIGHT
            new Score[]{ Utils.make_score(0, 0), Utils.make_score( 7, 39), Utils.make_score(24, 49), Utils.make_score( 0,  0), Utils.make_score(41,100), Utils.make_score(41,100) }, // BISHOP
            new Score[]{ Utils.make_score(0, 0), Utils.make_score(-1, 29), Utils.make_score(15, 49), Utils.make_score(15, 49), Utils.make_score( 0,  0), Utils.make_score(24, 49) }, // ROOK
            new Score[]{ Utils.make_score(0, 0), Utils.make_score(15, 39), Utils.make_score(15, 39), Utils.make_score(15, 39), Utils.make_score(15, 39), Utils.make_score( 0,  0) }  // QUEEN
          };

        // ThreatenedByPawnPenalty[PieceType] contains a penalty according to which
        // piece type is attacked by an enemy pawn.
        internal static readonly Score[] ThreatenedByPawnPenalty = new Score[] {
            Utils.make_score(0, 0), Utils.make_score(0, 0), Utils.make_score(56, 70), Utils.make_score(56, 70), Utils.make_score(76, 99), Utils.make_score(86, 118)
          };

        // Bonus for having the side to move (modified by Joona Kiiski)
        internal const Score Tempo = 1572875; // Utils.make_score(24, 11);

        // Rooks and queens on the 7th rank (modified by Joona Kiiski)
        internal const Score RookOn7thBonus = 3080290; // Utils.make_score(47, 98);
        internal const Score QueenOn7thBonus = 1769526; // Utils.make_score(27, 54);

        // Rooks on open files (modified by Joona Kiiski)
        internal const Score RookOpenFileBonus = 2818069; // Utils.make_score(43, 21);
        internal const Score RookHalfOpenFileBonus = 1245194; // Utils.make_score(19, 10);

        // Penalty for rooks trapped inside a friendly king which has lost the right to castle.
        internal const Value TrappedRookPenalty = (180);

        // Penalty for a bishop on a1/h1 (a8/h8 for black) which is trapped by
        // a friendly pawn on b2/g2 (b7/g7 for black). This can obviously only
        // happen in Chess960 games.
        internal const Score TrappedBishopA1H1Penalty = 6553700; // Utils.make_score(100, 100);

        // Penalty for an undefended bishop or knight
        internal const Score UndefendedMinorPenalty = 1638410; // Utils.make_score(25, 10);

        // The SpaceMask[Color] contains the area of the board which is considered
        // by the space evaluation. In the middle game, each side is given a bonus
        // based on how many squares inside this area are safe and available for
        // friendly minor pieces.
        internal static readonly Bitboard[] SpaceMask = new Bitboard[] {
            (1UL << SquareC.SQ_C2) | (1UL << SquareC.SQ_D2) | (1UL << SquareC.SQ_E2) | (1UL << SquareC.SQ_F2) |
            (1UL << SquareC.SQ_C3) | (1UL << SquareC.SQ_D3) | (1UL << SquareC.SQ_E3) | (1UL << SquareC.SQ_F3) |
            (1UL << SquareC.SQ_C4) | (1UL << SquareC.SQ_D4) | (1UL << SquareC.SQ_E4) | (1UL << SquareC.SQ_F4),
            (1UL << SquareC.SQ_C7) | (1UL << SquareC.SQ_D7) | (1UL << SquareC.SQ_E7) | (1UL << SquareC.SQ_F7) |
            (1UL << SquareC.SQ_C6) | (1UL << SquareC.SQ_D6) | (1UL << SquareC.SQ_E6) | (1UL << SquareC.SQ_F6) |
            (1UL << SquareC.SQ_C5) | (1UL << SquareC.SQ_D5) | (1UL << SquareC.SQ_E5) | (1UL << SquareC.SQ_F5)
          };

        // King danger constants and variables. The king danger scores are taken
        // from the KingDangerTable[]. Various little "meta-bonuses" measuring
        // the strength of the enemy attack are added up into an integer, which
        // is used as an index to KingDangerTable[].
        //
        // KingAttackWeights[PieceType] contains king attack weights by piece type
        internal static readonly int[] KingAttackWeights = new int[] { 0, 0, 2, 2, 3, 5 };

        // Bonuses for enemy's safe checks
        internal const int QueenContactCheckBonus = 6;
        internal const int RookContactCheckBonus = 4;
        internal const int QueenCheckBonus = 3;
        internal const int RookCheckBonus = 2;
        internal const int BishopCheckBonus = 1;
        internal const int KnightCheckBonus = 1;

        // InitKingDanger[Square] contains penalties based on the position of the
        // defending king, indexed by king's square (from white's point of view).
        internal static readonly int[] InitKingDanger = new int[] {
             2,  0,  2,  5,  5,  2,  0,  2,
             2,  2,  4,  8,  8,  4,  2,  2,
             7, 10, 12, 12, 12, 12, 10,  7,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15
          };

        // KingDangerTable[Color][attackUnits] contains the actual king danger
        // weighted scores, indexed by color and by a calculated integer number.
        internal static readonly Score[][] KingDangerTable = new Score[2][]; // 2, 128

        // TracedTerms[Color][PieceType || TracedType] contains a breakdown of the
        // evaluation terms, used when tracing.
        internal static readonly Score[][] TracedScores = new Score[2][]; // 2, 16
        internal static StringBuilder TraceStream = new StringBuilder();

        private const Value MaxSlope = (30);
        private const Value Peak = (1280);

        internal static Color RootColor;

        internal static void init()
        {
            // Alloc arrays
            TracedScores[0] = new Score[16];
            TracedScores[1] = new Score[16];
            KingDangerTable[0] = new Score[128];
            KingDangerTable[1] = new Score[128];

            // King safety is asymmetrical. Our king danger level is weighted by
            // "Cowardice" UCI parameter, instead the opponent one by "Aggressiveness".
            // If running in analysis mode, make sure we use symmetrical king safety. We
            // do this by replacing both Weights[kingDangerUs] and Weights[kingDangerThem]
            // by their average.
            Weights[EvalWeightC.Mobility] = weight_option("Mobility (Middle Game)", "Mobility (Endgame)", WeightsInternal[EvalWeightC.Mobility]);
            Weights[EvalWeightC.PassedPawns] = weight_option("Passed Pawns (Middle Game)", "Passed Pawns (Endgame)", WeightsInternal[EvalWeightC.PassedPawns]);
            Weights[EvalWeightC.Space] = weight_option("Space", "Space", WeightsInternal[EvalWeightC.Space]);
            Weights[EvalWeightC.KingDangerUs] = weight_option("Cowardice", "Cowardice", WeightsInternal[EvalWeightC.KingDangerUs]);
            Weights[EvalWeightC.KingDangerThem] = weight_option("Aggressiveness", "Aggressiveness", WeightsInternal[EvalWeightC.KingDangerThem]);

            // If running in analysis mode, make sure we use symmetrical king safety. We do this
            // by replacing both Weights[kingDangerUs] and Weights[kingDangerThem] by their average.
            if (bool.Parse(OptionMap.Instance["UCI_AnalyseMode"].v))
            {
                Weights[EvalWeightC.KingDangerUs] = Weights[EvalWeightC.KingDangerThem] = (Weights[EvalWeightC.KingDangerUs] + Weights[EvalWeightC.KingDangerThem]) / 2;
            }

            for (int t = 0, i = 1; i < 100; i++)
            {
                t = Math.Min(Peak, Math.Min((int)(0.4 * i * i), t + MaxSlope));

                KingDangerTable[1][i] = Utils.apply_weight(Utils.make_score(t, 0), Weights[EvalWeightC.KingDangerUs]);
                KingDangerTable[0][i] = Utils.apply_weight(Utils.make_score(t, 0), Weights[EvalWeightC.KingDangerThem]);
            }
        }

        /// trace() is like evaluate() but instead of a value returns a string suitable
        /// to be print on stdout with the detailed descriptions and values of each
        /// evaluation term. Used mainly for debugging.
        internal static string trace(Position pos)
        {
            Value margin = 0;
            string totals;

            RootColor = pos.sideToMove;

            TraceStream.Remove(0, TraceStream.Length);
            Array.Clear(TracedScores[0], 0, 16);
            Array.Clear(TracedScores[1], 0, 16);

            do_evaluate(true, pos, ref margin);

            totals = TraceStream.ToString();
            TraceStream.Remove(0, TraceStream.Length);

            TraceStream.Append("Eval term ".PadLeft(21, ' '));
            TraceStream.Append("|    White    |    Black    |     Total     \n");
            TraceStream.Append("                     |   MG    EG  |   MG    EG  |   MG     EG   \n");
            TraceStream.Append("---------------------+-------------+-------------+---------------\n");

            trace_row("Material, PST, Tempo", TracedTypeC.PST);
            trace_row("Material imbalance", TracedTypeC.IMBALANCE);
            trace_row("Pawns", PieceTypeC.PAWN);
            trace_row("Knights", PieceTypeC.KNIGHT);
            trace_row("Bishops", PieceTypeC.BISHOP);
            trace_row("Rooks", PieceTypeC.ROOK);
            trace_row("Queens", PieceTypeC.QUEEN);
            trace_row("Mobility", TracedTypeC.MOBILITY);
            trace_row("King safety", PieceTypeC.KING);
            trace_row("Threats", TracedTypeC.THREAT);
            trace_row("Passed pawns", TracedTypeC.PASSED);
            trace_row("Unstoppable pawns", TracedTypeC.UNSTOPPABLE);
            trace_row("Space", TracedTypeC.SPACE);

            TraceStream.Append("---------------------+-------------+-------------+---------------\n");
            trace_row("Total", TracedTypeC.TOTAL);
            TraceStream.Append(totals);

            return TraceStream.ToString();
        }

        /// evaluate() is the main evaluation function. It always computes two
        /// values, an endgame score and a middle game score, and interpolates
        /// between them based on the remaining material.
        internal static Value do_evaluate(bool Trace, Position pos, ref Value margin)
        {
            Debug.Assert(!pos.in_check());

            EvalInfo ei = EvalInfoBroker.GetObject();

            Value marginsWHITE, marginsBLACK;
            Score score = 0, mobilityWhite = 0, mobilityBlack = 0;

            // margins[] store the uncertainty estimation of position's evaluation
            // that typically is used by the search for pruning decisions.
            marginsWHITE = marginsBLACK = ValueC.VALUE_ZERO;

            // Initialize score by reading the incrementally updated scores included
            // in the position object (material + piece square tables) and adding
            // Tempo bonus. Score is computed from the point of view of white.
            score = pos.st.psqScore + (pos.sideToMove == ColorC.WHITE ? Tempo : -Tempo);

            // Probe the material hash table
            pos.this_thread().materialTable.probe(pos, out ei.mi);
            score += ((ei.mi.value << 16) + ei.mi.value);

            // If we have a specialized evaluation function for the current material
            // configuration, call it and return.
            if (ei.mi.evaluationFunction != null)
            {
                margin = ValueC.VALUE_ZERO;
                Value retval = ei.mi.evaluationFunction(ei.mi.evaluationFunctionColor, pos);
                ei.pi = null; ei.mi = null;
                EvalInfoBroker.Free();
                return retval;
            }

            // Probe the pawn hash table
            pos.this_thread().pawnTable.probe(pos, out ei.pi);
            score += ei.pi.value;

            // Initialize attack and king safety bitboards
            init_eval_info(ColorC.WHITE, pos, ei);
            init_eval_info(ColorC.BLACK, pos, ei);

            // Evaluate pieces and mobility
            score += evaluate_pieces_of_color(ColorC.WHITE, Trace, pos, ei, ref mobilityWhite)
                    - evaluate_pieces_of_color(ColorC.BLACK, Trace, pos, ei, ref mobilityBlack);

            score += (
                ((((int)(((((mobilityWhite - mobilityBlack) + 32768) & ~0xffff) / 0x10000)) * (((Weights[EvalWeightC.Mobility] + 32768) & ~0xffff) / 0x10000)) / 0x100) << 16) +
                (((int)(((Int16)((mobilityWhite - mobilityBlack) & 0xffff))) * ((Int16)(Weights[EvalWeightC.Mobility] & 0xffff))) / 0x100)
            );

            // Evaluate kings after all other pieces because we need complete attack
            // information when computing the king safety evaluation.
            score += evaluate_king(ColorC.WHITE, Trace, pos, ei, ref marginsWHITE, ref marginsBLACK) - evaluate_king(ColorC.BLACK, Trace, pos, ei, ref marginsWHITE, ref marginsBLACK);

            // Evaluate tactical threats, we need full attack information including king
            score += evaluate_threats(ColorC.WHITE, pos, ei) - evaluate_threats(ColorC.BLACK, pos, ei);

            // Evaluate passed pawns, we need full attack information including king
            score += evaluate_passed_pawns(ColorC.WHITE, pos, ei) - evaluate_passed_pawns(ColorC.BLACK, pos, ei);

            // If one side has only a king, check whether exists any unstoppable passed pawn
            if ((pos.st.npMaterialWHITE==0) || (pos.st.npMaterialBLACK==0))
            {
                score += evaluate_unstoppable_pawns(pos, ei);
            }

            // Evaluate space for both sides, only in middle-game.
            if (ei.mi.spaceWeight != 0)
            {
                int s = evaluate_space(ColorC.WHITE, pos, ei) - evaluate_space(ColorC.BLACK, pos, ei);
                score += (((((int)((((((s * ei.mi.spaceWeight) << 16) + 32768) & ~0xffff) / 0x10000)) * (((Weights[EvalWeightC.Space] + 32768) & ~0xffff) / 0x10000)) / 0x100) << 16) 
                    + (((int)(((Int16)(((s * ei.mi.spaceWeight) << 16) & 0xffff))) * ((Int16)(Weights[EvalWeightC.Space] & 0xffff))) / 0x100));
            }

            // Scale winning side if position is more drawish that what it appears
            ScaleFactor sf = ((Int16)(score & 0xffff)) > ValueC.VALUE_DRAW ? ei.mi.scale_factor_WHITE(pos) : ei.mi.scale_factor_BLACK(pos);

            // If we don't already have an unusual scale factor, check for opposite
            // colored bishop endgames, and use a lower scale for those.
            if (ei.mi.gamePhase < PhaseC.PHASE_MIDGAME
                && 
                (
                    pos.pieceCount[ColorC.WHITE][PieceTypeC.BISHOP] == 1
                    && 
                    pos.pieceCount[ColorC.BLACK][PieceTypeC.BISHOP] == 1
                    && 
                    (((((pos.pieceList[ColorC.WHITE][PieceTypeC.BISHOP][0] ^ pos.pieceList[ColorC.BLACK][PieceTypeC.BISHOP][0]) >> 3) ^ (pos.pieceList[ColorC.WHITE][PieceTypeC.BISHOP][0] ^ pos.pieceList[ColorC.BLACK][PieceTypeC.BISHOP][0])) & 1) != 0)
                )
                && sf == ScaleFactorC.SCALE_FACTOR_NORMAL)
            {
                // Only the two bishops ?
                if (pos.st.npMaterialWHITE == Constants.BishopValueMidgame && pos.st.npMaterialBLACK == Constants.BishopValueMidgame)
                {
                    // Check for KBP vs KB with only a single pawn that is almost
                    // certainly a draw or at least two pawns.
                    sf = (pos.pieceCount[ColorC.WHITE][PieceTypeC.PAWN] + pos.pieceCount[ColorC.BLACK][PieceTypeC.PAWN] == 1) ? (8) : (32);
                }
                else
                    // Endgame with opposite-colored bishops, but also other pieces. Still
                    // a bit drawish, but not as drawish as with only the two bishops.
                    sf = (50);
            }

            // Interpolate between the middle game and the endgame score
            margin = pos.sideToMove == ColorC.WHITE ? marginsWHITE : marginsBLACK;

            // interpolate
            int ev = (((Int16)(score & 0xffff)) * (int)(sf)) / ScaleFactorC.SCALE_FACTOR_NORMAL;
            int result = ((((score + 32768) & ~0xffff) / 0x10000) * (int)(ei.mi.gamePhase) + ev * (int)(128 - ei.mi.gamePhase)) / 128;
            Value v = ((result + GrainSize / 2) & ~(GrainSize - 1));

            // In case of tracing add all single evaluation contributions for both white and black
            if (Trace)
            {
                trace_add(TracedTypeC.PST, pos.psq_score());
                trace_add(TracedTypeC.IMBALANCE, ei.mi.material_value());
                trace_add(PieceTypeC.PAWN, ei.pi.pawns_value());
                trace_add(TracedTypeC.MOBILITY, Utils.apply_weight(mobilityWhite, Weights[EvalWeightC.Mobility]), Utils.apply_weight(mobilityBlack, Weights[EvalWeightC.Mobility]));
                trace_add(TracedTypeC.THREAT, evaluate_threats(ColorC.WHITE, pos, ei), evaluate_threats(ColorC.BLACK, pos, ei));
                trace_add(TracedTypeC.PASSED, evaluate_passed_pawns(ColorC.WHITE, pos, ei), evaluate_passed_pawns(ColorC.BLACK, pos, ei));
                trace_add(TracedTypeC.UNSTOPPABLE, evaluate_unstoppable_pawns(pos, ei));
                Score w = Utils.make_score(ei.mi.space_weight() * evaluate_space(ColorC.WHITE, pos, ei), 0);
                Score b = Utils.make_score(ei.mi.space_weight() * evaluate_space(ColorC.BLACK, pos, ei), 0);
                trace_add(TracedTypeC.SPACE, Utils.apply_weight(w, Weights[EvalWeightC.Space]), Utils.apply_weight(b, Weights[EvalWeightC.Space]));
                trace_add(TracedTypeC.TOTAL, score);

                TraceStream.Append("\nUncertainty margin: White: ");
                TraceStream.Append(FormatDouble(to_cp(marginsWHITE), null, true));
                TraceStream.Append(", Black: ");
                TraceStream.Append(FormatDouble(to_cp(marginsBLACK), null, true));
                TraceStream.Append("\nScaling: ");
                TraceStream.Append(FormatDouble((100.0 * ei.mi.game_phase() / 128.0), 6, false));
                TraceStream.Append("% MG, ");
                TraceStream.Append(FormatDouble((100.0 * (1.0 - ei.mi.game_phase() / 128.0)), 6, false));
                TraceStream.Append("% * ");
                TraceStream.Append(FormatDouble(((100.0 * sf) / ScaleFactorC.SCALE_FACTOR_NORMAL), 6, false));
                TraceStream.Append("% EG.\n");
                TraceStream.Append("Total evaluation: ");
                TraceStream.Append(FormatDouble(to_cp(v), null, false));
            }

            ei.pi = null; ei.mi = null;
            EvalInfoBroker.Free();

            return pos.sideToMove == ColorC.WHITE ? v : -v;
        }

        // init_eval_info() initializes king bitboards for given color adding
        // pawn attacks. To be done at the beginning of the evaluation.
        static void init_eval_info(Color Us, Position pos, EvalInfo ei)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);

            Bitboard b = ei.attackedBy[Them][PieceTypeC.KING] = Utils.StepAttacksBB_KING[pos.pieceList[Them][PieceTypeC.KING][0]];
            ei.attackedBy[Us][PieceTypeC.PAWN] = (Us == ColorC.WHITE) ? ei.pi.pawnAttacksWHITE : ei.pi.pawnAttacksBLACK;

            // Init king safety tables only if we are going to use them
            if ((pos.pieceCount[Us][PieceTypeC.QUEEN] != 0)
                && (Us == 0 ? pos.st.npMaterialWHITE : pos.st.npMaterialBLACK) >= Constants.QueenValueMidgame + Constants.RookValueMidgame)
            {
                ei.kingRing[Them] = (b | (Us == ColorC.WHITE ? b >> 8 : b << 8));
                b &= ei.attackedBy[Us][PieceTypeC.PAWN];
                ei.kingAttackersCount[Us] = (b != 0) ? Bitcount.popcount_1s_Max15(b) / 2 : 0;
                ei.kingAdjacentZoneAttacksCount[Us] = ei.kingAttackersWeight[Us] = 0;
            }
            else
            {
                ei.kingRing[Them] = 0; ei.kingAttackersCount[Us] = 0;
            }
        }

        // evaluate_threats<>() assigns bonuses according to the type of attacking piece
        // and the type of attacked one.
        static Score evaluate_threats(Color Us, Position pos, EvalInfo ei)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);

            Bitboard b, undefendedMinors, weakEnemies;
            Score score = ScoreC.SCORE_ZERO;

            // Undefended minors get penalized even if not under attack
            undefendedMinors = pos.byColorBB[Them]
                                 & (pos.byTypeBB[PieceTypeC.BISHOP] | pos.byTypeBB[PieceTypeC.KNIGHT])
                                 & ~ei.attackedBy[Them][0];

            if (undefendedMinors != 0)
                score += ((undefendedMinors & (undefendedMinors - 1)) != 0) ? UndefendedMinorPenalty * 2 : UndefendedMinorPenalty;

            // Enemy pieces not defended by a pawn and under our attack
            weakEnemies = pos.byColorBB[Them]
                                  & ~ei.attackedBy[Them][PieceTypeC.PAWN]
                                  & ei.attackedBy[Us][0];

            if (weakEnemies == 0)
                return score;

            // Add bonus according to type of attacked enemy piece and to the
            // type of attacking piece, from knights to queens. Kings are not
            // considered because are already handled in king evaluation.
            for (PieceType pt1 = PieceTypeC.KNIGHT; pt1 < PieceTypeC.KING; pt1++)
            {
                b = ei.attackedBy[Us][pt1] & weakEnemies;
                if (b != 0)
                    for (PieceType pt2 = PieceTypeC.PAWN; pt2 < PieceTypeC.KING; pt2++)
                        if ((b & pos.byTypeBB[pt2]) != 0)
                            score += ThreatBonus[pt1][pt2];
            }

            return score;
        }

        // evaluate_pieces_of_color<>() assigns bonuses and penalties to all the
        // pieces of a given color.
        static Score evaluate_pieces_of_color(Color Us, bool Trace, Position pos, EvalInfo ei, ref Score mobility)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);
            mobility = ScoreC.SCORE_ZERO;

            // Do not include in mobility squares protected by enemy pawns or occupied by our pieces
            Bitboard mobilityArea = ~(ei.attackedBy[Them][PieceTypeC.PAWN] | pos.byColorBB[Us]);

            #region Evaluate pieces

            Bitboard b = 0;
            int plPos = 0;
            Square s, ksq;
            int mob;
            File f;
            Score score, scores = ScoreC.SCORE_ZERO;

            Bitboard attackedByThemKing = ei.attackedBy[Them][PieceTypeC.KING];
            Bitboard attackedByThemPawn = ei.attackedBy[Them][PieceTypeC.PAWN];
            Bitboard kingRingThem = ei.kingRing[Them];

            for (PieceType Piece = PieceTypeC.KNIGHT; Piece < PieceTypeC.KING; Piece++)
            {
                score = ScoreC.SCORE_ZERO;
                ei.attackedBy[Us][Piece] = 0;
                Square[] pl = pos.pieceList[Us][Piece];
                plPos = 0;
                while ((s = pl[plPos++]) != SquareC.SQ_NONE)
                {
                    // Find attacked squares, including x-ray attacks for bishops and rooks
                    if (Piece == PieceTypeC.KNIGHT)
                    {
                        b = Utils.StepAttacksBB_KNIGHT[s];
                    }
                    else if (Piece == PieceTypeC.QUEEN)
                    {
#if X64
                        b = Utils.BAttacks[s][(((pos.occupied_squares & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])] | Utils.RAttacks[s][(((pos.occupied_squares & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])];
#else
                        b = Utils.bishop_attacks_bb(s, pos.occupied_squares) | Utils.rook_attacks_bb(s, pos.occupied_squares);
#endif
                    }
                    else if (Piece == PieceTypeC.BISHOP)
                    {
#if X64
                        b = Utils.BAttacks[s][(((
                              (pos.occupied_squares ^ (pos.byTypeBB[PieceTypeC.QUEEN] & pos.byColorBB[Us]))
                            & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])];
#else
                        b = Utils.bishop_attacks_bb(s, pos.occupied_squares ^ pos.pieces_PTC(PieceTypeC.QUEEN, Us));
#endif
                    }
                    else if (Piece == PieceTypeC.ROOK)
                    {
#if X64
                        b = Utils.RAttacks[s][(((
                              (pos.occupied_squares ^ ((pos.byTypeBB[PieceTypeC.ROOK] | pos.byTypeBB[PieceTypeC.QUEEN]) & pos.byColorBB[Us]))
                            & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])];
#else
                        b = Utils.rook_attacks_bb(s, pos.occupied_squares ^ pos.pieces(PieceTypeC.ROOK, PieceTypeC.QUEEN, Us));
#endif
                    }

                    // Update attack info
                    ei.attackedBy[Us][Piece] |= b;

                    // King attacks
                    if ((b & kingRingThem) != 0)
                    {
                        ei.kingAttackersCount[Us]++;
                        ei.kingAttackersWeight[Us] += KingAttackWeights[Piece];
                        Bitboard bb = (b & attackedByThemKing);//ei.attackedBy[Them][PieceTypeC.KING]);
                        if (bb != 0)
                        {
#if X64
                            bb -= (bb >> 1) & 0x5555555555555555UL;
                            bb = ((bb >> 2) & 0x3333333333333333UL) + (bb & 0x3333333333333333UL);
                            ei.kingAdjacentZoneAttacksCount[Us] += (int)((bb * 0x1111111111111111UL) >> 60);
#else
                            ei.kingAdjacentZoneAttacksCount[Us] += Bitcount.popcount_1s_Max15(bb);
#endif
                        }
                    }

                    // Mobility
#if X64
                    Bitboard bmob = b & mobilityArea;
                    if (Piece != PieceTypeC.QUEEN)
                    {
                        bmob -= (bmob >> 1) & 0x5555555555555555UL;
                        bmob = ((bmob >> 2) & 0x3333333333333333UL) + (bmob & 0x3333333333333333UL);
                        mob = (int)((bmob * 0x1111111111111111UL) >> 60);
                    }
                    else
                    {
                        bmob -= ((bmob >> 1) & 0x5555555555555555UL);
                        bmob = ((bmob >> 2) & 0x3333333333333333UL) + (bmob & 0x3333333333333333UL);
                        bmob = ((bmob >> 4) + bmob) & 0x0F0F0F0F0F0F0F0FUL;
                        mob = (int)((bmob * 0x0101010101010101UL) >> 56);
                    }
#else
                    mob = (Piece != PieceTypeC.QUEEN ? Bitcount.popcount_1s_Max15(b & mobilityArea) : Bitcount.popcount_1s_Full(b & mobilityArea));
#endif
                    mobility += MobilityBonus[Piece][mob];

                    // Add a bonus if a slider is pinning an enemy piece
                    if (
                        (Piece == PieceTypeC.BISHOP || Piece == PieceTypeC.ROOK || Piece == PieceTypeC.QUEEN)
                        &&
                        ((Utils.PseudoAttacks[Piece][pos.pieceList[Them][PieceTypeC.KING][0]] & Utils.SquareBB[s]) != 0)
                        )
                    {
                        b = Utils.BetweenBB[s][pos.pieceList[Them][PieceTypeC.KING][0]] & pos.occupied_squares;

                        Debug.Assert(b != 0);

                        if (((b & (b - 1)) == 0) && ((b & pos.byColorBB[Them]) != 0))
                        {
#if X64
                            score += ThreatBonus[Piece][pos.board[Utils.BSFTable[((b & (0xffffffffffffffff - b + 1)) * 0x218A392CD3D5DBFUL) >> 58]] & 7];
#else
                            score += ThreatBonus[Piece][pos.board[Utils.first_1(b)] & 7];
#endif
                        }
                    }

                    // Decrease score if we are attacked by an enemy pawn. Remaining part
                    // of threat evaluation must be done later when we have full attack info.
                    if ((attackedByThemPawn & Utils.SquareBB[s]) != 0)
                    {
                        score -= ThreatenedByPawnPenalty[Piece];
                    }

                    // Bishop and knight outposts squares
                    if (
                         (Piece == PieceTypeC.BISHOP || Piece == PieceTypeC.KNIGHT)
                         &&
                         (((pos.byTypeBB[PieceTypeC.PAWN] & pos.byColorBB[Them]) & Utils.AttackSpanMask[Us][s]) == 0)
                         )
                    {
                        #region Evaluate outposts inlined

                        // evaluate_outposts() evaluates bishop and knight outposts squares

                        // Initial bonus based on square
                        Value bonus = OutpostBonus[Piece == PieceTypeC.BISHOP ? 1 : 0][s ^ (Us * 56)];

                        // Increase bonus if supported by pawn, especially if the opponent has
                        // no minor piece which can exchange the outpost piece.
                        if ((bonus != 0) && ((ei.attackedBy[Us][PieceTypeC.PAWN] & Utils.SquareBB[s]) != 0))
                        {
                            if (
                                ((pos.byTypeBB[PieceTypeC.KNIGHT] & pos.byColorBB[Them]) == 0)
                                &&
                                (((((0xAA55AA55AA55AA55UL & Utils.SquareBB[s]) != 0) ? 0xAA55AA55AA55AA55UL : ~0xAA55AA55AA55AA55UL) & (pos.byTypeBB[PieceTypeC.BISHOP] & pos.byColorBB[Them])) == 0)
                                )
                            {
                                bonus += bonus;
                            }
                            else
                            {
                                bonus += bonus / 2;
                            }
                        }
                        score += ((bonus << 16) + bonus); // Utils.make_score(bonus, bonus);

                        #endregion
                    }

                    // Queen or rook on 7th rank
                    if (
                        (Piece == PieceTypeC.ROOK || Piece == PieceTypeC.QUEEN)
                        &&
                        ((s >> 3) ^ (Us * 7)) == RankC.RANK_7
                        &&
                        (((pos.pieceList[Them][PieceTypeC.KING][0]) >> 3) ^ (Us * 7)) == RankC.RANK_8
                    )
                    {
                        score += (Piece == PieceTypeC.ROOK ? RookOn7thBonus : QueenOn7thBonus);
                    }


                    // Special extra evaluation for bishops
                    if (pos.chess960 && (Piece == PieceTypeC.BISHOP))
                    {
                        // An important Chess960 pattern: A cornered bishop blocked by
                        // a friendly pawn diagonally in front of it is a very serious
                        // problem, especially when that pawn is also blocked.
                        if (s == Utils.relative_square(Us, SquareC.SQ_A1) || s == Utils.relative_square(Us, SquareC.SQ_H1))
                        {
                            Square d = Utils.pawn_push(Us) + (Utils.file_of(s) == FileC.FILE_A ? SquareC.DELTA_E : SquareC.DELTA_W);
                            if (pos.piece_on(s + d) == Utils.make_piece(Us, PieceTypeC.PAWN))
                            {
                                if (!pos.is_empty(s + d + Utils.pawn_push(Us)))
                                    score -= 2 * TrappedBishopA1H1Penalty;
                                else if (pos.piece_on(s + 2 * d) == Utils.make_piece(Us, PieceTypeC.PAWN))
                                    score -= TrappedBishopA1H1Penalty;
                                else
                                    score -= TrappedBishopA1H1Penalty / 2;
                            }
                        }
                    }

                    // Special extra evaluation for rooks
                    if (Piece == PieceTypeC.ROOK)
                    {
                        // Open and half-open files
                        f = (s & 7);

                        bool halfOpenUs = ((Us == ColorC.WHITE) ? (ei.pi.halfOpenFilesWHITE & (1 << f)) : (ei.pi.halfOpenFilesBLACK & (1 << f))) != 0;

                        if (halfOpenUs)
                        {
                            if (((Them == ColorC.WHITE) ? (ei.pi.halfOpenFilesWHITE & (1 << f)) : (ei.pi.halfOpenFilesBLACK & (1 << f))) != 0)
                                score += RookOpenFileBonus;
                            else
                                score += RookHalfOpenFileBonus;
                        }

                        // Penalize rooks which are trapped inside a king. Penalize more if
                        // king has lost right to castle.
                        if (mob > 6 || halfOpenUs)
                            continue;

                        ksq = pos.pieceList[Us][PieceTypeC.KING][0];

                        if (((ksq >> 3) ^ (Us * 7)) == RankC.RANK_1 || (ksq >> 3) == (s >> 3))
                        {
                            if ((ksq & 7) >= FileC.FILE_E)
                            {
                                if (f > (ksq & 7))
                                {
                                    // Is there a half-open file between the king and the edge of the board?
                                    if (((Us == ColorC.WHITE) ? (ei.pi.halfOpenFilesWHITE & ~((1 << ((ksq & 7) + 1)) - 1)) : (ei.pi.halfOpenFilesBLACK & ~((1 << ((ksq & 7) + 1)) - 1))) == 0)
                                    {
                                        score -= ((((pos.st.castleRights & (CastleRightC.WHITE_ANY << (Us << 1))) != 0) ? (TrappedRookPenalty - mob * 16) / 2 : (TrappedRookPenalty - mob * 16)) << 16);
                                    }
                                }
                            }
                            else
                            {
                                if (f < (ksq & 7))
                                {
                                    // Is there a half-open file between the king and the edge of the board?
                                    if (((Us == ColorC.WHITE) ? (ei.pi.halfOpenFilesWHITE & ((1 << (ksq & 7)) - 1)) : (ei.pi.halfOpenFilesBLACK & ((1 << (ksq & 7)) - 1))) == 0)
                                    {
                                        score -= ((((pos.st.castleRights & (CastleRightC.WHITE_ANY << (Us << 1))) != 0) ? (TrappedRookPenalty - mob * 16) / 2 : (TrappedRookPenalty - mob * 16)) << 16);
                                    }
                                }
                            }
                        }
                    }
                }

                scores += score;

                if (Trace)
                {
                    TracedScores[Us][Piece] = score;
                }
            }

            #endregion

            // Sum up all attacked squares
            ei.attackedBy[Us][0] = ei.attackedBy[Us][PieceTypeC.PAWN] | ei.attackedBy[Us][PieceTypeC.KNIGHT]
                                   | ei.attackedBy[Us][PieceTypeC.BISHOP] | ei.attackedBy[Us][PieceTypeC.ROOK]
                                   | ei.attackedBy[Us][PieceTypeC.QUEEN] | ei.attackedBy[Us][PieceTypeC.KING];

            return scores;
        }

        // evaluate_king<>() assigns bonuses and penalties to a king of a given color
        static Score evaluate_king(Color Us, bool Trace, Position pos, EvalInfo ei, ref Value marginsWHITE, ref Value marginsBLACK)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);

            Bitboard undefended, b, b1, b2, safe;
            int attackUnits;
            Score kingScore;
            Square ksq = pos.pieceList[Us][PieceTypeC.KING][0];

            // King shelter and enemy pawns storm
            Score score = ei.pi.king_safety(Us, pos, ksq);

            // King safety. This is quite complicated, and is almost certainly far
            // from optimally tuned.
            if (ei.kingAttackersCount[Them] >= 2
                && (ei.kingAdjacentZoneAttacksCount[Them] != 0))
            {
                // Find the attacked squares around the king which has no defenders
                // apart from the king itself
                undefended = ei.attackedBy[Them][0] & ei.attackedBy[Us][PieceTypeC.KING];
                undefended &= ~(ei.attackedBy[Us][PieceTypeC.PAWN] | ei.attackedBy[Us][PieceTypeC.KNIGHT]
                                | ei.attackedBy[Us][PieceTypeC.BISHOP] | ei.attackedBy[Us][PieceTypeC.ROOK]
                                | ei.attackedBy[Us][PieceTypeC.QUEEN]);

#if X64
                // Initialize the 'attackUnits' variable, which is used later on as an
                // index to the KingDangerTable[] array. The initial value is based on
                // the number and types of the enemy's attacking pieces, the number of
                // attacked and undefended squares around our king, the square of the
                // king, and the quality of the pawn shelter.
                b = undefended - ((undefended >> 1) & 0x5555555555555555UL);
                b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                attackUnits = Math.Min(25, (ei.kingAttackersCount[Them] * ei.kingAttackersWeight[Them]) / 2)
                             + 3 * (ei.kingAdjacentZoneAttacksCount[Them] + ((int)((b * 0x1111111111111111UL) >> 60)))
                             + InitKingDanger[(ksq ^ (Us * 56))]
                             - ((((score) + 32768) & ~0xffff) / 0x10000) / 32;

                // Analyse enemy's safe queen contact checks. First find undefended
                // squares around the king attacked by enemy queen...
                b = undefended & ei.attackedBy[Them][PieceTypeC.QUEEN] & ~(pos.byColorBB[Them]);
                if (b != 0)
                {
                    // ...then remove squares not supported by another enemy piece
                    b &= (ei.attackedBy[Them][PieceTypeC.PAWN] | ei.attackedBy[Them][PieceTypeC.KNIGHT]
                          | ei.attackedBy[Them][PieceTypeC.BISHOP] | ei.attackedBy[Them][PieceTypeC.ROOK]);
                    if (b != 0)
                    {
                        b -= (b >> 1) & 0x5555555555555555UL;
                        b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                        attackUnits += QueenContactCheckBonus
                                      * ((int)((b * 0x1111111111111111UL) >> 60))
                                      * (Them == pos.sideToMove ? 2 : 1);
                    }
                }

                // Analyse enemy's safe rook contact checks. First find undefended
                // squares around the king attacked by enemy rooks...
                b = undefended & ei.attackedBy[Them][PieceTypeC.ROOK] & ~pos.byColorBB[Them];

                // Consider only squares where the enemy rook gives check
                b &= Utils.PseudoAttacks_ROOK[ksq];

                if (b != 0)
                {
                    // ...then remove squares not supported by another enemy piece
                    b &= (ei.attackedBy[Them][PieceTypeC.PAWN] | ei.attackedBy[Them][PieceTypeC.KNIGHT]
                          | ei.attackedBy[Them][PieceTypeC.BISHOP] | ei.attackedBy[Them][PieceTypeC.QUEEN]);
                    if (b != 0)
                    {
                        b -= (b >> 1) & 0x5555555555555555UL;
                        b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                        attackUnits += RookContactCheckBonus
                                      * ((int)((b * 0x1111111111111111UL) >> 60))
                                      * (Them == pos.sideToMove ? 2 : 1);
                    }
                }

                // Analyse enemy's safe distance checks for sliders and knights
                safe = ~(pos.byColorBB[Them] | ei.attackedBy[Us][0]);

                b1 = (Utils.RAttacks[ksq][(((pos.occupied_squares & Utils.RMasks[ksq]) * Utils.RMagics[ksq]) >> Utils.RShifts[ksq])]) & safe;
                b2 = (Utils.BAttacks[ksq][(((pos.occupied_squares & Utils.BMasks[ksq]) * Utils.BMagics[ksq]) >> Utils.BShifts[ksq])]) & safe;

                // Enemy queen safe checks
                b = (b1 | b2) & ei.attackedBy[Them][PieceTypeC.QUEEN];
                if (b != 0)
                {
                    b -= (b >> 1) & 0x5555555555555555UL;
                    b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                    attackUnits += QueenCheckBonus * ((int)((b * 0x1111111111111111UL) >> 60));
                }

                // Enemy rooks safe checks
                b = b1 & ei.attackedBy[Them][PieceTypeC.ROOK];
                if (b != 0)
                {
                    b -= (b >> 1) & 0x5555555555555555UL;
                    b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                    attackUnits += RookCheckBonus * ((int)((b * 0x1111111111111111UL) >> 60));
                }

                // Enemy bishops safe checks
                b = b2 & ei.attackedBy[Them][PieceTypeC.BISHOP];
                if (b != 0)
                {
                    b -= (b >> 1) & 0x5555555555555555UL;
                    b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                    attackUnits += BishopCheckBonus * ((int)((b * 0x1111111111111111UL) >> 60));
                }

                // Enemy knights safe checks
                b = Utils.StepAttacksBB_KNIGHT[ksq] & ei.attackedBy[Them][PieceTypeC.KNIGHT] & safe;
                if (b != 0)
                {
                    b -= (b >> 1) & 0x5555555555555555UL;
                    b = ((b >> 2) & 0x3333333333333333UL) + (b & 0x3333333333333333UL);
                    attackUnits += KnightCheckBonus * ((int)((b * 0x1111111111111111UL) >> 60));
                }
#else
                // Initialize the 'attackUnits' variable, which is used later on as an
                // index to the KingDangerTable[] array. The initial value is based on
                // the number and types of the enemy's attacking pieces, the number of
                // attacked and undefended squares around our king, the square of the
                // king, and the quality of the pawn shelter.
                attackUnits = Math.Min(25, (ei.kingAttackersCount[Them] * ei.kingAttackersWeight[Them]) / 2)
                             + 3 * (ei.kingAdjacentZoneAttacksCount[Them] + Bitcount.popcount_1s_Max15(undefended))
                             + InitKingDanger[(ksq ^ (Us * 56))]
                             - ((((score) + 32768) & ~0xffff) / 0x10000) / 32;

                // Analyse enemy's safe queen contact checks. First find undefended
                // squares around the king attacked by enemy queen...
                b = undefended & ei.attackedBy[Them][PieceTypeC.QUEEN] & ~(pos.byColorBB[Them]);
                if (b != 0)
                {
                    // ...then remove squares not supported by another enemy piece
                    b &= (ei.attackedBy[Them][PieceTypeC.PAWN] | ei.attackedBy[Them][PieceTypeC.KNIGHT]
                          | ei.attackedBy[Them][PieceTypeC.BISHOP] | ei.attackedBy[Them][PieceTypeC.ROOK]);
                    if (b != 0)
                    {
                        attackUnits += QueenContactCheckBonus
                                      * Bitcount.popcount_1s_Max15(b)
                                      * (Them == pos.sideToMove ? 2 : 1);
                    }
                }

                // Analyse enemy's safe rook contact checks. First find undefended
                // squares around the king attacked by enemy rooks...
                b = undefended & ei.attackedBy[Them][PieceTypeC.ROOK] & ~pos.byColorBB[Them];

                // Consider only squares where the enemy rook gives check
                b &= Utils.PseudoAttacks_ROOK[ksq];

                if (b != 0)
                {
                    // ...then remove squares not supported by another enemy piece
                    b &= (ei.attackedBy[Them][PieceTypeC.PAWN] | ei.attackedBy[Them][PieceTypeC.KNIGHT]
                          | ei.attackedBy[Them][PieceTypeC.BISHOP] | ei.attackedBy[Them][PieceTypeC.QUEEN]);
                    if (b != 0)
                    {
                        attackUnits += RookContactCheckBonus
                                      * Bitcount.popcount_1s_Max15(b)
                                      * (Them == pos.sideToMove ? 2 : 1);
                    }
                }

                // Analyse enemy's safe distance checks for sliders and knights
                safe = ~(pos.byColorBB[Them] | ei.attackedBy[Us][0]);

                b1 = pos.attacks_from_ROOK(ksq) & safe;
                b2 = pos.attacks_from_BISHOP(ksq) & safe;

                // Enemy queen safe checks
                b = (b1 | b2) & ei.attackedBy[Them][PieceTypeC.QUEEN];
                if (b != 0)
                {
                    attackUnits += QueenCheckBonus * Bitcount.popcount_1s_Max15(b);
                }

                // Enemy rooks safe checks
                b = b1 & ei.attackedBy[Them][PieceTypeC.ROOK];
                if (b != 0)
                {
                    attackUnits += RookCheckBonus * Bitcount.popcount_1s_Max15(b);
                }

                // Enemy bishops safe checks
                b = b2 & ei.attackedBy[Them][PieceTypeC.BISHOP];
                if (b != 0)
                {
                    attackUnits += BishopCheckBonus * Bitcount.popcount_1s_Max15(b);
                }

                // Enemy knights safe checks
                b = Utils.StepAttacksBB_KNIGHT[ksq] & ei.attackedBy[Them][PieceTypeC.KNIGHT] & safe;
                if (b != 0)
                {
                    attackUnits += KnightCheckBonus * Bitcount.popcount_1s_Max15(b);
                }
#endif

                // To index KingDangerTable[] attackUnits must be in [0, 99] range
                attackUnits = Math.Min(99, Math.Max(0, attackUnits));

                // Finally, extract the king danger score from the KingDangerTable[]
                // array and subtract the score from evaluation. Set also margins[]
                // value that will be used for pruning because this value can sometimes
                // be very big, and so capturing a single attacking piece can therefore
                // result in a score change far bigger than the value of the captured piece.
                kingScore = KingDangerTable[Us == RootColor ? 1 : 0][attackUnits];
                score -= kingScore;
                if (Us == ColorC.WHITE)
                {
                    marginsWHITE += (((kingScore + 32768) & ~0xffff) / 0x10000);
                }
                else
                {
                    marginsBLACK += (((kingScore + 32768) & ~0xffff) / 0x10000);
                }
            }

            if (Trace)
                TracedScores[Us][PieceTypeC.KING] = score;

            return score;
        }

        // evaluate_passed_pawns<>() evaluates the passed pawns of the given color
        static Score evaluate_passed_pawns(Color Us, Position pos, EvalInfo ei)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);

            Bitboard b, squaresToQueen, defendedSquares, unsafeSquares, supportingPawns;
            Score score = ScoreC.SCORE_ZERO;

            b = (Us == ColorC.WHITE) ? ei.pi.passedPawnsWHITE : ei.pi.passedPawnsBLACK;

            if (b == 0)
                return ScoreC.SCORE_ZERO;

            do
            {
#if X64
                Bitboard bb = b;
                b &= (b - 1);
                Square s = (Utils.BSFTable[((bb & (0xffffffffffffffff - bb + 1)) * 0x218A392CD3D5DBFUL) >> 58]);
#else
                Square s = Utils.pop_1st_bit(ref b);
#endif
                Debug.Assert(pos.pawn_is_passed(Us, s));

                int r = ((s >> 3) ^ (Us * 7)) - RankC.RANK_2;
                int rr = r * (r - 1);

                // Base bonus based on rank
                Value mbonus = (20 * rr);
                Value ebonus = (10 * (rr + r + 1));

                if (rr != 0)
                {
                    Square blockSq = s + (Us == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S);

                    // Adjust bonus based on kings proximity
                    ebonus += (Utils.SquareDistance[pos.pieceList[Them][PieceTypeC.KING][0]][blockSq] * 5 * rr);
                    ebonus -= (Utils.SquareDistance[pos.pieceList[Us][PieceTypeC.KING][0]][blockSq] * 2 * rr);

                    // If blockSq is not the queening square then consider also a second push
                    if ((blockSq >> 3) != (Us == ColorC.WHITE ? RankC.RANK_8 : RankC.RANK_1))
                    {
                        ebonus -= (Utils.SquareDistance[pos.pieceList[Us][PieceTypeC.KING][0]][blockSq + (Us == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S)] * rr);
                    }

                    // If the pawn is free to advance, increase bonus
                    if (pos.board[blockSq] == PieceC.NO_PIECE)
                    {
                        squaresToQueen = Utils.ForwardBB[Us][s];
                        defendedSquares = squaresToQueen & ei.attackedBy[Us][0];

                        // If there is an enemy rook or queen attacking the pawn from behind,
                        // add all X-ray attacks by the rook or queen. Otherwise consider only
                        // the squares in the pawn's path attacked or occupied by the enemy.
                        if (
                            ((Utils.ForwardBB[Them][s] & ((pos.byTypeBB[PieceTypeC.ROOK] | pos.byTypeBB[PieceTypeC.QUEEN]) & pos.byColorBB[Them])) != 0)
                            &&
                            ((Utils.ForwardBB[Them][s] & ((pos.byTypeBB[PieceTypeC.ROOK] | pos.byTypeBB[PieceTypeC.QUEEN]) & pos.byColorBB[Them]) & pos.attacks_from_ROOK(s)) != 0)
                            )
                        {
                            unsafeSquares = squaresToQueen;
                        }
                        else
                        {
                            unsafeSquares = squaresToQueen & (ei.attackedBy[Them][0] | pos.byColorBB[Them]);
                        }

                        // If there aren't enemy attacks or pieces along the path to queen give
                        // huge bonus. Even bigger if we protect the pawn's path.
                        if (unsafeSquares == 0)
                            ebonus += (rr * (squaresToQueen == defendedSquares ? 17 : 15));
                        else
                            // OK, there are enemy attacks or pieces (but not pawns). Are those
                            // squares which are attacked by the enemy also attacked by us ?
                            // If yes, big bonus (but smaller than when there are no enemy attacks),
                            // if no, somewhat smaller bonus.
                            ebonus += (rr * ((unsafeSquares & defendedSquares) == unsafeSquares ? 13 : 8));
                    }
                } // rr != 0

                // Increase the bonus if the passed pawn is supported by a friendly pawn
                // on the same rank and a bit smaller if it's on the previous rank.
                supportingPawns = (pos.byTypeBB[PieceTypeC.PAWN] & pos.byColorBB[Us]) & Utils.AdjacentFilesBB[s & 7];
                if ((supportingPawns & Utils.RankBB[s >> 3]) != 0)
                    ebonus += (r * 20);
                else if ((supportingPawns & Utils.RankBB[(s - (Us == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S)) >> 3]) != 0)
                    ebonus += (r * 12);

                // Rook pawns are a special case: They are sometimes worse, and
                // sometimes better than other passed pawns. It is difficult to find
                // good rules for determining whether they are good or bad. For now,
                // we try the following: Increase the value for rook pawns if the
                // other side has no pieces apart from a knight, and decrease the
                // value if the other side has a rook or queen.
                if ((s & 7) == FileC.FILE_A || (s & 7) == FileC.FILE_H)
                {
                    if ((Them == 0 ? pos.st.npMaterialWHITE : pos.st.npMaterialBLACK) <= Constants.KnightValueMidgame)
                        ebonus += ebonus / 4;
                    else if (((pos.byTypeBB[PieceTypeC.ROOK] | pos.byTypeBB[PieceTypeC.QUEEN]) & pos.byColorBB[Them]) != 0)
                        ebonus -= ebonus / 4;
                }
                score += ((mbonus << 16) + ebonus);

            } while (b != 0);

            // Add the scores to the middle game and endgame eval
            return Utils.apply_weight(score, Weights[EvalWeightC.PassedPawns]);
        }

        // evaluate_unstoppable_pawns() evaluates the unstoppable passed pawns for both sides, this is quite
        // conservative and returns a winning score only when we are very sure that the pawn is winning.
        static Score evaluate_unstoppable_pawns(Position pos, EvalInfo ei)
        {
            Bitboard b, b2, blockers, supporters, queeningPath, candidates;
            Square s, blockSq, queeningSquare;
            Color c, winnerSide, loserSide;
            bool pathDefended, opposed;
            int pliesToGo = 0, movesToGo, oppMovesToGo = 0, sacptg, blockersCount, minKingDist, kingptg, d;
            int pliesToQueenWHITE = 256, pliesToQueenBLACK = 256, pliesToQueenWinner = 256;

            // Step 1. Hunt for unstoppable passed pawns. If we find at least one,
            // record how many plies are required for promotion.
            for (c = ColorC.WHITE; c <= ColorC.BLACK; c++)
            {
                // Skip if other side has non-pawn pieces
                if (pos.non_pawn_material(Utils.flip_C(c)) != 0)
                    continue;

                b = ei.pi.passed_pawns(c);

                while (b != 0)
                {
                    s = Utils.pop_1st_bit(ref b);
                    queeningSquare = Utils.relative_square(c, Utils.make_square(Utils.file_of(s), RankC.RANK_8));
                    queeningPath = Utils.forward_bb(c, s);

                    // Compute plies to queening and check direct advancement
                    movesToGo = Utils.rank_distance(s, queeningSquare) - (Utils.relative_rank_CS(c, s) == RankC.RANK_2 ? 1 : 0);
                    oppMovesToGo = Utils.square_distance(pos.king_square(Utils.flip_C(c)), queeningSquare) - ((c != pos.sideToMove) ? 1 : 0);
                    pathDefended = ((ei.attackedBy[c][0] & queeningPath) == queeningPath);

                    if (movesToGo >= oppMovesToGo && !pathDefended)
                        continue;

                    // Opponent king cannot block because path is defended and position
                    // is not in check. So only friendly pieces can be blockers.
                    Debug.Assert(!pos.in_check());
                    Debug.Assert((queeningPath & pos.occupied_squares) == (queeningPath & pos.pieces_C(c)));

                    // Add moves needed to free the path from friendly pieces and retest condition
                    movesToGo += Bitcount.popcount_1s_Max15(queeningPath & pos.pieces_C(c));

                    if (movesToGo >= oppMovesToGo && !pathDefended)
                        continue;

                    pliesToGo = 2 * movesToGo - ((c == pos.sideToMove) ? 1 : 0);

                    if (c == ColorC.WHITE)
                    {
                        pliesToQueenWHITE = Math.Min(pliesToQueenWHITE, pliesToGo);
                    }
                    else
                    {
                        pliesToQueenBLACK = Math.Min(pliesToQueenBLACK, pliesToGo);
                    }
                }
            }

            // Step 2. If either side cannot promote at least three plies before the other side then situation
            // becomes too complex and we give up. Otherwise we determine the possibly "winning side"
            if (Math.Abs(pliesToQueenWHITE - pliesToQueenBLACK) < 3)
                return ScoreC.SCORE_ZERO;

            winnerSide = (pliesToQueenWHITE < pliesToQueenBLACK ? ColorC.WHITE : ColorC.BLACK);
            pliesToQueenWinner = (winnerSide == ColorC.WHITE) ? pliesToQueenWHITE : pliesToQueenBLACK;
            loserSide = Utils.flip_C(winnerSide);

            // Step 3. Can the losing side possibly create a new passed pawn and thus prevent the loss?
            b = candidates = pos.pieces_PTC(PieceTypeC.PAWN, loserSide);

            while (b != 0)
            {
                s = Utils.pop_1st_bit(ref b);

                // Compute plies from queening
                queeningSquare = Utils.relative_square(loserSide, Utils.make_square(Utils.file_of(s), RankC.RANK_8));
                movesToGo = Utils.rank_distance(s, queeningSquare) - ((Utils.relative_rank_CS(loserSide, s) == RankC.RANK_2) ? 1 : 0);
                pliesToGo = 2 * movesToGo - ((loserSide == pos.sideToMove) ? 1 : 0);

                // Check if (without even considering any obstacles) we're too far away or doubled
                if (
                    (pliesToQueenWinner + 3 <= pliesToGo)
                    ||
                    ((Utils.forward_bb(loserSide, s) & pos.pieces_PTC(PieceTypeC.PAWN, loserSide)) != 0)
                    )
                {
                    Utils.xor_bit(ref candidates, s);
                }
            }

            // If any candidate is already a passed pawn it _may_ promote in time. We give up.
            if ((candidates & ei.pi.passed_pawns(loserSide)) != 0)
                return ScoreC.SCORE_ZERO;

            // Step 4. Check new passed pawn creation through king capturing and pawn sacrifices
            b = candidates;

            while (b != 0)
            {
                s = Utils.pop_1st_bit(ref b);
                sacptg = blockersCount = 0;
                minKingDist = kingptg = 256;

                // Compute plies from queening
                queeningSquare = Utils.relative_square(loserSide, Utils.make_square(Utils.file_of(s), RankC.RANK_8));
                movesToGo = Utils.rank_distance(s, queeningSquare) - ((Utils.relative_rank_CS(loserSide, s) == RankC.RANK_2) ? 1 : 0);
                pliesToGo = 2 * movesToGo - ((loserSide == pos.sideToMove) ? 1 : 0);

                // Generate list of blocking pawns and supporters
                supporters = Utils.adjacent_files_bb(Utils.file_of(s)) & candidates;
                opposed = (Utils.forward_bb(loserSide, s) & pos.pieces_PTC(PieceTypeC.PAWN, winnerSide)) != 0;
                blockers = Utils.passed_pawn_mask(loserSide, s) & pos.pieces_PTC(PieceTypeC.PAWN, winnerSide);

                Debug.Assert(blockers != 0);

                // How many plies does it take to remove all the blocking pawns?
                while (blockers != 0)
                {
                    blockSq = Utils.pop_1st_bit(ref blockers);
                    movesToGo = 256;

                    // Check pawns that can give support to overcome obstacle, for instance
                    // black pawns: a4, b4 white: b2 then pawn in b4 is giving support.
                    if (!opposed)
                    {
                        b2 = supporters & Utils.in_front_bb_CS(winnerSide, blockSq + Utils.pawn_push(winnerSide));

                        while (b2 != 0) // This while-loop could be replaced with LSB/MSB (depending on color)
                        {
                            d = Utils.square_distance(blockSq, Utils.pop_1st_bit(ref b2)) - 2;
                            movesToGo = Math.Min(movesToGo, d);
                        }
                    }

                    // Check pawns that can be sacrificed against the blocking pawn
                    b2 = Utils.attack_span_mask(winnerSide, blockSq) & candidates & ~(1UL << s);

                    while (b2 != 0) // This while-loop could be replaced with LSB/MSB (depending on color)
                    {
                        d = Utils.square_distance(blockSq, Utils.pop_1st_bit(ref b2)) - 2;
                        movesToGo = Math.Min(movesToGo, d);
                    }

                    // If obstacle can be destroyed with an immediate pawn exchange / sacrifice,
                    // it's not a real obstacle and we have nothing to add to pliesToGo.
                    if (movesToGo <= 0)
                        continue;

                    // Plies needed to sacrifice against all the blocking pawns
                    sacptg += movesToGo * 2;
                    blockersCount++;

                    // Plies needed for the king to capture all the blocking pawns
                    d = Utils.square_distance(pos.king_square(loserSide), blockSq);
                    minKingDist = Math.Min(minKingDist, d);
                    kingptg = (minKingDist + blockersCount) * 2;
                }

                // Check if pawn sacrifice plan _may_ save the day
                if (pliesToQueenWinner + 3 > pliesToGo + sacptg)
                    return ScoreC.SCORE_ZERO;

                // Check if king capture plan _may_ save the day (contains some false positives)
                if (pliesToQueenWinner + 3 > pliesToGo + kingptg)
                    return ScoreC.SCORE_ZERO;
            }

            // Winning pawn is unstoppable and will promote as first, return big score
            Score score = Utils.make_score(0, (Value)0x500 - 0x20 * pliesToQueenWinner);
            return winnerSide == ColorC.WHITE ? score : -score;
        }

        // evaluate_space() computes the space evaluation for a given side. The
        // space evaluation is a simple bonus based on the number of safe squares
        // available for minor pieces on the central four files on ranks 2--4. Safe
        // squares one, two or three squares behind a friendly pawn are counted
        // twice. Finally, the space bonus is scaled by a weight taken from the
        // material hash table. The aim is to improve play on game opening.
        static int evaluate_space(Color Us, Position pos, EvalInfo ei)
        {
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);

            // Find the safe squares for our pieces inside the area defined by
            // SpaceMask[]. A square is unsafe if it is attacked by an enemy
            // pawn, or if it is undefended and attacked by an enemy piece.
            Bitboard behind = pos.byTypeBB[PieceTypeC.PAWN] & pos.byColorBB[Us];
            Bitboard safe = SpaceMask[Us]
                           & ~behind
                           & ~ei.attackedBy[Them][PieceTypeC.PAWN]
                           & (ei.attackedBy[Us][0] | ~ei.attackedBy[Them][0]);

            // Find all squares which are at most three squares behind some friendly pawn
            behind |= (Us == ColorC.WHITE ? behind >> 8 : behind << 8);
            behind |= (Us == ColorC.WHITE ? behind >> 16 : behind << 16);

            return Bitcount.popcount_1s_Max15(safe) + Bitcount.popcount_1s_Max15(behind & safe);
        }

        // weight_option() computes the value of an evaluation weight, by combining
        // two UCI-configurable weights (midgame and endgame) with an internal weight.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        static Score weight_option(string mgOpt, string egOpt, Score internalWeight)
        {
            // Scale option value from 100 to 256
            int mg = int.Parse(OptionMap.Instance[mgOpt].v) * 256 / 100;
            int eg = int.Parse(OptionMap.Instance[egOpt].v) * 256 / 100;

            return Utils.apply_weight(Utils.make_score(mg, eg), internalWeight);
        }

        // interpolate() interpolates between a middle game and an endgame score,
        // based on game phase. It also scales the return value by a ScaleFactor array.

        // ALL CALLS INLINED
//#if AGGR_INLINE
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//#endif
//        static Value interpolate(Score v, Phase ph, ScaleFactor sf)
//        {
//            Debug.Assert(Utils.mg_value(v) > -ValueC.VALUE_INFINITE && Utils.mg_value(v) < ValueC.VALUE_INFINITE);
//            Debug.Assert(Utils.eg_value(v) > -ValueC.VALUE_INFINITE && Utils.eg_value(v) < ValueC.VALUE_INFINITE);
//            Debug.Assert(ph >= PhaseC.PHASE_ENDGAME && ph <= PhaseC.PHASE_MIDGAME);

//            int ev = (Utils.eg_value(v) * (int)(sf)) / ScaleFactorC.SCALE_FACTOR_NORMAL;
//            int result = (Utils.mg_value(v) * (int)(ph) + ev * (int)(128 - ph)) / 128;
//            return ((result + GrainSize / 2) & ~(GrainSize - 1));
//        }

        // A couple of little helpers used by tracing code, to_cp() converts a value to
        // a double in centipawns scale, trace_add() stores white and black scores.
        static double to_cp(Value v) { return (double)(v) / (double)(Constants.PawnValueMidgame); }

        static void trace_add(int idx, Score wScore)
        {
            TracedScores[ColorC.WHITE][idx] = wScore;
            TracedScores[ColorC.BLACK][idx] = ScoreC.SCORE_ZERO;
        }

        static void trace_add(int idx, Score wScore, Score bScore)
        {

            TracedScores[ColorC.WHITE][idx] = wScore;
            TracedScores[ColorC.BLACK][idx] = bScore;
        }

        // trace_row() is an helper function used by tracing code to register the
        // values of a single evaluation term.
        static string FormatDouble(double d, int? width, bool useSign)
        {
            string baseString = string.Empty;
            if (useSign)
            {
                baseString = ((d >= 0 ? "+" : "") + String.Format("{0:0.00}", d));
            }
            else
            {
                baseString = String.Format("{0:0.00}", d);
            }
            if (width.HasValue)
            {
                return baseString.PadLeft(width.Value, ' ');
            }
            return baseString;
        }

        static void trace_row(string name, int idx)
        {
            Score wScore = TracedScores[ColorC.WHITE][idx];
            Score bScore = TracedScores[ColorC.BLACK][idx];

            switch (idx)
            {
                case TracedTypeC.PST:
                case TracedTypeC.IMBALANCE:
                case PieceTypeC.PAWN:
                case TracedTypeC.UNSTOPPABLE:
                case TracedTypeC.TOTAL:
                    TraceStream.Append(name.PadLeft(20, ' '));
                    TraceStream.Append(" |   ---   --- |   ---   --- | ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.mg_value(wScore)), 6, true));
                    TraceStream.Append(" ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.eg_value(wScore)), 6, true));
                    TraceStream.Append(" \n");
                    break;
                default:
                    TraceStream.Append(name.PadLeft(20, ' '));
                    TraceStream.Append(" | ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.mg_value(wScore)), 5, false));
                    TraceStream.Append(" ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.eg_value(wScore)), 5, false));
                    TraceStream.Append(" | ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.mg_value(bScore)), 5, false));
                    TraceStream.Append(" ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.eg_value(bScore)), 5, false));
                    TraceStream.Append(" | ");

                    TraceStream.Append(FormatDouble(to_cp(Utils.mg_value(wScore - bScore)), 6, true));
                    TraceStream.Append(" ");
                    TraceStream.Append(FormatDouble(to_cp(Utils.eg_value(wScore - bScore)), 6, true));
                    TraceStream.Append(" \n");
                    break;
            }
        }
    }
}
