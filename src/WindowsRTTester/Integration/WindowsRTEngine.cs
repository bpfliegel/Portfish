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
            _thePlug = new WindowsRTPlug();
            Plug.Init(_thePlug);

            Windows.System.Threading.ThreadPool.RunAsync((sender) =>
            {
                new Engine().Run(new string[] {});
            }, Windows.System.Threading.WorkItemPriority.Normal);
        }
    }
}