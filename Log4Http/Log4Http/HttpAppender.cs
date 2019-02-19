namespace Log4Http
{
    using log4net.Appender;
    using log4net.Core;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class HttpAppender : AppenderSkeleton
    {
        public class RequestHeader
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private string _currentLog;
        private readonly object _lockObject = new object ();
        private Timer _messageTimer;
        private ManualResetEvent _resetEvent;
        private List<RequestHeader> _headers = new List<RequestHeader>();
        private uint _sendIntervalMinutes;
        private int _sendIntervalMS => (int) SendIntervalMinutes * 60 * 1000;

        // Config Values
        public string Url { get; set; }
        public string Content { get; set; }

        public uint SendIntervalMinutes
        {
            get
            {
                return _sendIntervalMinutes;
            }

            set
            {
                _sendIntervalMinutes = value;
                Start ();
            }
        }

        public RequestHeader Header
        {
            set => _headers.Add (value);
        }

        public bool DebugMode { get; set; } = false;


        private void Start ()
        {
            _resetEvent = new ManualResetEvent (false);
            _messageTimer = new Timer (MessageLoop, null, _sendIntervalMS, _sendIntervalMS);
        }

        protected override void OnClose ()
        {
            _resetEvent.WaitOne (5000);

            _messageTimer.Dispose ();

            base.OnClose ();
        }

        protected override void Append (LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderMessage (loggingEvent);

            lock (_lockObject)
            {
                _currentLog += renderedMessage;
            }            
        }

        private string RenderMessage (LoggingEvent loggingEvent)
        {
            using (StringWriter writer = new StringWriter ())
            {
                Layout.Format (writer, loggingEvent);
                return writer.ToString ();
            }
        }

        private async void MessageLoop (object state)
        {
            await PerformRequest ();
        }

        private async Task PerformRequest()
        {
            if (_currentLog == "")
                return;

            _resetEvent.Reset ();

            using (HttpClient client = new HttpClient ())
            {
                try
                {
                    string content;
                    lock (_lockObject)
                    {
                        content = Content.Replace ("%messages", WebUtility.UrlEncode(_currentLog));
                        _currentLog = "";
                    }
                   
                    Debug.WriteLineIf (DebugMode, "HttpAppender sending Request");
                    Debug.WriteLineIf (DebugMode, "Content: " + content);

                    var requestContent = new StringContent (content);
                    requestContent.Headers.Clear ();
                    foreach (var pair in _headers)
                        requestContent.Headers.Add (pair.Name, pair.Value);

                    var result = await client.PostAsync (Url, requestContent);
                    result.EnsureSuccessStatusCode ();
                    

                    var response = await result.Content.ReadAsStringAsync ();

                    Debug.WriteLineIf (DebugMode, "HttpAppender sended Request");
                    Debug.WriteLineIf (DebugMode, "Result: " + response);
                }
                catch (Exception ex)
                {
                    Debug.WriteLineIf (DebugMode, "Log4Http Exception: " + ex);
                }
            }

            _resetEvent.Set ();
        }
    }
}
