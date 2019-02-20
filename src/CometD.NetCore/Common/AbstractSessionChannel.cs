using System;
using System.Collections.Generic;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;

namespace CometD.NetCore.Common
{
    /// <summary> <p>A channel scoped to a {@link ClientSession}.</p></summary>
    public abstract class AbstractSessionChannel : IClientSessionChannel
    {
        private Dictionary<string, object> _attributes = new Dictionary<string, object>();
        private List<IClientSessionChannelListener> _listeners = new List<IClientSessionChannelListener>();
        private int _subscriptionCount = 0;
        private List<IMessageListener> _subscriptions = new List<IMessageListener>();

        public long ReplayId { get; private set; }
        protected AbstractSessionChannel(ChannelId id, long replayId)
        {
            ReplayId = replayId;
            ChannelId = id;
        }

        #region IClientSessionChannel
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


        #endregion

        #region IChannel

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
            _attributes.Remove(name);
            return old;
        }

        public void SetAttribute(string name, object val)
        {
            _attributes[name] = val;
        }

        #endregion
        
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
                    catch (Exception x)
                    {
                        Console.WriteLine("{0}", x);
                        //logger.info(x);
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
                        catch (System.Exception x)
                        {
                            Console.WriteLine("{0}", x);
                            //logger.info(x);
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
