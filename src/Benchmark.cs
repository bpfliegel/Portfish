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
        #region Arasan test suite
        /* static string[] Defaults = new string[] {
            "6k1/1b3pp1/1qr1p3/4R3/p6P/3p2P1/PP1Q1P1K/3B4 b - -",
            "r1b2rk1/1p1nbppp/pq1p4/3B4/P2NP3/2N1p3/1PP3PP/R2Q1R1K w - -",
            "2q2r2/3n1p2/p2p2k1/3PpRp1/P1n1P3/2P2QB1/1rB1R1P1/6K1 w - -",
            "2rq1rk1/pp1b1p2/2n3p1/3pPPbp/3P2B1/BP6/P1N4P/R2Q1R1K w - h6",
            "2rr3k/2qnbppp/p1n1p3/1p1pP3/3P1N2/1Q1BBP2/PP3P1P/1KR3R1 w - -",
            "4r1k1/1p3p2/p2q2p1/Q2p2n1/3P2Pp/PP5P/4rPB1/2R2RK1 b - -",
            "r1b1k2r/1p1pppb1/p5pp/3P4/q2p1B2/3P1Q2/PPP2PPP/R3R1K1 w kq -",
            "R4bk1/2Bbp2p/2p2pp1/1rPp4/3P4/4P2P/4BPPK/1q1Q4 w - -",
            "4r1k1/p4p2/np3npQ/2pP4/P2q1N2/2N4P/1P4PK/4R3 w - -",
            "r3rbk1/2q2p1p/2p1p1p1/pp1nP1B1/6Q1/P2B2P1/1PP2PK1/R6R w - -",
            "r7/5pk1/5p2/2p5/pp6/1P1B1KPN/P6P/8 b - -",
            "1r4k1/5r1p/bnpq1p2/p2p1Pp1/Pp1Pp1P1/1P2P1P1/2P1N3/1K1RQB1R b - -",
            "r2q3r/1p1bbQ2/4p1Bk/3pP3/1n1P1P1p/pP6/Pn4PP/R1B1R1K1 w - -",
            "8/5pk1/3p1bp1/1B5p/2P1PP2/3Q2PK/5q2/8 w - -",
            "r4k1r/p3Rpbp/2bpn1p1/q2N4/2P1P3/4B3/P3BPPP/3Q1RK1 w - -",
            "1r2brk1/4n1p1/4p2p/p2pP1qP/2pP1NP1/P1Q1BK2/2P4R/6R1 b - -",
            "2r1qrk1/1b1nbppp/4p3/pB1nP1B1/Pp2N3/5N1P/1P1Q1PP1/R2R2K1 w - -",
            "r3qrk1/p5b1/1p1p1p2/2pP2pp/1nN1PP1B/2Nn3P/PP2Q1P1/R4RK1 w - -",
            "r3k2r/2q1b1pp/pp2p3/2pR4/P7/4BP2/1PP1Q1PP/5RK1 w kq -",
            "r3r1k1/pp3pp1/3p4/2q4p/2P5/1PB2Q1P/n4PP1/3R1RK1 w - -",
            "r4r1k/1bq2pp1/2p1p2p/1pPnP1BQ/4B2P/1p4P1/P4P2/3RR1K1 w - -",
            "r4rk1/p4ppp/5n1n/2pp1Q2/1P6/P1N1P2P/3B1PPq/R2R1K2 w - -",
            "3r4/Nk3p2/4pP2/3q2Pr/Ppp5/3nQPP1/1P4K1/R2R4 b - -",
            "b7/P4kp1/R4p2/4p3/1Bp1P2p/2P2P2/1q3NP1/4K3 b - -",
            "r1br1nk1/5ppp/p2Bp3/qp1pPP2/3Q4/2P2B2/P1P3PP/3R1R1K w - -",
            "r1q2rk1/ppnbbpp1/n4P1p/4P3/3p4/2N1B1PP/PP4BK/R2Q1R2 w - -",
            "1R6/5p1k/4bPpp/3pN3/2pP1P1P/2r5/6PK/8 w - -",
            "3q1rk1/pr1b1p1p/1bp2p2/2ppP3/8/2P1BN2/PPQ3PP/R4RK1 w - -",
            "5k2/p7/5pP1/r4P2/p3PK2/R7/8/8 w - -",
            "1q1b4/1r1r2k1/1n3p1p/3pp1p1/3B3P/2NQ4/5PP1/1RR3K1 w - -",
            "r2qbr1k/1p3pp1/p1n2N1p/5R1Q/P7/3Bp3/1PP3PP/R5K1 w - -",
            "r5k1/1pp3b1/3p1qp1/p2Ppr2/2P4P/2Nn1N1R/PP6/R1BQ1K2 b - -",
            "2rq1rk1/3bbp1p/p3pp2/n2p1P2/7B/5N2/PPPQ2PP/1K1R1B1R w - -",
            "8/6p1/P1b1pp2/2p1p3/1k4P1/3PP3/1PK5/5B2 w - -",
            "r3rk2/pb2bpp1/1n5p/q1pP1B2/1p3B2/5N2/PPQ2PPP/R2R2K1 w - -",
            "r5n1/p1p1q2k/4b2p/3pB3/3PP1pP/8/PPPQ2P1/5RK1 w - -",
            "2b2rk1/r3q1pp/1nn1p3/3pP1NP/p1pP2Q1/2P1N3/1P1KBP2/R5R1 w - -",
            "2q4k/prpp1p2/1p2r2p/4P3/2P2P2/2N4Q/PP3K1P/R7 w - -",
            "r3r1k1/p3bppp/q1b2n2/5Q2/1p1B4/1BNR4/PPP3PP/2K2R2 w - -",
            "rn1rb1k1/pq2bppp/4p3/2p1N3/4PQ2/1PB3P1/P4PBP/2R2RK1 w - -",
            "3r1k2/2pq1np1/p2pQ3/1p1P1ppP/3P2N1/P7/1P3PP1/4R1K1 w - -",
            "1q6/6k1/5Np1/1r4Pp/2p4P/2Nrb3/PP6/KR5Q b - -",
            "b2rk3/r4p2/p3p3/P3Q1Np/2Pp3P/8/6P1/6K1 w - -",
            "rqbn1rk1/1p3ppp/p3p3/8/4NP2/5Q2/PPP1B1PP/1K1R3R w - -",
            "r1br2k1/ppp2q1p/nb3p2/6p1/2PN1B2/2P2B2/P1Q2PPP/3RR1K1 w - g6",
            "br4k1/1qrnbppp/pp1ppn2/8/NPPBP3/PN3P2/5QPP/2RR1B1K w - -",
            "r2q1rk1/ppp2p2/3p1np1/4pNQ1/4P1pP/1PPP4/1P3P2/R3K1R1 w Q -",
            "1qb2rk1/3p1pp1/1p6/1pbBp3/r5p1/3QB3/PPP2P1P/2KR2R1 w - -",
            "r1b2q1k/2Qn1p1p/1p1Rpp2/p6B/4P2P/6N1/P4PP1/6K1 w - -",
            "r1q2k1r/ppp2ppp/4n3/3N4/2B5/3Q4/Pb3PPP/4RRK1 w - -",
            "r4rk1/p4ppp/qp2p3/b5B1/n1R5/5N2/PP2QPPP/1R4K1 w - -",
            "r2q1rk1/4bppp/3pb3/2n1pP2/1p2P1PP/1P3Q2/1BP1N1B1/2KR3R b - -",
            "1rb4k/p5np/3p1rp1/1ppB4/2N2P2/1P2R1P1/P1P4P/4R1K1 w - -",
            "r4rk1/1b2qppp/pp6/2p1P3/5P2/2RB4/PP2Q1PP/5RK1 w - -",
            "8/1K6/1P6/5p2/5Pp1/3k4/6R1/1r6 w - -",
            "rnb1k2r/pp3ppp/3b4/q2pN1B1/8/3B4/Pp3PPP/1R1Q1RK1 w kq -",
            "2brr1k1/pp2bppn/1qp4p/3nNP2/2BPNB2/P6P/1P3QP1/3RR1K1 w - -",
            "4n3/2p5/1p2r2P/p1p2R2/P1N1k3/1PP4K/8/8 w - -",
            "r2qr1k1/pp1nbppp/3p1n2/2pP2N1/2B2B2/3P4/PPP1Q1PP/4RRK1 w - -",
            "8/4k2p/p1P5/1p2P3/2p5/P3K3/7P/8 w - -",
            "8/2p1k3/3p3p/2PP1pp1/1P1K1P2/6P1/8/8 w - -",
            "2b1qrk1/5p1p/pBn3p1/1p2p3/4P2N/bBP1Q3/P4PPP/3R2K1 w - -",
            "r1b1rk2/p1pq2p1/1p1b1p1p/n2P4/2P1NP2/P2B1R2/1BQ3PP/R6K w - -",
            "r2qr3/2p1b1pk/p5pp/1p2p3/nP2P1P1/1BP2RP1/P3QPK1/R1B5 w - -",
            "1rbq1rk1/p5bp/3p2p1/2pP4/1p1n1BP1/3P3P/PP2N1B1/1R1Q1RK1 b - -",
            "r1b1k1r1/2q1bpPp/p3pn1R/6Q1/1p2p3/1NN2P2/PPP3P1/1K1R1B2 w q -",
            "7b/8/kq6/8/8/1N2R3/K2P4/8 w - -",
            "q3nrk1/4bppp/3p4/r3nPP1/4P2P/NpQ1B3/1P4B1/1K1R3R b - -",
            "2r1rbk1/1b1n1pp1/nq1p3p/3P1N2/1pp1P3/R3RN1P/1P3PP1/1BBQ2K1 w - -",
            "r2qkb1r/ppp2ppp/1nn5/4p2b/8/1BNP1N1P/PPP2PP1/R1BQK2R w KQkq -",
            "4rr1k/p1p3p1/1p1bP2p/1N3p1q/4p2P/PQ2PnP1/1P3PK1/1BRR4 b - h3",
            "r7/n1rqppk1/p2p2p1/1b1P3p/2B4R/1P2QN1P/P4PP1/4R1K1 w - h6",
            "2qrrbk1/1b3ppp/pn1Pp3/6P1/1Pp2B2/1nN2NQB/1P3P1P/3RR1K1 w - -",
            "r3r1k1/pp3p1p/3bb1BB/4q1Q1/8/7P/P4PP1/R2R2K1 w - -",
            "2r2rk1/1b2bppn/1qp1p2p/p5N1/PpBPP2P/1P6/1BQ2PP1/1K1R3R w - -",
            "6k1/1p3p1p/5qp1/Q7/2Rp1P2/P2P3P/1P2r1P1/6K1 b - f3",
            "r1b1qr1k/pppn1pb1/4p1p1/4N1Bp/2BP2Q1/2P4R/P4PPP/4R1K1 w - h6",
            "6k1/Qb1q1pp1/2r4p/8/1P2P3/P4P2/2rp1NPP/R2R2K1 b - -",
            "8/k4r2/1p3q2/p1p1n3/P3Pnp1/3p1PQ1/1P4PP/R2B1R1K b - -",
            "r1b2rk1/pp1p2pR/8/1pb2p2/5N2/7Q/qPPB1PPP/6K1 w - -",
            "7q/3k2p1/n1p1p1Pr/1pPpPpQ1/3P1N1p/1P2KP2/6P1/7R w - -",
            "3rr3/6k1/2p3P1/1p2nP1p/pP2p3/P1B1NbPB/2P2K2/5R2 w - -",
            "3r2k1/pb3Np1/4pq1p/2pp1n2/3P4/1PQ5/P4PPP/R2R2K1 b - -",
            "r1bq1rk1/1p2b1pp/p1np4/8/2P1P1n1/N1N3B1/PP2BP1P/R2QK2R b KQ -",
            "8/p1p1r2p/1p5p/2p1r2k/2P1PR1P/5K2/PP6/6R1 w - -",
            "2kr1r2/ppq1b1p1/2n5/2PpPb1N/QP1B1pp1/2P5/P2N1P1P/R4RK1 b - -",
            "2r2r1k/pBBq2p1/1p5p/4p3/8/2Q1PPPb/P6P/2R3K1 b - -",
            "3r2k1/6p1/B1R2p1p/1pPr1P2/3P4/8/1P3nP1/2KR4 w - -",
            "3r2k1/p1q2pp1/2b1pb1p/2Pr4/6Q1/R4NP1/P1R1BP1P/6K1 b - -",
            "2kr3r/1p2bpp1/p1p1pn1p/R3N2P/2PP4/1Q6/PP1B1Pq1/2KR4 w - -",
            "3q1k2/p4pb1/3Pp3/p3P3/r6p/2QB3P/3B1P2/6K1 w - -",
            "r4r1k/ppqbn1pp/3b1p2/2pP3B/2P4N/7P/P2B1PP1/1R1QR1K1 w - -",
            "1r4k1/1q3pp1/r3b2p/p2N4/3R4/QP3P2/2P3PP/1K1R4 w - -",
            "r2q1r2/1bp1npk1/3p1p1p/p3p2Q/P3P2N/1BpPP3/1P1N2PP/5RK1 w - -",
            "2k1r2r/1pqb1p2/p2pp2b/4n1p1/PQ1NP2p/1P3P1P/2P1NBP1/R4RK1 w - -",
            "2r3r1/1p1qb2k/p5pp/2n1Bp2/2RP3P/1P2PNQ1/5P1K/3R4 w - -",
            "1rq5/2r1kpb1/3p4/4p1Pp/1P5R/pP1QBP2/P1B5/1K6 b - -",
            "rn1r2k1/p5bp/4b1p1/1Bp2p2/4qB2/2P2N2/P3QPPP/2R1K2R w K -",
            "8/6rk/7p/1PNppp1n/1P6/P1r4P/5R1K/3R4 w - -",
            "rnbqk2r/pp1pbppp/2p5/8/2BP1p2/2P5/P1P1N1PP/R1BQ1RK1 w kq -",
            "6k1/1p1q4/p1rP2p1/5p1p/5Q2/1P5P/5PP1/3R2K1 w - h6",
            "1qrrbbk1/1p1nnppp/p3p3/4P3/2P5/1PN1N3/PB2Q1PP/1B2RR1K w - -",
            "r1r2nk1/1p1b1pbp/p4np1/P1Np2q1/1P1Pp3/B1N1P3/Q3BPPP/RR4K1 w - -",
            "3r4/2q5/5pk1/p3n1p1/N3Pp1p/1PPr1P1P/2Q1R1P1/5R1K b - -",
            "r1b2r1k/ppqn2bp/3p2p1/3Q1p2/8/BP1BR2P/P1PP1PP1/4R1K1 w - -",
            "6r1/8/2k5/1pPp1p1p/pP3P2/P3P1P1/4K3/4B3 b - -",
            "8/6bP/8/5p2/2B5/p3kp1K/P7/8 b - -",
            "2r1rb1k/ppq2pp1/4b2p/3pP2Q/5B2/2PB2R1/P4PPP/1R4K1 w - -",
            "3rr1k1/1bp3p1/1p4qp/pNp1p3/PnPn1pP1/3P1P2/1P2P1BP/1R1QBR1K b - -",
            "rnb1kb1r/pp1p1ppp/1q2p3/8/3NP1n1/2N1B3/PPP2PPP/R2QKB1R w KQkq -",
            "r3kb1r/1b1n2p1/p3Nn1p/3Pp3/1p4PP/3QBP2/qPP5/2KR1B1R w kq -",
            "1r1qrbk1/pb3p2/2p1pPpp/1p4B1/2pP2PQ/2P5/P4PBP/R3R1K1 w - -",
            "2r1r2k/1b1n1p1p/p3pPp1/1p1pP2q/3N4/P3Q1P1/1PP4P/2KRRB2 w - -",
            "2r1b1k1/5p2/1R2nB2/1p2P2p/2p5/2Q1P2K/3R1PB1/r3q3 w - -",
            "rn2r1k1/ppq1pp1p/2b2bp1/8/2BNPP1B/2P4P/P1Q3P1/1R3RK1 w - -",
            "1kr5/1p3p2/q3p3/pRbpPp2/P1rNnP2/2P1B1Pp/1P2Q2P/R5K1 b - -",
            "r3r2k/1pq2pp1/4b2p/3pP3/p1nB3P/P2B1RQ1/1PP3P1/3R3K w - -",
            "r3brk1/2q1bp1p/pnn3p1/1p1pP1N1/3P4/3B2P1/PP1QNR1P/R1B3K1 w - -",
            "1r3r2/q5k1/4p1n1/1bPpPp1p/pPpR1Pp1/P1B1Q3/2B3PP/3R2K1 w - -",
            "rq3rk1/1b1n1ppp/ppn1p3/3pP3/5B2/2NBP2P/PP2QPP1/2RR2K1 w - -",
            "7r/k4pp1/pn2p1pr/2ppP3/1q3P2/1PN2R1P/P1P2QP1/3R3K w - -",
            "3r1k2/pb1q1r2/npp5/3pB3/3PPPpb/PQ4N1/1P1R2B1/5RK1 w - -",
            "1r3rk1/3bbppp/1qn2P2/p2pP1P1/3P4/2PB1N2/6K1/qNBQ1R2 w - -",
            "1r1qrbk1/5ppp/2b1p2B/2npP3/1p4QP/pP1B1N2/P1P2PP1/1K1R3R w - -",
            "r5k1/pbpq1pp1/3b2rp/N3n3/1N6/2P3B1/PP1Q1PPP/R4RK1 b - -",
            "2r2bk1/3n1pp1/1q5p/pp1pPp1P/3P4/P1B2QP1/1P2N1K1/5R2 w - -",
            "r4r2/pp1b1ppk/2n1p3/3pPnB1/q1pP2QP/P1P4R/2PKNPP1/R7 w - -",
            "r4rk1/3b1ppp/p1np4/qp1N1P1Q/3bP3/4R3/PPB3PK/1RB5 b - -",
            "2r1kb1r/1p1b1pp1/p5qp/3pP3/P2N1B2/7P/1PP2QP1/4RR1K w - -",
            "3r3k/2q2p1P/p7/Nr2p1P1/p3b2R/4B3/KbP2Q2/5R2 b - -",
            "8/2k2Bp1/2n5/p1P4p/4pPn1/P3PqPb/1r1BQ2P/2R1K1R1 b - -",
            "rnq1nrk1/pb2bppp/1p2p2B/1N2N3/2B5/6Q1/PP3PPP/3R1RK1 w - -",
            "8/5kpp/8/8/8/5P2/1RPK2PP/6r1 w - -",
            "8/5k1K/2N2p2/2b1p1p1/1p2P1P1/1P3P2/P7/8 w - -",
            "b4rk1/5p2/3q2p1/2p3n1/N2p1rBp/1PP1RP2/P3Q1PP/3R2K1 b - -",
            "8/3n4/1p1k1p2/p5p1/PP4P1/2B3K1/8/8 b - -",
            "1r1q2k1/2r3bp/B2p1np1/3P1p2/R1P1pP2/4B2P/P5PK/3Q1R2 b - -",
            "2r4k/1p2bpp1/pBqp1n1p/P3pP2/2r1P1P1/2N2Q1P/RPP1R3/5K2 b - -",
            "2r1rnk1/1p2pp1p/p1np2p1/q4PP1/3NP2Q/4B2R/PPP4P/3R3K w - -",
            "5qk1/1r1n3p/R4p1p/2pPpQ2/1r2P1B1/8/R5PK/8 w - -",
            "rnb1k2r/1q2bppp/p2ppn2/1p6/3NP3/1BN3Q1/PPP2PPP/R1B2RK1 w kq -",
            "1r1rkb2/2q2p2/p2p1P1B/P1pPp2Q/2P3b1/1P6/2B3PP/5R1K w - -",
            "1n1rn3/3rbpk1/p1qpp2p/2p3p1/2P1NBP1/P1NR3Q/1P2PP1P/3R2K1 w - -",
            "r4rk1/3b3p/p1pb4/1p1n2p1/2P2p2/1B1P2Pq/PP1NRP1P/R1BQ2K1 w - -",
            "1r3rk1/4bpp1/p3p2p/q1PpPn2/bn3Q1P/1PN1BN2/2P1BPP1/1KR2R2 b - -",
            "2nb2k1/1rqb1pp1/p2p1n1p/2pPp3/P1P1P3/2B1NN1P/2B2PP1/Q3R2K w - -",
            "3r2k1/p1qn1p1p/4p1p1/2p1N3/8/2P3P1/PP2QP1P/4R1K1 w - -",
            "r2q1rk1/pb1nbp1p/1pp1pp2/8/2BPN2P/5N2/PPP1QPP1/2KR3R w - -",
            "4rr2/3bp1bk/p2q1np1/2pPp2p/2P4P/1R4N1/1P1BB1P1/1Q3RK1 w - -",
            "8/8/4b1p1/2Bp3p/5P1P/1pK1Pk2/8/8 b - -",
            "r4b1r/pp1n2p1/1qp1k2p/4p3/3P4/2P5/P1P1Q1PP/R1B2RK1 w - -",
            "r4rk1/p1p3pp/1pPppn1q/1P1b1p2/P2Pn3/4PN2/1B2BPPP/R2Q1RK1 b - -",
            "r1bb1qk1/pp1p2p1/3Np2r/4p3/4B2P/2P5/PKP2PQ1/3R2R1 w - -",
            "rnbRrk2/2p5/1p2PB1p/pP4p1/8/P3R2P/2P2P2/6K1 w - -",
            "8/5p2/3p2p1/1bk4p/p2pBNnP/P5P1/1P3P2/4K3 b - -",
            "8/4nk2/1p3p2/1r1p2pp/1P1R1N1P/6P1/3KPP2/8 w - -",
            "6k1/1bq1bpp1/p6p/2p1pP2/1rP1P1P1/2NQ4/2P4P/K2RR3 b - -",
            "r3r1k1/1bqnbp1p/pp1pp1p1/6P1/Pn2PP1Q/1NN1BR2/1PPR2BP/6K1 w - -",
            "1R1B1n2/2Rqbppk/p2p3p/3Pp3/5rPP/8/P3Q2K/8 b - -",
            "1k1q2r1/pp1n4/3P4/1P3p1r/2R5/2Q3Bp/R6K/8 w - -",
            "8/5p1k/6p1/1p1Q3p/3P4/1R2P1KP/6P1/r4q2 b - -",
            "7k/3q1pp1/1p3r2/p1bP4/P1P2p2/1P2rNpP/2Q3P1/4RR1K b - -",
            "3r3r/k1p2pb1/B1b2q2/2RN3p/3P2p1/1Q2B1Pn/PP3PKP/5R2 w - -",
            "r1b3kr/pp1n2Bp/2pb2q1/3p3N/3P4/2P2Q2/P1P3PP/4RRK1 w - -",
            "2r3k1/1q3pp1/2n1b2p/4P3/3p1BP1/Q6P/1p3PB1/1R4K1 b - -",
            "rn2kb1r/1b1n1p1p/p3p1p1/1p2q1B1/3N3Q/2N5/PPP3PP/2KR1B1R w kq -",
            "r2nkb1r/pp1q1ppp/4p3/8/3N4/2P1PN2/PP2Q1PP/R4RK1 w kq -",
            "8/2p4k/1p1p4/1PPPp3/1r6/R3K3/P4P2/8 w - -",
            "1nr3k1/q4rpp/1p1p1n2/3Pp3/1PQ1P1b1/4B1P1/2R2NBP/2R3K1 w - -",
            "5bk1/2r2p2/b3p1p1/p2pP1P1/3N3R/qP2R3/5PKP/4Q3 w - -",
            "5rk1/2p1R2p/r5q1/2pPR2p/5p2/1p5P/P4PbK/2BQ4 w - -",
            "r1bqrb1k/1p2p2p/1n2np2/p2pP3/P1pP1NP1/1PPB1N1P/3BQ3/R5RK w - -",
            "b3r3/7k/2p3p1/2R4p/p3q1pP/2B3P1/3Q1P2/6K1 w - -",
            "r5k1/6b1/2Nq4/2pP1p2/p1P1pPr1/Pp6/3R2PQ/1K3R2 b - -",
            "r1r3k1/1q3p1p/4p1pP/1bnpP1P1/pp1Q1P2/1P6/P1PR1N2/1K3B1R b - -"
            };
             */
        #endregion

        #region Some endgames

        /*
          static string[] Defaults = new string[] {
          "1n6/8/8/8/8/8/6R1/2K1k3 w - -",
          "3n4/8/8/8/7R/8/8/2K1k3 w - -",
          "8/8/3n4/8/8/8/4R3/2K2k2 w - -",
          "8/8/7R/n7/8/8/8/2K2k2 w - -",
          "4n3/7R/8/8/8/8/8/2K2k2 w - -",
          "3R4/8/8/8/8/n7/8/2K2k2 w - -",
          "8/8/8/8/8/8/7k/n1K3R1 w - -",
          "8/3n4/8/8/8/6R1/8/1k1K4 w - -",
          "8/8/8/8/4n3/7R/8/1k1K4 w - -",
          "8/3n4/8/8/8/7R/8/1k1K4 w - -",
          "2n5/8/8/8/8/8/3R4/3K1k2 w - -",
          "4n3/8/8/8/7R/8/8/3K1k2 w - -",
          "8/8/8/8/5kp1/P7/8/1K1N4 w - -",
          "8/2p4P/8/kr6/6R1/8/8/1K6 w - -",
          "8/2p4P/6R1/kr6/8/8/8/1K6 w - -",
          "8/8/1P6/5pr1/8/4R3/7k/2K5 w - -"
          }; */

        #endregion

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

                Plug.Interface.Write("\nPosition: ");
                Plug.Interface.Write((i + 1).ToString());
                Plug.Interface.Write("/");
                Plug.Interface.Write(fens.Count.ToString());
                Plug.Interface.Write(Constants.endl);

                if (limitType == "perft")
                {
                    Int64 cnt = Search.perft(pos, limits.depth * DepthC.ONE_PLY);
                    Plug.Interface.Write("\nPerft ");
                    Plug.Interface.Write(limits.depth.ToString());
                    Plug.Interface.Write(" leaf nodes: ");
                    Plug.Interface.Write(cnt.ToString());
                    Plug.Interface.Write(Constants.endl);
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

                Plug.Interface.Write("\n===========================");
                Plug.Interface.Write("\nTotal time (ms) : ");
                Plug.Interface.Write(e.ToString());
                Plug.Interface.Write("\nNodes searched  : ");
                Plug.Interface.Write(nodes.ToString());
                Plug.Interface.Write("\nNodes/second    : ");
                Plug.Interface.Write(((int)(nodes / (e / 1000.0))).ToString());
                Plug.Interface.Write(Constants.endl);

            }

            Plug.Interface.Write("\n===========================");
            Plug.Interface.Write("\nTotal time (ms) : ");
            Plug.Interface.Write(eAll.ToString());
            Plug.Interface.Write("\nNodes searched  : ");
            Plug.Interface.Write(nodesAll.ToString());
            Plug.Interface.Write("\nNodes/second    : ");
            Plug.Interface.Write(((int)(nodesAll / (eAll / 1000.0))).ToString());
            Plug.Interface.Write(Constants.endl);

            //for (int i = 0; i < res.Length; i++)
            //{
            //    Plug.Interface.Write(string.Format("{0}: {1}", i, res[i]));
            //    Plug.Interface.Write(Constants.endl);
            //}
        }
    }
}
