using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WindowsRTTester
{
    public delegate void EngineMessageHandler(EngineMessageEventArgs e);

    public class EngineMessageEventArgs : EventArgs
    {
        public readonly string Message;

        public EngineMessageEventArgs(string message)
        {
            this.Message = message;
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            WindowsRTEngine.StartEngine();
            EngineMessage += new EngineMessageHandler(AppendMessage);
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = new TimeSpan(0, 0, 0, 0, 500);
            dt.Tick += MessageUpdate;
            dt.Start();
        }

        private string _lastMessage = string.Empty;

        void MessageUpdate(object sender, object e)
        {
            string thisMessage = _sbEngineMessages.ToString();
            if (thisMessage != _lastMessage)
            {
                txtEngine.Text = thisMessage;
                _lastMessage = thisMessage;
                scrScroller.ScrollToVerticalOffset(txtEngine.RenderSize.Height);
            }
        }

        public static event EngineMessageHandler EngineMessage;

        public static void RaiseEngineMessage(string message)
        {
            EngineMessageEventArgs e = new EngineMessageEventArgs(message);
            if (EngineMessage != null) { EngineMessage(e); }
        }

        private StringBuilder _sbEngineMessages = new StringBuilder();

        private void AppendMessage(EngineMessageEventArgs e)
        {
            _sbEngineMessages.Append(e.Message);
        }

        private void btnSendClick(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void txtMessageKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendMessage();
            }
        }

        private void SendMessage()
        {
            WindowsRTEngine.ThePlug.SendMessageToEngine(txtMessage.Text);
            txtMessage.Text = string.Empty;
        }
    }
}
