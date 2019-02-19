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

        private bool IsClosing;
        private string CurrentLog;
        private readonly object LockObject = new object ();
        private ManualResetEvent ResetEvent;
        public List<RequestHeader> Headers = new List<RequestHeader>();

        // Config Values
        public string Url { get; set; }
        public string Content { get; set; }
        public uint SendIntervalMinutes { get; set; }
        public RequestHeader Header
        {
            set => Headers.Add (value);
        }

        public bool DebugMode { get; set; } = false;

        public HttpAppender ()
        {
            CurrentLog = "";
            ResetEvent = new ManualResetEvent (false);

            Start ();
        }

        private void Start ()
        {
            var thread = new Thread (MessageLoop);
            thread.Start ();
        }

        protected override void OnClose ()
        {
            IsClosing = true;

            ResetEvent.WaitOne (5000);

            base.OnClose ();
        }

        protected override void Append (LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderMessage (loggingEvent);

            lock (LockObject)
            {
                CurrentLog += renderedMessage;
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

        private async void MessageLoop ()
        {
            while (!IsClosing)
            {
                uint intervalCounter = 0;
                while (intervalCounter <= SendIntervalMinutes * 60 * 1000)
                {
                    if (IsClosing)
                        break;

                    Thread.Sleep (1000);
                    intervalCounter += 1000;
                }

                await PerformRequest ();
            }

            ResetEvent.Set ();
        }

        private async Task PerformRequest()
        {
            if (CurrentLog == "")
                return;

            using (HttpClient client = new HttpClient ())
            {
                try
                {
                    string content;
                    lock (LockObject)
                    {
                        content = Content.Replace ("%messages", WebUtility.UrlEncode(CurrentLog));
                        CurrentLog = "";
                    }
                   
                    Debug.WriteLineIf (DebugMode, "HttpAppender sending Request");
                    Debug.WriteLineIf (DebugMode, "Content: " + content);

                    var requestContent = new StringContent (content);
                    requestContent.Headers.Clear ();
                    foreach (var pair in Headers)
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
        }
    }
}
