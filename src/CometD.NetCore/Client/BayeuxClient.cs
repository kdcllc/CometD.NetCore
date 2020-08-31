using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Timers;

using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;
using CometD.NetCore.Client.Transport;
using CometD.NetCore.Common;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace CometD.NetCore.Client
{
    public class BayeuxClient : AbstractClientSession, IBayeux, IDisposable
    {
        private static readonly Mutex StateUpdateInProgressMutex = new Mutex();

        private readonly ILogger _logger;

        private readonly TransportRegistry _transportRegistry = new TransportRegistry();
        private readonly Dictionary<string, object> _options = new Dictionary<string, object>();
        private readonly Queue<IMutableMessage> _messageQueue = new Queue<IMutableMessage>();
        private readonly CookieCollection _cookieCollection = new CookieCollection();
        private readonly ITransportListener _handshakeListener;
        private readonly ITransportListener _connectListener;
        private readonly ITransportListener _disconnectListener;
        private readonly ITransportListener _publishListener;
        private readonly AutoResetEvent _stateChanged = new AutoResetEvent(false);

        private BayeuxClientState _bayeuxClientState;
        private int _stateUpdateInProgress;

        public const string BACKOFF_INCREMENT_OPTION = "backoffIncrement";
        public const string MAX_BACKOFF_OPTION = "maxBackoff";
        public const string BAYEUX_VERSION = "1.0";

        public BayeuxClient(string url, params ClientTransport[] transports)
        {
            _handshakeListener = new HandshakeTransportListener(this);
            _connectListener = new ConnectTransportListener(this);
            _disconnectListener = new DisconnectTransportListener(this);
            _publishListener = new PublishTransportListener(this);

            if (transports == null)
            {
                throw new ArgumentNullException(nameof(transports));
            }

            if (transports.Length == 0)
            {
                throw new ArgumentException("No transports provided", nameof(transports));
            }

            if (transports.Any(t => t == null))
            {
                throw new ArgumentException("One of the transports was null", nameof(transports));
            }

            foreach (var t in transports)
            {
                _transportRegistry.Add(t);
            }

            foreach (var transportName in _transportRegistry.KnownTransports)
            {
                var clientTransport = _transportRegistry.GetTransport(transportName);
                if (clientTransport is HttpClientTransport httpTransport)
                {
                    httpTransport.Url = url;
                    httpTransport.SetCookieCollection(_cookieCollection);
                }
            }

            _bayeuxClientState = new DisconnectedState(this, null);
        }

        public BayeuxClient(string url, ILogger logger, params ClientTransport[] transports)
            : this(url, transports)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stateChanged?.Dispose();
            }
        }

        public override void Handshake()
        {
            Handshake(null);
        }

        public override void Handshake(IDictionary<string, object> handshakeFields)
        {
            Initialize();

            var allowedTransports = AllowedTransports;

            // Pick the first transport for the handshake, it will renegotiate if not right
            var initialTransport = _transportRegistry.GetTransport(allowedTransports[0]);
            initialTransport.Init();

            _logger?.LogDebug($"Using initial transport {initialTransport.Name}"
                             + $" from {Print.List(allowedTransports)}");

            UpdateBayeuxClientState(oldState => new HandshakingState(this, handshakeFields, initialTransport));
        }

        public override bool Connected => IsConnected(_bayeuxClientState);

        public override bool Handshook => IsHandshook(_bayeuxClientState);

        public override string Id => _bayeuxClientState.ClientId;

        public override void Disconnect()
        {
            UpdateBayeuxClientState(
                    oldState =>
                    {
                        if (IsConnected(oldState))
                        {
                            return new DisconnectingState(this, oldState.Transport, oldState.ClientId);
                        }
                        else
                        {
                            return new DisconnectedState(this, oldState.Transport);
                        }
                    });
        }

        protected override AbstractSessionChannel NewChannel(ChannelId channelId, long replayId)
        {
            return new BayeuxClientChannel(this, channelId, replayId);
        }

        protected override ChannelId NewChannelId(string channelId)
        {
            // Save some parsing by checking if there is already one
            Channels.TryGetValue(channelId, out var channel);
            return channel == null ? new ChannelId(channelId) : channel.ChannelId;
        }

        protected override void SendBatch()
        {
            var bayeuxClientState = _bayeuxClientState;
            if (IsHandshaking(bayeuxClientState))
            {
                return;
            }

            var messages = TakeMessages();
            if (messages.Count > 0)
            {
                SendMessages(messages);
            }
        }

        /// <inheritdoc/>
        public ICollection<string> KnownTransportNames => _transportRegistry.KnownTransports;

        /// <inheritdoc/>
        public ITransport GetTransport(string transport)
        {
            return _transportRegistry.GetTransport(transport);
        }

        /// <inheritdoc/>
        public IList<string> AllowedTransports => _transportRegistry.AllowedTransports;

        /// <inheritdoc/>
        public object GetOption(string qualifiedName)
        {
            _options.TryGetValue(qualifiedName, out var obj);
            return obj;
        }

        /// <inheritdoc/>
        public void SetOption(string qualifiedName, object val)
        {
            _options[qualifiedName] = val;
        }

        /// <inheritdoc/>
        public ICollection<string> OptionNames => _options.Keys;

        /// <inheritdoc/>
        public IDictionary<string, object> Options => _options;

        public long BackoffIncrement { get; private set; }

        public long MaxBackoff { get; private set; }

        public bool Disconnected => IsDisconnected(_bayeuxClientState);

        public string GetCookie(string name)
        {
            var cookie = _cookieCollection[name];
            if (cookie != null)
            {
                return cookie.Value;
            }

            return null;
        }

        public void SetCookie(string name, string val)
        {
            SetCookie(name, val, -1);
        }

        public void SetCookie(string name, string val, int maxAge)
        {
            var cookie = new Cookie(name, val, null, null);
            if (maxAge > 0)
            {
                cookie.Expires = DateTime.Now;
                cookie.Expires.AddMilliseconds(maxAge);
            }

            _cookieCollection.Add(cookie);
        }

        public void OnSending(IList<IMessage> messages)
        {
        }

        public void OnMessages(IList<IMutableMessage> messages)
        {
        }

        protected void FailMessages(Exception x, IList<IMessage> messages)
        {
            foreach (var message in messages)
            {
                var failed = NewMessage();
                failed.Id = message.Id;
                failed.Successful = false;
                failed.Channel = message.Channel;
                failed["message"] = messages;
                if (x != null)
                {
                    failed["exception"] = x;
                }

                Receive(failed);
            }
        }

        public virtual void OnFailure(Exception e, IList<IMessage> messages)
        {
            _logger?.LogError($"{e}");
        }

        public State Handshake(int waitMs)
        {
            return Handshake(null, waitMs);
        }

        public State Handshake(IDictionary<string, object> template, int waitMs)
        {
            Handshake(template);
            ICollection<State> states = new List<State>
            {
                State.CONNECTING,
                State.DISCONNECTED
            };
            return WaitFor(waitMs, states);
        }

        protected bool SendHandshake()
        {
            var bayeuxClientState = _bayeuxClientState;

            if (IsHandshaking(bayeuxClientState))
            {
                var message = NewMessage();
                if (bayeuxClientState.HandshakeFields != null)
                {
                    foreach (var kvp in bayeuxClientState.HandshakeFields)
                    {
                        message.Add(kvp.Key, kvp.Value);
                    }
                }

                message.Channel = ChannelFields.META_HANDSHAKE;
                message[MessageFields.SUPPORTED_CONNECTION_TYPES_FIELD] = AllowedTransports;
                message[MessageFields.VERSION_FIELD] = BAYEUX_VERSION;
                if (message.Id == null)
                {
                    message.Id = NewMessageId();
                }

                _logger?.LogDebug(
                    "Handshaking with extra fields {0}, transport {1}",
                    Print.Dictionary(bayeuxClientState.HandshakeFields),
                    Print.Dictionary(bayeuxClientState.Transport as IDictionary<string, object>));

                bayeuxClientState.Send(_handshakeListener, message);
                return true;
            }

            return false;
        }

        private bool IsHandshaking(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.TypeValue == State.HANDSHAKING
                || bayeuxClientState.TypeValue == State.REHANDSHAKING;
        }

        private bool IsHandshook(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.TypeValue == State.CONNECTING
                || bayeuxClientState.TypeValue == State.CONNECTED
                || bayeuxClientState.TypeValue == State.UNCONNECTED;
        }

        protected void ProcessHandshake(IMutableMessage handshake)
        {
            if (handshake.Successful)
            {
                var serverTransportObject = handshake[MessageFields.SUPPORTED_CONNECTION_TYPES_FIELD] as JArray;
                var serverTransports = serverTransportObject as IEnumerable<object>;

                var negotiatedTransports = _transportRegistry.Negotiate(serverTransports, BAYEUX_VERSION);
                var newTransport = negotiatedTransports.Count == 0 ? null : negotiatedTransports[0];
                if (newTransport == null)
                {
                    UpdateBayeuxClientState(
                            oldState => new DisconnectedState(this, oldState.Transport),
                            () => Receive(handshake));

                    // Signal the failure
                    handshake.Successful = false;
                    handshake[MessageFields.ERROR_FIELD] =
                            $"405:c{_transportRegistry.AllowedTransports},s{serverTransports}:no transport";

                    // TODO: also update the advice with reconnect=none for listeners ?
                }
                else
                {
                    UpdateBayeuxClientState(
                            oldState =>
                            {
                                if (newTransport != oldState.Transport)
                                {
                                    oldState.Transport.Reset();
                                    newTransport.Init();
                                }

                                var action = GetAdviceAction(handshake.Advice, MessageFields.RECONNECT_RETRY_VALUE);
                                if (MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                                {
                                    return new ConnectingState(
                                        this,
                                        oldState.HandshakeFields,
                                        handshake.Advice,
                                        newTransport,
                                        handshake.ClientId);
                                }
                                else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                                {
                                    return new DisconnectedState(this, oldState.Transport);
                                }

                                return null;
                            },
                            () => Receive(handshake));
                }
            }
            else
            {
                UpdateBayeuxClientState(
                        oldState =>
                        {
                            var action = GetAdviceAction(handshake.Advice, MessageFields.RECONNECT_HANDSHAKE_VALUE);
                            if (MessageFields.RECONNECT_HANDSHAKE_VALUE.Equals(action)
                                || MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                            {
                                return new RehandshakingState(this, oldState.HandshakeFields, oldState.Transport, oldState.NextBackoff());
                            }
                            else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                            {
                                return new DisconnectedState(this, oldState.Transport);
                            }

                            return null;
                        },
                        () => Receive(handshake));
            }
        }

        protected bool ScheduleHandshake(long interval, long backoff)
        {
            return ScheduleAction((sender, e) => SendHandshake(), interval, backoff);
        }

        protected State CurrentState => _bayeuxClientState.TypeValue;

        public State WaitFor(int waitMs, ICollection<State> states)
        {
            var stop = DateTime.Now.AddMilliseconds(waitMs);
            var duration = waitMs;

            var s = CurrentState;
            if (states.Contains(s))
            {
                return s;
            }

            while (_stateChanged.WaitOne(duration))
            {
                if (_stateUpdateInProgress == 0)
                {
                    s = CurrentState;
                    if (states.Contains(s))
                    {
                        return s;
                    }
                }

                duration = (int)(stop - DateTime.Now).TotalMilliseconds;
                if (duration <= 0)
                {
                    break;
                }
            }

            s = CurrentState;
            if (states.Contains(s))
            {
                return s;
            }

            return State.INVALID;
        }

        protected bool SendConnect(int clientTimeout)
        {
            var bayeuxClientState = _bayeuxClientState;
            if (IsHandshook(bayeuxClientState))
            {
                var message = NewMessage();
                message.Channel = ChannelFields.META_CONNECT;
                message[MessageFields.CONNECTION_TYPE_FIELD] = bayeuxClientState.Transport.Name;
                if (bayeuxClientState.TypeValue == State.CONNECTING || bayeuxClientState.TypeValue == State.UNCONNECTED)
                {
                    // First connect after handshake or after failure, add advice
                    message.GetAdvice(true)[MessageFields.TIMEOUT_FIELD] = 0;
                }

                bayeuxClientState.Send(_connectListener, message, clientTimeout);
                return true;
            }

            return false;
        }

        protected bool SendMessages(IList<IMutableMessage> messages)
        {
            var bayeuxClientState = _bayeuxClientState;
            if (bayeuxClientState.TypeValue == State.CONNECTING || IsConnected(bayeuxClientState))
            {
                bayeuxClientState.Send(_publishListener, messages);
                return true;
            }
            else
            {
                FailMessages(null, ObjectConverter.ToListOfIMessage(messages));
                return false;
            }
        }

        private int PendingMessages
        {
            get
            {
                var value = _messageQueue.Count;

                var state = _bayeuxClientState;
                if (state.Transport is ClientTransport clientTransport)
                {
                    value += clientTransport.IsSending ? 1 : 0;
                }

                return value;
            }
        }

        /// <summary>
        /// Wait for send queue to be emptied.
        /// </summary>
        /// <param name="timeoutMS"></param>
        /// <returns>true if queue is empty, false if timed out.</returns>
        public bool WaitForEmptySendQueue(int timeoutMS)
        {
            if (PendingMessages == 0)
            {
                return true;
            }

            var start = DateTime.Now;

            while ((DateTime.Now - start).TotalMilliseconds < timeoutMS)
            {
                if (PendingMessages == 0)
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return false;
        }

        public void Abort()
        {
            UpdateBayeuxClientState(oldState => new AbortedState(this, oldState.Transport));
        }

        private IList<IMutableMessage> TakeMessages()
        {
            IList<IMutableMessage> queue = new List<IMutableMessage>(_messageQueue);
            _messageQueue.Clear();
            return queue;
        }

        private bool IsConnected(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.TypeValue == State.CONNECTED;
        }

        private bool IsDisconnected(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.TypeValue == State.DISCONNECTING || bayeuxClientState.TypeValue == State.DISCONNECTED;
        }

        protected void ProcessConnect(IMutableMessage connect)
        {
            UpdateBayeuxClientState(
                    oldState =>
                    {
                        var advice = connect.Advice;
                        if (advice == null)
                        {
                            advice = oldState.Advice;
                        }

                        var action = GetAdviceAction(advice, MessageFields.RECONNECT_RETRY_VALUE);
                        if (connect.Successful)
                        {
                            if (MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                            {
                                return new ConnectedState(this, oldState.HandshakeFields, advice, oldState.Transport, oldState.ClientId);
                            }
                            else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                            {
                                // This case happens when the connect reply arrives after a disconnect
                                // We do not go into a disconnected state to allow normal processing of the disconnect reply
                                return new DisconnectingState(this, oldState.Transport, oldState.ClientId);
                            }
                        }
                        else
                        {
                            if (MessageFields.RECONNECT_HANDSHAKE_VALUE.Equals(action))
                            {
                                return new RehandshakingState(this, oldState.HandshakeFields, oldState.Transport, 0);
                            }
                            else if (MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                            {
                                return new UnconnectedState(this, oldState.HandshakeFields, advice, oldState.Transport, oldState.ClientId, oldState.NextBackoff());
                            }
                            else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                            {
                                return new DisconnectedState(this, oldState.Transport);
                            }
                        }

                        return null;
                    },
                    () => Receive(connect));
        }

        protected void ProcessDisconnect(IMutableMessage disconnect)
        {
            UpdateBayeuxClientState(
                    oldState => new DisconnectedState(this, oldState.Transport),
                    () => Receive(disconnect));
        }

        protected void ProcessMessage(IMutableMessage message)
        {
            Receive(message);
        }

        private string GetAdviceAction(IDictionary<string, object> advice, string defaultResult)
        {
            var action = defaultResult;
            if (advice?.ContainsKey(MessageFields.RECONNECT_FIELD) == true)
            {
                action = (string)advice[MessageFields.RECONNECT_FIELD];
            }

            return action;
        }

        protected bool ScheduleConnect(long interval, long backoff, int clientTimeout = ClientTransport.DEFAULT_TIMEOUT)
        {
            return ScheduleAction((object sender, ElapsedEventArgs e) => SendConnect(clientTimeout), interval, backoff);
        }

        private bool ScheduleAction(ElapsedEventHandler action, long interval, long backoff)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var timer = new System.Timers.Timer();
#pragma warning restore CA2000 // Dispose objects before losing scope
            timer.Elapsed += action;
            var wait = interval + backoff;
            if (wait <= 0)
            {
                wait = 1;
            }

            timer.Interval = wait;
            timer.AutoReset = false;
            timer.Enabled = true;
            return true;
        }

        protected void Initialize()
        {
            BackoffIncrement = ObjectConverter.ToInt64(GetOption(BACKOFF_INCREMENT_OPTION), 1000L);

            MaxBackoff = ObjectConverter.ToInt64(GetOption(MAX_BACKOFF_OPTION), 30000L);
        }

        protected void Terminate()
        {
            var messages = TakeMessages();
            FailMessages(null, ObjectConverter.ToListOfIMessage(messages));
        }

        protected IMutableMessage NewMessage()
        {
            return new DictionaryMessage();
        }

        protected void EnqueueSend(IMutableMessage message)
        {
            if (CanSend())
            {
                IList<IMutableMessage> messages = new List<IMutableMessage>
                {
                    message
                };
                var sent = SendMessages(messages);

                _logger?.LogDebug("{0} message {1}", sent ? "Sent" : "Failed", message);
            }
            else
            {
                _messageQueue.Enqueue(message);
                _logger?.LogDebug($"Enqueued message {message} (batching: {Batching})");
            }
        }

        private bool CanSend()
        {
            return !IsDisconnected(_bayeuxClientState) && !Batching && !IsHandshaking(_bayeuxClientState);
        }

        private void UpdateBayeuxClientState(BayeuxClientStateUpdater_createDelegate create)
        {
            UpdateBayeuxClientState(create, null);
        }

        private void UpdateBayeuxClientState(
            BayeuxClientStateUpdater_createDelegate create,
            BayeuxClientStateUpdater_postCreateDelegate postCreate)
        {
            StateUpdateInProgressMutex.WaitOne();
            ++_stateUpdateInProgress;
            StateUpdateInProgressMutex.ReleaseMutex();
            var oldState = _bayeuxClientState;

            var newState = create(oldState);
            if (newState == null)
            {
                throw new SystemException();
            }

            if (!oldState.IsUpdateableTo(newState))
            {
                _logger?.LogDebug($"State not updateable : {oldState} -> {newState}");
                return;
            }

            _bayeuxClientState = newState;

            postCreate?.Invoke();

            if (oldState.Type != newState.Type)
            {
                newState.Enter(oldState.Type);
            }

            newState.Execute();

            // Notify threads waiting in waitFor()
            StateUpdateInProgressMutex.WaitOne();
            --_stateUpdateInProgress;

            if (_stateUpdateInProgress == 0)
            {
                _stateChanged.Set();
            }

            StateUpdateInProgressMutex.ReleaseMutex();
        }

        public enum State
        {
            INVALID,
            UNCONNECTED,
            HANDSHAKING,
            REHANDSHAKING,
            CONNECTING,
            CONNECTED,
            DISCONNECTING,
            DISCONNECTED
        }

        private class PublishTransportListener : ITransportListener
        {
            protected BayeuxClient bayeuxClient;

            public PublishTransportListener(BayeuxClient bayeuxClient)
            {
                this.bayeuxClient = bayeuxClient;
            }

            public void OnSending(IList<IMessage> messages)
            {
                bayeuxClient.OnSending(messages);
            }

            public void OnMessages(IList<IMutableMessage> messages)
            {
                bayeuxClient.OnMessages(messages);
                foreach (var message in messages)
                {
                    ProcessMessage(message);
                }
            }

            public void OnConnectException(Exception x, IList<IMessage> messages)
            {
                OnFailure(x, messages);
            }

            public void OnException(Exception x, IList<IMessage> messages)
            {
                OnFailure(x, messages);
            }

            public void OnExpire(IList<IMessage> messages)
            {
                OnFailure(new TimeoutException("expired"), messages);
            }

            public void OnProtocolError(string info, IList<IMessage> messages)
            {
                OnFailure(new ProtocolViolationException(info), messages);
            }

            protected virtual void ProcessMessage(IMutableMessage message)
            {
                bayeuxClient.ProcessMessage(message);
            }

            protected virtual void OnFailure(Exception x, IList<IMessage> messages)
            {
                bayeuxClient.OnFailure(x, messages);
                bayeuxClient.FailMessages(x, messages);
            }
        }

        private class HandshakeTransportListener : PublishTransportListener
        {
            public HandshakeTransportListener(BayeuxClient bayeuxClient)
                : base(bayeuxClient)
            {
            }

            protected override void OnFailure(Exception x, IList<IMessage> messages)
            {
                bayeuxClient.UpdateBayeuxClientState(oldState => new RehandshakingState(bayeuxClient, oldState.HandshakeFields, oldState.Transport, oldState.NextBackoff()));
                base.OnFailure(x, messages);
            }

            protected override void ProcessMessage(IMutableMessage message)
            {
                if (ChannelFields.META_HANDSHAKE.Equals(message.Channel))
                {
                    bayeuxClient.ProcessHandshake(message);
                }
                else
                {
                    base.ProcessMessage(message);
                }
            }
        }

        private class ConnectTransportListener : PublishTransportListener
        {
            public ConnectTransportListener(BayeuxClient bayeuxClient)
                : base(bayeuxClient)
            {
            }

            protected override void OnFailure(Exception x, IList<IMessage> messages)
            {
                bayeuxClient.UpdateBayeuxClientState(
                        oldState => new UnconnectedState(bayeuxClient, oldState.HandshakeFields, oldState.Advice, oldState.Transport, oldState.ClientId, oldState.NextBackoff()));
                base.OnFailure(x, messages);
            }

            protected override void ProcessMessage(IMutableMessage message)
            {
                if (ChannelFields.META_CONNECT.Equals(message.Channel))
                {
                    bayeuxClient.ProcessConnect(message);
                }
                else
                {
                    base.ProcessMessage(message);
                }
            }
        }

        private class DisconnectTransportListener : PublishTransportListener
        {
            public DisconnectTransportListener(BayeuxClient bayeuxClient)
                : base(bayeuxClient)
            {
            }

            protected override void OnFailure(Exception x, IList<IMessage> messages)
            {
                bayeuxClient.UpdateBayeuxClientState(
                        oldState => new DisconnectedState(bayeuxClient, oldState.Transport));
                base.OnFailure(x, messages);
            }

            protected override void ProcessMessage(IMutableMessage message)
            {
                if (ChannelFields.META_DISCONNECT.Equals(message.Channel))
                {
                    bayeuxClient.ProcessDisconnect(message);
                }
                else
                {
                    base.ProcessMessage(message);
                }
            }
        }

        public class BayeuxClientChannel : AbstractSessionChannel
        {
            protected BayeuxClient bayeuxClient;

            public BayeuxClientChannel(BayeuxClient bayeuxClient, ChannelId channelId, long replayId)
                : base(channelId, replayId)
            {
                this.bayeuxClient = bayeuxClient;
            }

            public override IClientSession Session => this as IClientSession;

            protected override void SendSubscribe()
            {
                var message = bayeuxClient.NewMessage();
                message.Channel = ChannelFields.META_SUBSCRIBE;
                message[MessageFields.SUBSCRIPTION_FIELD] = Id;
                message.ReplayId = ReplayId;
                bayeuxClient.EnqueueSend(message);
            }

            protected override void SendUnSubscribe()
            {
                var message = bayeuxClient.NewMessage();
                message.Channel = ChannelFields.META_UNSUBSCRIBE;
                message[MessageFields.SUBSCRIPTION_FIELD] = Id;
                message.ReplayId = ReplayId;
                bayeuxClient.EnqueueSend(message);
            }

            public override void Publish(object data)
            {
                Publish(data, null);
            }

            public override void Publish(object data, string messageId)
            {
                var message = bayeuxClient.NewMessage();
                message.Channel = Id;
                message.Data = data;
                if (messageId != null)
                {
                    message.Id = messageId;
                }

                bayeuxClient.EnqueueSend(message);
            }
        }

        private delegate BayeuxClientState BayeuxClientStateUpdater_createDelegate(BayeuxClientState oldState);

        private delegate void BayeuxClientStateUpdater_postCreateDelegate();

        public abstract class BayeuxClientState
        {
            public State TypeValue;
            public IDictionary<string, object> HandshakeFields;
            public IDictionary<string, object> Advice;
            public ClientTransport Transport;
            public string ClientId;
            public long Backoff;
            protected BayeuxClient _bayeuxClient;

            protected BayeuxClientState(
                BayeuxClient bayeuxClient,
                State type,
                IDictionary<string, object> handshakeFields,
                IDictionary<string, object> advice,
                ClientTransport transport,
                string clientId,
                long backoff)
            {
                _bayeuxClient = bayeuxClient;
                TypeValue = type;
                HandshakeFields = handshakeFields;
                Advice = advice;
                Transport = transport;
                ClientId = clientId;
                Backoff = backoff;
            }

            public long Interval
            {
                get
                {
                    long result = 0;
                    if (Advice?.ContainsKey(MessageFields.INTERVAL_FIELD) == true)
                    {
                        result = ObjectConverter.ToInt64(Advice[MessageFields.INTERVAL_FIELD], result);
                    }

                    return result;
                }
            }

            public void Send(ITransportListener listener, IMutableMessage message, int clientTimeout = ClientTransport.DEFAULT_TIMEOUT)
            {
                IList<IMutableMessage> messages = new List<IMutableMessage>
                {
                    message
                };
                Send(listener, messages, clientTimeout);
            }

            public void Send(ITransportListener listener, IList<IMutableMessage> messages, int clientTimeout = ClientTransport.DEFAULT_TIMEOUT)
            {
                foreach (var message in messages)
                {
                    if (message.Id == null)
                    {
                        message.Id = _bayeuxClient.NewMessageId();
                    }

                    if (ClientId != null)
                    {
                        message.ClientId = ClientId;
                    }

                    if (!_bayeuxClient.ExtendSend(message))
                    {
                        messages.Remove(message);
                    }
                }

                if (messages.Count > 0)
                {
                    Transport.Send(listener, messages, clientTimeout);
                }
            }

            public long NextBackoff()
            {
                return Math.Min(Backoff + _bayeuxClient.BackoffIncrement, _bayeuxClient.MaxBackoff);
            }

            public abstract bool IsUpdateableTo(BayeuxClientState newState);

            public virtual void Enter(State oldState)
            {
            }

            public abstract void Execute();

            public State Type => TypeValue;

            public override string ToString()
            {
                return TypeValue.ToString();
            }
        }

        private class DisconnectedState : BayeuxClientState
        {
            public DisconnectedState(BayeuxClient bayeuxClient, ClientTransport transport)
                : base(bayeuxClient, State.DISCONNECTED, null, null, transport, null, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.HANDSHAKING;
            }

            public override void Execute()
            {
                Transport.Reset();
                _bayeuxClient.Terminate();
            }
        }

        private class AbortedState : DisconnectedState
        {
            public AbortedState(BayeuxClient bayeuxClient, ClientTransport transport)
                : base(bayeuxClient, transport)
            {
            }

            public override void Execute()
            {
                Transport.Abort();
                base.Execute();
            }
        }

        private class HandshakingState : BayeuxClientState
        {
            public HandshakingState(BayeuxClient bayeuxClient, IDictionary<string, object> handshakeFields, ClientTransport transport)
                : base(bayeuxClient, State.HANDSHAKING, handshakeFields, null, transport, null, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.REHANDSHAKING ||
                    newState.TypeValue == State.CONNECTING ||
                    newState.TypeValue == State.DISCONNECTED;
            }

            public override void Enter(State oldState)
            {
                // Always reset the subscriptions when a handshake has been requested.
                _bayeuxClient.ResetSubscriptions();
            }

            public override void Execute()
            {
                // The state could change between now and when sendHandshake() runs;
                // in this case the handshake message will not be sent and will not
                // be failed, because most probably the client has been disconnected.
                _bayeuxClient.SendHandshake();
            }
        }

        private class RehandshakingState : BayeuxClientState
        {
            public RehandshakingState(
                BayeuxClient bayeuxClient,
                IDictionary<string, object> handshakeFields,
                ClientTransport transport,
                long backoff)
                : base(bayeuxClient, State.REHANDSHAKING, handshakeFields, null, transport, null, backoff)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.CONNECTING ||
                    newState.TypeValue == State.REHANDSHAKING ||
                    newState.TypeValue == State.DISCONNECTED;
            }

            public override void Enter(State oldState)
            {
                // Reset the subscriptions if this is not a failure from a requested handshake.
                // Subscriptions may be queued after requested handshakes.
                if (oldState != State.HANDSHAKING)
                {
                    // Reset subscriptions if not queued after initial handshake
                    _bayeuxClient.ResetSubscriptions();
                }
            }

            public override void Execute()
            {
                _bayeuxClient.ScheduleHandshake(Interval, Backoff);
            }
        }

        private class ConnectingState : BayeuxClientState
        {
            public ConnectingState(
                BayeuxClient bayeuxClient,
                IDictionary<string, object> handshakeFields,
                IDictionary<string, object> advice,
                ClientTransport transport,
                string clientId)
                : base(bayeuxClient, State.CONNECTING, handshakeFields, advice, transport, clientId, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.CONNECTED ||
                    newState.TypeValue == State.UNCONNECTED ||
                    newState.TypeValue == State.REHANDSHAKING ||
                    newState.TypeValue == State.DISCONNECTING ||
                    newState.TypeValue == State.DISCONNECTED;
            }

            public override void Execute()
            {
                // Send the messages that may have queued up before the handshake completed
                _bayeuxClient.SendBatch();
                _bayeuxClient.ScheduleConnect(Interval, Backoff);
            }
        }

        private class ConnectedState : BayeuxClientState
        {
            public ConnectedState(
                BayeuxClient bayeuxClient,
                IDictionary<string, object> handshakeFields,
                IDictionary<string, object> advice,
                ClientTransport transport,
                string clientId)
                : base(bayeuxClient, State.CONNECTED, handshakeFields, advice, transport, clientId, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.CONNECTED ||
                    newState.TypeValue == State.UNCONNECTED ||
                    newState.TypeValue == State.REHANDSHAKING ||
                    newState.TypeValue == State.DISCONNECTING ||
                    newState.TypeValue == State.DISCONNECTED;
            }

            public override void Execute()
            {
                var adviceTimeoutField = _bayeuxClient?._bayeuxClientState?.Advice[MessageFields.TIMEOUT_FIELD];

                if (!(adviceTimeoutField is null))
                {
                    int adviceTimeoutValue = Convert.ToInt32(adviceTimeoutField);
                    if (adviceTimeoutValue != 0)
                    {
                        _bayeuxClient.ScheduleConnect(Interval, Backoff, adviceTimeoutValue);
                        return;
                    }
                }

                _bayeuxClient?.ScheduleConnect(Interval, Backoff);
            }
        }

        private class UnconnectedState : BayeuxClientState
        {
            public UnconnectedState(
                BayeuxClient bayeuxClient,
                IDictionary<string, object> handshakeFields,
                IDictionary<string, object> advice,
                ClientTransport transport,
                string clientId,
                long backoff)
                : base(bayeuxClient, State.UNCONNECTED, handshakeFields, advice, transport, clientId, backoff)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.CONNECTED ||
                    newState.TypeValue == State.UNCONNECTED ||
                    newState.TypeValue == State.REHANDSHAKING ||
                    newState.TypeValue == State.DISCONNECTED;
            }

            public override void Execute()
            {
                _bayeuxClient.ScheduleConnect(Interval, Backoff);
            }
        }

        private class DisconnectingState : BayeuxClientState
        {
            public DisconnectingState(BayeuxClient bayeuxClient, ClientTransport transport, string clientId)
                : base(bayeuxClient, State.DISCONNECTING, null, null, transport, clientId, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.TypeValue == State.DISCONNECTED;
            }

            public override void Execute()
            {
                var message = _bayeuxClient.NewMessage();
                message.Channel = ChannelFields.META_DISCONNECT;
                Send(_bayeuxClient._disconnectListener, message);
            }
        }
    }
}
