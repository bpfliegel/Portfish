using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Portfish;
using Windows.System.Threading;
using Windows.UI.Core;

namespace WindowsRTTester
{
    internal static class WindowsRTEngine
    {
        // The plug
        private static WindowsRTPlug _thePlug = null;
        public static WindowsRTPlug ThePlug
        {
            get
            {
                return _thePlug;
            }
        }

        public static void StartEngine()
        {
            // Initialize the plug, through what we will communicate with the engine
            _thePlug = new WindowsRTPlug();
            Plug.Init(_thePlug);

            // Run the engine in async mode
            Windows.Foundation.IAsyncAction action = Windows.System.Threading.ThreadPool.RunAsync(delegate { new Engine().Run(new string[] { }); }, WorkItemPriority.Normal);
        }
    }
}