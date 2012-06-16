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
        static byte[] inputBuffer = new byte[8192];

        static void Main(string[] args)
        {
            // Setup an 8k inputBuffer because really long UCI strings were getting truncated
            Stream inputStream = Console.OpenStandardInput(inputBuffer.Length);
            Console.SetIn(new StreamReader(inputStream, Encoding.UTF7, false, inputBuffer.Length));

            IPlug cp = (IPlug)new ConsolePlug();
            Plug.Init(cp);

            Engine e = new Engine();
            System.Threading.Thread t = new System.Threading.Thread(e.Run);
            t.Start(args);
        }
    }
}
