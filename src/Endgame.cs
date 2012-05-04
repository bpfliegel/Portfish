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
    internal delegate Value EndgameValue(Position pos);
    internal delegate ScaleFactor EndgameScaleFactor(Position pos);

    internal static class Endgames
    {
        private static readonly Dictionary<Key, EndgameValue> _dictValue = new Dictionary<Bitboard, EndgameValue>();
        private static readonly Dictionary<Key, EndgameScaleFactor> _dictScaleFactor = new Dictionary<Bitboard, EndgameScaleFactor>();
        private static readonly Dictionary<Key, Color> _dictScaleFactorColor = new Dictionary<Bitboard, Color>();

        // Get the material key of a Position out of the given endgame key code
        // like "KBPKN". The trick here is to first forge an ad-hoc fen string
        // and then let a Position object to do the work for us. Note that the
        // fen string could correspond to an illegal position.
        private static Key key(string code, Color c)
        {
            Debug.Assert(code.Length > 0 && code.Length < 8);
            Debug.Assert(code[0] == 'K');

            int kpos = code.IndexOf('K', 1);
            string[] sides = new string[] { code.Substring(kpos), code.Substring(0, kpos) };
            sides[c] = sides[c].ToLowerInvariant();

            string fen = sides[0] + (char)('0' + (int)(8 - code.Length))
                        + sides[1] + "/8/8/8/8/8/8/8 w - - 0 10";

            return new Position(fen, false, null).material_key();
        }

        internal static EndgameValue probeValue(Key key)
        {
            if (_dictValue.ContainsKey(key)) return (_dictValue[key]);
            return null;
        }

        internal static EndgameScaleFactor probeScaleFactor(Key key)
        {
            if (_dictScaleFactor.ContainsKey(key)) return (_dictScaleFactor[key]);
            return null;
        }

        internal static Color probeStrongerSideScaleFactor(Key key)
        {
            if (_dictScaleFactorColor.ContainsKey(key)) return _dictScaleFactorColor[key];
            return -1;
        }

        private static void AddValue(string code, EndgameValue functionWhite, EndgameValue functionBlack)
        {
            _dictValue.Add(key(code, ColorC.WHITE), functionWhite);
            _dictValue.Add(key(code, ColorC.BLACK), functionBlack);
        }

        private static void AddScaleFactor(string code, EndgameScaleFactor functionWhite, EndgameScaleFactor functionBlack)
        {
            _dictScaleFactor.Add(key(code, ColorC.WHITE), functionWhite);
            _dictScaleFactor.Add(key(code, ColorC.BLACK), functionBlack);
            _dictScaleFactorColor.Add(key(code, ColorC.WHITE), ColorC.WHITE);
            _dictScaleFactorColor.Add(key(code, ColorC.BLACK), ColorC.BLACK);
        }

        internal static void InitEndgames()
        {
            AddValue("KPK", Endgame.Endgame_KPK_WHITE, Endgame.Endgame_KPK_BLACK);
            AddValue("KNNK", Endgame.Endgame_KNNK_WHITE, Endgame.Endgame_KNNK_BLACK);
            AddValue("KBNK", Endgame.Endgame_KBNK_WHITE, Endgame.Endgame_KBNK_BLACK);
            AddValue("KRKP", Endgame.Endgame_KRKP_WHITE, Endgame.Endgame_KRKP_BLACK);
            AddValue("KRKB", Endgame.Endgame_KRKB_WHITE, Endgame.Endgame_KRKB_BLACK);
            AddValue("KRKN", Endgame.Endgame_KRKN_WHITE, Endgame.Endgame_KRKN_BLACK);
            AddValue("KQKR", Endgame.Endgame_KQKR_WHITE, Endgame.Endgame_KQKR_BLACK);
            AddValue("KBBKN", Endgame.Endgame_KBBKN_WHITE, Endgame.Endgame_KBBKN_BLACK);

            AddScaleFactor("KNPK", Endgame.Endgame_KNPK_WHITE, Endgame.Endgame_KNPK_BLACK);
            AddScaleFactor("KRPKR", Endgame.Endgame_KRPKR_WHITE, Endgame.Endgame_KRPKR_BLACK);
            AddScaleFactor("KBPKB", Endgame.Endgame_KBPKB_WHITE, Endgame.Endgame_KBPKB_BLACK);
            AddScaleFactor("KBPKN", Endgame.Endgame_KBPKN_WHITE, Endgame.Endgame_KBPKN_BLACK);
            AddScaleFactor("KBPPKB", Endgame.Endgame_KBPPKB_WHITE, Endgame.Endgame_KBPPKB_BLACK);
            AddScaleFactor("KRPPKRP", Endgame.Endgame_KRPPKRP_WHITE, Endgame.Endgame_KRPPKRP_BLACK);
        }
    }

    internal static class Endgame
    {
        // Penalties
        private static readonly int[] penalty = new int[] { 0, 10, 14, 20, 30, 42, 58, 80 };

        // Table used to drive the defending king towards the edge of the board
        // in KX vs K and KQ vs KR endgames.
        private static readonly int[] MateTable = new int[64] {
            100, 90, 80, 70, 70, 80, 90, 100,
             90, 70, 60, 50, 50, 60, 70,  90,
             80, 60, 40, 30, 30, 40, 60,  80,
             70, 50, 30, 20, 20, 30, 50,  70,
             70, 50, 30, 20, 20, 30, 50,  70,
             80, 60, 40, 30, 30, 40, 60,  80,
             90, 70, 60, 50, 50, 60, 70,  90,
            100, 90, 80, 70, 70, 80, 90, 100,
          };

        // Table used to drive the defending king towards a corner square of the
        // right color in KBN vs K endgames.
        private static readonly int[] KBNKMateTable = new int[64] {
            200, 190, 180, 170, 160, 150, 140, 130,
            190, 180, 170, 160, 150, 140, 130, 140,
            180, 170, 155, 140, 140, 125, 140, 150,
            170, 160, 140, 120, 110, 140, 150, 160,
            160, 150, 140, 110, 120, 140, 160, 170,
            150, 140, 125, 140, 140, 155, 170, 180,
            140, 130, 140, 150, 160, 170, 180, 190,
            130, 140, 150, 160, 170, 180, 190, 200
          };

        // The attacking side is given a descending bonus based on distance between
        // the two kings in basic endgames.
        private static readonly int[] DistanceBonus = new int[8] { 0, 0, 100, 80, 60, 40, 20, 10 };

        //private Color strongerSide, weakerSide;

        /// Mate with KX vs K. This function is used to evaluate positions with
        /// King and plenty of material vs a lone king. It simply gives the
        /// attacking side a bonus for driving the defending king towards the edge
        /// of the board, and for keeping the distance between the two kings small.
        /// KXK
        internal static Value Endgame_KXK_WHITE(Position pos) { return Endgame_KXK(ColorC.WHITE, pos); }
        internal static Value Endgame_KXK_BLACK(Position pos) { return Endgame_KXK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KXK(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(weakerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == ValueC.VALUE_ZERO);

            // Stalemate detection with lone king
            MList mlist = MListBroker.GetObject();
            Movegen.generate(MoveType.MV_LEGAL, pos, mlist.moves, ref mlist.pos);
            bool any = mlist.pos > 0;
            MListBroker.Free(mlist);

            if (pos.sideToMove == weakerSide
                && !pos.in_check()
                && !any)
            {
                return ValueC.VALUE_DRAW;
            }

            Square winnerKSq = pos.king_square(strongerSide);
            Square loserKSq = pos.king_square(weakerSide);

            Value result = pos.non_pawn_material(strongerSide)
                           + pos.piece_count(strongerSide, PieceTypeC.PAWN) * Constants.PawnValueEndgame
                           + MateTable[loserKSq]
                           + DistanceBonus[Utils.square_distance(winnerKSq, loserKSq)];

            if (pos.piece_count(strongerSide, PieceTypeC.QUEEN)!=0
            || pos.piece_count(strongerSide, PieceTypeC.ROOK)!=0
            || pos.bishop_pair(strongerSide))
            {

                result += ValueC.VALUE_KNOWN_WIN;
            }

            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// Mate with KBN vs K. This is similar to KX vs K, but we have to drive the
        /// defending king towards a corner square of the right color.
        internal static Value Endgame_KBNK_WHITE(Position pos) { return Endgame_KBNK(ColorC.WHITE, pos); }
        internal static Value Endgame_KBNK_BLACK(Position pos) { return Endgame_KBNK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KBNK(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(weakerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.KnightValueMidgame + Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.KNIGHT) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 0);

            Square winnerKSq = pos.king_square(strongerSide);
            Square loserKSq = pos.king_square(weakerSide);
            Square bishopSquare = pos.pieceList[strongerSide][PieceTypeC.BISHOP][0];

            // kbnk_mate_table() tries to drive toward corners A1 or H8,
            // if we have a bishop that cannot reach the above squares we
            // mirror the kings so to drive enemy toward corners A8 or H1.
            if (Utils.opposite_colors(bishopSquare, SquareC.SQ_A1))
            {
                winnerKSq = Utils.mirror(winnerKSq);
                loserKSq = Utils.mirror(loserKSq);
            }

            Value result = ValueC.VALUE_KNOWN_WIN
                          + DistanceBonus[Utils.square_distance(winnerKSq, loserKSq)]
                          + KBNKMateTable[loserKSq];

            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// KP vs K. This endgame is evaluated with the help of a bitbase.
        internal static Value Endgame_KPK_WHITE(Position pos) { return Endgame_KPK(ColorC.WHITE, pos); }
        internal static Value Endgame_KPK_BLACK(Position pos) { return Endgame_KPK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KPK(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.non_pawn_material(weakerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 1);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square wksq, bksq, wpsq;
            Color stm;

            if (strongerSide == ColorC.WHITE)
            {
                wksq = pos.king_square(ColorC.WHITE);
                bksq = pos.king_square(ColorC.BLACK);
                wpsq = pos.pieceList[ColorC.WHITE][PieceTypeC.PAWN][0];
                stm = pos.sideToMove;
            }
            else
            {
                wksq = Utils.flip_S(pos.king_square(ColorC.BLACK));
                bksq = Utils.flip_S(pos.king_square(ColorC.WHITE));
                wpsq = Utils.flip_S(pos.pieceList[ColorC.BLACK][PieceTypeC.PAWN][0]);
                stm = Utils.flip_C(pos.sideToMove);
            }

            if (Utils.file_of(wpsq) >= FileC.FILE_E)
            {
                wksq = Utils.mirror(wksq);
                bksq = Utils.mirror(bksq);
                wpsq = Utils.mirror(wpsq);
            }

            if (KPKPosition.probe_kpk_bitbase(wksq, wpsq, bksq, stm) == 0)
                return ValueC.VALUE_DRAW;

            Value result = ValueC.VALUE_KNOWN_WIN
                          + Constants.PawnValueEndgame
                          + Utils.rank_of(wpsq);

            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// KR vs KP. This is a somewhat tricky endgame to evaluate precisely without
        /// a bitbase. The function below returns drawish scores when the pawn is
        /// far advanced with support of the king, while the attacking king is far
        /// away.
        internal static Value Endgame_KRKP_WHITE(Position pos) { return Endgame_KRKP(ColorC.WHITE, pos); }
        internal static Value Endgame_KRKP_BLACK(Position pos) { return Endgame_KRKP(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KRKP(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.non_pawn_material(weakerSide) == 0);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 1);

            Square wksq, wrsq, bksq, bpsq;
            int tempo = (pos.sideToMove == strongerSide ? 1 : 0);

            wksq = pos.king_square(strongerSide);
            wrsq = pos.pieceList[strongerSide][PieceTypeC.ROOK][0];
            bksq = pos.king_square(weakerSide);
            bpsq = pos.pieceList[weakerSide][PieceTypeC.PAWN][0];

            if (strongerSide == ColorC.BLACK)
            {
                wksq = Utils.flip_S(wksq);
                wrsq = Utils.flip_S(wrsq);
                bksq = Utils.flip_S(bksq);
                bpsq = Utils.flip_S(bpsq);
            }

            Square queeningSq = Utils.make_square(Utils.file_of(bpsq), RankC.RANK_1);
            Value result;

            // If the stronger side's king is in front of the pawn, it's a win
            if (wksq < bpsq && Utils.file_of(wksq) == Utils.file_of(bpsq))
                result = Constants.RookValueEndgame - (Utils.square_distance(wksq, bpsq));

            // If the weaker side's king is too far from the pawn and the rook,
            // it's a win
            else if (Utils.square_distance(bksq, bpsq) - (tempo ^ 1) >= 3
                     && Utils.square_distance(bksq, wrsq) >= 3)
                result = Constants.RookValueEndgame - (Utils.square_distance(wksq, bpsq));

            // If the pawn is far advanced and supported by the defending king,
            // the position is drawish
            else if (Utils.rank_of(bksq) <= RankC.RANK_3
                     && Utils.square_distance(bksq, bpsq) == 1
                     && Utils.rank_of(wksq) >= RankC.RANK_4
                     && Utils.square_distance(wksq, bpsq) - tempo > 2)
                result = (80 - Utils.square_distance(wksq, bpsq) * 8);

            else
                result = (200)
                        - (Utils.square_distance(wksq, bpsq + SquareC.DELTA_S) * 8)
                        + (Utils.square_distance(bksq, bpsq + SquareC.DELTA_S) * 8)
                        + (Utils.square_distance(bpsq, queeningSq) * 8);

            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// KR vs KB. This is very simple, and always returns drawish scores.  The
        /// score is slightly bigger when the defending king is close to the edge.
        internal static Value Endgame_KRKB_WHITE(Position pos) { return Endgame_KRKB(ColorC.WHITE, pos); }
        internal static Value Endgame_KRKB_BLACK(Position pos) { return Endgame_KRKB(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KRKB(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.BISHOP) == 1);

            Value result = (MateTable[pos.king_square(weakerSide)]);
            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// KR vs KN.  The attacking side has slightly better winning chances than
        /// in KR vs KB, particularly if the king and the knight are far apart.
        internal static Value Endgame_KRKN_WHITE(Position pos) { return Endgame_KRKN(ColorC.WHITE, pos); }
        internal static Value Endgame_KRKN_BLACK(Position pos) { return Endgame_KRKN(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KRKN(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.KnightValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.KNIGHT) == 1);

            Square bksq = pos.king_square(weakerSide);
            Square bnsq = pos.pieceList[weakerSide][PieceTypeC.KNIGHT][0];
            Value result = (MateTable[bksq] + penalty[Utils.square_distance(bksq, bnsq)]);
            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// KQ vs KR.  This is almost identical to KX vs K:  We give the attacking
        /// king a bonus for having the kings close together, and for forcing the
        /// defending king towards the edge.  If we also take care to avoid null move
        /// for the defending side in the search, this is usually sufficient to be
        /// able to win KQ vs KR.
        internal static Value Endgame_KQKR_WHITE(Position pos) { return Endgame_KQKR(ColorC.WHITE, pos); }
        internal static Value Endgame_KQKR_BLACK(Position pos) { return Endgame_KQKR(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KQKR(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.QueenValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square winnerKSq = pos.king_square(strongerSide);
            Square loserKSq = pos.king_square(weakerSide);

            Value result = Constants.QueenValueEndgame
                          - Constants.RookValueEndgame
                          + MateTable[loserKSq]
                          + DistanceBonus[Utils.square_distance(winnerKSq, loserKSq)];

            return strongerSide == pos.sideToMove ? result : -result;
        }

        internal static Value Endgame_KBBKN_WHITE(Position pos) { return Endgame_KBBKN(ColorC.WHITE, pos); }
        internal static Value Endgame_KBBKN_BLACK(Position pos) { return Endgame_KBBKN(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KBBKN(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.BISHOP) == 2);
            Debug.Assert(pos.non_pawn_material(strongerSide) == 2 * Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.KNIGHT) == 1);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.KnightValueMidgame);
            Debug.Assert(pos.pieces_PT(PieceTypeC.PAWN) == 0);

            Value result = Constants.BishopValueEndgame;
            Square wksq = pos.king_square(strongerSide);
            Square bksq = pos.king_square(weakerSide);
            Square nsq = pos.pieceList[weakerSide][PieceTypeC.KNIGHT][0];

            // Bonus for attacking king close to defending king
            result += (DistanceBonus[Utils.square_distance(wksq, bksq)]);

            // Bonus for driving the defending king and knight apart
            result += (Utils.square_distance(bksq, nsq) * 32);

            // Bonus for restricting the knight's mobility
            result += ((8 - Bitcount.popcount_1s_Max15(pos.attacks_from_KNIGHT(nsq))) * 8);

            return strongerSide == pos.sideToMove ? result : -result;
        }

        /// K and two minors vs K and one or two minors or K and two knights against
        /// king alone are always draw.
        internal static Value Endgame_KmmKm_WHITE(Position pos) { return Endgame_KmmKm(ColorC.WHITE, pos); }
        internal static Value Endgame_KmmKm_BLACK(Position pos) { return Endgame_KmmKm(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KmmKm(Color c, Position pos)
        {
            return ValueC.VALUE_DRAW;
        }

        internal static Value Endgame_KNNK_WHITE(Position pos) { return Endgame_KNNK(ColorC.WHITE, pos); }
        internal static Value Endgame_KNNK_BLACK(Position pos) { return Endgame_KNNK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value Endgame_KNNK(Color c, Position pos)
        {
            return ValueC.VALUE_DRAW;
        }

        /// K, bishop and one or more pawns vs K. It checks for draws with rook pawns and
        /// a bishop of the wrong color. If such a draw is detected, SCALE_FACTOR_DRAW
        /// is returned. If not, the return value is SCALE_FACTOR_NONE, i.e. no scaling
        /// will be used.
        internal static ScaleFactor Endgame_KBPsK_WHITE(Position pos) { return Endgame_KBPsK(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KBPsK_BLACK(Position pos) { return Endgame_KBPsK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KBPsK(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) >= 1);

            // No Debug.Assertions about the material of weakerSide, because we want draws to
            // be detected even when the weaker side has some pawns.

            Bitboard pawns = pos.pieces_PTC(PieceTypeC.PAWN, strongerSide);
            File pawnFile = Utils.file_of(pos.pieceList[strongerSide][PieceTypeC.PAWN][0]);

            // All pawns are on a single rook file ?
            if ((pawnFile == FileC.FILE_A || pawnFile == FileC.FILE_H)
                && (((pawns & ~Utils.file_bb_F(pawnFile))) == 0))
            {
                Square bishopSq = pos.pieceList[strongerSide][PieceTypeC.BISHOP][0];
                Square queeningSq = Utils.relative_square(strongerSide, Utils.make_square(pawnFile, RankC.RANK_8));
                Square kingSq = pos.king_square(weakerSide);

                if (Utils.opposite_colors(queeningSq, bishopSq)
                    && Math.Abs(Utils.file_of(kingSq) - pawnFile) <= 1)
                {
                    // The bishop has the wrong color, and the defending king is on the
                    // file of the pawn(s) or the adjacent file. Find the rank of the
                    // frontmost pawn.
                    Rank rank;
                    if (strongerSide == ColorC.WHITE)
                    {
                        for (rank = RankC.RANK_7; (((Utils.rank_bb_R(rank) & pawns)) == 0); rank--) { }
                        Debug.Assert(rank >= RankC.RANK_2 && rank <= RankC.RANK_7);
                    }
                    else
                    {
                        for (rank = RankC.RANK_2; (((Utils.rank_bb_R(rank) & pawns)) == 0); rank++) { }
                        rank = (rank ^ 7);  // HACK to get the relative rank
                        Debug.Assert(rank >= RankC.RANK_2 && rank <= RankC.RANK_7);
                    }
                    // If the defending king has distance 1 to the promotion square or
                    // is placed somewhere in front of the pawn, it's a draw.
                    if (Utils.square_distance(kingSq, queeningSq) <= 1
                        || Utils.relative_rank_CS(strongerSide, kingSq) >= rank)
                        return ScaleFactorC.SCALE_FACTOR_DRAW;
                }
            }
            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K and queen vs K, rook and one or more pawns. It tests for fortress draws with
        /// a rook on the third rank defended by a pawn.
        internal static ScaleFactor Endgame_KQKRPs_WHITE(Position pos) { return Endgame_KQKRPs(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KQKRPs_BLACK(Position pos) { return Endgame_KQKRPs(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KQKRPs(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.QueenValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.QUEEN) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 0);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.ROOK) == 1);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) >= 1);

            Square kingSq = pos.king_square(weakerSide);
            if (Utils.relative_rank_CS(weakerSide, kingSq) <= RankC.RANK_2
                && Utils.relative_rank_CS(weakerSide, pos.king_square(strongerSide)) >= RankC.RANK_4
                && ((pos.pieces_PTC(PieceTypeC.ROOK, weakerSide) & Utils.rank_bb_R(Utils.relative_rank_CR(weakerSide, RankC.RANK_3))) != 0)
                && ((pos.pieces_PTC(PieceTypeC.PAWN, weakerSide) & Utils.rank_bb_R(Utils.relative_rank_CR(weakerSide, RankC.RANK_2))) != 0)
                && ((pos.attacks_from_KING(kingSq) & pos.pieces_PTC(PieceTypeC.PAWN, weakerSide)) != 0)
                )
            {
                Square rsq = pos.pieceList[weakerSide][PieceTypeC.ROOK][0];
                if ((pos.attacks_from_PAWN(rsq, strongerSide) & pos.pieces_PTC(PieceTypeC.PAWN, weakerSide)) != 0)
                    return ScaleFactorC.SCALE_FACTOR_DRAW;
            }
            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K, rook and one pawn vs K and a rook. This function knows a handful of the
        /// most important classes of drawn positions, but is far from perfect. It would
        /// probably be a good idea to add more knowledge in the future.
        ///
        /// It would also be nice to rewrite the actual code for this function,
        /// which is mostly copied from Glaurung 1.x, and not very pretty.
        internal static ScaleFactor Endgame_KRPKR_WHITE(Position pos) { return Endgame_KRPKR(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KRPKR_BLACK(Position pos) { return Endgame_KRPKR(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KRPKR(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 1);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square wksq = pos.king_square(strongerSide);
            Square wrsq = pos.pieceList[strongerSide][PieceTypeC.ROOK][0];
            Square wpsq = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Square bksq = pos.king_square(weakerSide);
            Square brsq = pos.pieceList[weakerSide][PieceTypeC.ROOK][0];

            // Orient the board in such a way that the stronger side is white, and the
            // pawn is on the left half of the board.
            if (strongerSide == ColorC.BLACK)
            {
                wksq = Utils.flip_S(wksq);
                wrsq = Utils.flip_S(wrsq);
                wpsq = Utils.flip_S(wpsq);
                bksq = Utils.flip_S(bksq);
                brsq = Utils.flip_S(brsq);
            }
            if (Utils.file_of(wpsq) > FileC.FILE_D)
            {
                wksq = Utils.mirror(wksq);
                wrsq = Utils.mirror(wrsq);
                wpsq = Utils.mirror(wpsq);
                bksq = Utils.mirror(bksq);
                brsq = Utils.mirror(brsq);
            }

            File f = Utils.file_of(wpsq);
            Rank r = Utils.rank_of(wpsq);
            Square queeningSq = Utils.make_square(f, RankC.RANK_8);
            int tempo = (pos.sideToMove == strongerSide ? 1 : 0);

            // If the pawn is not too far advanced and the defending king defends the
            // queening square, use the third-rank defence.
            if (r <= RankC.RANK_5
                && Utils.square_distance(bksq, queeningSq) <= 1
                && wksq <= SquareC.SQ_H5
                && (Utils.rank_of(brsq) == RankC.RANK_6 || (r <= RankC.RANK_3 && Utils.rank_of(wrsq) != RankC.RANK_6)))
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            // The defending side saves a draw by checking from behind in case the pawn
            // has advanced to the 6th rank with the king behind.
            if (r == RankC.RANK_6
                && Utils.square_distance(bksq, queeningSq) <= 1
                && Utils.rank_of(wksq) + tempo <= RankC.RANK_6
                && (Utils.rank_of(brsq) == RankC.RANK_1 || ((tempo == 0) && Math.Abs(Utils.file_of(brsq) - f) >= 3)))
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            if (r >= RankC.RANK_6
                && bksq == queeningSq
                && Utils.rank_of(brsq) == RankC.RANK_1
                && ((tempo == 0) || Utils.square_distance(wksq, wpsq) >= 2))
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            // White pawn on a7 and rook on a8 is a draw if black's king is on g7 or h7
            // and the black rook is behind the pawn.
            if (wpsq == SquareC.SQ_A7
                && wrsq == SquareC.SQ_A8
                && (bksq == SquareC.SQ_H7 || bksq == SquareC.SQ_G7)
                && Utils.file_of(brsq) == FileC.FILE_A
                && (Utils.rank_of(brsq) <= RankC.RANK_3 || Utils.file_of(wksq) >= FileC.FILE_D || Utils.rank_of(wksq) <= RankC.RANK_5))
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            // If the defending king blocks the pawn and the attacking king is too far
            // away, it's a draw.
            if (r <= RankC.RANK_5
                && bksq == wpsq + SquareC.DELTA_N
                && Utils.square_distance(wksq, wpsq) - tempo >= 2
                && Utils.square_distance(wksq, brsq) - tempo >= 2)
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            // Pawn on the 7th rank supported by the rook from behind usually wins if the
            // attacking king is closer to the queening square than the defending king,
            // and the defending king cannot gain tempi by threatening the attacking rook.
            if (r == RankC.RANK_7
                && f != FileC.FILE_A
                && Utils.file_of(wrsq) == f
                && wrsq != queeningSq
                && (Utils.square_distance(wksq, queeningSq) < Utils.square_distance(bksq, queeningSq) - 2 + tempo)
                && (Utils.square_distance(wksq, queeningSq) < Utils.square_distance(bksq, wrsq) + tempo))
                return (ScaleFactorC.SCALE_FACTOR_MAX - 2 * Utils.square_distance(wksq, queeningSq));

            // Similar to the above, but with the pawn further back
            if (f != FileC.FILE_A
                && Utils.file_of(wrsq) == f
                && wrsq < wpsq
                && (Utils.square_distance(wksq, queeningSq) < Utils.square_distance(bksq, queeningSq) - 2 + tempo)
                && (Utils.square_distance(wksq, wpsq + SquareC.DELTA_N) < Utils.square_distance(bksq, wpsq + SquareC.DELTA_N) - 2 + tempo)
                && (Utils.square_distance(bksq, wrsq) + tempo >= 3
                    || (Utils.square_distance(wksq, queeningSq) < Utils.square_distance(bksq, wrsq) + tempo
                        && (Utils.square_distance(wksq, wpsq + SquareC.DELTA_N) < Utils.square_distance(bksq, wrsq) + tempo))))
                return (ScaleFactorC.SCALE_FACTOR_MAX
                                   - 8 * Utils.square_distance(wpsq, queeningSq)
                                   - 2 * Utils.square_distance(wksq, queeningSq));

            // If the pawn is not far advanced, and the defending king is somewhere in
            // the pawn's path, it's probably a draw.
            if (r <= RankC.RANK_4 && bksq > wpsq)
            {
                if (Utils.file_of(bksq) == Utils.file_of(wpsq))
                    return (10);
                if (Math.Abs(Utils.file_of(bksq) - Utils.file_of(wpsq)) == 1
                    && Utils.square_distance(wksq, bksq) > 2)
                    return (24 - 2 * Utils.square_distance(wksq, bksq));
            }
            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K, rook and two pawns vs K, rook and one pawn. There is only a single
        /// pattern: If the stronger side has no passed pawns and the defending king
        /// is actively placed, the position is drawish.
        internal static ScaleFactor Endgame_KRPPKRP_WHITE(Position pos) { return Endgame_KRPPKRP(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KRPPKRP_BLACK(Position pos) { return Endgame_KRPPKRP(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KRPPKRP(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 2);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.RookValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 1);

            Square wpsq1 = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Square wpsq2 = pos.pieceList[strongerSide][PieceTypeC.PAWN][1];
            Square bksq = pos.king_square(weakerSide);

            // Does the stronger side have a passed pawn?
            if (pos.pawn_is_passed(strongerSide, wpsq1)
                || pos.pawn_is_passed(strongerSide, wpsq2))
                return ScaleFactorC.SCALE_FACTOR_NONE;

            Rank r = Math.Max(Utils.relative_rank_CS(strongerSide, wpsq1), Utils.relative_rank_CS(strongerSide, wpsq2));

            if (Utils.file_distance(bksq, wpsq1) <= 1
                && Utils.file_distance(bksq, wpsq2) <= 1
                && Utils.relative_rank_CS(strongerSide, bksq) > r)
            {
                switch (r)
                {
                    case RankC.RANK_2: return (10);
                    case RankC.RANK_3: return (10);
                    case RankC.RANK_4: return (15);
                    case RankC.RANK_5: return (20);
                    case RankC.RANK_6: return (40);
                    default: Debug.Assert(false); break;
                }
            }
            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K and two or more pawns vs K. There is just a single rule here: If all pawns
        /// are on the same rook file and are blocked by the defending king, it's a draw.
        internal static ScaleFactor Endgame_KPsK_WHITE(Position pos) { return Endgame_KPsK(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KPsK_BLACK(Position pos) { return Endgame_KPsK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KPsK(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) >= 2);
            Debug.Assert(pos.non_pawn_material(weakerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square ksq = pos.king_square(weakerSide);
            Bitboard pawns = pos.pieces_PTC(PieceTypeC.PAWN, strongerSide);

            // Are all pawns on the 'a' file?
            if ((pawns & ~Constants.FileABB) == 0)
            {
                // Does the defending king block the pawns?
                if (Utils.square_distance(ksq, Utils.relative_square(strongerSide, SquareC.SQ_A8)) <= 1
                    || (Utils.file_of(ksq) == FileC.FILE_A
                        && ((Utils.in_front_bb_CS(strongerSide, ksq)) & pawns) == 0))
                    return ScaleFactorC.SCALE_FACTOR_DRAW;
            }
            // Are all pawns on the 'h' file?
            else if ((pawns & ~Constants.FileHBB) == 0)
            {
                // Does the defending king block the pawns?
                if (Utils.square_distance(ksq, Utils.relative_square(strongerSide, SquareC.SQ_H8)) <= 1
                    || (Utils.file_of(ksq) == FileC.FILE_H
                        && ((Utils.in_front_bb_CS(strongerSide, ksq)) & pawns) == 0))
                    return ScaleFactorC.SCALE_FACTOR_DRAW;
            }
            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K, bishop and a pawn vs K and a bishop. There are two rules: If the defending
        /// king is somewhere along the path of the pawn, and the square of the king is
        /// not of the same color as the stronger side's bishop, it's a draw. If the two
        /// bishops have opposite color, it's almost always a draw.
        internal static ScaleFactor Endgame_KBPKB_WHITE(Position pos) { return Endgame_KBPKB(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KBPKB_BLACK(Position pos) { return Endgame_KBPKB(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KBPKB(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 1);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square pawnSq = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Square strongerBishopSq = pos.pieceList[strongerSide][PieceTypeC.BISHOP][0];
            Square weakerBishopSq = pos.pieceList[weakerSide][PieceTypeC.BISHOP][0];
            Square weakerKingSq = pos.king_square(weakerSide);

            // Case 1: Defending king blocks the pawn, and cannot be driven away
            if (Utils.file_of(weakerKingSq) == Utils.file_of(pawnSq)
                && Utils.relative_rank_CS(strongerSide, pawnSq) < Utils.relative_rank_CS(strongerSide, weakerKingSq)
                && (Utils.opposite_colors(weakerKingSq, strongerBishopSq)
                    || Utils.relative_rank_CS(strongerSide, weakerKingSq) <= RankC.RANK_6))
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            // Case 2: Opposite colored bishops
            if (Utils.opposite_colors(strongerBishopSq, weakerBishopSq))
            {
                // We assume that the position is drawn in the following three situations:
                //
                //   a. The pawn is on rank 5 or further back.
                //   b. The defending king is somewhere in the pawn's path.
                //   c. The defending bishop attacks some square along the pawn's path,
                //      and is at least three squares away from the pawn.
                //
                // These rules are probably not perfect, but in practice they work
                // reasonably well.

                if (Utils.relative_rank_CS(strongerSide, pawnSq) <= RankC.RANK_5)
                    return ScaleFactorC.SCALE_FACTOR_DRAW;
                else
                {
                    Bitboard path = Utils.forward_bb(strongerSide, pawnSq);

                    if ((path & pos.pieces_PTC(PieceTypeC.KING, weakerSide)) != 0)
                        return ScaleFactorC.SCALE_FACTOR_DRAW;

                    if (((pos.attacks_from_BISHOP(weakerBishopSq) & path) != 0)
                        && Utils.square_distance(weakerBishopSq, pawnSq) >= 3)
                        return ScaleFactorC.SCALE_FACTOR_DRAW;
                }
            }
            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K, bishop and two pawns vs K and bishop. It detects a few basic draws with
        /// opposite-colored bishops.
        internal static ScaleFactor Endgame_KBPPKB_WHITE(Position pos) { return Endgame_KBPPKB(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KBPPKB_BLACK(Position pos) { return Endgame_KBPPKB(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KBPPKB(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 2);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square wbsq = pos.pieceList[strongerSide][PieceTypeC.BISHOP][0];
            Square bbsq = pos.pieceList[weakerSide][PieceTypeC.BISHOP][0];

            if (!Utils.opposite_colors(wbsq, bbsq))
                return ScaleFactorC.SCALE_FACTOR_NONE;

            Square ksq = pos.king_square(weakerSide);
            Square psq1 = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Square psq2 = pos.pieceList[strongerSide][PieceTypeC.PAWN][1];
            Rank r1 = Utils.rank_of(psq1);
            Rank r2 = Utils.rank_of(psq2);
            Square blockSq1, blockSq2;

            if (Utils.relative_rank_CS(strongerSide, psq1) > Utils.relative_rank_CS(strongerSide, psq2))
            {
                blockSq1 = psq1 + Utils.pawn_push(strongerSide);
                blockSq2 = Utils.make_square(Utils.file_of(psq2), Utils.rank_of(psq1));
            }
            else
            {
                blockSq1 = psq2 + Utils.pawn_push(strongerSide);
                blockSq2 = Utils.make_square(Utils.file_of(psq1), Utils.rank_of(psq2));
            }

            switch (Utils.file_distance(psq1, psq2))
            {
                case 0:
                    // Both pawns are on the same file. Easy draw if defender firmly controls
                    // some square in the frontmost pawn's path.
                    if (Utils.file_of(ksq) == Utils.file_of(blockSq1)
                  && Utils.relative_rank_CS(strongerSide, ksq) >= Utils.relative_rank_CS(strongerSide, blockSq1)
                  && Utils.opposite_colors(ksq, wbsq))
                        return ScaleFactorC.SCALE_FACTOR_DRAW;
                    else
                        return ScaleFactorC.SCALE_FACTOR_NONE;

                case 1:
                    // Pawns on adjacent files. Draw if defender firmly controls the square
                    // in front of the frontmost pawn's path, and the square diagonally behind
                    // this square on the file of the other pawn.
                    if (ksq == blockSq1
                        && Utils.opposite_colors(ksq, wbsq)
                        && (bbsq == blockSq2
                            || (((pos.attacks_from_BISHOP(blockSq2) & pos.pieces_PTC(PieceTypeC.BISHOP, weakerSide))) != 0)
                            || Math.Abs(r1 - r2) >= 2))
                        return ScaleFactorC.SCALE_FACTOR_DRAW;

                    else if (ksq == blockSq2
                             && Utils.opposite_colors(ksq, wbsq)
                             && (bbsq == blockSq1
                                 || (((pos.attacks_from_BISHOP(blockSq1) & pos.pieces_PTC(PieceTypeC.BISHOP, weakerSide)))) != 0))
                        return ScaleFactorC.SCALE_FACTOR_DRAW;
                    else
                        return ScaleFactorC.SCALE_FACTOR_NONE;

                default:
                    // The pawns are not on the same file or adjacent files. No scaling.
                    return ScaleFactorC.SCALE_FACTOR_NONE;
            }
        }

        /// K, bishop and a pawn vs K and knight. There is a single rule: If the defending
        /// king is somewhere along the path of the pawn, and the square of the king is
        /// not of the same color as the stronger side's bishop, it's a draw.
        internal static ScaleFactor Endgame_KBPKN_WHITE(Position pos) { return Endgame_KBPKN(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KBPKN_BLACK(Position pos) { return Endgame_KBPKN(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KBPKN(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.BishopValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.BISHOP) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 1);
            Debug.Assert(pos.non_pawn_material(weakerSide) == Constants.KnightValueMidgame);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.KNIGHT) == 1);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square pawnSq = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Square strongerBishopSq = pos.pieceList[strongerSide][PieceTypeC.BISHOP][0];
            Square weakerKingSq = pos.king_square(weakerSide);

            if (Utils.file_of(weakerKingSq) == Utils.file_of(pawnSq)
                && Utils.relative_rank_CS(strongerSide, pawnSq) < Utils.relative_rank_CS(strongerSide, weakerKingSq)
                && (Utils.opposite_colors(weakerKingSq, strongerBishopSq)
                    || Utils.relative_rank_CS(strongerSide, weakerKingSq) <= RankC.RANK_6))
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K, knight and a pawn vs K. There is a single rule: If the pawn is a rook pawn
        /// on the 7th rank and the defending king prevents the pawn from advancing, the
        /// position is drawn.
        internal static ScaleFactor Endgame_KNPK_WHITE(Position pos) { return Endgame_KNPK(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KNPK_BLACK(Position pos) { return Endgame_KNPK(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KNPK(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == Constants.KnightValueMidgame);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.KNIGHT) == 1);
            Debug.Assert(pos.piece_count(strongerSide, PieceTypeC.PAWN) == 1);
            Debug.Assert(pos.non_pawn_material(weakerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(weakerSide, PieceTypeC.PAWN) == 0);

            Square pawnSq = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Square weakerKingSq = pos.king_square(weakerSide);

            if (pawnSq == Utils.relative_square(strongerSide, SquareC.SQ_A7)
                && Utils.square_distance(weakerKingSq, Utils.relative_square(strongerSide, SquareC.SQ_A8)) <= 1)
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            if (pawnSq == Utils.relative_square(strongerSide, SquareC.SQ_H7)
                && Utils.square_distance(weakerKingSq, Utils.relative_square(strongerSide, SquareC.SQ_H8)) <= 1)
                return ScaleFactorC.SCALE_FACTOR_DRAW;

            return ScaleFactorC.SCALE_FACTOR_NONE;
        }

        /// K and a pawn vs K and a pawn. This is done by removing the weakest side's
        /// pawn and probing the KP vs K bitbase: If the weakest side has a draw without
        /// the pawn, she probably has at least a draw with the pawn as well. The exception
        /// is when the stronger side's pawn is far advanced and not on a rook file; in
        /// this case it is often possible to win (e.g. 8/4k3/3p4/3P4/6K1/8/8/8 w - - 0 1).
        internal static ScaleFactor Endgame_KPKP_WHITE(Position pos) { return Endgame_KPKP(ColorC.WHITE, pos); }
        internal static ScaleFactor Endgame_KPKP_BLACK(Position pos) { return Endgame_KPKP(ColorC.BLACK, pos); }
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static ScaleFactor Endgame_KPKP(Color c, Position pos)
        {
            Color strongerSide = c;
            Color weakerSide = Utils.flip_C(c);

            Debug.Assert(pos.non_pawn_material(strongerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.non_pawn_material(weakerSide) == ValueC.VALUE_ZERO);
            Debug.Assert(pos.piece_count(ColorC.WHITE, PieceTypeC.PAWN) == 1);
            Debug.Assert(pos.piece_count(ColorC.BLACK, PieceTypeC.PAWN) == 1);

            Square wksq = pos.king_square(strongerSide);
            Square bksq = pos.king_square(weakerSide);
            Square wpsq = pos.pieceList[strongerSide][PieceTypeC.PAWN][0];
            Color stm = pos.sideToMove;

            if (strongerSide == ColorC.BLACK)
            {
                wksq = Utils.flip_S(wksq);
                bksq = Utils.flip_S(bksq);
                wpsq = Utils.flip_S(wpsq);
                stm = Utils.flip_C(stm);
            }

            if (Utils.file_of(wpsq) >= FileC.FILE_E)
            {
                wksq = Utils.mirror(wksq);
                bksq = Utils.mirror(bksq);
                wpsq = Utils.mirror(wpsq);
            }

            // If the pawn has advanced to the fifth rank or further, and is not a
            // rook pawn, it's too dangerous to assume that it's at least a draw.
            if (Utils.rank_of(wpsq) >= RankC.RANK_5
                && Utils.file_of(wpsq) != FileC.FILE_A)
                return ScaleFactorC.SCALE_FACTOR_NONE;

            // Probe the KPK bitbase with the weakest side's pawn removed. If it's a draw,
            // it's probably at least a draw even with the pawn.
            return (KPKPosition.probe_kpk_bitbase(wksq, wpsq, bksq, stm) != 0) ? ScaleFactorC.SCALE_FACTOR_NONE : ScaleFactorC.SCALE_FACTOR_DRAW;
        }
    }
}
