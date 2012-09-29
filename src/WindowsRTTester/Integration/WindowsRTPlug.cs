using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Portfish;
using System.Text;
using Windows.UI.Core;
using System.Threading;
using Windows.UI.Xaml;

namespace WindowsRTTester
{
    internal sealed class WindowsRTPlug : IPlug
    {
        #region Communication

        private void DispatchMessageFromEngine(string message)
        {
            // Dispatches the message to the UI/Application logic
            // =====================================================================
            // TODO: do your custom implementation here
            // =====================================================================
            MainPage.RaiseEngineMessage(message);
        }

        public void SendMessageToEngine(string message)
        {
            // Sends a message to the engine
            SendMessageToEngineInternal(message);
        }

        #endregion

        #region Internal methods

        #region Write

        // Hold a buffer until we get a full line
        private StringBuilder _sb = new StringBuilder();
        private void WriteInternal(string message)
        {
            _sb.Append(message);
            if (message.IndexOf("\n")>-1)
            {
                string wholeMessage = _sb.ToString();
                _sb.Clear();

                // Do something with the message
                DispatchMessageFromEngine(wholeMessage);
            }
        }

        #endregion

        #region Read

        AutoResetEvent _readEvent = new AutoResetEvent(false);
        private string _message = string.Empty;

        private void SendMessageToEngineInternal(string message)
        {
            _message = message;
            _readEvent.Set();
        }

        private string ReadInternal()
        {
            _readEvent.WaitOne();
            return _message;
        }

        #endregion

        #endregion

        #region Interface methods

        public void Write(string message)
        {
            WriteInternal(message);
        }

        public string ReadLine()
        {
            return ReadInternal();
        }

        #endregion
    }
}