using System;
using System.Collections.Generic;
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
    /// <summary> </summary>
    public class BayeuxClient : AbstractClientSession, IBayeux
    {
        //private static ILogger<BayeuxClient> = new LoggerFactory()
        public const string BACKOFF_INCREMENT_OPTION = "backoffIncrement";
        public const string MAX_BACKOFF_OPTION = "maxBackoff";
        public const string BAYEUX_VERSION = "1.0";

        private TransportRegistry transportRegistry = new TransportRegistry();
        private Dictionary<string, object> options = new Dictionary<string, object>();
        private BayeuxClientState bayeuxClientState;
        private Queue<IMutableMessage> messageQueue = new Queue<IMutableMessage>();
        private CookieCollection cookieCollection = new CookieCollection();
        private ITransportListener handshakeListener;
        private ITransportListener connectListener;
        private ITransportListener disconnectListener;
        private ITransportListener publishListener;
        private static Mutex stateUpdateInProgressMutex = new Mutex();
        private int stateUpdateInProgress;
        private AutoResetEvent stateChanged = new AutoResetEvent(false);

        public BayeuxClient(string url, params ClientTransport[] transports)
        {
            //logger = Log.getLogger(GetType().FullName + "@" + this.GetHashCode());
            //Console.WriteLine(GetType().FullName + "@" + this.GetHashCode());

            handshakeListener = new HandshakeTransportListener(this);
            connectListener = new ConnectTransportListener(this);
            disconnectListener = new DisconnectTransportListener(this);
            publishListener = new PublishTransportListener(this);

            if (transports == null || transports.Length == 0 || transports[0] == null)
            {
                throw new ArgumentNullException(nameof(transports));
            }

            foreach (var t in transports)
            {
                transportRegistry.Add(t);
            }

            foreach (var transportName in transportRegistry.KnownTransports)
            {
                var clientTransport = transportRegistry.GetTransport(transportName);
                if (clientTransport is HttpClientTransport httpTransport)
                {
                    httpTransport.Url = url;
                    httpTransport.SetCookieCollection(cookieCollection);
                }
            }

            bayeuxClientState = new DisconnectedState(this, null);
        }

        #region AbstractClientSession overrides

        #region IClientSession
        public override void Handshake()
        {
            Handshake(null);
        }

        public override void Handshake(IDictionary<string, object> handshakeFields)
        {
            Initialize();

            var allowedTransports = AllowedTransports;
            // Pick the first transport for the handshake, it will renegotiate if not right
            var initialTransport = transportRegistry.GetTransport(allowedTransports[0]);
            initialTransport.Init();
            //Console.WriteLine("Using initial transport {0} from {1}", initialTransport.Name, Print.List(allowedTransports));

            UpdateBayeuxClientState(
                    delegate (BayeuxClientState oldState)
                    {
                        return new HandshakingState(this, handshakeFields, initialTransport);
                    });
        }
        #endregion

        #region ISession
        public override bool Connected => IsConnected(bayeuxClientState);

        public override bool Handshook => IsHandshook(bayeuxClientState);

        public override string Id => bayeuxClientState.clientId;
        
        public override void Disconnect()
        {
            UpdateBayeuxClientState(
                    delegate (BayeuxClientState oldState)
                    {
                        if (IsConnected(oldState))
                        {
                            return new DisconnectingState(this, oldState.transport, oldState.clientId);
                        }
                        else
                        {
                            return new DisconnectedState(this, oldState.transport);
                        }
                    });
        }

        #endregion

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
            var bayeuxClientState = this.bayeuxClientState;
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

        #endregion

        #region IBayeux
        
        ///<inheritdoc/>
        public ICollection<string> KnownTransportNames => transportRegistry.KnownTransports;

        ///<inheritdoc/>
        public ITransport GetTransport(string transport)
        {
            return transportRegistry.GetTransport(transport);
        }

        ///<inheritdoc/>
        public IList<string> AllowedTransports => transportRegistry.AllowedTransports;

        #region Option

        ///<inheritdoc/>
        public object GetOption(string qualifiedName)
        {
            options.TryGetValue(qualifiedName, out var obj);
            return obj;
        }

        ///<inheritdoc/>
        public void SetOption(string qualifiedName, object val)
        {
            options[qualifiedName] = val;
        }

        ///<inheritdoc/>
        public ICollection<string> OptionNames => options.Keys;

        ///<inheritdoc/>
        public IDictionary<string, object> Options => options;

        #endregion

        #endregion

        #region Public Properties
        public long BackoffIncrement { get; private set; }

        public long MaxBackoff { get; private set; }



        public bool Disconnected => IsDisconnected(bayeuxClientState);


        #endregion

        #region Cookie
        public string GetCookie(string name)
        {
            var cookie = cookieCollection[name];
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
            cookieCollection.Add(cookie);
        }
        #endregion

        #region ITransportListener
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
            //if (!(e is WebException) || ((WebException)e).Status != WebExceptionStatus.Timeout)
            //{
            // the normal flow of cometd long polling is to timeout after the configured timeout value
            // and then reconnect again, so ignore those
            Console.WriteLine("{0}", e);
            //}
        }

        #endregion

        #region Handshake
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
            var bayeuxClientState = this.bayeuxClientState;

            if (IsHandshaking(bayeuxClientState))
            {
                var message = NewMessage();
                if (bayeuxClientState.handshakeFields != null)
                {
                    foreach (var kvp in bayeuxClientState.handshakeFields)
                    {
                        message.Add(kvp.Key, kvp.Value);
                    }
                }

                message.Channel = ChannelFields.META_HANDSHAKE;
                message[MessageFields.SUPPORTED_CONNECTION_TYPES_FIELD] = AllowedTransports;
                message[MessageFields.VERSION_FIELD] = BayeuxClient.BAYEUX_VERSION;
                if (message.Id == null)
                {
                    message.Id = NewMessageId();
                }

                //Console.WriteLine("Handshaking with extra fields {0}, transport {1}", Print.Dictionary(bayeuxClientState.handshakeFields), Print.Dictionary(bayeuxClientState.transport as IDictionary<string, object>));
                bayeuxClientState.Send(handshakeListener, message);
                return true;
            }
            return false;
        }

        private bool IsHandshaking(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.type == State.HANDSHAKING || bayeuxClientState.type == State.REHANDSHAKING;
        }

        private bool IsHandshook(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.type == State.CONNECTING || bayeuxClientState.type == State.CONNECTED || bayeuxClientState.type == State.UNCONNECTED;
        }

        protected void ProcessHandshake(IMutableMessage handshake)
        {
            if (handshake.Successful)
            {
                var serverTransportObject = handshake[MessageFields.SUPPORTED_CONNECTION_TYPES_FIELD] as JArray;
                var serverTransports = serverTransportObject as IEnumerable<object>;

                var negotiatedTransports = transportRegistry.Negotiate(serverTransports, BAYEUX_VERSION);
                var newTransport = negotiatedTransports.Count == 0 ? null : negotiatedTransports[0];
                if (newTransport == null)
                {
                    UpdateBayeuxClientState(
                            delegate (BayeuxClientState oldState)
                            {
                                return new DisconnectedState(this, oldState.transport);
                            },
                            delegate ()
                            {
                                Receive(handshake);
                            });

                    // Signal the failure
                    var error = "405:c" + transportRegistry.AllowedTransports + ",s" + serverTransports.ToString() + ":no transport";

                    handshake.Successful = false;
                    handshake[MessageFields.ERROR_FIELD] = error;
                    // TODO: also update the advice with reconnect=none for listeners ?
                }
                else
                {
                    UpdateBayeuxClientState(
                            delegate (BayeuxClientState oldState)
                            {
                                if (newTransport != oldState.transport)
                                {
                                    oldState.transport.Reset();
                                    newTransport.Init();
                                }

                                var action = GetAdviceAction(handshake.Advice, MessageFields.RECONNECT_RETRY_VALUE);
                                if (MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                                {
                                    return new ConnectingState(this, oldState.handshakeFields, handshake.Advice, newTransport, handshake.ClientId);
                                }
                                else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                                {
                                    return new DisconnectedState(this, oldState.transport);
                                }

                                return null;
                            },
                            delegate ()
                            {
                                Receive(handshake);
                            });
                }
            }
            else
            {
                UpdateBayeuxClientState(
                        delegate (BayeuxClientState oldState)
                        {
                            var action = GetAdviceAction(handshake.Advice, MessageFields.RECONNECT_HANDSHAKE_VALUE);
                            if (MessageFields.RECONNECT_HANDSHAKE_VALUE.Equals(action) || MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                            {
                                return new RehandshakingState(this, oldState.handshakeFields, oldState.transport, oldState.NextBackoff());
                            }
                            else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                            {
                                return new DisconnectedState(this, oldState.transport);
                            }

                            return null;
                        },
                        delegate ()
                        {
                            Receive(handshake);
                        });
            }
        }

        protected bool ScheduleHandshake(long interval, long backoff)
        {
            return ScheduleAction(
                    delegate (object sender, ElapsedEventArgs e)
                    {
                        SendHandshake();
                    }
                    , interval, backoff);
        }

        #endregion

        protected State CurrentState => bayeuxClientState.type;
        
        public State WaitFor(int waitMs, ICollection<State> states)
        {
            var stop = DateTime.Now.AddMilliseconds(waitMs);
            var duration = waitMs;

            var s = CurrentState;
            if (states.Contains(s))
            {
                return s;
            }

            while (stateChanged.WaitOne(duration))
            {
                if (stateUpdateInProgress == 0)
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

        protected bool SendConnect()
        {
            var bayeuxClientState = this.bayeuxClientState;
            if (IsHandshook(bayeuxClientState))
            {
                var message = NewMessage();
                message.Channel = ChannelFields.META_CONNECT;
                message[MessageFields.CONNECTION_TYPE_FIELD] = bayeuxClientState.transport.Name;
                if (bayeuxClientState.type == State.CONNECTING || bayeuxClientState.type == State.UNCONNECTED)
                {
                    // First connect after handshake or after failure, add advice
                    message.GetAdvice(true)["timeout"] = 0;
                }
                bayeuxClientState.Send(connectListener, message);
                return true;
            }
            return false;
        }
        
        protected bool SendMessages(IList<IMutableMessage> messages)
        {
            var bayeuxClientState = this.bayeuxClientState;
            if (bayeuxClientState.type == State.CONNECTING || IsConnected(bayeuxClientState))
            {
                bayeuxClientState.Send(publishListener, messages);
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
                var value = messageQueue.Count;

                var state = bayeuxClientState;
                if (state.transport is ClientTransport clientTransport)
                {
                    value += clientTransport.IsSending ? 1 : 0;
                }

                return value;
            }
        }

        /// <summary>
        /// Wait for send queue to be emptied
        /// </summary>
        /// <param name="timeoutMS"></param>
        /// <returns>true if queue is empty, false if timed out</returns>
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
            UpdateBayeuxClientState(
                    delegate (BayeuxClientState oldState)
                    {
                        return new AbortedState(this, oldState.transport);
                    });
        }

        private IList<IMutableMessage> TakeMessages()
        {
            IList<IMutableMessage> queue = new List<IMutableMessage>(messageQueue);
            messageQueue.Clear();
            return queue;
        }

        private bool IsConnected(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.type == State.CONNECTED;
        }
        
        private bool IsDisconnected(BayeuxClientState bayeuxClientState)
        {
            return bayeuxClientState.type == State.DISCONNECTING || bayeuxClientState.type == State.DISCONNECTED;
        }
        
        protected void ProcessConnect(IMutableMessage connect)
        {
            UpdateBayeuxClientState(
                    delegate (BayeuxClientState oldState)
                    {
                        var advice = connect.Advice;
                        if (advice == null)
                        {
                            advice = oldState.advice;
                        }

                        var action = GetAdviceAction(advice, MessageFields.RECONNECT_RETRY_VALUE);
                        if (connect.Successful)
                        {
                            if (MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                            {
                                return new ConnectedState(this, oldState.handshakeFields, advice, oldState.transport, oldState.clientId);
                            }
                            else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                            {
                                // This case happens when the connect reply arrives after a disconnect
                                // We do not go into a disconnected state to allow normal processing of the disconnect reply
                                return new DisconnectingState(this, oldState.transport, oldState.clientId);
                            }
                        }
                        else
                        {
                            if (MessageFields.RECONNECT_HANDSHAKE_VALUE.Equals(action))
                            {
                                return new RehandshakingState(this, oldState.handshakeFields, oldState.transport, 0);
                            }
                            else if (MessageFields.RECONNECT_RETRY_VALUE.Equals(action))
                            {
                                return new UnconnectedState(this, oldState.handshakeFields, advice, oldState.transport, oldState.clientId, oldState.NextBackoff());
                            }
                            else if (MessageFields.RECONNECT_NONE_VALUE.Equals(action))
                            {
                                return new DisconnectedState(this, oldState.transport);
                            }
                        }

                        return null;
                    },
                delegate ()
                {
                    Receive(connect);
                });
        }

        protected void ProcessDisconnect(IMutableMessage disconnect)
        {
            UpdateBayeuxClientState(
                    delegate (BayeuxClientState oldState)
                    {
                        return new DisconnectedState(this, oldState.transport);
                    },
                    delegate ()
                    {
                        Receive(disconnect);
                    });
        }

        protected void ProcessMessage(IMutableMessage message)
        {
            // logger.debug("Processing message {}", message);
            Receive(message);
        }

        private string GetAdviceAction(IDictionary<string, object> advice, string defaultResult)
        {
            var action = defaultResult;
            if (advice != null && advice.ContainsKey(MessageFields.RECONNECT_FIELD))
            {
                action = ((string)advice[MessageFields.RECONNECT_FIELD]);
            }

            return action;
        }
        
        protected bool ScheduleConnect(long interval, long backoff)
        {
            return ScheduleAction(
                    delegate (object sender, ElapsedEventArgs e)
                    {
                        SendConnect();
                    }
                    , interval, backoff);
        }

        private bool ScheduleAction(ElapsedEventHandler action, long interval, long backoff)
        {
            var timer = new System.Timers.Timer(); // @@ax: What about support for multiple timers?
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
            var backoffIncrement = ObjectConverter.ToInt64(GetOption(BACKOFF_INCREMENT_OPTION), 1000L);
            BackoffIncrement = backoffIncrement;

            var maxBackoff = ObjectConverter.ToInt64(GetOption(MAX_BACKOFF_OPTION), 30000L);
            MaxBackoff = maxBackoff;
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
                //Console.WriteLine("{0} message {1}", sent?"Sent":"Failed", message);
            }
            else
            {
                messageQueue.Enqueue(message);
                //Console.WriteLine("Enqueued message {0} (batching: {1})", message, this.Batching);
            }
        }

        private bool CanSend()
        {
            return !IsDisconnected(bayeuxClientState) && !Batching && !IsHandshaking(bayeuxClientState);
        }
        
        private void UpdateBayeuxClientState(BayeuxClientStateUpdater_createDelegate create)
        {
            UpdateBayeuxClientState(create, null);
        }

        private void UpdateBayeuxClientState(BayeuxClientStateUpdater_createDelegate create, BayeuxClientStateUpdater_postCreateDelegate postCreate)
        {
            stateUpdateInProgressMutex.WaitOne();
            ++stateUpdateInProgress;
            stateUpdateInProgressMutex.ReleaseMutex();

            BayeuxClientState newState = null;
            var oldState = bayeuxClientState;

            newState = create(oldState);
            if (newState == null)
            {
                throw new SystemException();
            }

            if (!oldState.IsUpdateableTo(newState))
            {
                //Console.WriteLine("State not updateable : {0} -> {1}", oldState, newState);
                return;
            }

            bayeuxClientState = newState;

            postCreate?.Invoke();

            if (oldState.Type != newState.Type)
            {
                newState.Enter(oldState.Type);
            }

            newState.Execute();

            // Notify threads waiting in waitFor()
            stateUpdateInProgressMutex.WaitOne();
            --stateUpdateInProgress;

            if (stateUpdateInProgress == 0)
            {
                stateChanged.Set();
            }

            stateUpdateInProgressMutex.ReleaseMutex();
        }
        
        public enum State
        {
            INVALID, UNCONNECTED, HANDSHAKING, REHANDSHAKING, CONNECTING, CONNECTED, DISCONNECTING, DISCONNECTED
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
                bayeuxClient.UpdateBayeuxClientState(
                        delegate (BayeuxClientState oldState)
                        {
                            return new RehandshakingState(bayeuxClient, oldState.handshakeFields, oldState.transport, oldState.NextBackoff());
                        });
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
                        delegate (BayeuxClientState oldState)
                        {
                            return new UnconnectedState(bayeuxClient, oldState.handshakeFields, oldState.advice, oldState.transport, oldState.clientId, oldState.NextBackoff());
                        });
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
                        delegate (BayeuxClientState oldState)
                        {
                            return new DisconnectedState(bayeuxClient, oldState.transport);
                        });
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

        abstract public class BayeuxClientState
        {
            public State type;
            public IDictionary<string, object> handshakeFields;
            public IDictionary<string, object> advice;
            public ClientTransport transport;
            public string clientId;
            public long backoff;
            protected BayeuxClient bayeuxClient;

            public BayeuxClientState(BayeuxClient bayeuxClient, State type, IDictionary<string, object> handshakeFields,
                    IDictionary<string, object> advice, ClientTransport transport, string clientId, long backoff)
            {
                this.bayeuxClient = bayeuxClient;
                this.type = type;
                this.handshakeFields = handshakeFields;
                this.advice = advice;
                this.transport = transport;
                this.clientId = clientId;
                this.backoff = backoff;
            }

            public long Interval
            {
                get
                {
                    long result = 0;
                    if (advice != null && advice.ContainsKey(MessageFields.INTERVAL_FIELD))
                    {
                        result = ObjectConverter.ToInt64(advice[MessageFields.INTERVAL_FIELD], result);
                    }

                    return result;
                }
            }

            public void Send(ITransportListener listener, IMutableMessage message)
            {
                IList<IMutableMessage> messages = new List<IMutableMessage>
                {
                    message
                };
                Send(listener, messages);
            }

            public void Send(ITransportListener listener, IList<IMutableMessage> messages)
            {
                foreach (var message in messages)
                {
                    if (message.Id == null)
                    {
                        message.Id = bayeuxClient.NewMessageId();
                    }

                    if (clientId != null)
                    {
                        message.ClientId = clientId;
                    }

                    if (!bayeuxClient.ExtendSend(message))
                    {
                        messages.Remove(message);
                    }
                }
                if (messages.Count > 0)
                {
                    transport.Send(listener, messages);
                }
            }

            public long NextBackoff()
            {
                return Math.Min(backoff + bayeuxClient.BackoffIncrement, bayeuxClient.MaxBackoff);
            }

            public abstract bool IsUpdateableTo(BayeuxClientState newState);

            public virtual void Enter(State oldState)
            {
            }

            public abstract void Execute();

            public State Type => type;

            public override string ToString()
            {
                return type.ToString();
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
                return newState.type == State.HANDSHAKING;
            }

            public override void Execute()
            {
                transport.Reset();
                bayeuxClient.Terminate();
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
                transport.Abort();
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
                return newState.type == State.REHANDSHAKING ||
                    newState.type == State.CONNECTING ||
                    newState.type == State.DISCONNECTED;
            }

            public override void Enter(State oldState)
            {
                // Always reset the subscriptions when a handshake has been requested.
                bayeuxClient.ResetSubscriptions();
            }

            public override void Execute()
            {
                // The state could change between now and when sendHandshake() runs;
                // in this case the handshake message will not be sent and will not
                // be failed, because most probably the client has been disconnected.
                bayeuxClient.SendHandshake();
            }
        }

        private class RehandshakingState : BayeuxClientState
        {
            public RehandshakingState(BayeuxClient bayeuxClient, IDictionary<string, object> handshakeFields, ClientTransport transport, long backoff)
                : base(bayeuxClient, State.REHANDSHAKING, handshakeFields, null, transport, null, backoff)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.type == State.CONNECTING ||
                    newState.type == State.REHANDSHAKING ||
                    newState.type == State.DISCONNECTED;
            }

            public override void Enter(State oldState)
            {
                // Reset the subscriptions if this is not a failure from a requested handshake.
                // Subscriptions may be queued after requested handshakes.
                if (oldState != State.HANDSHAKING)
                {
                    // Reset subscriptions if not queued after initial handshake
                    bayeuxClient.ResetSubscriptions();
                }
            }

            public override void Execute()
            {
                bayeuxClient.ScheduleHandshake(Interval, backoff);
            }
        }

        private class ConnectingState : BayeuxClientState
        {
            public ConnectingState(BayeuxClient bayeuxClient, IDictionary<string, object> handshakeFields, IDictionary<string, object> advice, ClientTransport transport, string clientId)
                : base(bayeuxClient, State.CONNECTING, handshakeFields, advice, transport, clientId, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.type == State.CONNECTED ||
                    newState.type == State.UNCONNECTED ||
                    newState.type == State.REHANDSHAKING ||
                    newState.type == State.DISCONNECTING ||
                    newState.type == State.DISCONNECTED;
            }

            public override void Execute()
            {
                // Send the messages that may have queued up before the handshake completed
                bayeuxClient.SendBatch();
                bayeuxClient.ScheduleConnect(Interval, backoff);
            }
        }

        private class ConnectedState : BayeuxClientState
        {
            public ConnectedState(BayeuxClient bayeuxClient, IDictionary<string, object> handshakeFields, IDictionary<string, object> advice, ClientTransport transport, string clientId)
                : base(bayeuxClient, State.CONNECTED, handshakeFields, advice, transport, clientId, 0)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.type == State.CONNECTED ||
                    newState.type == State.UNCONNECTED ||
                    newState.type == State.REHANDSHAKING ||
                    newState.type == State.DISCONNECTING ||
                    newState.type == State.DISCONNECTED;
            }

            public override void Execute()
            {
                bayeuxClient.ScheduleConnect(Interval, backoff);
            }
        }

        private class UnconnectedState : BayeuxClientState
        {
            public UnconnectedState(BayeuxClient bayeuxClient, IDictionary<string, object> handshakeFields, IDictionary<string, object> advice, ClientTransport transport, string clientId, long backoff)
                : base(bayeuxClient, State.UNCONNECTED, handshakeFields, advice, transport, clientId, backoff)
            {
            }

            public override bool IsUpdateableTo(BayeuxClientState newState)
            {
                return newState.type == State.CONNECTED ||
                    newState.type == State.UNCONNECTED ||
                    newState.type == State.REHANDSHAKING ||
                    newState.type == State.DISCONNECTED;
            }

            public override void Execute()
            {
                bayeuxClient.ScheduleConnect(Interval, backoff);
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
                return newState.type == State.DISCONNECTED;
            }

            public override void Execute()
            {
                var message = bayeuxClient.NewMessage();
                message.Channel = ChannelFields.META_DISCONNECT;
                Send(bayeuxClient.disconnectListener, message);
            }
        }
    }
}
