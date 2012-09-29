using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Reflection;

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

namespace Portfish
{
    internal static class Utils
    {
        #region Defines

#if X64
        private const string cpu64 = " 64bit";
#else
        private const string cpu64 = "";
#endif

        #endregion

        #region Fields

        internal static readonly Bitboard[] RMasks = new Bitboard[64];
        internal static readonly Bitboard[] RMagics = new Bitboard[64];
        internal static readonly Bitboard[][] RAttacks = new Bitboard[64][];
        internal static readonly Int32[] RShifts = new Int32[64];

        internal static readonly Bitboard[] BMasks = new Bitboard[64];
        internal static readonly Bitboard[] BMagics = new Bitboard[64];
        internal static readonly Bitboard[][] BAttacks = new Bitboard[64][];
        internal static readonly Int32[] BShifts = new Int32[64];

        internal static readonly Bitboard[] occupancy = new Bitboard[4096];
        internal static readonly Bitboard[] reference = new Bitboard[4096];

        internal static readonly Bitboard[] SquareBB = new Bitboard[64];
        internal static readonly Bitboard[] FileBB = new Bitboard[8];
        internal static readonly Bitboard[] RankBB = new Bitboard[8];
        internal static readonly Bitboard[] AdjacentFilesBB = new Bitboard[8];
        internal static readonly Bitboard[] ThisAndAdjacentFilesBB = new Bitboard[8];

        internal static readonly Bitboard[][] InFrontBB = new Bitboard[2][]; // 2,8
        internal static readonly Bitboard[][] ForwardBB = new Bitboard[2][]; // 2,64
        internal static readonly Bitboard[][] PassedPawnMask = new Bitboard[2][]; // 2,64
        internal static readonly Bitboard[][] AttackSpanMask = new Bitboard[2][]; // 2,64
        internal static readonly Bitboard[][] BetweenBB = new Bitboard[64][]; // 64, 64

        internal static readonly Bitboard[][] StepAttacksBB = new Bitboard[16][]; // 16, 64
        internal static readonly Bitboard[] StepAttacksBB_KING = new Bitboard[64]; // 64
        internal static readonly Bitboard[] StepAttacksBB_KNIGHT = new Bitboard[64]; // 64

        internal static readonly Bitboard[][] PseudoAttacks = new Bitboard[6][]; // 6, 64
        internal static readonly Bitboard[] PseudoAttacks_ROOK = new Bitboard[64]; // 64
        internal static readonly Bitboard[] PseudoAttacks_BISHOP = new Bitboard[64]; // 64
        internal static readonly Bitboard[] PseudoAttacks_QUEEN = new Bitboard[64]; // 64

        internal static readonly int[] BSFTable = new int[64];
        internal static readonly int[][] SquareDistance = new int[64][]; // 64, 64
        internal static readonly byte[] BitCount8Bit = new byte[256];
        internal static readonly Square[] MS1BTable = new Square[256];

        internal static readonly Square[] RDeltas = new Square[] { SquareC.DELTA_N, SquareC.DELTA_E, SquareC.DELTA_S, SquareC.DELTA_W };
        internal static readonly Square[] BDeltas = new Square[] { SquareC.DELTA_NE, SquareC.DELTA_SE, SquareC.DELTA_SW, SquareC.DELTA_NW };
        internal static readonly int[][] steps = new int[][] { new int[] {}, new int[] { 7, 9 }, new int[] { 17, 15, 10, 6, -6, -10, -15, -17 },
                     new int[] {}, new int[] {}, new int[] {}, new int[] { 9, 7, -7, -9, 8, 1, -1, -8 } };
        internal static readonly int[][] MagicBoosters = new int[][] { new int[] { 3191, 2184, 1310, 3618, 2091, 1308, 2452, 3996 },
                               new int[] { 1059, 3608,  605, 3234, 3326,   38, 2029, 3043 } };

        internal static readonly RKISS rk = new RKISS();

        internal static readonly char[] _pieces = " PNBRQK".ToCharArray();

        #endregion

        #region Lookup init

