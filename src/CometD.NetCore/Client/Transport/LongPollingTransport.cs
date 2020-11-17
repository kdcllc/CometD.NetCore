using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using CometD.NetCore.Bayeux;
using CometD.NetCore.Common;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace CometD.NetCore.Client.Transport
{
    public class LongPollingTransport : HttpClientTransport
    {
        private static ILogger _logger;

        private readonly List<TransportExchange> _exchanges = new List<TransportExchange>();
        private readonly List<LongPollingRequest> _transportQueue = new List<LongPollingRequest>();
        private readonly HashSet<LongPollingRequest> _transmissions = new HashSet<LongPollingRequest>();
        private readonly object _lockObject = new object();

        private bool _appendMessageType;

        public LongPollingTransport(
            IDictionary<string, object> options,
            NameValueCollection headers)
            : base("long-polling", options, headers)
        {
        }

        public LongPollingTransport(
            IDictionary<string, object> options,
            NameValueCollection headers,
            ILogger logger)
            : this(options, headers)
        {
            _logger = logger;
        }

        public override bool Accept(string version)
        {
            return true;
        }

        public override void Init()
        {
            base.Init();

            // _aborted = false;
            var uriRegex = new Regex("(^https?://(([^:/\\?#]+)(:(\\d+))?))?([^\\?#]*)(.*)?");
            var uriMatch = uriRegex.Match(Url);
            if (uriMatch.Success)
            {
                var afterPath = uriMatch.Groups[7].ToString();
                _appendMessageType = afterPath == null || afterPath.Trim().Length == 0;
            }
        }

        public override void Abort()
        {
            // _aborted = true;
            lock (_lockObject)
            {
                foreach (var exchange in _exchanges)
                {
                    exchange.Abort();
                }

                _exchanges.Clear();
            }
        }

        public override void Reset()
        {
        }

        // Fix for not running more than two simultaneous requests:
        public class LongPollingRequest
        {
            public int RequestTimout;
            public TransportExchange Exchange;

            private readonly ITransportListener _listener;
            private readonly IList<IMutableMessage> _messages;
            private readonly HttpWebRequest _request;

            public LongPollingRequest(
                ITransportListener listener,
                IList<IMutableMessage> messages,
                HttpWebRequest request,
                int requestTimeout = DEFAULT_TIMEOUT)
            {
                _listener = listener;
                _messages = messages;
                _request = request;
                RequestTimout = requestTimeout;
            }

            public void Send()
            {
                try
                {
                    _request.BeginGetRequestStream(new AsyncCallback(GetRequestStreamCallback), Exchange);
                }
                catch (Exception e)
                {
                    Exchange.Dispose();
                    _listener.OnException(e, ObjectConverter.ToListOfIMessage(_messages));
                }
            }
        }

        private void PerformNextRequest()
        {
            var ok = false;
            LongPollingRequest nextRequest = null;

            lock (_lockObject)
            {
                if (_transportQueue.Count > 0 && _transmissions.Count <= 1)
                {
                    ok = true;
                    nextRequest = _transportQueue[0];
                    _transportQueue.Remove(nextRequest);
                    _transmissions.Add(nextRequest);
                }
            }

            if (ok && nextRequest != null)
            {
                nextRequest.Send();
            }
        }

        public void AddRequest(LongPollingRequest request)
        {
            lock (_lockObject)
            {
                _transportQueue.Add(request);
            }

            PerformNextRequest();
        }

        public void RemoveRequest(LongPollingRequest request)
        {
            lock (_lockObject)
            {
                _transmissions.Remove(request);
            }

            PerformNextRequest();
        }

        public override void Send(ITransportListener listener, IList<IMutableMessage> messages, int requestTimeout)
        {
            _logger?.LogDebug($"send({messages.Count} message(s)");

            var url = Url;

            if (_appendMessageType
                && messages.Count == 1
                && messages[0].Meta)
            {
                var type = messages[0].Channel.Substring(ChannelFields.META.Length);
                if (url.EndsWith("/"))
                {
                    url = url.Substring(0, url.Length - 1);
                }

                url += type;
            }

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json;charset=UTF-8";

            (request.CookieContainer ?? (request.CookieContainer = new CookieContainer())).Add(GetCookieCollection());

            (request.Headers ?? (request.Headers = new WebHeaderCollection())).Add(GetHeaderCollection());
            if (requestTimeout < DEFAULT_TIMEOUT)
            {
                request.Timeout = requestTimeout + GetOption(MAX_NETWORK_DELAY_OPTION, 5000);
            }

            var content = JsonConvert.SerializeObject(ObjectConverter.ToListOfDictionary(messages));

            _logger?.LogDebug($"Send: {content}");

            var longPollingRequest = new LongPollingRequest(listener, messages, request);

            var exchange = new TransportExchange(this, listener, messages, longPollingRequest)
            {
                Content = content,
                Request = request
            };
            lock (_lockObject)
            {
                _exchanges.Add(exchange);
            }

            longPollingRequest.Exchange = exchange;
            AddRequest(longPollingRequest);
        }

        public override bool IsSending
        {
            get
            {
                lock (_lockObject)
                {
                    if (_transportQueue.Count > 0)
                    {
                        return true;
                    }

                    foreach (var transmission in _transmissions)
                    {
                        if (transmission.Exchange.IsSending)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        // From http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.begingetrequeststream.aspx
        private static void GetRequestStreamCallback(IAsyncResult asynchronousResult)
        {
            var exchange = (TransportExchange)asynchronousResult.AsyncState;

            try
            {
                // End the operation
                using (var postStream = exchange.Request.EndGetRequestStream(asynchronousResult))
                {
                    // Convert the string into a byte array.
                    var byteArray = Encoding.UTF8.GetBytes(exchange.Content);

                    // Write to the request stream.
                    postStream.Write(byteArray, 0, exchange.Content.Length);
                    postStream.Close();
                }

                // Start the asynchronous operation to get the response
                exchange.Listener.OnSending(ObjectConverter.ToListOfIMessage(exchange.Messages));
                var result = exchange.Request.BeginGetResponse(new AsyncCallback(GetResponseCallback), exchange);

                long timeout = exchange?.LongPollingRequest?.RequestTimout ?? DEFAULT_TIMEOUT;
                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), exchange, timeout, true);

                exchange.IsSending = false;
            }
            catch (Exception e)
            {
                exchange.Request?.Abort();

                exchange.Dispose();
                exchange.Listener.OnException(e, ObjectConverter.ToListOfIMessage(exchange.Messages));
            }
        }

        private static void GetResponseCallback(IAsyncResult asynchronousResult)
        {
            var exchange = (TransportExchange)asynchronousResult.AsyncState;

            try
            {
                // End the operation
                string responsestring;
                using (var response = (HttpWebResponse)exchange.Request.EndGetResponse(asynchronousResult))
                {
                    using (var streamResponse = response.GetResponseStream())
                    {
                        using var streamRead = new StreamReader(streamResponse);
                        responsestring = streamRead.ReadToEnd();
                    }

                    _logger?.LogDebug("Received message(s).");

                    if (response.Cookies != null)
                    {
                        foreach (Cookie cookie in response.Cookies)
                        {
                            exchange.AddCookie(cookie);
                        }
                    }

                    response.Close();
                }

                try
                {
                    exchange.Messages = DictionaryMessage.ParseMessages(responsestring);
                }
                catch (Exception e)
                {
                    _logger?.LogError($"Failed to parse the messages json: {e}");
                }

                exchange.Listener.OnMessages(exchange.Messages);
                exchange.Dispose();
            }
            catch (Exception e)
            {
                exchange.Listener.OnException(e, ObjectConverter.ToListOfIMessage(exchange.Messages));
                exchange.Dispose();
            }
        }

        // From http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.begingetresponse.aspx
        // Abort the request if the timer fires.
        private static void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                _logger?.LogDebug("Timeout");

                var exchange = state as TransportExchange;

                exchange?.Request?.Abort();

                exchange?.Dispose();
            }
        }

        public class TransportExchange
        {
            public string Content;
            public HttpWebRequest Request;
            public ITransportListener Listener;
            public IList<IMutableMessage> Messages;
            public LongPollingRequest LongPollingRequest;
            public bool IsSending;

            private readonly LongPollingTransport _parent;

            public TransportExchange(
                LongPollingTransport parent,
                ITransportListener listener,
                IList<IMutableMessage> messages,
                LongPollingRequest _lprequest)
            {
                _parent = parent;
                Listener = listener;
                Messages = messages;
                Request = null;
                LongPollingRequest = _lprequest;
                IsSending = true;
            }

            public void AddCookie(Cookie cookie)
            {
                _parent.AddCookie(cookie);
            }

            public void Dispose()
            {
                _parent.RemoveRequest(LongPollingRequest);
                lock (_parent)
                {
                    _parent._exchanges.Remove(this);
                }
            }

            public void Abort()
            {
                Request?.Abort();
            }
        }
    }
}
