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
using CastlingSide = System.Int32;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Portfish
{
    internal enum MoveType
    {
        MV_CAPTURE,
        MV_QUIET,
        MV_QUIET_CHECK,
        MV_EVASION,
        MV_NON_EVASION,
        MV_LEGAL
    };

    internal static class Movegen
    {
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static Bitboard move_pawns(Square Delta, Bitboard p)
        {
            return Delta == SquareC.DELTA_N ? p << 8
              : Delta == SquareC.DELTA_S ? p >> 8
              : Delta == SquareC.DELTA_NE ? (p & ~Constants.FileHBB) << 9
              : Delta == SquareC.DELTA_SE ? (p & ~Constants.FileHBB) >> 7
              : Delta == SquareC.DELTA_NW ? (p & ~Constants.FileABB) << 7
              : Delta == SquareC.DELTA_SW ? (p & ~Constants.FileABB) >> 9 : 0;
        }

        private static void generate_castle(CastlingSide Side, bool OnlyChecks, Position pos, MoveStack[] ms, ref int mpos, Color us)
        {
            if (pos.castle_impeded(us, Side) || (pos.can_castle_CR(Utils.make_castle_right(us, Side))==0) )
                return;

            // After castling, the rook and king final positions are the same in Chess960
            // as they would be in standard chess.
            Square kfrom = pos.king_square(us);
            Square rfrom = pos.castle_rook_square(us, Side);
            Square kto = Utils.relative_square(us, Side == CastlingSideC.KING_SIDE ? SquareC.SQ_G1 : SquareC.SQ_C1);

            Bitboard enemies = pos.pieces_C(us ^ 1);

            Debug.Assert(!pos.in_check());

            for (Square s = Math.Min(kfrom, kto), e = Math.Max(kfrom, kto); s <= e; s++)
                if (s != kfrom // We are not in check
                    && ((pos.attackers_to(s) & enemies) != 0))
                    return;

            // Because we generate only legal castling moves we need to verify that
            // when moving the castling rook we do not discover some hidden checker.
            // For instance an enemy queen in SQ_A1 when castling rook is in SQ_B1.
            if (pos.chess960
                && ((pos.attackers_to(kto, Utils.xor_bit(pos.occupied_squares, rfrom)) & enemies) != 0))
                return;

            Move m = Utils.make_castle(kfrom, rfrom);

            if (OnlyChecks)
            {
                CheckInfo ci = CheckInfoBroker.GetObject();
                ci.CreateCheckInfo(pos);
                bool givesCheck = pos.move_gives_check(m, ci);
                CheckInfoBroker.Free();
                if (!givesCheck) return;
            }

            ms[mpos++].move = m;
        }

        private static void generate_promotions(MoveType Type, Square Delta, MoveStack[] ms, ref int mpos, Bitboard pawnsOn7, Bitboard target, Square ksq)
        {
            Bitboard b = move_pawns(Delta, pawnsOn7) & target;
            while (b != 0)
            {
                Square to = Utils.pop_1st_bit(ref b);

                if (Type == MoveType.MV_CAPTURE || Type == MoveType.MV_EVASION || Type == MoveType.MV_NON_EVASION)
                    ms[mpos++].move = Utils.make_promotion(to - Delta, to, PieceTypeC.QUEEN);

                if (Type == MoveType.MV_QUIET || Type == MoveType.MV_EVASION || Type == MoveType.MV_NON_EVASION)
                {
                    ms[mpos++].move = Utils.make_promotion(to - Delta, to, PieceTypeC.ROOK);
                    ms[mpos++].move = Utils.make_promotion(to - Delta, to, PieceTypeC.BISHOP);
                    ms[mpos++].move = Utils.make_promotion(to - Delta, to, PieceTypeC.KNIGHT);
                }

                // Knight-promotion is the only one that can give a direct check not
                // already included in the queen-promotion.
                if (Type == MoveType.MV_QUIET_CHECK && (Utils.bit_is_set(Utils.StepAttacksBB[PieceC.W_KNIGHT][to], ksq) != 0))
                    ms[mpos++].move = Utils.make_promotion(to - Delta, to, PieceTypeC.KNIGHT);
            }
        }

        private static void generate_pawn_moves(Color Us, MoveType Type, Position pos, MoveStack[] ms, ref int mpos, Bitboard target, Square ksq)
        {
            // Compute our parametrized parameters at compile time, named according to
            // the point of view of white side.
            Color Them = (Us == ColorC.WHITE ? ColorC.BLACK : ColorC.WHITE);
            Bitboard TRank8BB = (Us == ColorC.WHITE ? Constants.Rank8BB : Constants.Rank1BB);
            Bitboard TRank7BB = (Us == ColorC.WHITE ? Constants.Rank7BB : Constants.Rank2BB);
            Bitboard TRank3BB = (Us == ColorC.WHITE ? Constants.Rank3BB : Constants.Rank6BB);
            Square UP = (Us == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S);
            Square RIGHT = (Us == ColorC.WHITE ? SquareC.DELTA_NE : SquareC.DELTA_SW);
            Square LEFT = (Us == ColorC.WHITE ? SquareC.DELTA_NW : SquareC.DELTA_SE);

            Bitboard b1, b2, dc1, dc2, emptySquares = 0;
            Square to;

            Bitboard pawnsOn7 = (pos.byTypeBB[PieceTypeC.PAWN] & pos.byColorBB[Us]) & TRank7BB;
            Bitboard pawnsNotOn7 = (pos.byTypeBB[PieceTypeC.PAWN] & pos.byColorBB[Us]) & ~TRank7BB;

            Bitboard enemies = (Type == MoveType.MV_EVASION ? pos.byColorBB[Them] & target :
                Type == MoveType.MV_CAPTURE ? target : pos.byColorBB[Them]);

            // Single and double pawn pushes, no promotions
            if (Type != MoveType.MV_CAPTURE)
            {
                emptySquares = (Type == MoveType.MV_QUIET ? target : ~pos.occupied_squares);

                b1 = move_pawns(UP, pawnsNotOn7) & emptySquares;
                b2 = move_pawns(UP, b1 & TRank3BB) & emptySquares;

                if (Type == MoveType.MV_EVASION) // Consider only blocking squares
                {
                    b1 &= target;
                    b2 &= target;
                }

                if (Type == MoveType.MV_QUIET_CHECK)
                {
                    b1 &= Utils.StepAttacksBB[((Them << 3) | PieceTypeC.PAWN)][ksq];
                    b2 &= Utils.StepAttacksBB[((Them << 3) | PieceTypeC.PAWN)][ksq];

                    // Add pawn pushes which give discovered check. This is possible only
                    // if the pawn is not on the same file as the enemy king, because we
                    // don't generate captures. Note that a possible discovery check
                    // promotion has been already generated among captures.
                    if ((pawnsNotOn7 & target) != 0) // Target is dc bitboard
                    {
                        dc1 = move_pawns(UP, pawnsNotOn7 & target) & emptySquares & ~(Utils.FileBB[ksq & 7]);
                        dc2 = move_pawns(UP, dc1 & TRank3BB) & emptySquares;

                        b1 |= dc1;
                        b2 |= dc2;
                    }
                }

                while (b1 != 0) { to = Utils.pop_1st_bit(ref b1); ms[mpos++].move = (to | ((to - UP) << 6)); }
                while (b2 != 0) { to = Utils.pop_1st_bit(ref b2); ms[mpos++].move = (to | ((to - UP - UP) << 6)); }
            }

            // Promotions and underpromotions
            if ((pawnsOn7 != 0) && (Type != MoveType.MV_EVASION || ((target & TRank8BB) != 0)))
            {
                if (Type == MoveType.MV_CAPTURE)
                    emptySquares = ~pos.occupied_squares;

                if (Type == MoveType.MV_EVASION)
                    emptySquares &= target;

                generate_promotions(Type, RIGHT, ms, ref mpos, pawnsOn7, enemies, ksq);
                generate_promotions(Type, LEFT, ms, ref mpos, pawnsOn7, enemies, ksq);
                generate_promotions(Type, UP, ms, ref mpos, pawnsOn7, emptySquares, ksq);
            }

            // Standard and en-passant captures
            if (Type == MoveType.MV_CAPTURE || Type == MoveType.MV_EVASION || Type == MoveType.MV_NON_EVASION)
            {
                b1 = move_pawns(RIGHT, pawnsNotOn7) & enemies;
                b2 = move_pawns(LEFT, pawnsNotOn7) & enemies;

                while (b1 != 0) { to = Utils.pop_1st_bit(ref b1); ms[mpos++].move = (to | ((to - RIGHT) << 6)); }
                while (b2 != 0) { to = Utils.pop_1st_bit(ref b2); ms[mpos++].move = (to | ((to - LEFT) << 6)); }

                if (pos.st.epSquare != SquareC.SQ_NONE)
                {
                    Debug.Assert(Utils.rank_of(pos.st.epSquare) == Utils.relative_rank_CR(Us, RankC.RANK_6));

                    // An en passant capture can be an evasion only if the checking piece
                    // is the double pushed pawn and so is in the target. Otherwise this
                    // is a discovery check and we are forced to do otherwise.
                    if (Type == MoveType.MV_EVASION && (((target & Utils.SquareBB[pos.st.epSquare - UP]) == 0)))
                        return;

                    b1 = pawnsNotOn7 & Utils.StepAttacksBB[((Them << 3) | PieceTypeC.PAWN)][pos.st.epSquare];

                    Debug.Assert(b1 != 0);

                    while (b1 != 0)
                    {
                        ms[mpos++].move = (pos.st.epSquare | (Utils.pop_1st_bit(ref b1) << 6) | (2 << 14));
                    }
                }
            }
        }

        private static void generate_direct_checks(PieceType Pt, Position pos, MoveStack[] ms, ref int mpos, Color us, CheckInfo ci)
        {
            Debug.Assert(Pt != PieceTypeC.KING && Pt != PieceTypeC.PAWN);

            Bitboard b, target;

            Square[] pl = pos.pieceList[us][Pt];
            int plPos = 0;
            Square from = pl[plPos];

            if ((from = pl[plPos]) != SquareC.SQ_NONE)
            {
                target = ci.checkSq[Pt] & ~pos.occupied_squares; // Non capture checks only

                do
                {
                    if ((Pt == PieceTypeC.BISHOP || Pt == PieceTypeC.ROOK || Pt == PieceTypeC.QUEEN)
                        && ((Utils.PseudoAttacks[Pt][from] & target) == 0))
                        continue;

                    if ((ci.dcCandidates != 0) && (Utils.bit_is_set(ci.dcCandidates, from) != 0))
                        continue;

                    b = pos.attacks_from_PTS(Pt, from) & target;
                    while (b != 0) { ms[mpos++].move = Utils.make_move(from, Utils.pop_1st_bit(ref b)); }

                } while ((from = pl[++plPos]) != SquareC.SQ_NONE);
            }
        }

        private static void generate_king_moves(Position pos, MoveStack[] ms, ref int mpos, Color us, Bitboard target)
        {
            Square from = pos.pieceList[us][PieceTypeC.KING][0];
            Bitboard b = Utils.StepAttacksBB_KING[from] & target;
            while (b != 0) 
            {
#if X64
                Bitboard bb = b;
                b &= (b - 1);
                ms[mpos++].move = ((Utils.BSFTable[((bb & (0xffffffffffffffff - bb + 1)) * 0x218A392CD3D5DBFUL) >> 58]) | (from << 6));
#else
                ms[mpos++].move = Utils.make_move(from, Utils.pop_1st_bit(ref b));
#endif
            }
        }

        private static void generate_moves(Position pos, MoveStack[] ms, ref int mpos, Color us, Bitboard target)
        {
            Bitboard b; 
            int plPos;
            for (PieceType pieceType = PieceTypeC.KNIGHT; pieceType < PieceTypeC.KING; pieceType++)
            {
                Square[] pl = pos.pieceList[us][pieceType];
                Square s = pl[plPos = 0];
                if (s != SquareC.SQ_NONE)
                {
                    do
                    {
#if X64
                        if (pieceType == PieceTypeC.BISHOP) { b = Utils.BAttacks[s][(((pos.occupied_squares & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])] & target; }
                        else if (pieceType == PieceTypeC.ROOK) { b = Utils.RAttacks[s][(((pos.occupied_squares & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])] & target; }
                        else if (pieceType == PieceTypeC.QUEEN) { b = (Utils.BAttacks[s][(((pos.occupied_squares & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])] | Utils.RAttacks[s][(((pos.occupied_squares & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])]) & target; }
                        else b = Utils.StepAttacksBB[pieceType][s] & target;

                        while (b != 0) 
                        {
                            Bitboard bb = b;
                            b &= (b - 1);
                            ms[mpos++].move = (Utils.BSFTable[((bb & (0xffffffffffffffff - bb + 1)) * 0x218A392CD3D5DBFUL) >> 58]) | (s << 6);
                        }
#else
                        if (pieceType == PieceTypeC.BISHOP) { b = Utils.bishop_attacks_bb(s, pos.occupied_squares) & target; ; }
                        else if (pieceType == PieceTypeC.ROOK) { b = Utils.rook_attacks_bb(s, pos.occupied_squares) & target; ; }
                        else if (pieceType == PieceTypeC.QUEEN) { b = (Utils.bishop_attacks_bb(s, pos.occupied_squares) | Utils.rook_attacks_bb(s, pos.occupied_squares)) & target; }
                        else b = Utils.StepAttacksBB[pieceType][s] & target;

                        while (b != 0) { ms[mpos++].move = Utils.make_move(s, Utils.pop_1st_bit(ref b)); }
#endif
                    }
                    while ((s = pl[++plPos]) != SquareC.SQ_NONE);
                }
            }
        }

        internal static void generate_legal(Position pos, MoveStack[] ms, ref int mpos)
        {
            /// generate<MV_LEGAL> generates all the legal moves in the given position
            Bitboard pinned = pos.pinned_pieces();

            if (pos.in_check()) { generate_evasion(pos, ms, ref mpos); }
            else { generate_non_evasion(pos, ms, ref mpos); }

            int last = mpos;
            int cur = 0;
            while (cur != last)
            {
                if (!pos.pl_move_is_legal(ms[cur].move, pinned))
                {
                    ms[cur].move = ms[--last].move;
                }
                else
                {
                    cur++;
                }
            }
            mpos = last;
        }

        internal static void generate_evasion(Position pos, MoveStack[] ms, ref int mpos)
        {
            /// generate<MV_EVASION> generates all pseudo-legal check evasions when the side
            /// to move is in check. Returns a pointer to the end of the move list.
            Debug.Assert(pos.in_check());

            Bitboard b;
            Square from, checksq;
            int checkersCnt = 0;
            Color us = pos.sideToMove;
            Square ksq = pos.king_square(us);
            Bitboard sliderAttacks = 0;
            Bitboard checkers = pos.st.checkersBB;

            Debug.Assert(checkers != 0);

            // Find squares attacked by slider checkers, we will remove them from the king
            // evasions so to skip known illegal moves avoiding useless legality check later.
            b = checkers;
            do
            {
                checkersCnt++;
                checksq = Utils.pop_1st_bit(ref b);

                Debug.Assert(Utils.color_of(pos.piece_on(checksq)) == Utils.flip_C(us));

                switch (Utils.type_of(pos.piece_on(checksq)))
                {
                    case PieceTypeC.BISHOP: sliderAttacks |= Utils.PseudoAttacks[PieceTypeC.BISHOP][checksq]; break;
                    case PieceTypeC.ROOK: sliderAttacks |= Utils.PseudoAttacks[PieceTypeC.ROOK][checksq]; break;
                    case PieceTypeC.QUEEN:
                        // If queen and king are far or not on a diagonal line we can safely
                        // remove all the squares attacked in the other direction becuase are
                        // not reachable by the king anyway.
                        if ((Utils.between_bb(ksq, checksq) != 0) || ((Utils.bit_is_set(Utils.PseudoAttacks[PieceTypeC.BISHOP][checksq], ksq)) == 0))
                            sliderAttacks |= Utils.PseudoAttacks[PieceTypeC.QUEEN][checksq];

                        // Otherwise we need to use real rook attacks to check if king is safe
                        // to move in the other direction. For example: king in B2, queen in A1
                        // a knight in B1, and we can safely move to C1.
                        else
                            sliderAttacks |= Utils.PseudoAttacks[PieceTypeC.BISHOP][checksq] | pos.attacks_from_ROOK(checksq);
                        break;
                    default:
                        break;
                }
            } while (b != 0);

            // Generate evasions for king, capture and non capture moves
            b = Position.attacks_from_KING(ksq) & ~pos.pieces_C(us) & ~sliderAttacks;
            from = ksq;
            while (b != 0) { ms[mpos++].move = Utils.make_move(from, Utils.pop_1st_bit(ref b)); }

            // Generate evasions for other pieces only if not under a double check
            if (checkersCnt > 1)
                return;

            // Blocking evasions or captures of the checking piece
            Bitboard target = Utils.between_bb(checksq, ksq) | checkers;
            generate_pawn_moves(us, MoveType.MV_EVASION, pos, ms, ref mpos, target, SquareC.SQ_NONE);
            generate_moves(pos, ms, ref mpos, us, target);
        }

        internal static void generate_quiet_check(Position pos, MoveStack[] ms, ref int mpos)
        {
            /// generate<MV_NON_CAPTURE_CHECK> generates all pseudo-legal non-captures and knight
            /// underpromotions that give check. Returns a pointer to the end of the move list.
            Debug.Assert(!pos.in_check());

            Color us = pos.sideToMove;
            CheckInfo ci = CheckInfoBroker.GetObject();
            ci.CreateCheckInfo(pos);
            Bitboard dc = ci.dcCandidates;

            while (dc != 0)
            {
                Square from = Utils.pop_1st_bit(ref dc);
                PieceType pt = Utils.type_of(pos.piece_on(from));

                if (pt == PieceTypeC.PAWN)
                    continue; // Will be generated together with direct checks

                Bitboard b = pos.attacks_from_PTS(pt, from) & ~pos.occupied_squares;

                if (pt == PieceTypeC.KING)
                    b &= ~Utils.PseudoAttacks[PieceTypeC.QUEEN][ci.ksq];

                while (b != 0) { ms[mpos++].move = Utils.make_move(from, Utils.pop_1st_bit(ref b)); }
            }

            generate_pawn_moves(us, MoveType.MV_QUIET_CHECK, pos, ms, ref mpos, ci.dcCandidates, ci.ksq);

            generate_direct_checks(PieceTypeC.KNIGHT, pos, ms, ref mpos, us, ci);
            generate_direct_checks(PieceTypeC.BISHOP, pos, ms, ref mpos, us, ci);
            generate_direct_checks(PieceTypeC.ROOK, pos, ms, ref mpos, us, ci);
            generate_direct_checks(PieceTypeC.QUEEN, pos, ms, ref mpos, us, ci);

            if (pos.can_castle_C(us) != 0)
            {
                generate_castle(CastlingSideC.KING_SIDE, true, pos, ms, ref mpos, us);
                generate_castle(CastlingSideC.QUEEN_SIDE, true, pos, ms, ref mpos, us);
            }

            CheckInfoBroker.Free();
        }

        internal static void generate_quiet(Position pos, MoveStack[] ms, ref int mpos)
        {
            Debug.Assert(!pos.in_check());

            Color us = pos.sideToMove;
            Bitboard target = ~pos.occupied_squares;

            generate_pawn_moves(us, MoveType.MV_QUIET, pos, ms, ref mpos, target, SquareC.SQ_NONE);
            generate_moves(pos, ms, ref mpos, us, target);
            generate_king_moves(pos, ms, ref mpos, us, target);

            if ((pos.st.castleRights & (CastleRightC.WHITE_ANY << (us << 1))) != 0)
            {
                generate_castle(CastlingSideC.KING_SIDE, false, pos, ms, ref mpos, us);
                generate_castle(CastlingSideC.QUEEN_SIDE, false, pos, ms, ref mpos, us);
            }
        }

        internal static void generate_non_evasion(Position pos, MoveStack[] ms, ref int mpos)
        {
            Debug.Assert(!pos.in_check());

            Color us = pos.sideToMove;
            Bitboard target = ~(pos.byColorBB[us]);

            generate_pawn_moves(us, MoveType.MV_NON_EVASION, pos, ms, ref mpos, target, SquareC.SQ_NONE);
            generate_moves(pos, ms, ref mpos, us, target);
            generate_king_moves(pos, ms, ref mpos, us, target);

            if ((pos.st.castleRights & (CastleRightC.WHITE_ANY << (us << 1))) != 0)
            {
                generate_castle(CastlingSideC.KING_SIDE, false, pos, ms, ref mpos, us);
                generate_castle(CastlingSideC.QUEEN_SIDE, false, pos, ms, ref mpos, us);
            }
        }

        internal static void generate_capture(Position pos, MoveStack[] ms, ref int mpos)
        {
            Debug.Assert(!pos.in_check());

            Color us = pos.sideToMove;
            Bitboard target = pos.byColorBB[us ^ 1];

            generate_pawn_moves(us, MoveType.MV_CAPTURE, pos, ms, ref mpos, target, SquareC.SQ_NONE);
            generate_moves(pos, ms, ref mpos, us, target);
            generate_king_moves(pos, ms, ref mpos, us, target);
        }
    }
}
