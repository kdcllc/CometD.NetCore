using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;

using Microsoft.Extensions.Logging;

namespace CometD.NetCore.Common
{
    /// <summary> <p>A channel scoped to a {@link ClientSession}.</p></summary>
    public abstract class AbstractSessionChannel : IClientSessionChannel
    {
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, object> _attributes = new ConcurrentDictionary<string, object>();
        private readonly List<IClientSessionChannelListener> _listeners = new List<IClientSessionChannelListener>();
        private readonly List<IMessageListener> _subscriptions = new List<IMessageListener>();

        private int _subscriptionCount;

        public long ReplayId { get; private set; }

        protected AbstractSessionChannel(ChannelId id, long replayId)
        {
            ReplayId = replayId;
            ChannelId = id;
        }

        protected AbstractSessionChannel(ChannelId id, long replayId, ILogger logger)
            : this(id, replayId)
        {
            _logger = logger;
        }

        public abstract IClientSession Session { get; }

        public void AddListener(IClientSessionChannelListener listener)
        {
            _listeners.Add(listener);
        }

        public abstract void Publish(object param1);

        public abstract void Publish(object param1, string param2);

        public void RemoveListener(IClientSessionChannelListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Subscribe(IMessageListener listener)
        {
            _subscriptions.Add(listener);

            _subscriptionCount++;
            var count = _subscriptionCount;
            if (count == 1)
            {
                SendSubscribe();
            }
        }

        public void Unsubscribe(IMessageListener listener)
        {
            _subscriptions.Remove(listener);

            _subscriptionCount--;
            if (_subscriptionCount < 0)
            {
                _subscriptionCount = 0;
            }

            var count = _subscriptionCount;
            if (count == 0)
            {
                SendUnSubscribe();
            }
        }

        public void Unsubscribe()
        {
            foreach (var listener in new List<IMessageListener>(_subscriptions))
            {
                Unsubscribe(listener);
            }
        }

        public ICollection<string> AttributeNames => _attributes.Keys;

        public ChannelId ChannelId { get; }

        public bool DeepWild => ChannelId.DeepWild;

        public string Id => ChannelId.ToString();

        public bool Meta => ChannelId.IsMeta();

        public bool Service => ChannelId.IsService();

        public bool Wild => ChannelId.Wild;

        public object GetAttribute(string name)
        {
            _attributes.TryGetValue(name, out var obj);
            return obj;
        }

        public object RemoveAttribute(string name)
        {
            var old = GetAttribute(name);

            if (_attributes.TryRemove(name, out var va))
            {
                return va;
            }

            return old;
        }

        public void SetAttribute(string name, object val)
        {
            _attributes[name] = val;
        }

        public void NotifyMessageListeners(IMessage message)
        {
            foreach (var listener in _listeners)
            {
                if (listener is IMessageListener)
                {
                    try
                    {
                        ((IMessageListener)listener).OnMessage(this, message);
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError($"{e}");
                    }
                }
            }

            var list = new List<IMessageListener>(_subscriptions);
            foreach (IClientSessionChannelListener listener in list)
            {
                if (listener is IMessageListener)
                {
                    if (message.Data != null)
                    {
                        try
                        {
                            ((IMessageListener)listener).OnMessage(this, message);
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError($"{e}");
                        }
                    }
                }
            }
        }

        public void ResetSubscriptions()
        {
            foreach (var listener in new List<IMessageListener>(_subscriptions))
            {
                _subscriptions.Remove(listener);
                _subscriptionCount--;
            }
        }

        public override string ToString()
        {
            return ChannelId.ToString();
        }

        protected abstract void SendSubscribe();

        protected abstract void SendUnSubscribe();
    }
}
