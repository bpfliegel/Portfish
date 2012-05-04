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

        public static void Init(IPlug iFace)
        {
            Interface = iFace;
        }
    }
}
