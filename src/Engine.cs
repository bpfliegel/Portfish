using System;
using System.Collections.Generic;
using System.Text;

namespace Portfish
{
    public class Engine
    {
        public void Run(object arguments)
        {
            string[] args = (string[])arguments;

            Plug.Interface.Write(Utils.engine_info());
            Plug.Interface.Write(Constants.endl);

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

            Utils.InitLookups();
            Position.init();
            KPKPosition.kpk_bitbase_init();
            Endgames.InitEndgames();
            Search.init();
            Evaluate.init();

            Threads.init();

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