        internal static void init()
        {
            for (int k = 0, i = 0; i < 8; i++)
            {
                while (k < (2 << i))
                {
                    MS1BTable[k++] = i;
                }
            }

            for (Bitboard b = 0; b < 256; b++)
            {
                BitCount8Bit[b] = (byte)(Bitcount.popcount_1s_Max15(b));
            }

            for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
            {
                SquareBB[s] = 1UL << s;
            }

            FileBB[FileC.FILE_A] = Constants.FileABB;
            RankBB[RankC.RANK_1] = Constants.Rank1BB;

            for (int i = 1; i < 8; i++)
            {
                FileBB[i] = FileBB[i - 1] << 1;
                RankBB[i] = RankBB[i - 1] << 8;
            }

            for (File f = FileC.FILE_A; f <= FileC.FILE_H; f++)
            {
                AdjacentFilesBB[f] = (f > FileC.FILE_A ? FileBB[f - 1] : 0) | (f < FileC.FILE_H ? FileBB[f + 1] : 0);
                ThisAndAdjacentFilesBB[f] = FileBB[f] | AdjacentFilesBB[f];
            }

            InFrontBB[ColorC.WHITE] = new Bitboard[8];
            InFrontBB[ColorC.BLACK] = new Bitboard[8];
            for (Rank r = RankC.RANK_1; r < RankC.RANK_8; r++)
                InFrontBB[ColorC.WHITE][r] = ~(InFrontBB[ColorC.BLACK][r + 1] = InFrontBB[ColorC.BLACK][r] | RankBB[r]);

            for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
            {
                ForwardBB[c] = new Bitboard[64];
                PassedPawnMask[c] = new Bitboard[64];
                AttackSpanMask[c] = new Bitboard[64];
                for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
                {
                    ForwardBB[c][s] = InFrontBB[c][Utils.rank_of(s)] & FileBB[Utils.file_of(s)];
                    PassedPawnMask[c][s] = InFrontBB[c][Utils.rank_of(s)] & ThisAndAdjacentFilesBB[Utils.file_of(s)];
                    AttackSpanMask[c][s] = InFrontBB[c][Utils.rank_of(s)] & AdjacentFilesBB[Utils.file_of(s)];
                }
            }

            for (Square s1 = SquareC.SQ_A1; s1 <= SquareC.SQ_H8; s1++)
            {
                SquareDistance[s1] = new int[64];
                for (Square s2 = SquareC.SQ_A1; s2 <= SquareC.SQ_H8; s2++)
                {
                    SquareDistance[s1][s2] = Math.Max(Utils.file_distance(s1, s2), Utils.rank_distance(s1, s2));
                }
            }

#if X64
            for (int i = 0; i < 64; i++)
            {
                BSFTable[((1UL << i) * 0x218A392CD3D5DBFUL) >> 58] = i;
            }
#else
            // Matt Taylor's folding trick for 32 bit systems
            for (int i = 0; i < 64; i++)
            {
                ulong b = (1UL << i);
                b ^= b - 1;
                b ^= b >> 32;
                BSFTable[(UInt32)(b * 0x783A9B23) >> 26] = i;
            }
#endif

            for (Color c = ColorC.WHITE; c <= ColorC.BLACK; c++)
            {
                for (PieceType pt = PieceTypeC.PAWN; pt <= PieceTypeC.KING; pt++)
                {
                    Piece piece = Utils.make_piece(c, pt);
                    StepAttacksBB[piece] = new Bitboard[64];
                    for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
                    {
                        for (int k = 0; k < steps[pt].Length; k++)
                        {
                            Square to = s + (c == ColorC.WHITE ? steps[pt][k] : -steps[pt][k]);
                            if (Utils.is_ok_S(to) && SquareDistance[s][to] < 3)
                            {
                                StepAttacksBB[piece][s] |= SquareBB[to];
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < 64; i++)
            {
                StepAttacksBB_KING[i] = StepAttacksBB[PieceTypeC.KING][i];
                StepAttacksBB_KNIGHT[i] = StepAttacksBB[PieceTypeC.KNIGHT][i];
            }

            for (int i = 0; i < 6; i++)
            {
                PseudoAttacks[i] = new Bitboard[64];
            }

            init_magics(PieceTypeC.ROOK, RAttacks, RMagics, RMasks, RShifts, RDeltas, magic_index);
            init_magics(PieceTypeC.BISHOP, BAttacks, BMagics, BMasks, BShifts, BDeltas, magic_index);

            for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
            {
                PseudoAttacks[PieceTypeC.QUEEN][s] = PseudoAttacks[PieceTypeC.BISHOP][s] = bishop_attacks_bb(s, 0);
                PseudoAttacks[PieceTypeC.QUEEN][s] |= PseudoAttacks[PieceTypeC.ROOK][s] = rook_attacks_bb(s, 0);

                PseudoAttacks_BISHOP[s] = PseudoAttacks[PieceTypeC.BISHOP][s];
                PseudoAttacks_ROOK[s] = PseudoAttacks[PieceTypeC.ROOK][s];
                PseudoAttacks_QUEEN[s] = PseudoAttacks[PieceTypeC.QUEEN][s];
            }

            for (Square s1 = SquareC.SQ_A1; s1 <= SquareC.SQ_H8; s1++)
            {
                BetweenBB[s1] = new Bitboard[64];
                for (Square s2 = SquareC.SQ_A1; s2 <= SquareC.SQ_H8; s2++)
                {
                    if ((PseudoAttacks[PieceTypeC.QUEEN][s1] & SquareBB[s2]) != 0)
                    {
                        Square delta = ((s2 - s1) / SquareDistance[s1][s2]);
                        for (Square s = s1 + delta; s != s2; s += delta)
                        {
                            set_bit(ref BetweenBB[s1][s2], s);
                        }
                    }
                }
            }
        }

        private static Bitboard sliding_attack(Square[] deltas, Square sq, Bitboard occupied)
        {
            Bitboard attack = 0;

            for (int i = 0; i < 4; i++)
                for (Square s = sq + deltas[i];
                     is_ok_S(s) && square_distance(s, s - deltas[i]) == 1;
                     s += deltas[i])
                {
                    Utils.set_bit(ref attack, s);

                    if (Utils.bit_is_set(occupied, s) != 0)
                        break;
                }

            return attack;
        }

        private static Bitboard pick_random(Bitboard mask, RKISS rk, int booster)
        {
            Bitboard magic;

            // Values s1 and s2 are used to rotate the candidate magic of a
            // quantity known to be the optimal to quickly find the magics.
            int s1 = booster & 63, s2 = (booster >> 6) & 63;

            while (true)
            {
                magic = rk.rand();
                magic = (magic >> s1) | (magic << (64 - s1));
                magic &= rk.rand();
                magic = (magic >> s2) | (magic << (64 - s2));
                magic &= rk.rand();

                if (BitCount8Bit[(mask * magic) >> 56] >= 6)
                    return magic;
            }
        }

        // init_magics() computes all rook and bishop attacks at startup. Magic
        // bitboards are used to look up attacks of sliding pieces. As a reference see
        // chessprogramming.wikispaces.com/Magic+Bitboards. In particular, here we
        // use the so called "fancy" approach.

        private static void init_magics(PieceType Pt, Bitboard[][] attacks, Bitboard[] magics,
                         Bitboard[] masks, Int32[] shifts, Square[] deltas, Fn index)
        {
            Bitboard edges, b;
            int i, size, booster;

            for (Square s = SquareC.SQ_A1; s <= SquareC.SQ_H8; s++)
            {
                // Board edges are not considered in the relevant occupancies
                edges = ((Constants.Rank1BB | Constants.Rank8BB) & ~Utils.rank_bb_S(s)) | ((Constants.FileABB | Constants.FileHBB) & ~Utils.file_bb_S(s));

                // Given a square 's', the mask is the bitboard of sliding attacks from
                // 's' computed on an empty board. The index must be big enough to contain
                // all the attacks for each possible subset of the mask and so is 2 power
                // the number of 1s of the mask. Hence we deduce the size of the shift to
                // apply to the 64 or 32 bits word to get the index.
                masks[s] = sliding_attack(deltas, s, 0) & ~edges;

#if X64
                shifts[s] = 64 - Bitcount.popcount_1s_Max15(masks[s]);
#else
                shifts[s] = 32 - Bitcount.popcount_1s_Max15(masks[s]);
#endif

                // Use Carry-Rippler trick to enumerate all subsets of masks[s] and
                // store the corresponding sliding attack bitboard in reference[].
                b = 0; size = 0;
                do
                {
                    occupancy[size] = b;
                    reference[size++] = sliding_attack(deltas, s, b);
                    b = (b - masks[s]) & masks[s];
                } while (b != 0);

                // Set the offset for the table of the next square. We have individual
                // table sizes for each square with "Fancy Magic Bitboards".
#if X64
                booster = MagicBoosters[1][rank_of(s)];
#else
                booster = MagicBoosters[0][rank_of(s)];
#endif

                attacks[s] = new Bitboard[size];

                // Find a magic for square 's' picking up an (almost) random number
                // until we find the one that passes the verification test.
                do
                {
                    magics[s] = pick_random(masks[s], rk, booster);
                    Array.Clear(attacks[s], 0, size);

                    // A good magic must map every possible occupancy to an index that
                    // looks up the correct sliding attack in the attacks[s] database.
                    // Note that we build up the database for square 's' as a side
                    // effect of verifying the magic.
                    for (i = 0; i < size; i++)
                    {
                        uint idx = index(Pt, s, occupancy[i]);

                        Bitboard attack = attacks[s][idx];

                        if ((attack != 0) && attack != reference[i])
                            break;

                        attacks[s][idx] = reference[i];
                    }
                }
                while (i != size);
            }
        }

        #endregion

        #region String operations

        internal static char piece_type_to_char(PieceType pt)
        {
            return _pieces[(int)pt];
        }

        internal static bool isdigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        internal static bool islower(char token)
        {
            return token.ToString().ToLowerInvariant() == token.ToString();
        }

        internal static char toupper(char token)
        {
            return token.ToString().ToUpperInvariant()[0];
        }

        internal static char tolower(char token)
        {
            return token.ToString().ToLowerInvariant()[0];
        }

        internal static char file_to_char(File f)
        {
            return (char)(f - FileC.FILE_A + (int)('a'));
        }

        internal static char rank_to_char(Rank r)
        {
            return (char)(r - RankC.RANK_1 + (int)('1'));
        }

        internal static string square_to_string(Square s)
        {
            return string.Concat(file_to_char(file_of(s)), rank_to_char(rank_of(s)));
        }

        internal static Stack<string> CreateStack(string input)
        {
            string[] lines = input.Trim().Split(' ');
            Stack<string> stack = new Stack<string>(); // LIFO
            for (int i = (lines.Length - 1); i >= 0; i--)
            {
                string line = lines[i];
                if (!String.IsNullOrEmpty(line))
                {
                    line = line.Trim();
                    stack.Push(line);
                }
            }
            return stack;
        }

        #endregion

        #region Bit operations

        /// Functions for testing whether a given bit is set in a bitboard, and for
        /// setting and clearing bits.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard bit_is_set(Bitboard b, Square s)
        {
            return b & SquareBB[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void set_bit(ref Bitboard b, Square s)
        {
            b |= SquareBB[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void xor_bit(ref Bitboard b, Square s)
        {
            b ^= SquareBB[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard set_bit(Bitboard b, Square s)
        {
            return b | SquareBB[s];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard xor_bit(Bitboard b, Square s)
        {
            return b ^ SquareBB[s];
        }

        /// single_bit() returns true if in the 'b' bitboard is set a single bit (or if
        /// b == 0).
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool single_bit(Bitboard b)
        {
            return ((b & (b - 1)) == 0);
        }

        /// more_than_one() returns true if in 'b' there is more than one bit set
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool more_than_one(Bitboard b)
        {
            return ((b & (b - 1)) != 0);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square pop_1st_bit(ref Bitboard b)
        {
#if X64
            Bitboard bb = b;
            b &= (b - 1);
            return (BSFTable[((bb & (0xffffffffffffffff - bb + 1)) * 0x218A392CD3D5DBFUL) >> 58]);
#else
            UInt64 bb = b ^ (b - 1);
            b &= (b - 1);
            return BSFTable[(((UInt32)((bb & 0xffffffff) ^ (bb >> 32))) * 0x783a9b23) >> 26];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square first_1(Bitboard b)
        {
#if X64
            return BSFTable[((b & (0xffffffffffffffff - b + 1)) * 0x218A392CD3D5DBFUL) >> 58];
#else
            b ^= (b - 1);
            return BSFTable[(((UInt32)((b & 0xffffffff) ^ (b >> 32))) * 0x783A9B23) >> 26];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square last_1(Bitboard b)
        {
            Square result = 0;
            if (b > 0xFFFFFFFF)
            {
                b >>= 32;
                result = 32;
            }
            if (b > 0xFFFF)
            {
                b >>= 16;
                result += 16;
            }
            if (b > 0xFF)
            {
                b >>= 8;
                result += 8;
            }
            return result + MS1BTable[b];
        }

        #endregion

        #region Bitboard operations
        
        /// Functions used to update a bitboard after a move. This is faster
        /// then calling a sequence of clear_bit() + set_bit()

        /// rank_bb() and file_bb() take a file or a square as input and return
        /// a bitboard representing all squares on the given file or rank.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard rank_bb_R(Rank r)
        {
            return RankBB[r];
        }

        /// rank_bb() and file_bb() take a file or a square as input and return
        /// a bitboard representing all squares on the given file or rank.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard rank_bb_S(Square s)
        {
            return RankBB[Utils.rank_of(s)];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard file_bb_S(Square s)
        {
            return FileBB[Utils.file_of(s)];
        }

        /// rank_bb() and file_bb() take a file or a square as input and return
        /// a bitboard representing all squares on the given file or rank.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard file_bb_F(File f)
        {
            return FileBB[f];
        }

        /// adjacent_files_bb takes a file as input and returns a bitboard representing
        /// all squares on the adjacent files.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard adjacent_files_bb(File f)
        {
            return AdjacentFilesBB[f];
        }

        /// this_and_adjacent_files_bb takes a file as input and returns a bitboard
        /// representing all squares on the given and adjacent files.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard this_and_adjacent_files_bb(File f)
        {
            return ThisAndAdjacentFilesBB[f];
        }

        /// in_front_bb() takes a color and a rank or square as input, and returns a
        /// bitboard representing all the squares on all ranks in front of the rank
        /// (or square), from the given color's point of view.  For instance,
        /// in_front_bb(WHITE, RANK_5) will give all squares on ranks 6, 7 and 8, while
        /// in_front_bb(BLACK, SQ_D3) will give all squares on ranks 1 and 2.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard in_front_bb_CR(Color c, Rank r)
        {
            return InFrontBB[c][r];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard in_front_bb_CS(Color c, Square s)
        {
            return InFrontBB[c][Utils.rank_of(s)];
        }

        /// Functions for computing sliding attack bitboards. rook_attacks_bb(),
        /// bishop_attacks_bb() and queen_attacks_bb() all take a square and a
        /// bitboard of occupied squares as input, and return a bitboard representing
        /// all squares attacked by a rook, bishop or queen on the given square.
        internal delegate uint Fn(PieceType Pt, Square s, Bitboard occ);

        /// Functions for computing sliding attack bitboards. Function attacks_bb() takes
        /// a square and a bitboard of occupied squares as input, and returns a bitboard
        /// representing all squares attacked by Pt (bishop or rook) on the given square.
        internal static uint magic_index(PieceType Pt, Square s, Bitboard occ)
        {
#if X64
            Bitboard[] Masks = Pt == PieceTypeC.ROOK ? RMasks : BMasks;
            Bitboard[] Magics = Pt == PieceTypeC.ROOK ? RMagics : BMagics;
            int[] Shifts = Pt == PieceTypeC.ROOK ? RShifts : BShifts;
            return (uint)(((occ & Masks[s]) * Magics[s]) >> Shifts[s]);
#else
            Bitboard[] Masks = Pt == PieceTypeC.ROOK ? RMasks : BMasks;
            Bitboard[] Magics = Pt == PieceTypeC.ROOK ? RMagics : BMagics;
            int[] Shifts = Pt == PieceTypeC.ROOK ? RShifts : BShifts;

            UInt32 lo = (UInt32)(occ) & (UInt32)(Masks[s]);
            UInt32 hi = (UInt32)(occ >> 32) & (UInt32)(Masks[s] >> 32);
            return (lo * (UInt32)(Magics[s]) ^ hi * (UInt32)(Magics[s] >> 32)) >> Shifts[s];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard rook_attacks_bb(Square s, Bitboard occ)
        {
#if X64
            return RAttacks[s][(((occ & RMasks[s]) * RMagics[s]) >> RShifts[s])];
#else
            UInt32 lo = (UInt32)(occ) & (UInt32)(RMasks[s]);
            UInt32 hi = (UInt32)(occ >> 32) & (UInt32)(RMasks[s] >> 32);
            return RAttacks[s][(lo * (UInt32)(RMagics[s]) ^ hi * (UInt32)(RMagics[s] >> 32)) >> RShifts[s]];
#endif
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard bishop_attacks_bb(Square s, Bitboard occ)
        {
#if X64
            return BAttacks[s][(((occ & BMasks[s]) * BMagics[s]) >> BShifts[s])];
#else
            UInt32 lo = (UInt32)(occ) & (UInt32)(BMasks[s]);
            UInt32 hi = (UInt32)(occ >> 32) & (UInt32)(BMasks[s] >> 32);
            return BAttacks[s][(lo * (UInt32)(BMagics[s]) ^ hi * (UInt32)(BMagics[s] >> 32)) >> BShifts[s]];
#endif
        }

        /// between_bb returns a bitboard representing all squares between two squares.
        /// For instance, between_bb(SQ_C4, SQ_F7) returns a bitboard with the bits for
        /// square d5 and e6 set.  If s1 and s2 are not on the same line, file or diagonal,
        /// 0 is returned.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard between_bb(Square s1, Square s2)
        {
            return BetweenBB[s1][s2];
        }

        /// forward_bb takes a color and a square as input, and returns a bitboard
        /// representing all squares along the line in front of the square, from the
        /// point of view of the given color. Definition of the table is:
        /// ForwardBB[c][s] = in_front_bb(c, s) & file_bb(s)

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard forward_bb(Color c, Square s)
        {
            return ForwardBB[c][s];
        }

        /// passed_pawn_mask takes a color and a square as input, and returns a
        /// bitboard mask which can be used to test if a pawn of the given color on
        /// the given square is a passed pawn. Definition of the table is:
        /// PassedPawnMask[c][s] = in_front_bb(c, s) & this_and_adjacent_files_bb(s)

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard passed_pawn_mask(Color c, Square s)
        {
            return PassedPawnMask[c][s];
        }

        /// attack_span_mask takes a color and a square as input, and returns a bitboard
        /// representing all squares that can be attacked by a pawn of the given color
        /// when it moves along its file starting from the given square. Definition is:
        /// AttackSpanMask[c][s] = in_front_bb(c, s) & adjacent_files_bb(s);

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard attack_span_mask(Color c, Square s)
        {
            return AttackSpanMask[c][s];
        }

        /// squares_aligned returns true if the squares s1, s2 and s3 are aligned
        /// either on a straight or on a diagonal line.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool squares_aligned(Square s1, Square s2, Square s3)
        {
            return ((BetweenBB[s1][s2] | BetweenBB[s1][s3] | BetweenBB[s2][s3])
                  & (SquareBB[s1] | SquareBB[s2] | SquareBB[s3])) != 0;
        }

        /// same_color_squares() returns a bitboard representing all squares with
        /// the same color of the given square.

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Bitboard same_color_squares(Square s)
        {
            return (bit_is_set(0xAA55AA55AA55AA55UL, s) != 0) ? 0xAA55AA55AA55AA55UL
                                                        : ~0xAA55AA55AA55AA55UL;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static int square_distance(Square s1, Square s2)
        {
            return SquareDistance[s1][s2];
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value mate_in(int ply)
        {
            return ValueC.VALUE_MATE - ply;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value mated_in(int ply)
        {
            return -ValueC.VALUE_MATE + ply;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Piece make_piece(Color c, PieceType pt)
        {
            return ((c << 3) | pt);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static PieceType type_of(Piece p)
        {
            return (p & 7);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Color color_of(Piece p)
        {
            return (p >> 3);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Color flip_C(Color c)
        {
            return (c ^ 1);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square make_square(File f, Rank r)
        {
            return ((r << 3) | f);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_ok_S(Square s)
        {
            return s >= SquareC.SQ_A1 && s <= SquareC.SQ_H8;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_ok_M(Move m)
        {
            return from_sq(m) != to_sq(m); // Catches also MOVE_NULL and MOVE_NONE
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static File file_of(Square s)
        {
            return (s & 7);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Rank rank_of(Square s)
        {
            return (s >> 3);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square flip_S(Square s)
        {
            return (s ^ 56);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square mirror(Square s)
        {
            return (s ^ 7); // Horizontal flip SQ_A1 -> SQ_H1
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square relative_square(Color c, Square s)
        {
            return (s ^ (c * 56));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Rank relative_rank_CR(Color c, Rank r)
        {
            return (r ^ (c * 7));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Rank relative_rank_CS(Color c, Square s)
        {
            return ((s >> 3) ^ (c * 7));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool opposite_colors(Square s1, Square s2)
        {
            int s = s1 ^ s2;
            return (((s >> 3) ^ s) & 1) != 0;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static int file_distance(Square s1, Square s2)
        {
            return Math.Abs(file_of(s1) - file_of(s2));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static int rank_distance(Square s1, Square s2)
        {
            return Math.Abs(rank_of(s1) - rank_of(s2));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square pawn_push(Color c)
        {
            return c == ColorC.WHITE ? SquareC.DELTA_N : SquareC.DELTA_S;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square from_sq(Move m)
        {
            return ((m >> 6) & 0x3F);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Square to_sq(Move m)
        {
            return (m & 0x3F);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_special(Move m)
        {
            return (m & (3 << 14)) != 0;
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_promotion(Move m)
        {
            return (m & (3 << 14)) == (1 << 14);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_enpassant(Move m)
        {
            return (m & (3 << 14)) == (2 << 14);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool is_castle(Move m)
        {
            return (m & (3 << 14)) == (3 << 14);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static PieceType promotion_type(Move m)
        {
            return (((m >> 12) & 3) + 2);
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Move make_move(Square from, Square to)
        {
            return (to | (from << 6));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Move make_promotion(Square from, Square to, PieceType pt)
        {
            return (to | (from << 6) | (1 << 14) | ((pt - 2) << 12));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Move make_enpassant(Square from, Square to)
        {
            return (to | (from << 6) | (2 << 14));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Move make_castle(Square from, Square to)
        {
            return (to | (from << 6) | (3 << 14));
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static CastleRight make_castle_right(Color c, CastlingSide s)
        {
            return CastleRightC.WHITE_OO << ((s == CastlingSideC.QUEEN_SIDE ? 1 : 0) + 2 * c);
        }

        #endregion

        #region Engine info

        /// engine_info() returns the full name of the current Stockfish version.
        /// This will be either "Portfish YYMMDD" (where YYMMDD is the date when
        /// the program was compiled) or "Portfish <version number>", depending
        /// on whether Version is empty.
        internal static string engine_info() { return engine_info(false); }

        internal static string engine_info(bool to_uci)
        {
            // Get current assembly
            Assembly assembly = typeof(Engine).GetTypeInfo().Assembly;

            // File version
            AssemblyFileVersionAttribute fileVersionRaw = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            Version fileVersion = new Version(fileVersionRaw.Version);

            // Extract version/build date
            string fullName = assembly.FullName;
            int vspos = fullName.IndexOf("Version=");
            int vepos = fullName.IndexOf(",", vspos);
            string versionRaw = fullName.Substring(vspos + 8, vepos - vspos - 8);
            Version version = new Version(versionRaw);
            DateTime buildDateTime = new DateTime(2000, 1, 1).Add(
                new TimeSpan(
                    TimeSpan.TicksPerDay * version.Build + // days since 1 January 2000
                    TimeSpan.TicksPerSecond * 2 * version.Revision)); // seconds since midnight, (multiply by 2 to get original)

            // Get version info
            string versionInfo = buildDateTime.Year.ToString() + buildDateTime.Month.ToString().PadLeft(2, '0') + buildDateTime.Day.ToString().PadLeft(2, '0');
            if (fileVersion != null)
            {
                versionInfo = fileVersion.ToString();
            }

            // Create version
            StringBuilder sb = new StringBuilder();
            sb.Append("Portfish ").Append(versionInfo).Append(cpu64);
            sb.Append(to_uci ? "\nid author " : " by ").Append("Tord Romstad, Marco Costalba, Joona Kiiski and Balint Pfliegel");
            return sb.ToString();
        }

        #endregion

        #region Move printing

        /// move_to_uci() converts a move to a string in coordinate notation
        /// (g1f3, a7a8q, etc.). The only special case is castling moves, where we
        /// print in the e1g1 notation in normal chess mode, and in e1h1 notation in
        /// Chess960 mode. Instead internally Move is coded as "king captures rook".
        internal static string move_to_uci(Move m, bool chess960)
        {
            Square from = from_sq(m);
            Square to = to_sq(m);
            string promotion = string.Empty;

            if (m == MoveC.MOVE_NONE)
                return "(none)";

            if (m == MoveC.MOVE_NULL)
                return "0000";

            if (is_castle(m) && !chess960)
                to = from + (file_of(to) == FileC.FILE_H ? 2 : -2);

            if (is_promotion(m))
                promotion = tolower(piece_type_to_char(promotion_type(m))).ToString();

            return string.Concat(square_to_string(from), square_to_string(to), promotion);
        }

        /// move_from_uci() takes a position and a string representing a move in
        /// simple coordinate notation and returns an equivalent Move if any.
        /// Moves are guaranteed to be legal.
        internal static Move move_from_uci(Position pos, string str)
        {
            string strLowerPromotion = (str.Length == 5 ? str.Substring(0, 4) + str.Substring(4).ToLowerInvariant() : str);
            MList mlist = MListBroker.GetObject(); mlist.pos = 0;
            Movegen.generate_legal(pos, mlist.moves, ref mlist.pos);
            for (int i = 0; i < mlist.pos; i++)
            {
                if (strLowerPromotion == Utils.move_to_uci(mlist.moves[i].move, pos.chess960))
                {
                    Move retval = mlist.moves[i].move;
                    MListBroker.Free();
                    return retval;
                }
            }
            MListBroker.Free();
            return MoveC.MOVE_NONE;
        }

        /// move_to_san() takes a position and a move as input, where it is assumed
        /// that the move is a legal move for the position. The return value is
        /// a string containing the move in short algebraic notation.
        internal static string move_to_san(Position pos, Move m)
        {
            if (m == MoveC.MOVE_NONE)
                return "(none)";

            if (m == MoveC.MOVE_NULL)
                return "(null)";

            Debug.Assert(is_ok_M(m));

            Bitboard attackers;
            bool ambiguousMove, ambiguousFile, ambiguousRank;
            Square sq, from = from_sq(m);
            Square to = to_sq(m);
            PieceType pt = type_of(pos.piece_moved(m));

            StringBuilder san = new StringBuilder();

            if (is_castle(m))
                san.Append((to_sq(m) < from_sq(m) ? "O-O-O" : "O-O"));
            else
            {
                if (pt != PieceTypeC.PAWN)
                {
                    san.Append(piece_type_to_char(pt).ToString());

                    // Disambiguation if we have more then one piece with destination 'to'
                    // note that for pawns is not needed because starting file is explicit.
                    attackers = pos.attackers_to(to) & pos.pieces_PTC(pt, pos.sideToMove);
                    xor_bit(ref attackers, from);
                    ambiguousMove = ambiguousFile = ambiguousRank = false;

                    while (attackers != 0)
                    {
                        sq = pop_1st_bit(ref attackers);

                        // Pinned pieces are not included in the possible sub-set
                        if (!pos.pl_move_is_legal(make_move(sq, to), pos.pinned_pieces()))
                            continue;

                        if (file_of(sq) == file_of(from))
                            ambiguousFile = true;

                        if (rank_of(sq) == rank_of(from))
                            ambiguousRank = true;

                        ambiguousMove = true;
                    }

                    if (ambiguousMove)
                    {
                        if (!ambiguousFile)
                            san.Append(file_to_char(file_of(from)));
                        else if (!ambiguousRank)
                            san.Append(rank_to_char(rank_of(from)));
                        else
                            san.Append(square_to_string(from));
                    }
                }

                if (pos.is_capture(m))
                {
                    if (pt == PieceTypeC.PAWN)
                        san.Append(file_to_char(file_of(from)));

                    san.Append('x');
                }

                san.Append(square_to_string(to));

                if (is_promotion(m))
                {
                    san.Append('=');
                    san.Append(piece_type_to_char(promotion_type(m)));
                }
            }

            CheckInfo ci = CheckInfoBroker.GetObject();
            ci.CreateCheckInfo(pos);
            if (pos.move_gives_check(m, ci))
            {
                StateInfo st = new StateInfo();
                pos.do_move(m, st);
                MList mlist = MListBroker.GetObject(); mlist.pos = 0;
                Movegen.generate_legal(pos, mlist.moves, ref mlist.pos);
                san.Append(mlist.pos > 0 ? "+" : "#");
                MListBroker.Free();
                pos.undo_move(m);
            }
            CheckInfoBroker.Free();

            return san.ToString();
        }

        #endregion

        #region Scores

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Score make_score(int mg, int eg) { return ((mg << 16) + eg); }

        /// Extracting the signed lower and upper 16 bits it not so trivial because
        /// according to the standard a simple cast to short is implementation defined
        /// and so is a right shift of a signed integer.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value mg_value(Score s) { return (((s + 32768) & ~0xffff) / 0x10000); }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static Value eg_value(Score s)
        {
            return ((Int16)(s & 0xffff));
        }

        // apply_weight() applies an evaluation weight to a value trying to prevent overflow
        internal static Score apply_weight(Score v, Score w)
        {
            return (((((int)((((v + 32768) & ~0xffff) / 0x10000)) * (((w + 32768) & ~0xffff) / 0x10000)) / 0x100) << 16) + (((int)(((Int16)(v & 0xffff))) * ((Int16)(w & 0xffff))) / 0x100));
        }

        #endregion

        #region Sort and existance

        internal static void sort(MoveStack[] data, int firstMove, int lastMove)
        {
            MoveStack tmp;
            int p, q;

            for (p = firstMove + 1; p < lastMove; p++)
            {
                tmp = data[p];
                for (q = p; q != firstMove && data[q - 1].score < tmp.score; --q)
                    data[q] = data[q - 1];
                data[q] = tmp;
            }
        }

        internal static void sort(List<RootMove> data, int firstMove, int lastMove)
        {
            RootMove tmp;
            int p, q;

            for (p = firstMove + 1; p < lastMove; p++)
            {
                tmp = data[p];
                for (q = p; q != firstMove && data[q - 1].score < tmp.score; --q)
                    data[q] = data[q - 1];
                data[q] = tmp;
            }
        }

        internal static bool existSearchMove(List<Move> moves, Move m) // count elements that match _Val
        {
            int moveLength = moves.Count;
            if (moveLength == 0) return false;
            for (int i = 0; i < moveLength; i++)
            {
                if (moves[i] == m) return true;
            }
            return false;
        }

        internal static bool existRootMove(List<RootMove> moves, Move m) // count elements that match _Val
        {
            int moveLength = moves.Count;
            if (moveLength == 0) return false;
            for (int i = 0; i < moveLength; i++)
            {
                if (moves[i].pv[0] == m) return true;
            }
            return false;
        }

        #endregion

        #region PSQT

        /// PSQT[PieceType][Square] contains Piece-Square scores. For each piece type on
        /// a given square a (midgame, endgame) score pair is assigned. PSQT is defined
        /// for white side, for black side the tables are symmetric.
        internal static readonly Score[][] PSQT = new Score[][]{
          new Score[] { },
          new Score[] { // Pawn
           make_score( 0, 0), make_score( 0, 0), make_score( 0, 0), make_score( 0, 0), make_score(0,  0), make_score( 0, 0), make_score( 0, 0), make_score(  0, 0),
           make_score(-28,-8), make_score(-6,-8), make_score( 4,-8), make_score(14,-8), make_score(14,-8), make_score( 4,-8), make_score(-6,-8), make_score(-28,-8),
           make_score(-28,-8), make_score(-6,-8), make_score( 9,-8), make_score(36,-8), make_score(36,-8), make_score( 9,-8), make_score(-6,-8), make_score(-28,-8),
           make_score(-28,-8), make_score(-6,-8), make_score(17,-8), make_score(58,-8), make_score(58,-8), make_score(17,-8), make_score(-6,-8), make_score(-28,-8),
           make_score(-28,-8), make_score(-6,-8), make_score(17,-8), make_score(36,-8), make_score(36,-8), make_score(17,-8), make_score(-6,-8), make_score(-28,-8),
           make_score(-28,-8), make_score(-6,-8), make_score( 9,-8), make_score(14,-8), make_score(14,-8), make_score( 9,-8), make_score(-6,-8), make_score(-28,-8),
           make_score(-28,-8), make_score(-6,-8), make_score( 4,-8), make_score(14,-8), make_score(14,-8), make_score( 4,-8), make_score(-6,-8), make_score(-28,-8),
           make_score(  0, 0), make_score( 0, 0), make_score( 0, 0), make_score( 0, 0), make_score(0,  0), make_score( 0, 0), make_score( 0, 0), make_score(  0, 0)
          },
          new Score[]{ // Knight
           make_score(-135,-104), make_score(-107,-79), make_score(-80,-55), make_score(-67,-42), make_score(-67,-42), make_score(-80,-55), make_score(-107,-79), make_score(-135,-104),
           make_score( -93, -79), make_score( -67,-55), make_score(-39,-30), make_score(-25,-17), make_score(-25,-17), make_score(-39,-30), make_score( -67,-55), make_score( -93, -79),
           make_score( -53, -55), make_score( -25,-30), make_score(  1, -6), make_score( 13,  5), make_score( 13,  5), make_score(  1, -6), make_score( -25,-30), make_score( -53, -55),
           make_score( -25, -42), make_score(   1,-17), make_score( 27,  5), make_score( 41, 18), make_score( 41, 18), make_score( 27,  5), make_score(   1,-17), make_score( -25, -42),
           make_score( -11, -42), make_score(  13,-17), make_score( 41,  5), make_score( 55, 18), make_score( 55, 18), make_score( 41,  5), make_score(  13,-17), make_score( -11, -42),
           make_score( -11, -55), make_score(  13,-30), make_score( 41, -6), make_score( 55,  5), make_score( 55,  5), make_score( 41, -6), make_score(  13,-30), make_score( -11, -55),
           make_score( -53, -79), make_score( -25,-55), make_score(  1,-30), make_score( 13,-17), make_score( 13,-17), make_score(  1,-30), make_score( -25,-55), make_score( -53, -79),
           make_score(-193,-104), make_score( -67,-79), make_score(-39,-55), make_score(-25,-42), make_score(-25,-42), make_score(-39,-55), make_score( -67,-79), make_score(-193,-104)
          },
          new Score[]{ // Bishop
           make_score(-40,-59), make_score(-40,-42), make_score(-35,-35), make_score(-30,-26), make_score(-30,-26), make_score(-35,-35), make_score(-40,-42), make_score(-40,-59),
           make_score(-17,-42), make_score(  0,-26), make_score( -4,-18), make_score(  0,-11), make_score(  0,-11), make_score( -4,-18), make_score(  0,-26), make_score(-17,-42),
           make_score(-13,-35), make_score( -4,-18), make_score(  8,-11), make_score(  4, -4), make_score(  4, -4), make_score(  8,-11), make_score( -4,-18), make_score(-13,-35),
           make_score( -8,-26), make_score(  0,-11), make_score(  4, -4), make_score( 17,  4), make_score( 17,  4), make_score(  4, -4), make_score(  0,-11), make_score( -8,-26),
           make_score( -8,-26), make_score(  0,-11), make_score(  4, -4), make_score( 17,  4), make_score( 17,  4), make_score(  4, -4), make_score(  0,-11), make_score( -8,-26),
           make_score(-13,-35), make_score( -4,-18), make_score(  8,-11), make_score(  4, -4), make_score(  4, -4), make_score(  8,-11), make_score( -4,-18), make_score(-13,-35),
           make_score(-17,-42), make_score(  0,-26), make_score( -4,-18), make_score(  0,-11), make_score(  0,-11), make_score( -4,-18), make_score(  0,-26), make_score(-17,-42),
           make_score(-17,-59), make_score(-17,-42), make_score(-13,-35), make_score( -8,-26), make_score( -8,-26), make_score(-13,-35), make_score(-17,-42), make_score(-17,-59)
          },
          new Score[]{ // Rook
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3),
           make_score(-12, 3), make_score(-7, 3), make_score(-2, 3), make_score(2, 3), make_score(2, 3), make_score(-2, 3), make_score(-7, 3), make_score(-12, 3)
          },
          new Score[]{ // Queen
           make_score(8,-80), make_score(8,-54), make_score(8,-42), make_score(8,-30), make_score(8,-30), make_score(8,-42), make_score(8,-54), make_score(8,-80),
           make_score(8,-54), make_score(8,-30), make_score(8,-18), make_score(8, -6), make_score(8, -6), make_score(8,-18), make_score(8,-30), make_score(8,-54),
           make_score(8,-42), make_score(8,-18), make_score(8, -6), make_score(8,  6), make_score(8,  6), make_score(8, -6), make_score(8,-18), make_score(8,-42),
           make_score(8,-30), make_score(8, -6), make_score(8,  6), make_score(8, 18), make_score(8, 18), make_score(8,  6), make_score(8, -6), make_score(8,-30),
           make_score(8,-30), make_score(8, -6), make_score(8,  6), make_score(8, 18), make_score(8, 18), make_score(8,  6), make_score(8, -6), make_score(8,-30),
           make_score(8,-42), make_score(8,-18), make_score(8, -6), make_score(8,  6), make_score(8,  6), make_score(8, -6), make_score(8,-18), make_score(8,-42),
           make_score(8,-54), make_score(8,-30), make_score(8,-18), make_score(8, -6), make_score(8, -6), make_score(8,-18), make_score(8,-30), make_score(8,-54),
           make_score(8,-80), make_score(8,-54), make_score(8,-42), make_score(8,-30), make_score(8,-30), make_score(8,-42), make_score(8,-54), make_score(8,-80)
          },
          new Score[]{ // King
           make_score(287, 18), make_score(311, 77), make_score(262,105), make_score(214,135), make_score(214,135), make_score(262,105), make_score(311, 77), make_score(287, 18),
           make_score(262, 77), make_score(287,135), make_score(238,165), make_score(190,193), make_score(190,193), make_score(238,165), make_score(287,135), make_score(262, 77),
           make_score(214,105), make_score(238,165), make_score(190,193), make_score(142,222), make_score(142,222), make_score(190,193), make_score(238,165), make_score(214,105),
           make_score(190,135), make_score(214,193), make_score(167,222), make_score(119,251), make_score(119,251), make_score(167,222), make_score(214,193), make_score(190,135),
           make_score(167,135), make_score(190,193), make_score(142,222), make_score( 94,251), make_score( 94,251), make_score(142,222), make_score(190,193), make_score(167,135),
           make_score(142,105), make_score(167,165), make_score(119,193), make_score( 69,222), make_score( 69,222), make_score(119,193), make_score(167,165), make_score(142,105),
           make_score(119, 77), make_score(142,135), make_score( 94,165), make_score( 46,193), make_score( 46,193), make_score( 94,165), make_score(142,135), make_score(119, 77),
           make_score(94,  18), make_score(119, 77), make_score( 69,105), make_score( 21,135), make_score( 21,135), make_score( 69,105), make_score(119, 77), make_score( 94, 18)
          }
        };

        #endregion

        #region Debug methods

        /// Debug functions used mainly to collect run-time statistics
        static readonly UInt64[] hits = new UInt64[2];
        static readonly UInt64[] means = new UInt64[2];

        internal static void dbg_hit_on(bool b) { hits[0]++; if (b) hits[1]++; }
        internal static void dbg_hit_on_c(bool c, bool b) { if (c) dbg_hit_on(b); }
        internal static void dbg_mean_of(int v) { means[0]++; means[1] += (UInt64)v; }

        internal static void dbg_print()
        {
            if (hits[0] != 0)
            {
                Plug.Write("Total ");
                Plug.Write(hits[0].ToString());
                Plug.Write(" Hits ");
                Plug.Write(hits[1].ToString());
                Plug.Write(" hit rate (%) ");
                Plug.Write((100 * hits[1] / hits[0]).ToString());
                Plug.Write(Constants.endl);
            }

            if (means[0] != 0)
            {
                Plug.Write("Total ");
                Plug.Write(means[0].ToString());
                Plug.Write(" Mean ");
                Plug.Write(((float)means[1] / means[0]).ToString());
                Plug.Write(Constants.endl);
            }
        }

        #endregion
    }
}
