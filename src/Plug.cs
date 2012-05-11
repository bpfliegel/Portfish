using System;
using System.Collections.Generic;
using System.Text;

namespace Portfish
{
    public interface IPlug
    {
        void Write(string message);
        string ReadLine();
    }

    public static class Plug
    {
        internal static IPlug Interface = null;
        internal static bool IsWarmup = false;

        public static void Init(IPlug iFace)
        {
            Interface = iFace;
        }

        public static void Write(string message)
        {
            if (!Plug.IsWarmup) Interface.Write(message);
        }

        public static string ReadLine()
        {
            return Interface.ReadLine();
        }
    }
}
