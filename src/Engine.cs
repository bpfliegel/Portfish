using System;
using System.Collections.Generic;
using System.Text;

namespace Portfish
{
    public sealed class Engine
    {
        public void Run(object arguments)
        {
            string[] args = (string[])arguments;

            Plug.Write(Utils.engine_info());
            Plug.Write(Constants.endl);

            CheckInfoBroker.init();
            EvalInfoBroker.init();
            SwapListBroker.init();
            MovesSearchedBroker.init();
            PositionBroker.init();
            StateInfoArrayBroker.init();

            MListBroker.init();
            LoopStackBroker.init();
            MovePickerBroker.init();
            StateInfoBroker.init();

            Utils.init();
#if WINDOWS_RT
#else
            Book.init();
#endif
            Position.init();
            KPKPosition.init();
            Endgame.init();
            Search.init();
            Evaluate.init();

            Threads.init();

            // .Net warmup sequence
            Plug.IsWarmup = true;
            Position pos = new Position(Uci.StartFEN, false, Threads.main_thread());
            Stack<string> stack = Utils.CreateStack("go depth 7");
            Uci.go(pos, stack);
            Threads.wait_for_search_finished();
            Plug.IsWarmup = false;

            StringBuilder sb = new StringBuilder();
            for (int i = 1; i < args.Length; i++)
            {
                sb.Append(args[i]).Append(" ");
            }

            Uci.uci_loop(sb.ToString());

            Threads.exit();
        }
    }
}
