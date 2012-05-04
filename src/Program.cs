using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Portfish
{
    internal sealed class ConsolePlug : IPlug
    {
        public void Write(string message)
        {
            Console.Write(message);
        }
        public string ReadLine()
        {
            return Console.ReadLine();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            IPlug cp = (IPlug)new ConsolePlug();
            Plug.Init(cp);

            Engine e = new Engine();
            System.Threading.Thread t = new System.Threading.Thread(e.Run);
            t.Start(args);
        }
    }
}
