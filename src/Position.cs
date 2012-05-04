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
    internal sealed class CheckInfo
    {
        internal CheckInfo(Position pos)
        {
            Color them = Utils.flip_C(pos.sideToMove);
            ksq = pos.king_square(them);

            pinned = pos.pinned_pieces();
            dcCandidates = pos.discovered_check_candidates();

            checkSq[PieceTypeC.PAWN] = pos.attacks_from_PAWN(ksq, them);
            checkSq[PieceTypeC.KNIGHT] = pos.attacks_from_KNIGHT(ksq);
            checkSq[PieceTypeC.BISHOP] = pos.attacks_from_BISHOP(ksq);
            checkSq[PieceTypeC.ROOK] = pos.attacks_from_ROOK(ksq);
            checkSq[PieceTypeC.QUEEN] = checkSq[PieceTypeC.BISHOP] | checkSq[PieceTypeC.ROOK];
            checkSq[PieceTypeC.KING] = 0;
        }

        public CheckInfo()
        {
        }

        internal void CreateCheckInfo(Position pos)
        {
            Color them = pos.sideToMove ^ 1;
            ksq = pos.pieceList[them][PieceTypeC.KING][0];

            pinned = pos.pinned_pieces();
            dcCandidates = pos.discovered_check_candidates();

            checkSq[PieceTypeC.PAWN] = Utils.StepAttacksBB[((them << 3) | PieceTypeC.PAWN)][ksq];
            checkSq[PieceTypeC.KNIGHT] = Utils.StepAttacksBB_KNIGHT[ksq];
#if X64
            checkSq[PieceTypeC.BISHOP] = Utils.BAttacks[ksq][(((pos.occupied_squares & Utils.BMasks[ksq]) * Utils.BMagics[ksq]) >> Utils.BShifts[ksq])];
            checkSq[PieceTypeC.ROOK] = Utils.RAttacks[ksq][(((pos.occupied_squares & Utils.RMasks[ksq]) * Utils.RMagics[ksq]) >> Utils.RShifts[ksq])];
#else
            checkSq[PieceTypeC.BISHOP] = pos.attacks_from_BISHOP(ksq);
            checkSq[PieceTypeC.ROOK] = pos.attacks_from_ROOK(ksq);
#endif
            checkSq[PieceTypeC.QUEEN] = checkSq[PieceTypeC.BISHOP] | checkSq[PieceTypeC.ROOK];
            checkSq[PieceTypeC.KING] = 0;
        }

        internal Bitboard dcCandidates;
        internal Bitboard pinned;
        internal readonly Bitboard[] checkSq = new Bitboard[8];
        internal Square ksq;
    };


    /// The StateInfo struct stores information we need to restore a Position
    /// object to its previous state when we retract a move. Whenever a move
    /// is made on the board (by calling do_move), an StateInfo object
    /// must be passed as a parameter.

    internal sealed class StateInfo
    {
        internal Key pawnKey, materialKey;
        internal Value npMaterialWHITE, npMaterialBLACK;
        internal int castleRights, rule50, pliesFromNull;
        internal Score psqScore;
        internal Square epSquare;

        internal Key key;
        internal Bitboard checkersBB;
        internal PieceType capturedType;
        internal StateInfo previous = null;

        internal void Clear()
        {
            this.pawnKey = 0;
            this.materialKey = 0;
            this.npMaterialWHITE = 0;
            this.npMaterialBLACK = 0;
            this.castleRights = 0;
            this.rule50 = 0;
            this.pliesFromNull = 0;
            this.psqScore = 0;
            this.epSquare = 0;

            this.key = 0;
            this.checkersBB = 0;
            this.capturedType = 0;
            this.previous = null;
        }

        internal static void CopyReducedStateInfo(StateInfo newSI, StateInfo oldSI)
        {
            newSI.pawnKey = oldSI.pawnKey;
            newSI.materialKey = oldSI.materialKey;
            newSI.npMaterialWHITE = oldSI.npMaterialWHITE;
            newSI.npMaterialBLACK = oldSI.npMaterialBLACK;
            newSI.castleRights = oldSI.castleRights;
            newSI.rule50 = oldSI.rule50;
            newSI.pliesFromNull = oldSI.pliesFromNull;
            newSI.psqScore = oldSI.psqScore;
            newSI.epSquare = oldSI.epSquare;
        }

        public void Recycle()
        {
            this.previous = null;
        }
    };

    /// The position data structure. A position consists of the following data:
    ///
    ///    * For each piece type, a bitboard representing the squares occupied
    ///      by pieces of that type.
    ///    * For each color, a bitboard representing the squares occupied by
    ///      pieces of that color.
    ///    * A bitboard of all occupied squares.
    ///    * A bitboard of all checking pieces.
    ///    * A 64-entry array of pieces, indexed by the squares of the board.
    ///    * The current side to move.
    ///    * Information about the castling rights for both sides.
    ///    * The initial files of the kings and both pairs of rooks. This is
    ///      used to implement the Chess960 castling rules.
    ///    * The en passant square (which is SQ_NONE if no en passant capture is
    ///      possible).
    ///    * The squares of the kings for both sides.
    ///    * Hash keys for the position itself, the current pawn structure, and
    ///      the current material situation.
    ///    * Hash keys for all previous positions in the game for detecting
    ///      repetition draws.
    ///    * A counter for detecting 50 move rule draws.

    internal sealed class Position
    {
        // To convert a Piece to and from a FEN char
        const string PieceToChar = " PNBRQK  pnbrqk  .";

        // Material values arrays, indexed by Piece
        internal static readonly Value[] PieceValueMidgame = new Value[] {
          ValueC.VALUE_ZERO,
          Constants.PawnValueMidgame, Constants.KnightValueMidgame, Constants.BishopValueMidgame,
          Constants.RookValueMidgame, Constants.QueenValueMidgame,
          ValueC.VALUE_ZERO, ValueC.VALUE_ZERO, ValueC.VALUE_ZERO,
          Constants.PawnValueMidgame, Constants.KnightValueMidgame, Constants.BishopValueMidgame,
          Constants.RookValueMidgame, Constants.QueenValueMidgame
          // 15
          ,0,0,0
        };

        internal static readonly Value[] PieceValueEndgame = new Value[] {
          ValueC.VALUE_ZERO,
          Constants.PawnValueEndgame, Constants.KnightValueEndgame, Constants.BishopValueEndgame,
          Constants.RookValueEndgame, Constants.QueenValueEndgame,
          ValueC.VALUE_ZERO, ValueC.VALUE_ZERO, ValueC.VALUE_ZERO,
          Constants.PawnValueEndgame, Constants.KnightValueEndgame, Constants.BishopValueEndgame,
          Constants.RookValueEndgame, Constants.QueenValueEndgame
          // 15
          ,0,0,0
        };

        // Static variables
        static readonly Score[][] pieceSquareTable = new Score[16][];    // [piece][square] 16, 64
        static readonly Key[][][] zobrist = new Key[2][][];              // [color][pieceType][square]/[piece count] 2,8,64
        static readonly Key[] zobEp = new Key[8];                        // [square]
        static readonly Key[] zobCastle = new Key[16];                   // [castleRight]
        static Key zobSideToMove;
        static Key zobExclusion;

        internal readonly Piece[] board = new Piece[64];                   // [square]

        // Bitboards
        internal readonly Bitboard[] byTypeBB = new Bitboard[8];           // [pieceType]
        internal readonly Bitboard[] byColorBB = new Bitboard[2];          // [color]
        internal Bitboard occupied_squares;                                // byTypeBB[ALL_PIECES];

        // Piece counts
        internal readonly int[][] pieceCount = new int[2][];               // [color][pieceType] 2, 8

        // Piece lists
        internal readonly Square[][][] pieceList = new Square[2][][];      // [color][pieceType][index] 2, 8, 16
        internal readonly int[] index = new int[64];                       // [square]

        // Other info
        internal readonly int[] castleRightsMask = new int[64];            // [square]
        internal readonly Square[][] castleRookSquare = new Square[2][];      // [color][side] 2,2
        internal readonly Bitboard[][] castlePath = new Bitboard[2][];          // [color][side] 2,2

        internal StateInfo startState = new StateInfo();
        internal Int64 nodes;
        internal int startPosPly;
        internal Color sideToMove;
        internal Thread thisThread = null;
        internal StateInfo st = null;
        internal bool chess960;

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Piece piece_on(Square s)
        {
            return board[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Piece piece_moved(Move m)
        {
            return board[((m >> 6) & 0x3F)];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool is_empty(Square s)
        {
            return board[s] == PieceC.NO_PIECE;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard pieces_C(Color c)
        {
            return byColorBB[c];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard pieces_PT(PieceType pt)
        {
            return byTypeBB[pt];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard pieces_PTC(PieceType pt, Color c)
        {
            return byTypeBB[pt] & byColorBB[c];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard pieces_PTPT(PieceType pt1, PieceType pt2)
        {
            return byTypeBB[pt1] | byTypeBB[pt2];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard pieces(PieceType pt1, PieceType pt2, Color c)
        {
            return (byTypeBB[pt1] | byTypeBB[pt2]) & byColorBB[c];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal int piece_count(Color c, PieceType pt)
        {
            return pieceCount[c][pt];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Square king_square(Color c)
        {
            return pieceList[c][PieceTypeC.KING][0];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal int can_castle_CR(CastleRight f)
        {
            return st.castleRights & f;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal int can_castle_CR_bit(CastleRight f)
        {
            return (st.castleRights & f) != 0 ? 1 : 0;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal int can_castle_C(Color c)
        {
            return st.castleRights & ((CastleRightC.WHITE_OO | CastleRightC.WHITE_OOO) << c);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool castle_impeded(Color c, CastlingSide s)
        {
            return (occupied_squares & castlePath[c][s]) != 0;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Square castle_rook_square(Color c, CastlingSide s)
        {
            return castleRookSquare[c][s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_PAWN(Square s, Color c)
        {
            return Utils.StepAttacksBB[((c << 3) | PieceTypeC.PAWN)][s];
        }

        // Knight and King and white pawns
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_PTS(PieceType pieceType, Square s)
        {
            if (pieceType == PieceTypeC.BISHOP) return Utils.bishop_attacks_bb(s, occupied_squares);
            if (pieceType == PieceTypeC.ROOK) return Utils.rook_attacks_bb(s, occupied_squares);
            if (pieceType == PieceTypeC.QUEEN) return Utils.bishop_attacks_bb(s, occupied_squares) | Utils.rook_attacks_bb(s, occupied_squares);
            return Utils.StepAttacksBB[pieceType][s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_BISHOP(Square s)
        {
#if X64
            return Utils.BAttacks[s][(((occupied_squares & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])];
#else
            UInt32 lo = (UInt32)(occupied_squares) & (UInt32)(Utils.BMasks[s]);
            UInt32 hi = (UInt32)(occupied_squares >> 32) & (UInt32)(Utils.BMasks[s] >> 32);
            return Utils.BAttacks[s][(lo * (UInt32)(Utils.BMagics[s]) ^ hi * (UInt32)(Utils.BMagics[s] >> 32)) >> Utils.BShifts[s]];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_ROOK(Square s)
        {
#if X64
            return Utils.RAttacks[s][(((occupied_squares & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])];
#else
            UInt32 lo = (UInt32)(occupied_squares) & (UInt32)(Utils.RMasks[s]);
            UInt32 hi = (UInt32)(occupied_squares >> 32) & (UInt32)(Utils.RMasks[s] >> 32);
            return Utils.RAttacks[s][(lo * (UInt32)(Utils.RMagics[s]) ^ hi * (UInt32)(Utils.RMagics[s] >> 32)) >> Utils.RShifts[s]];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_QUEEN(Square s)
        {
#if X64
            return Utils.BAttacks[s][(((occupied_squares & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])] | Utils.RAttacks[s][(((occupied_squares & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])];
#else
            UInt32 lor = (UInt32)(occupied_squares) & (UInt32)(Utils.RMasks[s]);
            UInt32 hir = (UInt32)(occupied_squares >> 32) & (UInt32)(Utils.RMasks[s] >> 32);
            UInt32 lob = (UInt32)(occupied_squares) & (UInt32)(Utils.BMasks[s]);
            UInt32 hib = (UInt32)(occupied_squares >> 32) & (UInt32)(Utils.BMasks[s] >> 32);
            return Utils.BAttacks[s][(lob * (UInt32)(Utils.BMagics[s]) ^ hib * (UInt32)(Utils.BMagics[s] >> 32)) >> Utils.BShifts[s]] | Utils.RAttacks[s][(lor * (UInt32)(Utils.RMagics[s]) ^ hir * (UInt32)(Utils.RMagics[s] >> 32)) >> Utils.RShifts[s]];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_KING(Square s)
        {
            return Utils.StepAttacksBB_KING[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_KNIGHT(Square s)
        {
            return Utils.StepAttacksBB_KNIGHT[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from_PS(Piece p, Square s)
        {
            return attacks_from(p, s, occupied_squares);
        }

        /// attacks_from() computes a bitboard of all attacks of a given piece
        /// put in a given square. Slider attacks use occ bitboard as occupancy.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Bitboard attacks_from(Piece p, Square s, Bitboard occ)
        {
            PieceType pieceType = Utils.type_of(p);
            if (pieceType == PieceTypeC.BISHOP) return Utils.bishop_attacks_bb(s, occ);
            if (pieceType == PieceTypeC.ROOK) return Utils.rook_attacks_bb(s, occ);
            if (pieceType == PieceTypeC.QUEEN) return Utils.bishop_attacks_bb(s, occ) | Utils.rook_attacks_bb(s, occ);
            return Utils.StepAttacksBB[p][s];
        }

        internal Bitboard attackers_to(Square s)
        {
#if X64
            return ((Utils.StepAttacksBB[((ColorC.BLACK << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.WHITE]))
                  | ((Utils.StepAttacksBB[((ColorC.WHITE << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.BLACK]))
                  | (Utils.StepAttacksBB[PieceTypeC.KNIGHT][s] & byTypeBB[PieceTypeC.KNIGHT])
                  | ((Utils.RAttacks[s][(((occupied_squares & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])]) & (byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]))
                  | ((Utils.BAttacks[s][(((occupied_squares & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])]) & (byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]))
                  | (Utils.StepAttacksBB[PieceTypeC.KING][s] & byTypeBB[PieceTypeC.KING]);
#else
            return ((Utils.StepAttacksBB[((ColorC.BLACK << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.WHITE]))
                  | ((Utils.StepAttacksBB[((ColorC.WHITE << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.BLACK]))
                  | (Utils.StepAttacksBB[PieceTypeC.KNIGHT][s] & byTypeBB[PieceTypeC.KNIGHT])
                  | (Utils.rook_attacks_bb(s, occupied_squares) & (byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]))
                  | (Utils.bishop_attacks_bb(s, occupied_squares) & (byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]))
                  | (Utils.StepAttacksBB[PieceTypeC.KING][s] & byTypeBB[PieceTypeC.KING]);
#endif
        }

        /// attackers_to() computes a bitboard of all pieces which attack a
        /// given square. Slider attacks use occ bitboard as occupancy.
        internal Bitboard attackers_to(Square s, Bitboard occ)
        {
#if X64
            return ((Utils.StepAttacksBB[((ColorC.BLACK << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.WHITE]))
                  | ((Utils.StepAttacksBB[((ColorC.WHITE << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.BLACK]))
                  | (Utils.StepAttacksBB[PieceTypeC.KNIGHT][s] & byTypeBB[PieceTypeC.KNIGHT])
                  | ((Utils.RAttacks[s][(((occ & Utils.RMasks[s]) * Utils.RMagics[s]) >> Utils.RShifts[s])]) & (byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]))
                  | ((Utils.BAttacks[s][(((occ & Utils.BMasks[s]) * Utils.BMagics[s]) >> Utils.BShifts[s])]) & (byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]))
                  | (Utils.StepAttacksBB[PieceTypeC.KING][s] & byTypeBB[PieceTypeC.KING]);
#else
            return ((Utils.StepAttacksBB[((ColorC.BLACK << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.WHITE]))
                  | ((Utils.StepAttacksBB[((ColorC.WHITE << 3) | PieceTypeC.PAWN)][s]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[ColorC.BLACK]))
                  | (Utils.StepAttacksBB[PieceTypeC.KNIGHT][s] & byTypeBB[PieceTypeC.KNIGHT])
                  | (Utils.rook_attacks_bb(s, occ) & (byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]))
                  | (Utils.bishop_attacks_bb(s, occ) & (byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]))
                  | (Utils.StepAttacksBB[PieceTypeC.KING][s] & byTypeBB[PieceTypeC.KING]);
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool in_check()
        {
            return st.checkersBB != 0;
        }

        internal Bitboard discovered_check_candidates()
        {
            // Pinned pieces protect our king, dicovery checks attack the enemy king
            Bitboard b, result = 0;
            Square ksq = pieceList[sideToMove ^ 1][PieceTypeC.KING][0];

            // Pinners are sliders, that give check when candidate pinned is removed
            Bitboard pinners = byColorBB[sideToMove] & (((byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]) & Utils.PseudoAttacks_ROOK[ksq])
                      | ((byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]) & Utils.PseudoAttacks_BISHOP[ksq]));

            while (pinners != 0)
            {
#if X64
                Bitboard bb = pinners;
                pinners &= (pinners - 1);
                b = (Utils.BetweenBB[ksq][(Utils.BSFTable[((bb & (0xffffffffffffffff - bb + 1)) * 0x218A392CD3D5DBFUL) >> 58])]) & occupied_squares;
#else
                b = (Utils.BetweenBB[ksq][Utils.pop_1st_bit(ref pinners)]) & occupied_squares;
#endif
                // Only one bit set and is an our piece?
                if ((b != 0) && ((b & (b - 1)) == 0) && ((b & byColorBB[sideToMove]) != 0))
                {
                    result |= b;
                }
            }
            return result;
        }

        internal Bitboard pinned_pieces()
        {
            // Pinned pieces protect our king, dicovery checks attack the enemy king
            Bitboard b, result = 0;
            Square ksq = pieceList[sideToMove][PieceTypeC.KING][0];

            // Pinners are sliders, that give check when candidate pinned is removed
            Bitboard pinners = byColorBB[sideToMove ^ 1] & (((byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]) & Utils.PseudoAttacks_ROOK[ksq])
                      | ((byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]) & Utils.PseudoAttacks_BISHOP[ksq]));

            while (pinners != 0)
            {
#if X64
                Bitboard bb = pinners;
                pinners &= (pinners - 1);
                b = (Utils.BetweenBB[ksq][(Utils.BSFTable[((bb & (0xffffffffffffffff - bb + 1)) * 0x218A392CD3D5DBFUL) >> 58])]) & occupied_squares;
#else
                b = (Utils.BetweenBB[ksq][Utils.pop_1st_bit(ref pinners)]) & occupied_squares;
#endif
                // Only one bit set and is an our piece?
                if ((b != 0) && ((b & (b - 1)) == 0) && ((b & byColorBB[sideToMove]) != 0))
                {
                    result |= b;
                }
            }
            return result;
        }

        /// Position:hidden_checkers<>() returns a bitboard of all pinned (against the
        /// king) pieces for the given color. Or, when template parameter FindPinned is
        /// false, the function return the pieces of the given color candidate for a
        /// discovery check against the enemy king.
        /*Bitboard hidden_checkers_original_unused(bool FindPinned)
        {

            // Pinned pieces protect our king, dicovery checks attack the enemy king
            Bitboard b, result = 0;
            Bitboard pinners = pieces_C(FindPinned ? Utils.flip_C(sideToMove) : sideToMove);
            Square ksq = king_square(FindPinned ? sideToMove : Utils.flip_C(sideToMove));

            // Pinners are sliders, that give check when candidate pinned is removed
            pinners &= (pieces_PTPT(PieceTypeC.ROOK, PieceTypeC.QUEEN) & Utils.PseudoAttacks[PieceTypeC.ROOK][ksq])
                      | (pieces_PTPT(PieceTypeC.BISHOP, PieceTypeC.QUEEN) & Utils.PseudoAttacks[PieceTypeC.BISHOP][ksq]);

            while (pinners != 0)
            {
                b = Utils.squares_between(ksq, Utils.pop_1st_bit(ref pinners)) & occupied_squares;

                // Only one bit set and is an our piece?
                if ((b != 0) && Utils.single_bit(b) && ((b & pieces_C(sideToMove)) != 0))
                {
                    result |= b;
                }
            }
            return result;
        }*/

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool pawn_is_passed(Color c, Square s)
        {
            return ((byTypeBB[PieceTypeC.PAWN] & byColorBB[c ^ 1]) & Utils.PassedPawnMask[c][s]) == 0;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Key key()
        {
            return st.key;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Key exclusion_key()
        {
            return st.key ^ zobExclusion;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Key pawn_key()
        {
            return st.pawnKey;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Key material_key()
        {
            return st.materialKey;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Score psq_delta(Piece piece, Square from, Square to)
        {
            return pieceSquareTable[piece][to] - pieceSquareTable[piece][from];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Score psq_score()
        {
            return st.psqScore;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Value non_pawn_material(Color c)
        {
            return c == 0 ? st.npMaterialWHITE : st.npMaterialBLACK;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool is_passed_pawn_push(Move m)
        {
            return (board[((m >> 6) & 0x3F)] & 7) == PieceTypeC.PAWN
                && (((byTypeBB[PieceTypeC.PAWN] & byColorBB[sideToMove ^ 1]) & Utils.PassedPawnMask[sideToMove][m & 0x3F]) == 0);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal int startpos_ply_counter()
        {
            return startPosPly + st.pliesFromNull; // HACK
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool opposite_bishops()
        {
            return pieceCount[ColorC.WHITE][PieceTypeC.BISHOP] == 1
                && pieceCount[ColorC.BLACK][PieceTypeC.BISHOP] == 1
                && Utils.opposite_colors(pieceList[ColorC.WHITE][PieceTypeC.BISHOP][0], pieceList[ColorC.BLACK][PieceTypeC.BISHOP][0]);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool bishop_pair(Color c)
        {
            // Assumes that there are only two bishops
            return pieceCount[c][PieceTypeC.BISHOP] >= 2 &&
                    Utils.opposite_colors(pieceList[c][PieceTypeC.BISHOP][0], pieceList[c][PieceTypeC.BISHOP][1]);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool pawn_on_7th(Color c)
        {
            return (pieces_PTC(PieceTypeC.PAWN, c) & Utils.rank_bb_R(Utils.relative_rank_CR(c, RankC.RANK_7))) != 0;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool is_capture_or_promotion(Move m)
        {
            return ((m & (3 << 14)) != 0) ? ((m & (3 << 14)) != (3 << 14)) : (board[m & 0x3F] != PieceC.NO_PIECE);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal bool is_capture(Move m)
        {
            // Note that castle is coded as "king captures the rook"
            return ((board[m & 0x3F] != PieceC.NO_PIECE) && !((m & (3 << 14)) == (3 << 14))) || ((m & (3 << 14)) == (2 << 14));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal PieceType captured_piece_type()
        {
            return st.capturedType;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Thread this_thread()
        {
            return thisThread;
        }

        /// Position c'tors. Here we always create a copy of the original position
        /// or the FEN string, we want the new born Position object do not depend
        /// on any external data so we detach state pointer from the source one.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void copy(Position pos)
        {
            // MEMCPY
            Array.Copy(pos.board, this.board, 64);
            Array.Copy(pos.byTypeBB, this.byTypeBB, 8);
            Array.Copy(pos.byColorBB, this.byColorBB, 2);
            this.occupied_squares = pos.occupied_squares;

            for (int i = 0; i < 2; i++)
            {
                Array.Copy(pos.castleRookSquare[i], this.castleRookSquare[i], 2);
                Array.Copy(pos.castlePath[i], this.castlePath[i], 2);
                Array.Copy(pos.pieceCount[i], this.pieceCount[i], 8);
                for (int j = 0; j < 8; j++)
                {
                    Array.Copy(pos.pieceList[i][j], this.pieceList[i][j], 16);
                }
            }

            Array.Copy(pos.index, this.index, 64);
            Array.Copy(pos.castleRightsMask, this.castleRightsMask, 64);

            this.startState = pos.startState;
            this.startPosPly = pos.startPosPly;
            this.sideToMove = pos.sideToMove;

            this.st = pos.st;
            this.chess960 = pos.chess960;
            this.thisThread = pos.thisThread;

            startState = st;
            st = startState;
            nodes = 0;

            Debug.Assert(pos_is_ok());
        }

        internal void copy(Position pos, Thread t)
        {
            copy(pos);
            this.thisThread = t;
        }

        public Position()
        {
            for (int i = 0; i < 2; i++)
            {
                castleRookSquare[i] = new Square[2];
                castlePath[i] = new Bitboard[2];
                pieceCount[i] = new int[8];
                pieceList[i] = new int[8][];
                for (int j = 0; j < 8; j++)
                {
                    pieceList[i][j] = new int[16];
                }
            }
        }

        internal Position(Position pos)
            : this()
        {
            copy(pos);
        }

        internal Position(Position pos, Thread t)
            : this()
        {
            copy(pos);
            thisThread = t;
        }

        internal Position(string f, bool c960, Thread t)
            : this()
        {
            from_fen(f, c960, t);
        }

        /// Position::from_fen() initializes the position object with the given FEN
        /// string. This function is not very robust - make sure that input FENs are
        /// correct (this is assumed to be the responsibility of the GUI).
        internal void from_fen(string fenStr, bool isChess960, Thread th)
        {
            /*
               A FEN string defines a particular position using only the ASCII character set.

               A FEN string contains six fields separated by a space. The fields are:

               1) Piece placement (from white's perspective). Each rank is described, starting
                  with rank 8 and ending with rank 1; within each rank, the contents of each
                  square are described from file A through file H. Following the Standard
                  Algebraic Notation (SAN), each piece is identified by a single letter taken
                  from the standard English names. White pieces are designated using upper-case
                  letters ("PNBRQK") while Black take lowercase ("pnbrqk"). Blank squares are
                  noted using digits 1 through 8 (the number of blank squares), and "/"
                  separates ranks.

               2) Active color. "w" means white moves next, "b" means black.

               3) Castling availability. If neither side can castle, this is "-". Otherwise,
                  this has one or more letters: "K" (White can castle kingside), "Q" (White
                  can castle queenside), "k" (Black can castle kingside), and/or "q" (Black
                  can castle queenside).

               4) En passant target square (in algebraic notation). If there's no en passant
                  target square, this is "-". If a pawn has just made a 2-square move, this
                  is the position "behind" the pawn. This is recorded regardless of whether
                  there is a pawn in position to make an en passant capture.

               5) Halfmove clock. This is the number of halfmoves since the last pawn advance
                  or capture. This is used to determine if a draw can be claimed under the
                  fifty-move rule.

               6) Fullmove number. The number of the full move. It starts at 1, and is
                  incremented after Black's move.
            */

            char col, row, token;
            int p;
            Square sq = SquareC.SQ_A8;

            char[] fen = fenStr.ToCharArray();
            int fenPos = 0;
            clear();

            // 1. Piece placement
            while ((token = fen[fenPos++]) != ' ')
            {
                if (Utils.isdigit(token))
                    sq += (token - '0'); // Advance the given number of files
                else if (token == '/')
                    sq = Utils.make_square(FileC.FILE_A, Utils.rank_of(sq) - 2);
                else
                {
                    p = PieceToChar.IndexOf(token);
                    if (p > -1)
                    {
                        put_piece(p, sq);
                        sq++;
                    }
                }

            }

            // 2. Active color
            token = fen[fenPos++];
            sideToMove = (token == 'w' ? ColorC.WHITE : ColorC.BLACK);
            token = fen[fenPos++];

            // 3. Castling availability. Compatible with 3 standards: Normal FEN standard,
            // Shredder-FEN that uses the letters of the columns on which the rooks began
            // the game instead of KQkq and also X-FEN standard that, in case of Chess960,
            // if an inner rook is associated with the castling right, the castling tag is
            // replaced by the file letter of the involved rook, as for the Shredder-FEN.
            while ((token = fen[fenPos++]) != ' ')
            {
                Square rsq;
                Color c = Utils.islower(token) ? ColorC.BLACK : ColorC.WHITE;
                token = Utils.toupper(token);

                if (token == 'K')
                {
                    for (rsq = Utils.relative_square(c, SquareC.SQ_H1); Utils.type_of(piece_on(rsq)) != PieceTypeC.ROOK; rsq--) { }
                }
                else if (token == 'Q')
                {
                    for (rsq = Utils.relative_square(c, SquareC.SQ_A1); Utils.type_of(piece_on(rsq)) != PieceTypeC.ROOK; rsq++) { }
                }
                else if (token >= 'A' && token <= 'H')
                {
                    rsq = Utils.make_square((token - 'A'), Utils.relative_rank_CR(c, RankC.RANK_1));
                }
                else
                {
                    continue;
                }

                set_castle_right(c, rsq);
            }

            if (fenPos < fenStr.Length)
            {
                col = fen[fenPos++];
                if (fenPos < fenStr.Length)
                {
                    row = fen[fenPos++];

                    // 4. En passant square. Ignore if no pawn capture is possible
                    if (((col >= 'a' && col <= 'h'))
                        && ((row == '3' || row == '6')))
                    {
                        st.epSquare = Utils.make_square((col - 'a'), (row - '1'));

                        if ((attackers_to(st.epSquare) & pieces_PTC(PieceTypeC.PAWN, sideToMove)) == 0)
                            st.epSquare = SquareC.SQ_NONE;
                    }
                }
            }

            // 5-6. Halfmove clock and fullmove number
            Stack<string> tokens = Utils.CreateStack(fenStr.Substring(fenPos));
            if (tokens.Count > 0)
            {
                st.rule50 = int.Parse(tokens.Pop());
            }
            if (tokens.Count > 0)
            {
                startPosPly = int.Parse(tokens.Pop());
            }

            // Convert from fullmove starting from 1 to ply starting from 0,
            // handle also common incorrect FEN with fullmove = 0.
            startPosPly = Math.Max(2 * (startPosPly - 1), 0) + ((sideToMove == ColorC.BLACK) ? 1 : 0);

            st.key = compute_key();
            st.pawnKey = compute_pawn_key();
            st.materialKey = compute_material_key();
            st.psqScore = compute_psq_score();
            st.npMaterialWHITE = compute_non_pawn_material(ColorC.WHITE);
            st.npMaterialBLACK = compute_non_pawn_material(ColorC.BLACK);
            st.checkersBB = attackers_to(king_square(sideToMove)) & pieces_C(Utils.flip_C(sideToMove));
            chess960 = isChess960;
            thisThread = th;

            Debug.Assert(pos_is_ok());
        }

        /// Position::to_fen() returns a FEN representation of the position. In case
        /// of Chess960 the Shredder-FEN notation is used. Mainly a debugging function.
        internal string to_fen()
        {
            //std::ostringstream fen;
            StringBuilder fen = new StringBuilder();
            Square sq;
            int emptyCnt;

            for (Rank rank = RankC.RANK_8; rank >= RankC.RANK_1; rank--)
            {
                emptyCnt = 0;

                for (File file = FileC.FILE_A; file <= FileC.FILE_H; file++)
                {
                    sq = Utils.make_square(file, rank);

                    if (is_empty(sq))
                        emptyCnt++;
                    else
                    {
                        if (emptyCnt > 0)
                        {
                            //fen << emptyCnt;
                            fen.Append(emptyCnt.ToString());
                            emptyCnt = 0;
                        }
                        fen.Append(PieceToChar[piece_on(sq)]);
                    }
                }

                if (emptyCnt > 0)
                    fen.Append(emptyCnt.ToString());

                if (rank > RankC.RANK_1)
                    fen.Append('/');
            }

            fen.Append(sideToMove == ColorC.WHITE ? " w " : " b ");

            if (can_castle_CR(CastleRightC.WHITE_OO) != 0)
                fen.Append(chess960 ? (Utils.toupper(Utils.file_to_char(Utils.file_of(castle_rook_square(ColorC.WHITE, CastlingSideC.KING_SIDE))))) : 'K');

            if (can_castle_CR(CastleRightC.WHITE_OOO) != 0)
                fen.Append(chess960 ? (Utils.toupper(Utils.file_to_char(Utils.file_of(castle_rook_square(ColorC.WHITE, CastlingSideC.QUEEN_SIDE))))) : 'Q');

            if (can_castle_CR(CastleRightC.BLACK_OO) != 0)
                fen.Append(chess960 ? Utils.file_to_char(Utils.file_of(castle_rook_square(ColorC.BLACK, CastlingSideC.KING_SIDE))) : 'k');

            if (can_castle_CR(CastleRightC.BLACK_OOO) != 0)
                fen.Append(chess960 ? Utils.file_to_char(Utils.file_of(castle_rook_square(ColorC.BLACK, CastlingSideC.QUEEN_SIDE))) : 'q');

            if (st.castleRights == CastleRightC.CASTLES_NONE)
                fen.Append('-');

            fen.Append(st.epSquare == SquareC.SQ_NONE ? " - " : " " + Utils.square_to_string(st.epSquare) + " ");
            fen.Append(st.rule50).Append(" ").Append(1 + (startPosPly - (sideToMove == ColorC.BLACK ? 1 : 0)) / 2);

            return fen.ToString();
        }

        const string dottedLine = "\n+---+---+---+---+---+---+---+---+\n";

        /// Position::print() prints an ASCII representation of the position to
        /// the standard output. If a move is given then also the san is printed.
        internal void print(Move move)
        {
            if (move != 0)
            {
                Position p = new Position(this);
                Plug.Interface.Write("\nMove is: ");
                Plug.Interface.Write((sideToMove == ColorC.BLACK ? ".." : ""));
                Plug.Interface.Write(Utils.move_to_san(p, move));
            }

            for (Rank rank = RankC.RANK_8; rank >= RankC.RANK_1; rank--)
            {
                Plug.Interface.Write(dottedLine);
                Plug.Interface.Write("|");
                for (File file = FileC.FILE_A; file <= FileC.FILE_H; file++)
                {
                    Square sq = Utils.make_square(file, rank);
                    Piece piece = piece_on(sq);
                    char c = (Utils.color_of(piece) == ColorC.BLACK ? '=' : ' ');

                    if (piece == PieceC.NO_PIECE && !Utils.opposite_colors(sq, SquareC.SQ_A1))
                        piece++; // Index the dot

                    Plug.Interface.Write(c.ToString());
                    Plug.Interface.Write(PieceToChar[piece].ToString());
                    Plug.Interface.Write(c.ToString());
                    Plug.Interface.Write("|");
                }
            }
            Plug.Interface.Write(dottedLine);
            Plug.Interface.Write("Fen is: ");
            Plug.Interface.Write(to_fen());
            Plug.Interface.Write("\nKey is: ");
            Plug.Interface.Write(st.key.ToString());
            Plug.Interface.Write(Constants.endl);
        }

        /// Position::set_castle_right() is an helper function used to set castling
        /// rights given the corresponding color and the rook starting square.
        internal void set_castle_right(Color c, Square rfrom)
        {
            Square kfrom = king_square(c);
            CastlingSide cs = kfrom < rfrom ? CastlingSideC.KING_SIDE : CastlingSideC.QUEEN_SIDE;
            CastleRight cr = Utils.make_castle_right(c, cs);

            st.castleRights |= cr;
            castleRightsMask[kfrom] |= cr;
            castleRightsMask[rfrom] |= cr;
            castleRookSquare[c][cs] = rfrom;

            Square kto = Utils.relative_square(c, cs == CastlingSideC.KING_SIDE ? SquareC.SQ_G1 : SquareC.SQ_C1);
            Square rto = Utils.relative_square(c, cs == CastlingSideC.KING_SIDE ? SquareC.SQ_F1 : SquareC.SQ_D1);

            for (Square s = Math.Min(rfrom, rto); s <= Math.Max(rfrom, rto); s++)
                if (s != kfrom && s != rfrom)
                    Utils.set_bit(ref castlePath[c][cs], s);

            for (Square s = Math.Min(kfrom, kto); s <= Math.Max(kfrom, kto); s++)
                if (s != kfrom && s != rfrom)
                    Utils.set_bit(ref castlePath[c][cs], s);
        }

        /// move_attacks_square() tests whether a move from the current
        /// position attacks a given square.
        internal bool move_attacks_square(Move m, Square s)
        {
            Debug.Assert(Utils.is_ok_M(m));
            Debug.Assert(Utils.is_ok_S(s));

            Bitboard occ, xray;
            Square from = Utils.from_sq(m);
            Square to = Utils.to_sq(m);
            Piece piece = piece_moved(m);

            Debug.Assert(!is_empty(from));

            // Update occupancy as if the piece is moving
            occ = Utils.xor_bit(Utils.xor_bit(occupied_squares, from), to);

            // The piece moved in 'to' attacks the square 's' ?
            if (Utils.bit_is_set(attacks_from(piece, to, occ), s) != 0)
                return true;

            // Scan for possible X-ray attackers behind the moved piece
            xray = (Utils.rook_attacks_bb(s, occ) & pieces(PieceTypeC.ROOK, PieceTypeC.QUEEN, Utils.color_of(piece)))
                  | (Utils.bishop_attacks_bb(s, occ) & pieces(PieceTypeC.BISHOP, PieceTypeC.QUEEN, Utils.color_of(piece)));

            // Verify attackers are triggered by our move and not already existing
            return (xray != 0) && ((xray ^ (xray & attacks_from_QUEEN(s))) != 0);
        }

        /// pl_move_is_legal() tests whether a pseudo-legal move is legal
        internal bool pl_move_is_legal(Move m, Bitboard pinned)
        {
            Debug.Assert(Utils.is_ok_M(m));
            Debug.Assert(pinned == pinned_pieces());

            Color us = sideToMove;
            Square from = ((m >> 6) & 0x3F);

            Debug.Assert(Utils.color_of(piece_moved(m)) == us);
            Debug.Assert(piece_on(king_square(us)) == Utils.make_piece(us, PieceTypeC.KING));

            // En passant captures are a tricky special case. Because they are rather
            // uncommon, we do it simply by testing whether the king is attacked after
            // the move is made.
            if ((m & (3 << 14)) == (2 << 14))
            {
                Color them = us ^ 1;
                Square to = (m & 0x3F);
                Square capsq = to + (them == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S);
                Square ksq = pieceList[us][PieceTypeC.KING][0];
                Bitboard b = (occupied_squares ^ Utils.SquareBB[from] ^ Utils.SquareBB[capsq]) | Utils.SquareBB[to];

                Debug.Assert(to == st.epSquare);
                Debug.Assert(piece_moved(m) == Utils.make_piece(us, PieceTypeC.PAWN));
                Debug.Assert(piece_on(capsq) == Utils.make_piece(them, PieceTypeC.PAWN));
                Debug.Assert(piece_on(to) == PieceC.NO_PIECE);

                return ((Utils.rook_attacks_bb(ksq, b) & ((byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]) & byColorBB[them])) == 0)
                      && ((Utils.bishop_attacks_bb(ksq, b) & ((byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]) & byColorBB[them])) == 0);
            }

            // If the moving piece is a king, check whether the destination
            // square is attacked by the opponent. Castling moves are checked
            // for legality during move generation.
            if ((board[from] & 7) == PieceTypeC.KING)
                return ((m & (3 << 14)) == (3 << 14)) || ((attackers_to((m & 0x3F)) & byColorBB[us ^ 1]) == 0);

            // A non-king move is legal if and only if it is not pinned or it
            // is moving along the ray towards or away from the king.
            return (pinned == 0)
                  || ((pinned & Utils.SquareBB[from]) == 0)
                  || Utils.squares_aligned(from, (m & 0x3F), pieceList[us][PieceTypeC.KING][0]);
        }

        /// move_is_legal() takes a random move and tests whether the move
        /// is legal. This version is not very fast and should be used only in non
        /// time-critical paths.
        internal bool move_is_legal(Move m)
        {
            MList mlist = MListBroker.GetObject();
            Movegen.generate(MoveType.MV_LEGAL, this, mlist.moves, ref mlist.pos);
            for (int i = 0; i < mlist.pos; i++)
            {
                if (mlist.moves[i].move == m)
                {
                    MListBroker.Free(mlist);
                    return true;
                }
            }
            MListBroker.Free(mlist);
            return false;
        }

        /// is_pseudo_legal() takes a random move and tests whether the move
        /// is pseudo legal. It is used to validate moves from TT that can be corrupted
        /// due to SMP concurrent access or hash position key aliasing.
        internal bool is_pseudo_legal(Move m)
        {
            Color us = sideToMove;
            Color them = sideToMove ^ 1;
            Square from = ((m >> 6) & 0x3F);
            Square to = (m & 0x3F);
            Piece pc = board[((m >> 6) & 0x3F)];

            // Use a slower but simpler function for uncommon cases
            if ((m & (3 << 14)) != 0)
                return move_is_legal(m);

            // Is not a promotion, so promotion piece must be empty
            if (((m >> 12) & 3) != PieceTypeC.NO_PIECE_TYPE)
                return false;

            // If the from square is not occupied by a piece belonging to the side to
            // move, the move is obviously not legal.
            if (pc == PieceC.NO_PIECE || (pc >> 3) != us)
                return false;

            // The destination square cannot be occupied by a friendly piece
            if ((board[to] >> 3) == us)
                return false;

            // Handle the special case of a pawn move
            if ((pc & 7) == PieceTypeC.PAWN)
            {
                // Move direction must be compatible with pawn color
                int direction = to - from;
                if ((us == ColorC.WHITE) != (direction > 0))
                    return false;

                // We have already handled promotion moves, so destination
                // cannot be on the 8/1th rank.
                if ((to >> 3) == RankC.RANK_8 || (to >> 3) == RankC.RANK_1)
                    return false;

                // Proceed according to the square delta between the origin and
                // destination squares.
                switch (direction)
                {
                    case SquareC.DELTA_NW:
                    case SquareC.DELTA_NE:
                    case SquareC.DELTA_SW:
                    case SquareC.DELTA_SE:
                        // Capture. The destination square must be occupied by an enemy
                        // piece (en passant captures was handled earlier).
                        if ((board[to] >> 3) != them)
                            return false;

                        // From and to files must be one file apart, avoids a7h5
                        if (Math.Abs((from & 7) - (to & 7)) != 1)
                            return false;
                        break;

                    case SquareC.DELTA_N:
                    case SquareC.DELTA_S:
                        // Pawn push. The destination square must be empty.
                        if (board[to] != PieceC.NO_PIECE)
                            return false;
                        break;

                    case SquareC.DELTA_NN:
                        // Double white pawn push. The destination square must be on the fourth
                        // rank, and both the destination square and the square between the
                        // source and destination squares must be empty.
                        if ((to >> 3) != RankC.RANK_4
                            || (board[to] != PieceC.NO_PIECE)
                            || (board[from + SquareC.DELTA_N] != PieceC.NO_PIECE))
                            return false;
                        break;

                    case SquareC.DELTA_SS:
                        // Double black pawn push. The destination square must be on the fifth
                        // rank, and both the destination square and the square between the
                        // source and destination squares must be empty.
                        if ((to >> 3) != RankC.RANK_5
                            || (board[to] != PieceC.NO_PIECE)
                            || (board[from + SquareC.DELTA_S] != PieceC.NO_PIECE))
                            return false;
                        break;

                    default:
                        return false;
                }
            }
            else if ((attacks_from(pc, from, occupied_squares) & Utils.SquareBB[to]) == 0)
                return false;

            // Evasions generator already takes care to avoid some kind of illegal moves
            // and pl_move_is_legal() relies on this. So we have to take care that the
            // same kind of moves are filtered out here.
            if (st.checkersBB != 0)
            {
                if ((pc & 7) != PieceTypeC.KING)
                {
                    Bitboard b = st.checkersBB;
                    Square checksq = Utils.pop_1st_bit(ref b);

                    if (b != 0) // double check ? In this case a king move is required
                        return false;

                    // Our move must be a blocking evasion or a capture of the checking piece
                    if (((Utils.BetweenBB[checksq][pieceList[us][PieceTypeC.KING][0]] | st.checkersBB) & Utils.SquareBB[to]) == 0)
                        return false;
                }
                // In case of king moves under check we have to remove king so to catch
                // as invalid moves like b1a1 when opposite queen is on c1.
                else if ((attackers_to(to, (occupied_squares ^ Utils.SquareBB[from])) & byColorBB[us ^ 1]) != 0)
                    return false;
            }

            return true;
        }

        /// move_gives_check() tests whether a pseudo-legal move gives a check
        internal bool move_gives_check(Move m, CheckInfo ci)
        {
            Debug.Assert(Utils.is_ok_M(m));
            Debug.Assert(ci.dcCandidates == discovered_check_candidates());
            Debug.Assert(Utils.color_of(piece_moved(m)) == sideToMove);

            Square from = (m >> 6) & 0x3F;
            Square to = m & 0x3F;
            PieceType pt = board[from] & 7;

            // Direct check ?
            if ((ci.checkSq[pt] & Utils.SquareBB[to]) != 0)
                return true;

            // Discovery check ?
            if ((ci.dcCandidates != 0) && ((ci.dcCandidates & Utils.SquareBB[from]) != 0))
            {
                // For pawn and king moves we need to verify also direction
                if ((pt != PieceTypeC.PAWN && pt != PieceTypeC.KING)
                    || (((Utils.BetweenBB[from][to] | Utils.BetweenBB[from][pieceList[sideToMove ^ 1][PieceTypeC.KING][0]] | Utils.BetweenBB[to][pieceList[sideToMove ^ 1][PieceTypeC.KING][0]]) & (Utils.SquareBB[from] | Utils.SquareBB[to] | Utils.SquareBB[pieceList[sideToMove ^ 1][PieceTypeC.KING][0]])) == 0))
                    return true;
            }

            // Can we skip the ugly special cases ?
            if ((m & (3 << 14)) == 0)
                return false;

            Color us = sideToMove;
            Square ksq = pieceList[sideToMove ^ 1][PieceTypeC.KING][0];

            // Promotion with check ?
            if ((m & (3 << 14)) == (1 << 14))
            {
                return (attacks_from((((m >> 12) & 3) + 2), to, occupied_squares ^ Utils.SquareBB[from]) & Utils.SquareBB[ksq]) != 0;
            }

            // En passant capture with check ? We have already handled the case
            // of direct checks and ordinary discovered check, the only case we
            // need to handle is the unusual case of a discovered check through
            // the captured pawn.
            if ((m & (3 << 14)) == (2 << 14))
            {
                Square capsq = (((from >> 3) << 3) | (to & 7));
                Bitboard b = (occupied_squares ^ Utils.SquareBB[from] ^ Utils.SquareBB[capsq]) | Utils.SquareBB[to];
                return ((Utils.rook_attacks_bb(ksq, b) & ((byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]) & byColorBB[us])) != 0)
                      || ((Utils.bishop_attacks_bb(ksq, b) & ((byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]) & byColorBB[us])) != 0);
            }

            // Castling with check ?
            if ((m & (3 << 14)) == (3 << 14))
            {
                Square kfrom = from;
                Square rfrom = to; // 'King captures the rook' notation
                Square kto = ((rfrom > kfrom ? SquareC.SQ_G1 : SquareC.SQ_C1) ^ (us * 56));
                Square rto = ((rfrom > kfrom ? SquareC.SQ_F1 : SquareC.SQ_D1) ^ (us * 56));
                Bitboard b = (occupied_squares ^ Utils.SquareBB[kfrom] ^ Utils.SquareBB[rfrom]) | Utils.SquareBB[rto] | Utils.SquareBB[kto];
                return (Utils.rook_attacks_bb(rto, b) & Utils.SquareBB[ksq]) != 0;
            }

            return false;
        }

        /// do_move() makes a move, and saves all information necessary
        /// to a StateInfo object. The move is assumed to be legal. Pseudo-legal
        /// moves should be filtered out before this function is called.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void do_move(Move m, StateInfo newSt)
        {
            CheckInfo ci = CheckInfoBroker.GetObject();
            ci.CreateCheckInfo(this);
            do_move(m, newSt, ci, move_gives_check(m, ci));
            CheckInfoBroker.Free(ci);
        }

        internal void do_move(Move m, StateInfo newSt, CheckInfo ci, bool moveIsCheck)
        {
            Debug.Assert(Utils.is_ok_M(m));
            Debug.Assert(newSt != st);

            nodes++;
            Key k = st.key;

            newSt.pawnKey = st.pawnKey;
            newSt.materialKey = st.materialKey;
            newSt.npMaterialWHITE = st.npMaterialWHITE;
            newSt.npMaterialBLACK = st.npMaterialBLACK;
            newSt.castleRights = st.castleRights;
            newSt.rule50 = st.rule50;
            newSt.pliesFromNull = st.pliesFromNull;
            newSt.psqScore = st.psqScore;
            newSt.epSquare = st.epSquare;

            newSt.previous = st;
            st = newSt;

            // Update side to move
            k ^= zobSideToMove;

            // Increment the 50 moves rule draw counter. Resetting it to zero in the
            // case of a capture or a pawn move is taken care of later.
            st.rule50++;
            st.pliesFromNull++;

            if ((m & (3 << 14)) == (3 << 14))
            {
                st.key = k;
                do_castle_move(true, m);
                return;
            }

            Color us = sideToMove;
            Color them = us ^ 1;
            Square from = ((m >> 6) & 0x3F);
            Square to = (m & 0x3F);
            Piece piece = board[from];
            PieceType pt = (piece & 7);
            PieceType capture = ((m & (3 << 14)) == (2 << 14)) ? PieceTypeC.PAWN : (board[to] & 7);

            Debug.Assert(Utils.color_of(piece) == us);
            Debug.Assert(Utils.color_of(piece_on(to)) != us);
            Debug.Assert(capture != PieceTypeC.KING);

            if (capture != 0)
            {
                Square capsq = to;

                // If the captured piece is a pawn, update pawn hash key, otherwise
                // update non-pawn material.
                if (capture == PieceTypeC.PAWN)
                {
                    if ((m & (3 << 14)) == (2 << 14))
                    {
                        capsq += (them == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S);

                        Debug.Assert(pt == PieceTypeC.PAWN);
                        Debug.Assert(to == st.epSquare);
                        Debug.Assert(Utils.relative_rank_CS(us, to) == RankC.RANK_6);
                        Debug.Assert(piece_on(to) == PieceC.NO_PIECE);
                        Debug.Assert(piece_on(capsq) == Utils.make_piece(them, PieceTypeC.PAWN));

                        board[capsq] = PieceC.NO_PIECE;
                    }

                    st.pawnKey ^= zobrist[them][PieceTypeC.PAWN][capsq];
                }
                else
                {
                    if (them == 0) { st.npMaterialWHITE -= PieceValueMidgame[capture]; } else { st.npMaterialBLACK -= PieceValueMidgame[capture]; }
                }

                // Remove the captured piece
                Bitboard capPieceMask = Utils.SquareBB[capsq];
                occupied_squares ^= capPieceMask;
                byTypeBB[capture] ^= capPieceMask;
                byColorBB[them] ^= capPieceMask;

                // Update piece list, move the last piece at index[capsq] position and
                // shrink the list.
                //
                // WARNING: This is a not revresible operation. When we will reinsert the
                // captured piece in undo_move() we will put it at the end of the list and
                // not in its original place, it means index[] and pieceList[] are not
                // guaranteed to be invariant to a do_move() + undo_move() sequence.
                int[] plThemCapture = pieceList[them][capture];
                int pcThemCapture = --pieceCount[them][capture];
                Square lastSquare = plThemCapture[pcThemCapture];
                index[lastSquare] = index[capsq];
                plThemCapture[index[lastSquare]] = lastSquare;
                plThemCapture[pcThemCapture] = SquareC.SQ_NONE;

                // Update hash keys
                k ^= zobrist[them][capture][capsq];
                st.materialKey ^= zobrist[them][capture][pcThemCapture];

                // Update incremental scores
                st.psqScore -= pieceSquareTable[((them << 3) | capture)][capsq];

                // Reset rule 50 counter
                st.rule50 = 0;
            }

            // Update hash key
            k ^= zobrist[us][pt][from] ^ zobrist[us][pt][to];

            // Reset en passant square
            if (st.epSquare != SquareC.SQ_NONE)
            {
                k ^= zobEp[st.epSquare & 7];
                st.epSquare = SquareC.SQ_NONE;
            }

            // Update castle rights if needed
            if ((st.castleRights != 0) && ((castleRightsMask[from] | castleRightsMask[to]) != 0))
            {
                int cr = castleRightsMask[from] | castleRightsMask[to];
                k ^= zobCastle[st.castleRights & cr];
                st.castleRights &= ~cr;
            }

            // Move the piece
            Bitboard from_to_bb = Utils.SquareBB[from] ^ Utils.SquareBB[to];
            occupied_squares ^= from_to_bb;
            byTypeBB[pt] ^= from_to_bb;
            byColorBB[us] ^= from_to_bb;

            board[to] = board[from];
            board[from] = PieceC.NO_PIECE;

            // Update piece lists, index[from] is not updated and becomes stale. This
            // works as long as index[] is accessed just by known occupied squares.
            index[to] = index[from];
            pieceList[us][pt][index[to]] = to;

            // If the moving piece is a pawn do some special extra work
            if (pt == PieceTypeC.PAWN)
            {
                // Set en-passant square, only if moved pawn can be captured
                if ((to ^ from) == 16
                    && ((((Utils.StepAttacksBB[((us << 3) | PieceTypeC.PAWN)][from + (us == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S)]) & (byTypeBB[PieceTypeC.PAWN] & byColorBB[them]))) != 0))
                {
                    st.epSquare = ((from + to) / 2);
                    k ^= zobEp[st.epSquare & 7];
                }

                if ((m & (3 << 14)) == (1 << 14))
                {
                    PieceType promotion = (((m >> 12) & 3) + 2);

                    Debug.Assert(Utils.relative_rank_CS(us, to) == RankC.RANK_8);
                    Debug.Assert(promotion >= PieceTypeC.KNIGHT && promotion <= PieceTypeC.QUEEN);

                    // Replace the pawn with the promoted piece
                    byTypeBB[PieceTypeC.PAWN] ^= Utils.SquareBB[to];
                    byTypeBB[promotion] |= Utils.SquareBB[to];
                    board[to] = ((us << 3) | promotion);

                    // Update piece lists, move the last pawn at index[to] position
                    // and shrink the list. Add a new promotion piece to the list.
                    int[] plUsPawn = pieceList[us][PieceTypeC.PAWN];
                    Square lastSquare = plUsPawn[--pieceCount[us][PieceTypeC.PAWN]];
                    index[lastSquare] = index[to];
                    plUsPawn[index[lastSquare]] = lastSquare;
                    plUsPawn[pieceCount[us][PieceTypeC.PAWN]] = SquareC.SQ_NONE;
                    index[to] = pieceCount[us][promotion];
                    pieceList[us][promotion][index[to]] = to;

                    // Update hash keys
                    k ^= zobrist[us][PieceTypeC.PAWN][to] ^ zobrist[us][promotion][to];
                    st.pawnKey ^= zobrist[us][PieceTypeC.PAWN][to];
                    st.materialKey ^= zobrist[us][promotion][pieceCount[us][promotion]++]
                                      ^ zobrist[us][PieceTypeC.PAWN][pieceCount[us][PieceTypeC.PAWN]];

                    // Update incremental score
                    st.psqScore += pieceSquareTable[((us << 3) | promotion)][to] - pieceSquareTable[((us << 3) | PieceTypeC.PAWN)][to];

                    // Update material
                    if (us == 0) { st.npMaterialWHITE += PieceValueMidgame[promotion]; }
                    else { st.npMaterialBLACK += PieceValueMidgame[promotion]; }
                }

                // Update pawn hash key
                st.pawnKey ^= zobrist[us][PieceTypeC.PAWN][from] ^ zobrist[us][PieceTypeC.PAWN][to];

                // Reset rule 50 draw counter
                st.rule50 = 0;
            }

            // Update incremental scores
            st.psqScore += (pieceSquareTable[piece][to] - pieceSquareTable[piece][from]);

            // Set capture piece
            st.capturedType = capture;

            // Update the key with the final value
            st.key = k;

            // Update checkers bitboard, piece must be already moved
            st.checkersBB = 0;

            if (moveIsCheck)
            {
                if ((m & (3 << 14)) != 0)
                    st.checkersBB = attackers_to(pieceList[them][PieceTypeC.KING][0]) & byColorBB[us];
                else
                {
                    // Direct checks
                    if ((ci.checkSq[pt] & Utils.SquareBB[to]) != 0)
                    {
                        st.checkersBB |= Utils.SquareBB[to];
                    }

                    // Discovery checks
                    if ((ci.dcCandidates != 0) && ((ci.dcCandidates & Utils.SquareBB[from]) != 0))
                    {
                        if (pt != PieceTypeC.ROOK)
                            st.checkersBB |= attacks_from_ROOK(pieceList[them][PieceTypeC.KING][0]) & ((byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]) & byColorBB[us]);

                        if (pt != PieceTypeC.BISHOP)
                            st.checkersBB |= attacks_from_BISHOP(pieceList[them][PieceTypeC.KING][0]) & ((byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]) & byColorBB[us]);
                    }
                }
            }

            // Finish
            sideToMove = sideToMove ^ 1;

            Debug.Assert(pos_is_ok());
        }

        /// undo_move() unmakes a move. When it returns, the position should
        /// be restored to exactly the same state as before the move was made.
        internal void undo_move(Move m)
        {
            Debug.Assert(Utils.is_ok_M(m));

            sideToMove = sideToMove ^ 1;

            if ((m & (3 << 14)) == (3 << 14))
            {
                do_castle_move(false, m);
                return;
            }

            Color us = sideToMove;
            Color them = us ^ 1;
            Square from = ((m >> 6) & 0x3F);
            Square to = (m & 0x3F);
            Piece piece = board[to];
            PieceType pt = piece & 7;
            PieceType capture = st.capturedType;

            Debug.Assert(is_empty(from));
            Debug.Assert(Utils.color_of(piece) == us);
            Debug.Assert(capture != PieceTypeC.KING);

            if ((m & (3 << 14)) == (1 << 14))
            {
                PieceType promotion = (((m >> 12) & 3) + 2);

                Debug.Assert(promotion == pt);
                Debug.Assert(Utils.relative_rank_CS(us, to) == RankC.RANK_8);
                Debug.Assert(promotion >= PieceTypeC.KNIGHT && promotion <= PieceTypeC.QUEEN);

                // Replace the promoted piece with the pawn
                byTypeBB[promotion] ^= Utils.SquareBB[to];
                byTypeBB[PieceTypeC.PAWN] |= Utils.SquareBB[to];
                board[to] = ((us << 3) | PieceTypeC.PAWN);

                // Update piece lists, move the last promoted piece at index[to] position
                // and shrink the list. Add a new pawn to the list.
                Square lastSquare = pieceList[us][promotion][--pieceCount[us][promotion]];
                index[lastSquare] = index[to];
                int[] plUsPromotion = pieceList[us][promotion];
                plUsPromotion[index[lastSquare]] = lastSquare;
                plUsPromotion[pieceCount[us][promotion]] = SquareC.SQ_NONE;
                index[to] = pieceCount[us][PieceTypeC.PAWN]++;
                pieceList[us][PieceTypeC.PAWN][index[to]] = to;

                pt = PieceTypeC.PAWN;
            }

            // Put the piece back at the source square
            Bitboard from_to_bb = Utils.SquareBB[from] ^ Utils.SquareBB[to];
            occupied_squares ^= from_to_bb;
            byTypeBB[pt] ^= from_to_bb;
            byColorBB[us] ^= from_to_bb;

            board[from] = board[to];
            board[to] = PieceC.NO_PIECE;

            // Update piece lists, index[to] is not updated and becomes stale. This
            // works as long as index[] is accessed just by known occupied squares.
            index[from] = index[to];
            pieceList[us][pt][index[from]] = from;

            if (capture != 0)
            {
                Square capsq = to;

                if ((m & (3 << 14)) == (2 << 14))
                {
                    capsq -= (us == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S);

                    Debug.Assert(pt == PieceTypeC.PAWN);
                    Debug.Assert(to == st.previous.epSquare);
                    Debug.Assert(Utils.relative_rank_CS(us, to) == RankC.RANK_6);
                    Debug.Assert(piece_on(capsq) == PieceC.NO_PIECE);
                }

                // Restore the captured piece
                Bitboard capSqMask = Utils.SquareBB[capsq];
                occupied_squares |= capSqMask;
                byTypeBB[capture] |= capSqMask;
                byColorBB[them] |= capSqMask;
                board[capsq] = ((them << 3) | capture);

                // Update piece list, add a new captured piece in capsq square
                index[capsq] = pieceCount[them][capture]++;
                pieceList[them][capture][index[capsq]] = capsq;
            }

            // Finally point our state pointer back to the previous state
            st = st.previous;

            Debug.Assert(pos_is_ok());
        }

        /// do_castle_move() is a private method used to do/undo a castling
        /// move. Note that castling moves are encoded as "king captures friendly rook"
        /// moves, for instance white short castling in a non-Chess960 game is encoded
        /// as e1h1.
        internal void do_castle_move(bool Do, Move m)
        {
            Debug.Assert(Utils.is_ok_M(m));
            Debug.Assert(Utils.is_castle(m));

            Square kto, kfrom, rfrom, rto, kAfter, rAfter;

            Color us = sideToMove;
            Square kBefore = Utils.from_sq(m);
            Square rBefore = Utils.to_sq(m);

            // Find after-castle squares for king and rook
            if (rBefore > kBefore) // O-O
            {
                kAfter = Utils.relative_square(us, SquareC.SQ_G1);
                rAfter = Utils.relative_square(us, SquareC.SQ_F1);
            }
            else // O-O-O
            {
                kAfter = Utils.relative_square(us, SquareC.SQ_C1);
                rAfter = Utils.relative_square(us, SquareC.SQ_D1);
            }

            kfrom = Do ? kBefore : kAfter;
            rfrom = Do ? rBefore : rAfter;

            kto = Do ? kAfter : kBefore;
            rto = Do ? rAfter : rBefore;

            Debug.Assert(piece_on(kfrom) == Utils.make_piece(us, PieceTypeC.KING));
            Debug.Assert(piece_on(rfrom) == Utils.make_piece(us, PieceTypeC.ROOK));

            // Move the pieces, with some care; in chess960 could be kto == rfrom
            Bitboard k_from_to_bb = Utils.SquareBB[kfrom] ^ Utils.SquareBB[kto];
            Bitboard r_from_to_bb = Utils.SquareBB[rfrom] ^ Utils.SquareBB[rto];
            byTypeBB[PieceTypeC.KING] ^= k_from_to_bb;
            byTypeBB[PieceTypeC.ROOK] ^= r_from_to_bb;
            occupied_squares ^= k_from_to_bb ^ r_from_to_bb;
            byColorBB[us] ^= k_from_to_bb ^ r_from_to_bb;

            // Update board
            Piece king = Utils.make_piece(us, PieceTypeC.KING);
            Piece rook = Utils.make_piece(us, PieceTypeC.ROOK);
            board[kfrom] = board[rfrom] = PieceC.NO_PIECE;
            board[kto] = king;
            board[rto] = rook;

            // Update piece lists
            pieceList[us][PieceTypeC.KING][index[kfrom]] = kto;
            pieceList[us][PieceTypeC.ROOK][index[rfrom]] = rto;
            int tmp = index[rfrom]; // In Chess960 could be kto == rfrom
            index[kto] = index[kfrom];
            index[rto] = tmp;

            if (Do)
            {
                // Reset capture field
                st.capturedType = PieceTypeC.NO_PIECE_TYPE;

                // Update incremental scores
                st.psqScore += psq_delta(king, kfrom, kto);
                st.psqScore += psq_delta(rook, rfrom, rto);

                // Update hash key
                st.key ^= zobrist[us][PieceTypeC.KING][kfrom] ^ zobrist[us][PieceTypeC.KING][kto];
                st.key ^= zobrist[us][PieceTypeC.ROOK][rfrom] ^ zobrist[us][PieceTypeC.ROOK][rto];

                // Clear en passant square
                if (st.epSquare != SquareC.SQ_NONE)
                {
                    st.key ^= zobEp[Utils.file_of(st.epSquare)];
                    st.epSquare = SquareC.SQ_NONE;
                }

                // Update castling rights
                st.key ^= zobCastle[st.castleRights & castleRightsMask[kfrom]];
                st.castleRights &= ~castleRightsMask[kfrom];

                // Update checkers BB
                st.checkersBB = attackers_to(king_square(Utils.flip_C(us))) & pieces_C(us);

                // Finish
                sideToMove = Utils.flip_C(sideToMove);
            }
            else
                // Undo: point our state pointer back to the previous state
                st = st.previous;

            Debug.Assert(pos_is_ok());
        }

        /// do_null_move() is used to do/undo a "null move": It flips the side
        /// to move and updates the hash key without executing any move on the board.
        internal void do_null_move(bool Do, StateInfo backupSt)
        {
            Debug.Assert(!in_check());

            // Back up the information necessary to undo the null move to the supplied
            // StateInfo object. Note that differently from normal case here backupSt
            // is actually used as a backup storage not as the new state. This reduces
            // the number of fields to be copied.
            StateInfo src = Do ? st : backupSt;
            StateInfo dst = Do ? backupSt : st;

            dst.key = src.key;
            dst.epSquare = src.epSquare;
            dst.psqScore = src.psqScore;
            dst.rule50 = src.rule50;
            dst.pliesFromNull = src.pliesFromNull;

            sideToMove = Utils.flip_C(sideToMove);

            if (Do)
            {
                if (st.epSquare != SquareC.SQ_NONE)
                    st.key ^= zobEp[Utils.file_of(st.epSquare)];

                st.key ^= zobSideToMove;
                st.epSquare = SquareC.SQ_NONE;
                st.rule50++;
                st.pliesFromNull = 0;
            }
            Debug.Assert(pos_is_ok());
        }

        /// see() is a static exchange evaluator: It tries to estimate the
        /// material gain or loss resulting from a move. There are three versions of
        /// this function: One which takes a destination square as input, one takes a
        /// move, and one which takes a 'from' and a 'to' square. The function does
        /// not yet understand promotions captures.
        internal int see(Move m, bool with_sign)
        {
            if ((with_sign) && (PieceValueMidgame[board[m & 0x3F]] >= PieceValueMidgame[board[((m >> 6) & 0x3F)]]))
                return 1;

            Square from, to;
            Bitboard occ, attackers, stmAttackers, b;

            int slIndex = 1;
            PieceType capturedType, pt;
            Color stm;

            Debug.Assert(Utils.is_ok_M(m));

            // As castle moves are implemented as capturing the rook, they have
            // SEE == RookValueMidgame most of the times (unless the rook is under
            // attack).
            if ((m & (3 << 14)) == (3 << 14))
                return 0;

            from = ((m >> 6) & 0x3F);
            to = (m & 0x3F);
            capturedType = board[to] & 7;
            occ = occupied_squares;

            // Handle en passant moves
            if ((m & (3 << 14)) == (2 << 14))
            {
                Square capQq = to - (sideToMove == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S);

                Debug.Assert(capturedType == 0);
                Debug.Assert(Utils.type_of(piece_on(capQq)) == PieceTypeC.PAWN);

                // Remove the captured pawn
                occ ^= Utils.SquareBB[capQq];
                capturedType = PieceTypeC.PAWN;
            }

            // Find all attackers to the destination square, with the moving piece
            // removed, but possibly an X-ray attacker added behind it.
            occ ^= Utils.SquareBB[from];
            attackers = attackers_to(to, occ);

            // If the opponent has no attackers we are finished
            stm = ((board[from] >> 3) ^ 1);
            stmAttackers = attackers & byColorBB[stm];
            if (stmAttackers == 0)
            {
                return PieceValueMidgame[capturedType];
            }

            // The destination square is defended, which makes things rather more
            // difficult to compute. We proceed by building up a "swap list" containing
            // the material gain or loss at each stop in a sequence of captures to the
            // destination square, where the sides alternately capture, and always
            // capture with the least valuable piece. After each capture, we look for
            // new X-ray attacks from behind the capturing piece.
            SwapList swap = SwapListBroker.GetObject();
            int[] swapList = swap.list;

            swapList[0] = PieceValueMidgame[capturedType];
            capturedType = board[from] & 7;

            do
            {
                // Locate the least valuable attacker for the side to move. The loop
                // below looks like it is potentially infinite, but it isn't. We know
                // that the side to move still has at least one attacker left.
                for (pt = PieceTypeC.PAWN; (stmAttackers & byTypeBB[pt]) == 0; pt++)
                    Debug.Assert(pt < PieceTypeC.KING);

                // Remove the attacker we just found from the 'occupied' bitboard,
                // and scan for new X-ray attacks behind the attacker.
                b = stmAttackers & byTypeBB[pt];
                occ ^= (b & (~b + 1));

#if X64
                attackers |= ((Utils.RAttacks[to][(((occ & Utils.RMasks[to]) * Utils.RMagics[to]) >> Utils.RShifts[to])]) & (byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]))
                            | ((Utils.BAttacks[to][(((occ & Utils.BMasks[to]) * Utils.BMagics[to]) >> Utils.BShifts[to])]) & (byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]));
#else
                attackers |= (Utils.rook_attacks_bb(to, occ) & (byTypeBB[PieceTypeC.ROOK] | byTypeBB[PieceTypeC.QUEEN]))
                            | (Utils.bishop_attacks_bb(to, occ) & (byTypeBB[PieceTypeC.BISHOP] | byTypeBB[PieceTypeC.QUEEN]));
#endif

                attackers &= occ; // Cut out pieces we've already done

                // Add the new entry to the swap list
                Debug.Assert(slIndex < 32);
                swapList[slIndex] = -swapList[slIndex - 1] + PieceValueMidgame[capturedType];
                slIndex++;

                // Remember the value of the capturing piece, and change the side to
                // move before beginning the next iteration.
                capturedType = pt;
                stm = stm ^ 1;
                stmAttackers = attackers & byColorBB[stm];

                // Stop before processing a king capture
                if (capturedType == PieceTypeC.KING && (stmAttackers != 0))
                {
                    Debug.Assert(slIndex < 32);
                    swapList[slIndex++] = Constants.QueenValueMidgame * 10;
                    break;
                }
            } while (stmAttackers != 0);

            // Having built the swap list, we negamax through it to find the best
            // achievable score from the point of view of the side to move.
            while ((--slIndex) != 0)
                swapList[slIndex - 1] = Math.Min(-swapList[slIndex], swapList[slIndex - 1]);

            int retval = swapList[0];
            SwapListBroker.Free(swap);

            return retval;
        }

        /// clear() erases the position object to a pristine state, with an
        /// empty board, white to move, and no castling rights.
        internal void clear()
        {
            Array.Clear(byColorBB, 0, 2);
            Array.Clear(byTypeBB, 0, 8);
            Array.Clear(pieceCount[0], 0, 8);
            Array.Clear(pieceCount[1], 0, 8);
            Array.Clear(index, 0, 64);
            Array.Clear(castleRightsMask, 0, 64);
            Array.Clear(castleRookSquare[0], 0, 2);
            Array.Clear(castleRookSquare[1], 0, 2);
            Array.Clear(castlePath[0], 0, 2);
            Array.Clear(castlePath[1], 0, 2);

            startState.Clear();
            occupied_squares = 0;
            nodes = 0;
            startPosPly = 0;
            sideToMove = 0;
            thisThread = null;
            chess960 = false;

            startState.epSquare = SquareC.SQ_NONE;
            st = startState;

            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 16; j++)
                    pieceList[0][i][j] = pieceList[1][i][j] = SquareC.SQ_NONE;

            for (Square sq = SquareC.SQ_A1; sq <= SquareC.SQ_H8; sq++)
                board[sq] = PieceC.NO_PIECE;
        }

        /// put_piece() puts a piece on the given square of the board,
        /// updating the board array, pieces list, bitboards, and piece counts.
        internal void put_piece(Piece p, Square s)
        {
            Color c = Utils.color_of(p);
            PieceType pt = Utils.type_of(p);

            board[s] = p;
            index[s] = pieceCount[c][pt]++;
            pieceList[c][pt][index[s]] = s;

            Utils.set_bit(ref occupied_squares, s);
            Utils.set_bit(ref byTypeBB[pt], s);
            Utils.set_bit(ref byColorBB[c], s);
        }

        /// compute_key() computes the hash key of the position. The hash
        /// key is usually updated incrementally as moves are made and unmade, the
        /// compute_key() function is only used when a new position is set up, and
        /// to verify the correctness of the hash key when running in debug mode.
        internal Key compute_key()
        {
            Key k = zobCastle[st.castleRights];

            for (Bitboard b = occupied_squares; b != 0; )
            {
                Square s = Utils.pop_1st_bit(ref b);
                k ^= zobrist[Utils.color_of(piece_on(s))][Utils.type_of(piece_on(s))][s];
            }

            if (st.epSquare != SquareC.SQ_NONE)
                k ^= zobEp[Utils.file_of(st.epSquare)];

            if (sideToMove == ColorC.BLACK)
                k ^= zobSideToMove;

            return k;
        }

        /// compute_pawn_key() computes the hash key of the position. The
        /// hash key is usually updated incrementally as moves are made and unmade,
        /// the compute_pawn_key() function is only used when a new position is set
        /// up, and to verify the correctness of the pawn hash key when running in
        /// debug mode.
        internal Key compute_pawn_key()
        {
            Key k = 0;

            for (Bitboard b = pieces_PT(PieceTypeC.PAWN); b != 0; )
            {
                Square s = Utils.pop_1st_bit(ref b);
                k ^= zobrist[Utils.color_of(piece_on(s))][PieceTypeC.PAWN][s];
            }

            return k;
        }

        /// compute_material_key() computes the hash key of the position.
        /// The hash key is usually updated incrementally as moves are made and unmade,
        /// the compute_material_key() function is only used when a new position is set
        /// up, and to verify the correctness of the material hash key when running in
        /// debug mode.
        internal Key compute_material_key()
        {
            Key k = 0;

            for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
                for (PieceType pt = PieceTypeC.PAWN; pt <= PieceTypeC.QUEEN; pt++)
                    for (int cnt = 0; cnt < piece_count(c, pt); cnt++)
                        k ^= zobrist[c][pt][cnt];

            return k;
        }

        /// Position::compute_psq_score() computes the incremental scores for the middle
        /// game and the endgame. These functions are used to initialize the incremental
        /// scores when a new position is set up, and to verify that the scores are correctly
        /// updated by do_move and undo_move when the program is running in debug mode.
        internal Score compute_psq_score()
        {
            Score score = ScoreC.SCORE_ZERO;

            for (Bitboard b = occupied_squares; b != 0; )
            {
                Square s = Utils.pop_1st_bit(ref b);
                score += pieceSquareTable[piece_on(s)][s];
            }

            return score;
        }

        /// compute_non_pawn_material() computes the total non-pawn middle
        /// game material value for the given side. Material values are updated
        /// incrementally during the search, this function is only used while
        /// initializing a new Position object.
        internal Value compute_non_pawn_material(Color c)
        {
            Value value = ValueC.VALUE_ZERO;

            for (PieceType pt = PieceTypeC.KNIGHT; pt <= PieceTypeC.QUEEN; pt++)
                value += piece_count(c, pt) * PieceValueMidgame[pt];

            return value;
        }

        /// is_draw() tests whether the position is drawn by material,
        /// repetition, or the 50 moves rule. It does not detect stalemates, this
        /// must be done by the search.
        internal bool is_draw(bool SkipRepetition)
        {
            // Draw by material?
            if (
                (byTypeBB[PieceTypeC.PAWN] == 0)
                &&
                ((st.npMaterialWHITE + st.npMaterialBLACK) <= Constants.BishopValueMidgame)
                )
            {
                return true;
            }

            // Draw by the 50 moves rule?
            if (st.rule50 > 99)
            {
                if (st.checkersBB == 0) return true;
                MList mlist = MListBroker.GetObject();
                Position pos2 = this;
                Movegen.generate(MoveType.MV_LEGAL, pos2, mlist.moves, ref mlist.pos);
                bool any = mlist.pos > 0;
                MListBroker.Free(mlist);
                if (any)
                {
                    return true;
                }
            }

            // Draw by repetition?
            if (!SkipRepetition)
            {
                int i = 4, e = Math.Min(st.rule50, st.pliesFromNull);
                if (i <= e)
                {
                    StateInfo stp = st.previous.previous;
                    do
                    {
                        stp = stp.previous.previous;
                        if (stp.key == st.key)
                            return true;
                        i += 2;
                    } while (i <= e);
                }
            }
            return false;
        }

        /// init() is a static member function which initializes at startup
        /// the various arrays used to compute hash keys and the piece square tables.
        /// The latter is a two-step operation: First, the white halves of the tables
        /// are copied from PSQT[] tables. Second, the black halves of the tables are
        /// initialized by flipping and changing the sign of the white scores.
        internal static void init()
        {
            RKISS rk = new RKISS();

            for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
            {
                zobrist[c] = new Bitboard[8][];
                for (PieceType pt = PieceTypeC.PAWN; pt <= PieceTypeC.KING; pt++)
                {
                    zobrist[c][pt] = new Bitboard[64];
                    for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
                    {
                        zobrist[c][pt][s] = rk.rand();
                    }
                }
            }

            for (File f = FileC.FILE_A; f <= FileC.FILE_H; f++)
                zobEp[f] = rk.rand();

            Bitboard one = 1;
            for (int cr = CastleRightC.CASTLES_NONE; cr <= CastleRightC.ALL_CASTLES; cr++)
            {
                Bitboard b = (Bitboard)cr;
                while (b != 0)
                {
                    Key k = zobCastle[one << Utils.pop_1st_bit(ref b)];
                    zobCastle[cr] ^= ((k != 0) ? k : rk.rand());
                }
            }

            zobSideToMove = rk.rand();
            zobExclusion = rk.rand();

            for (int i = 0; i < 16; i++)
            {
                pieceSquareTable[i] = new Score[64];
            }

            for (PieceType pt = PieceTypeC.PAWN; pt <= PieceTypeC.KING; pt++)
            {
                Score v = Utils.make_score(PieceValueMidgame[pt], PieceValueEndgame[pt]);

                for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
                {
                    pieceSquareTable[Utils.make_piece(ColorC.WHITE, pt)][s] = (v + Utils.PSQT[pt][s]);
                    pieceSquareTable[Utils.make_piece(ColorC.BLACK, pt)][Utils.flip_S(s)] = -(v + Utils.PSQT[pt][s]);
                }
            }
        }

        /// flip() flips position with the white and black sides reversed. This
        /// is only useful for debugging especially for finding evaluation symmetry bugs.
        internal void flip()
        {
            // Make a copy of current position before to start changing
            Position pos = new Position(this);
            clear();

            sideToMove = pos.sideToMove ^ 1;
            thisThread = pos.this_thread();
            nodes = pos.nodes;
            chess960 = pos.chess960;
            startPosPly = pos.startpos_ply_counter();

            for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
                if (!pos.is_empty(s))
                    put_piece((pos.piece_on(s) ^ 8), Utils.flip_S(s));

            if (pos.can_castle_CR(CastleRightC.WHITE_OO) != 0)
                set_castle_right(ColorC.BLACK, Utils.flip_S(pos.castle_rook_square(ColorC.WHITE, CastlingSideC.KING_SIDE)));
            if (pos.can_castle_CR(CastleRightC.WHITE_OOO) != 0)
                set_castle_right(ColorC.BLACK, Utils.flip_S(pos.castle_rook_square(ColorC.WHITE, CastlingSideC.QUEEN_SIDE)));
            if (pos.can_castle_CR(CastleRightC.BLACK_OO) != 0)
                set_castle_right(ColorC.WHITE, Utils.flip_S(pos.castle_rook_square(ColorC.BLACK, CastlingSideC.KING_SIDE)));
            if (pos.can_castle_CR(CastleRightC.BLACK_OOO) != 0)
                set_castle_right(ColorC.WHITE, Utils.flip_S(pos.castle_rook_square(ColorC.BLACK, CastlingSideC.QUEEN_SIDE)));


            if (pos.st.epSquare != SquareC.SQ_NONE)
                st.epSquare = Utils.flip_S(pos.st.epSquare);

            // Checkers
            st.checkersBB = attackers_to(king_square(sideToMove)) & pieces_C(Utils.flip_C(sideToMove));

            // Hash keys
            st.key = compute_key();
            st.pawnKey = compute_pawn_key();
            st.materialKey = compute_material_key();

            // Incremental scores
            st.psqScore = compute_psq_score();

            // Material
            st.npMaterialWHITE = compute_non_pawn_material(ColorC.WHITE);
            st.npMaterialBLACK = compute_non_pawn_material(ColorC.BLACK);

            Debug.Assert(pos_is_ok());
        }

        /// pos_is_ok() performs some consitency checks for the position object.
        /// This is meant to be helpful when debugging.
        internal bool pos_is_ok()
        {
            int junk = 0;
            return pos_is_ok(ref junk);
        }

        bool pos_is_ok(ref int step)//failedStep)
        {
            //int dummy, *step = failedStep ? failedStep : &dummy;

            // What features of the position should be verified?
            bool all = false;

            bool debugBitboards = all || false;
            bool debugKingCount = all || false;
            bool debugKingCapture = false; // all || false; // TODO: fixthis
            bool debugCheckerCount = all || false;
            bool debugKey = all || false;
            bool debugMaterialKey = all || false;
            bool debugPawnKey = all || false;
            bool debugIncrementalEval = all || false;
            bool debugNonPawnMaterial = all || false;
            bool debugPieceCounts = all || false;
            bool debugPieceList = all || false;
            bool debugCastleSquares = all || false;

            step = 1;
            if (sideToMove != ColorC.WHITE && sideToMove != ColorC.BLACK)
                return false;

            step++;
            if (piece_on(king_square(ColorC.WHITE)) != PieceC.W_KING)
                return false;

            step++;
            if (piece_on(king_square(ColorC.BLACK)) != PieceC.B_KING)
                return false;

            step++;
            if (debugKingCount)
            {
                int[] kingCount = new int[2];

                for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
                    if (Utils.type_of(piece_on(s)) == PieceTypeC.KING)
                        kingCount[Utils.color_of(piece_on(s))]++;

                if (kingCount[0] != 1 || kingCount[1] != 1)
                    return false;
            }

            step++;
            if (debugKingCapture)
                if ((attackers_to(king_square(sideToMove ^ 1)) & pieces_C(sideToMove)) != 0)
                    return false;

            step++;
            if (debugCheckerCount && Bitcount.popcount_1s_Full(st.checkersBB) > 2)
                return false;

            step++;
            if (debugBitboards)
            {
                // The intersection of the white and black pieces must be empty
                if ((pieces_C(ColorC.WHITE) & pieces_C(ColorC.BLACK)) != 0)
                    return false;

                // The union of the white and black pieces must be equal to all
                // occupied squares
                if ((pieces_C(ColorC.WHITE) | pieces_C(ColorC.BLACK)) != occupied_squares)
                    return false;

                // Separate piece type bitboards must have empty intersections
                for (PieceType p1 = PieceTypeC.PAWN; p1 <= PieceTypeC.KING; p1++)
                    for (PieceType p2 = PieceTypeC.PAWN; p2 <= PieceTypeC.KING; p2++)
                        if (p1 != p2 && ((pieces_PT(p1) & pieces_PT(p2)) != 0))
                            return false;
            }

            step++;
            if (st.epSquare != SquareC.SQ_NONE && Utils.relative_rank_CS(sideToMove, st.epSquare) != RankC.RANK_6)
                return false;

            step++;
            if (debugKey && st.key != compute_key())
                return false;

            step++;
            if (debugPawnKey && st.pawnKey != compute_pawn_key())
                return false;

            step++;
            if (debugMaterialKey && st.materialKey != compute_material_key())
                return false;

            step++;
            if (debugIncrementalEval && st.psqScore != compute_psq_score())
                return false;

            step++;
            if (debugNonPawnMaterial)
            {
                if (st.npMaterialWHITE != compute_non_pawn_material(ColorC.WHITE)
                    || st.npMaterialBLACK != compute_non_pawn_material(ColorC.BLACK))
                    return false;
            }

            step++;
            if (debugPieceCounts)
                for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
                    for (PieceType pt = PieceTypeC.PAWN; pt <= PieceTypeC.KING; pt++)
                        if (pieceCount[c][pt] != Bitcount.popcount_1s_Full(pieces_PTC(pt, c)))
                            return false;

            step++;
            if (debugPieceList)
                for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
                    for (PieceType pt = PieceTypeC.PAWN; pt <= PieceTypeC.KING; pt++)
                        for (int i = 0; i < pieceCount[c][pt]; i++)
                        {
                            if (piece_on(pieceList[c][pt][i]) != Utils.make_piece(c, pt))
                                return false;

                            if (index[pieceList[c][pt][i]] != i)
                                return false;
                        }

            step++;
            if (debugCastleSquares)
                for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
                    for (CastlingSide s = CastlingSideC.KING_SIDE; s <= CastlingSideC.QUEEN_SIDE; s = (s + 1))
                    {
                        CastleRight cr = Utils.make_castle_right(c, s);

                        if (can_castle_CR(cr) == 0)
                            continue;

                        if ((castleRightsMask[king_square(c)] & cr) != cr)
                            return false;

                        if (piece_on(castleRookSquare[c][s]) != Utils.make_piece(c, PieceTypeC.ROOK)
                            || castleRightsMask[castleRookSquare[c][s]] != cr)
                            return false;
                    }

            step = 0;
            return true;
        }
    }
}
