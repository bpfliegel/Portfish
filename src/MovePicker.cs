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
    /// MovePicker class is used to pick one pseudo legal move at a time from the
    /// current position. The most important method is next_move(), which returns a
    /// new pseudo legal move each time it is called, until there are no moves left,
    /// when MOVE_NONE is returned. In order to improve the efficiency of the alpha
    /// beta algorithm, MovePicker attempts to return the moves which are most likely
    /// to get a cut-off first.
    internal sealed class MovePicker
    {
        Position pos;
        History H;
        Depth depth;
        Move ttMove;
        Square recaptureSquare;
        int captureThreshold, phase;
        int curMovePos, lastMovePos, lastQuietPos, lastBadCapturePos;
        MovePicker mpExternal = null;
        readonly MoveStack[] ms = new MoveStack[Constants.MAX_MOVES + 2]; // 2 additional for the killers at the end
        int mpos = 0;

        public void Recycle()
        {
            pos = null;
            H = null;
            mpExternal = null;
        }

        #region Helpers

        // Unary predicate used by std::partition to split positive scores from remaining
        // ones so to sort separately the two sets, and with the second sort delayed.
        //internal static bool has_positive_score(ref MoveStack move) { return move.score > 0; }

        // Picks and pushes to the front the best move in range [firstMove, lastMove),
        // it is faster than sorting all the moves in advance when moves are few, as
        // normally are the possible captures.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        int pick_best()
        {
            int first = curMovePos;
            int max = curMovePos;
            if (first != lastMovePos)
            {
                for (; ++first != lastMovePos; )
                {
                    if (ms[max].score < ms[first].score)
                    {
                        max = first;
                    }
                }
            }
            MoveStack temp = ms[curMovePos]; ms[curMovePos] = ms[max]; ms[max] = temp;
            return curMovePos;
        }

        //Rearranges the elements in the range [first,last), in such a way that all the elements for which pred returns true precede all those for which it returns false. 
        // The iterator returned points to the first element of the second group.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        int partition(int first, int last)
        {
            // move elements satisfying _Pred to beginning of sequence
            for (; ; ++first)
            {	// find any out-of-order pair
                for (; first != last && (ms[first].score>0); ++first)
                    ;	// skip in-place elements at beginning
                if (first == last)
                    break;	// done

                for (; first != --last && (ms[last].score<=0); )
                    ;	// skip in-place elements at end
                if (first == last)
                    break;	// done

                MoveStack temp = ms[last]; ms[last] = ms[first]; ms[first] = temp;
            }
            return first;
        }

        #endregion

        internal static class SequencerC
        {
            internal const int MAIN_SEARCH = 0, CAPTURES_S1 = 1, KILLERS_S1 = 2, QUIETS_1_S1 = 3, QUIETS_2_S1 = 4, BAD_CAPTURES_S1 = 5,
            EVASION = 6, EVASIONS_S2 = 7,
            QSEARCH_0 = 8, CAPTURES_S3 = 9, QUIET_CHECKS_S3 = 10,
            QSEARCH_1 = 11, CAPTURES_S4 = 12,
            PROBCUT = 13, CAPTURES_S5 = 14,
            RECAPTURE = 15, CAPTURES_S6 = 16,
            STOP = 17;
        };

        /// Constructors of the MovePicker class. As arguments we pass information
        /// to help it to return the presumably good moves first, to decide which
        /// moves to return (in the quiescence search, for instance, we only want to
        /// search captures, promotions and some checks) and about how important good
        /// move ordering is at the current node.
        internal void MovePickerC(Position p, Move ttm, Depth d, History h,
                       Stack ss, Value beta, MovePicker mpExt)
        {
            pos = p;
            H = h;
            depth = d;
            mpExternal = mpExt;

            Debug.Assert(d > DepthC.DEPTH_ZERO);

            captureThreshold = 0;
            curMovePos = lastMovePos = 0;
            lastBadCapturePos = Constants.MAX_MOVES - 1;

            recaptureSquare = 0;
            lastQuietPos = 0;
            mpos = 0;

            if (p.in_check())
            {
                phase = SequencerC.EVASION;
            }
            else
            {
                phase = SequencerC.MAIN_SEARCH;

                ms[Constants.MAX_MOVES].move = ss.killers0;
                ms[Constants.MAX_MOVES + 1].move = ss.killers1;

                // Consider sligtly negative captures as good if at low depth and far from beta
                if (ss.eval < beta - Constants.PawnValueMidgame && d < 3 * DepthC.ONE_PLY)
                    captureThreshold = -Constants.PawnValueMidgame;

                // Consider negative captures as good if still enough to reach beta
                else if (ss.eval > beta)
                    captureThreshold = beta - ss.eval;
            }

            ttMove = (ttm != 0 && pos.is_pseudo_legal(ttm) ? ttm : MoveC.MOVE_NONE);
            lastMovePos += ((ttMove != MoveC.MOVE_NONE) ? 1 : 0);
        }

        internal void MovePickerC(Position p, Move ttm, Depth d, History h,
                               Square sq)
        {
            pos = p;
            H = h;
            curMovePos = 0;
            lastMovePos = 0;

            Debug.Assert(d <= DepthC.DEPTH_ZERO);

            depth = 0;
            recaptureSquare = 0;
            captureThreshold = 0;
            lastQuietPos = 0; lastBadCapturePos = 0;
            mpos = 0;

            if (p.in_check())
                phase = SequencerC.EVASION;

            else if (d > DepthC.DEPTH_QS_NO_CHECKS)
                phase = SequencerC.QSEARCH_0;

            else if (d > DepthC.DEPTH_QS_RECAPTURES)
            {
                phase = SequencerC.QSEARCH_1;

                // Skip TT move if is not a capture or a promotion, this avoids qsearch
                // tree explosion due to a possible perpetual check or similar rare cases
                // when TT table is full.
                if ((ttm != 0) && !pos.is_capture_or_promotion(ttm))
                    ttm = MoveC.MOVE_NONE;
            }
            else
            {
                phase = SequencerC.RECAPTURE;
                recaptureSquare = sq;
                ttm = MoveC.MOVE_NONE;
            }

            ttMove = ((ttm != 0) && pos.is_pseudo_legal(ttm) ? ttm : MoveC.MOVE_NONE);
            lastMovePos += ((ttMove != MoveC.MOVE_NONE) ? 1 : 0);
        }

        internal void MovePickerC(Position p, Move ttm, History h, PieceType pt)
        {
            pos = p;
            H = h;
            curMovePos = 0;
            lastMovePos = 0;

            Debug.Assert(!pos.in_check());

            depth = 0;
            ttMove = 0;
            lastQuietPos = 0; lastBadCapturePos = 0;
            mpos = 0;

            phase = SequencerC.PROBCUT;

            // In ProbCut we generate only captures better than parent's captured piece
            captureThreshold = Position.PieceValueMidgame[pt];
            ttMove = ((ttm != 0) && pos.is_pseudo_legal(ttm) ? ttm : MoveC.MOVE_NONE);

            if ((ttMove != 0) && (!pos.is_capture(ttMove) || pos.see(ttMove, false) <= captureThreshold))
                ttMove = MoveC.MOVE_NONE;

            lastMovePos += ((ttMove != MoveC.MOVE_NONE) ? 1 : 0);
        }

        /// MovePicker::score_captures(), MovePicker::score_noncaptures() and
        /// MovePicker::score_evasions() assign a numerical move ordering score
        /// to each move in a move list.  The moves with highest scores will be
        /// picked first by next_move().
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void score_captures()
        {
            // Winning and equal captures in the main search are ordered by MVV/LVA.
            // Suprisingly, this appears to perform slightly better than SEE based
            // move ordering. The reason is probably that in a position with a winning
            // capture, capturing a more valuable (but sufficiently defended) piece
            // first usually doesn't hurt. The opponent will have to recapture, and
            // the hanging piece will still be hanging (except in the unusual cases
            // where it is possible to recapture with the hanging piece). Exchanging
            // big pieces before capturing a hanging piece probably helps to reduce
            // the subtree size.
            // In main search we want to push captures with negative SEE values to
            // badCaptures[] array, but instead of doing it now we delay till when
            // the move has been picked up in pick_move_from_list(), this way we save
            // some SEE calls in case we get a cutoff (idea from Pablo Vazquez).
            for (int idx = 0; idx < lastMovePos; idx++)
            {
                Move m = ms[idx].move;
                ms[idx].score = Position.PieceValueMidgame[pos.board[m & 0x3F]] - (pos.board[((m >> 6) & 0x3F)] & 7);
                if ((m & (3 << 14)) == (1 << 14))
                {
                    ms[idx].score += Position.PieceValueMidgame[(((m >> 12) & 3) + 2)];
                }
            }
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void score_noncaptures()
        {
            for (int idx = 0; idx < lastMovePos; idx++)
            {
                ms[idx].score = H.history[pos.board[((ms[idx].move >> 6) & 0x3F)]][ms[idx].move & 0x3F];
            }
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void score_evasions()
        {
            // Try good captures ordered by MVV/LVA, then non-captures if destination square
            // is not under attack, ordered by history value, then bad-captures and quiet
            // moves with a negative SEE. This last group is ordered by the SEE score.

            // Skip if we don't have at least two moves to order
            if (lastMovePos < 2)
                return;

            int seeScore;
            for (int idx = 0; idx < lastMovePos; idx++)
            {
                Move m = ms[idx].move;
                if ((seeScore = ((Position.PieceValueMidgame[pos.board[m & 0x3F]] >= Position.PieceValueMidgame[pos.board[((m >> 6) & 0x3F)]]) ? 1 : pos.see(m, false))) < 0)
                {
                    ms[idx].score = seeScore - History.MaxValue; // Be sure we are at the bottom
                }
                else if (((pos.board[m & 0x3F] != PieceC.NO_PIECE) && !((m & (3 << 14)) == (3 << 14))) || ((m & (3 << 14)) == (2 << 14)))
                {
                    ms[idx].score = Position.PieceValueMidgame[pos.board[(m & 0x3F)]] - (pos.board[((m >> 6) & 0x3F)] & 7) + History.MaxValue;
                }
                else
                {
                    ms[idx].score = H.value(pos.board[((m >> 6) & 0x3F)], (m & 0x3F));
                }
            }
        }

#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void sort()
        {
            MoveStack tmp;
            int p, q;

            for (p = curMovePos + 1; p < lastMovePos; p++)
            {
                tmp = ms[p];
                for (q = p; q != curMovePos && ms[q - 1].score < tmp.score; --q)
                    ms[q] = ms[q - 1];
                ms[q] = tmp;
            }
        }

        /// MovePicker::generate_next() generates, scores and sorts the next bunch of moves,
        /// when there are no more moves to try for the current phase.
        private void generate_next()
        {
            curMovePos = 0;

            switch (++phase)
            {
                case SequencerC.CAPTURES_S1:
                case SequencerC.CAPTURES_S3:
                case SequencerC.CAPTURES_S4:
                case SequencerC.CAPTURES_S5:
                case SequencerC.CAPTURES_S6:
                    mpos = 0;
                    Movegen.generate_capture(pos, ms, ref mpos);
                    lastMovePos = mpos;
                    score_captures();
                    return;

                case SequencerC.KILLERS_S1:
                    curMovePos = Constants.MAX_MOVES;//killers[0];
                    lastMovePos = curMovePos + 2;
                    return;

                case SequencerC.QUIETS_1_S1:
                    mpos = 0;
                    Movegen.generate_quiet(pos, ms, ref mpos);
                    lastQuietPos = lastMovePos = mpos;
                    score_noncaptures();
                    lastMovePos = partition(curMovePos, lastMovePos);
                    sort();
                    return;

                case SequencerC.QUIETS_2_S1:
                    curMovePos = lastMovePos;
                    lastMovePos = lastQuietPos;
                    if (depth >= 3 * DepthC.ONE_PLY)
                    {
                        sort();
                    }
                    return;

                case SequencerC.BAD_CAPTURES_S1:
                    // Just pick them in reverse order to get MVV/LVA ordering
                    curMovePos = Constants.MAX_MOVES - 1;
                    lastMovePos = lastBadCapturePos;
                    return;

                case SequencerC.EVASIONS_S2:
                    mpos = 0;
                    Movegen.generate_evasion(pos, ms, ref mpos);
                    lastMovePos = mpos;
                    score_evasions();
                    return;

                case SequencerC.QUIET_CHECKS_S3:
                    mpos = 0;
                    Movegen.generate_quiet_check(pos, ms, ref mpos);
                    lastMovePos = mpos;
                    return;

                case SequencerC.EVASION:
                case SequencerC.QSEARCH_0:
                case SequencerC.QSEARCH_1:
                case SequencerC.PROBCUT:
                case SequencerC.RECAPTURE:
                    phase = SequencerC.STOP;
                    lastMovePos = curMovePos + 1; // Avoid another next_phase() call
                    break;

                case SequencerC.STOP:
                    lastMovePos = curMovePos + 1; // Avoid another next_phase() call
                    return;

                default:
                    Debug.Assert(false);
                    break;
            }
        }

        /// MovePicker::next_move() is the most important method of the MovePicker class.
        /// It returns a new pseudo legal move every time it is called, until there
        /// are no more moves left. It picks the move with the biggest score from a list
        /// of generated moves taking care not to return the tt move if has already been
        /// searched previously. Note that this function is not thread safe so should be
        /// lock protected by caller when accessed through a shared MovePicker object.
        internal Move next_move()
        {
            if (mpExternal!=null) { return mpExternal.next_move(); }
            
            Move move;
            int bestpos = 0;

            while (true)
            {
                while (curMovePos == lastMovePos)
                    generate_next();

                switch (phase)
                {
                    case SequencerC.MAIN_SEARCH:
                    case SequencerC.EVASION:
                    case SequencerC.QSEARCH_0:
                    case SequencerC.QSEARCH_1:
                    case SequencerC.PROBCUT:
                        curMovePos++;
                        return ttMove;

                    case SequencerC.CAPTURES_S1:
                        bestpos = pick_best(); curMovePos++;
                        move = ms[bestpos].move;
                        if (move != ttMove)
                        {
                            Debug.Assert(captureThreshold <= 0); // Otherwise we cannot use see_sign()

                            if (pos.see(move, true) >= captureThreshold)
                                return move;

                            // Losing capture, move it to the tail of the array
                            ms[lastBadCapturePos--].move = move;
                        }
                        break;

                    case SequencerC.KILLERS_S1:
                        move = ms[curMovePos++].move;
                        if (move != MoveC.MOVE_NONE
                            && pos.is_pseudo_legal(move)
                            && move != ttMove
                            && !pos.is_capture(move))
                            return move;
                        break;

                    case SequencerC.QUIETS_1_S1:
                    case SequencerC.QUIETS_2_S1:
                        move = ms[curMovePos++].move;
                        if (move != ttMove
                            && move != ms[Constants.MAX_MOVES].move
                            && move != ms[Constants.MAX_MOVES + 1].move)
                            return move;
                        break;

                    case SequencerC.BAD_CAPTURES_S1:
                        return ms[curMovePos--].move;

                    case SequencerC.EVASIONS_S2:
                    case SequencerC.CAPTURES_S3:
                    case SequencerC.CAPTURES_S4:
                        bestpos = pick_best(); curMovePos++;
                        move = ms[bestpos].move;
                        if (move != ttMove)
                            return move;
                        break;

                    case SequencerC.CAPTURES_S5:
                        bestpos = pick_best(); curMovePos++;
                        move = ms[bestpos].move;
                        if (move != ttMove && pos.see(move, false) > captureThreshold)
                            return move;
                        break;

                    case SequencerC.CAPTURES_S6:
                        bestpos = pick_best(); curMovePos++;
                        move = ms[bestpos].move;
                        if (Utils.to_sq(move) == recaptureSquare)
                            return move;
                        break;

                    case SequencerC.QUIET_CHECKS_S3:
                        move = ms[curMovePos++].move;
                        if (move != ttMove)
                            return move;
                        break;

                    case SequencerC.STOP:
                        return MoveC.MOVE_NONE;

                    default:
                        Debug.Assert(false);
                        break;
                }
            }
        }
    }
}
