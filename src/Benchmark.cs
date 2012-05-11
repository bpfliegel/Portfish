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

namespace Portfish
{
    internal static class Benchmark
    {
        static string[] Defaults = new string[] {
          "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
          "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 10",
          "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 11",
          "4rrk1/pp1n3p/3q2pQ/2p1pb2/2PP4/2P3N1/P2B2PP/4RRK1 b - - 7 19",
          "rq3rk1/ppp2ppp/1bnpb3/3N2B1/3NP3/7P/PPPQ1PP1/2KR3R w - - 7 14",
          "r1bq1r1k/1pp1n1pp/1p1p4/4p2Q/4Pp2/1BNP4/PPP2PPP/3R1RK1 w - - 2 14",
          "r3r1k1/2p2ppp/p1p1bn2/8/1q2P3/2NPQN2/PPP3PP/R4RK1 b - - 2 15",
          "r1bbk1nr/pp3p1p/2n5/1N4p1/2Np1B2/8/PPP2PPP/2KR1B1R w kq - 0 13",
          "r1bq1rk1/ppp1nppp/4n3/3p3Q/3P4/1BP1B3/PP1N2PP/R4RK1 w - - 1 16",
          "4r1k1/r1q2ppp/ppp2n2/4P3/5Rb1/1N1BQ3/PPP3PP/R5K1 w - - 1 17",
          "2rqkb1r/ppp2p2/2npb1p1/1N1Nn2p/2P1PP2/8/PP2B1PP/R1BQK2R b KQ - 0 11",
          "r1bq1r1k/b1p1npp1/p2p3p/1p6/3PP3/1B2NN2/PP3PPP/R2Q1RK1 w - - 1 16",
          "3r1rk1/p5pp/bpp1pp2/8/q1PP1P2/b3P3/P2NQRPP/1R2B1K1 b - - 6 22",
          "r1q2rk1/2p1bppp/2Pp4/p6b/Q1PNp3/4B3/PP1R1PPP/2K4R w - - 2 18",
          "4k2r/1pb2ppp/1p2p3/1R1p4/3P4/2r1PN2/P4PPP/1R4K1 b - - 3 22",
          "3q2k1/pb3p1p/4pbp1/2r5/PpN2N2/1P2P2P/5PP1/Q2R2K1 b - - 4 26"
        };

        /// benchmark() runs a simple benchmark by letting Stockfish analyze a set
        /// of positions for a given limit each. There are five parameters; the
        /// transposition table size, the number of search threads that should
        /// be used, the limit value spent for each position (optional, default is
        /// depth 12), an optional file name where to look for positions in fen
        /// format (defaults are the positions defined above) and the type of the
        /// limit value: depth (default), time in secs or number of nodes.
        internal static void benchmark(Position current, Stack<string> stack)
        {
            List<string> fens = new List<string>();

            LimitsType limits = new LimitsType();
            Int64 nodes = 0;
            Int64 nodesAll = 0;
            long e = 0;
            long eAll = 0;

            // Assign default values to missing arguments
            string ttSize = (stack.Count > 0) ? (stack.Pop()) : "128";
            string threads = (stack.Count > 0) ? (stack.Pop()) : "1";
            string limit = (stack.Count > 0) ? (stack.Pop()) : "12";
            string fenFile = (stack.Count > 0) ? (stack.Pop()) : "default";
            string limitType = (stack.Count > 0) ? (stack.Pop()) : "depth";

            OptionMap.Instance["Hash"].v = ttSize;
            OptionMap.Instance["Threads"].v = threads;
            TT.clear();

            if (limitType == "time")
                limits.movetime = 1000 * int.Parse(limit); // maxTime is in ms

            else if (limitType == "nodes")
                limits.nodes = int.Parse(limit);

            else
                limits.depth = int.Parse(limit);

            if (fenFile == "default")
            {
                fens.AddRange(Defaults);
            }
            else if (fenFile == "current")
            {
                fens.Add(current.to_fen());
            }
            else
            {
#if PORTABLE
                throw new Exception("File cannot be read.");
#else
                System.IO.StreamReader sr = new System.IO.StreamReader(fenFile, true);
                string fensFromFile = sr.ReadToEnd();
                sr.Close();
                sr.Dispose();

                string[] split = fensFromFile.Replace("\r", "").Split('\n');
                foreach (string fen in split)
                {
                    if (fen.Trim().Length > 0)
                    {
                        fens.Add(fen.Trim());
                    }
                }
#endif
            }

            Stopwatch time = new Stopwatch();
            long[] res = new long[fens.Count];
            for (int i = 0; i < fens.Count; i++)
            {
                time.Reset(); time.Start();
                Position pos = new Position(fens[i], bool.Parse(OptionMap.Instance["UCI_Chess960"].v), Threads.main_thread());

                Plug.Write("\nPosition: ");
                Plug.Write((i + 1).ToString());
                Plug.Write("/");
                Plug.Write(fens.Count.ToString());
                Plug.Write(Constants.endl);

                if (limitType == "perft")
                {
                    Int64 cnt = Search.perft(pos, limits.depth * DepthC.ONE_PLY);
                    Plug.Write("\nPerft ");
                    Plug.Write(limits.depth.ToString());
                    Plug.Write(" leaf nodes: ");
                    Plug.Write(cnt.ToString());
                    Plug.Write(Constants.endl);
                    nodes = cnt;
                }
                else
                {
                    Threads.start_searching(pos, limits, new List<Move>());
                    Threads.wait_for_search_finished();
                    nodes = Search.RootPosition.nodes;
                    res[i] = nodes;
                }

                e = time.ElapsedMilliseconds;

                nodesAll += nodes;
                eAll += e;

                Plug.Write("\n===========================");
                Plug.Write("\nTotal time (ms) : ");
                Plug.Write(e.ToString());
                Plug.Write("\nNodes searched  : ");
                Plug.Write(nodes.ToString());
                Plug.Write("\nNodes/second    : ");
                Plug.Write(((int)(nodes / (e / 1000.0))).ToString());
                Plug.Write(Constants.endl);

            }

            Plug.Write("\n===========================");
            Plug.Write("\nTotal time (ms) : ");
            Plug.Write(eAll.ToString());
            Plug.Write("\nNodes searched  : ");
            Plug.Write(nodesAll.ToString());
            Plug.Write("\nNodes/second    : ");
            Plug.Write(((int)(nodesAll / (eAll / 1000.0))).ToString());
            Plug.Write(Constants.endl);

            //for (int i = 0; i < res.Length; i++)
            //{
            //    Plug.Write(string.Format("{0}: {1}", i, res[i]));
            //    Plug.Write(Constants.endl);
            //}
        }
    }
}
