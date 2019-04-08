using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CometD.NetCore.Bayeux;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CometD.NetCore.Common
{
    [Serializable]
    public class DictionaryMessage : Dictionary<string, object>, IMutableMessage
    {
        private const long SerialVersionUID = 4318697940670212190L;

        public DictionaryMessage()
        {
        }

        public DictionaryMessage(IDictionary<string, object> message)
        {
            foreach (var kvp in message)
            {
                Add(kvp.Key, kvp.Value);
            }
        }

        public IDictionary<string, object> Advice
        {
            get
            {
                TryGetValue(MessageFields.ADVICE_FIELD, out var advice);
                if (advice is JObject)
                {
                    advice = JsonConvert.DeserializeObject<IDictionary<string, object>>(advice.ToString());
                    this[MessageFields.ADVICE_FIELD] = advice;
                }
                return (IDictionary<string, object>)advice;
            }
        }

        public string Channel
        {
            get
            {
                TryGetValue(MessageFields.CHANNEL_FIELD, out var obj);
                return (string)obj;
            }
            set => this[MessageFields.CHANNEL_FIELD] = value;
        }

        public string Subscription
        {
            get
            {
                TryGetValue(MessageFields.SUBSCRIPTION_FIELD, out var obj);
                return (string)obj;
            }
            set => this[MessageFields.SUBSCRIPTION_FIELD] = value;
        }

        public long ReplayId
        {
            get
            {
                TryGetValue(MessageFields.REPLAY_ID_FIELD, out var obj);
                return (long)obj;
            }
            set => this[MessageFields.REPLAY_ID_FIELD] = value;
        }

        public ChannelId ChannelId => new ChannelId(Channel);

        public string ClientId
        {
            get
            {
                TryGetValue(MessageFields.CLIENT_ID_FIELD, out var obj);
                return (string)obj;
            }
            set => this[MessageFields.CLIENT_ID_FIELD] = value;
        }

        public object Data
        {
            get
            {
                TryGetValue(MessageFields.DATA_FIELD, out var obj);
                return obj;
            }
            set => this[MessageFields.DATA_FIELD] = value;
        }

        public IDictionary<string, object> DataAsDictionary
        {
            get
            {
                TryGetValue(MessageFields.DATA_FIELD, out var data);
                if (data is string)
                {
                    data = JsonConvert.DeserializeObject<Dictionary<string, object>>(data as string);
                    this[MessageFields.DATA_FIELD] = data;
                }
                return (Dictionary<string, object>)data;
            }
        }

        public IDictionary<string, object> Ext
        {
            get
            {
                TryGetValue(MessageFields.EXT_FIELD, out var ext);
                if (ext is string)
                {
                    ext = JsonConvert.DeserializeObject<Dictionary<string, object>>(ext as string);
                    this[MessageFields.EXT_FIELD] = ext;
                }
                if (ext is JObject)
                {
                    ext = JsonConvert.DeserializeObject<Dictionary<string, object>>(ext.ToString());
                    this[MessageFields.EXT_FIELD] = ext;
                }
                return (Dictionary<string, object>)ext;
            }
        }

        public string Id
        {
            get
            {
                TryGetValue(MessageFields.ID_FIELD, out var obj);
                return (string)obj;
            }
            set => this[MessageFields.ID_FIELD] = value;
        }

        public string Json => JsonConvert.SerializeObject(this as IDictionary<string, object>);

        public bool Meta => ChannelId.IsMeta(Channel);

        public bool Successful
        {
            get
            {
                TryGetValue(MessageFields.SUCCESSFUL_FIELD, out var obj);
                return ObjectConverter.ToBoolean(obj, false);
            }
            set => this[MessageFields.SUCCESSFUL_FIELD] = value;
        }

        public static IList<IMutableMessage> ParseMessages(string content)
        {
            // Will throw when unable to parse -- letting the consumer decide what to do about that.
            var dictionaryList = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(content);

            var messages = new List<IMutableMessage>();

            if (dictionaryList == null)
            {
                return messages;
            }

            foreach (var message in dictionaryList)
            {
                if (message != null)
                {
                    messages.Add(new DictionaryMessage(message));
                }
            }

            return messages;
        }

        public IDictionary<string, object> GetAdvice(bool create)
        {
            var advice = Advice;
            if (create && advice == null)
            {
                advice = new Dictionary<string, object>();
                this[MessageFields.ADVICE_FIELD] = advice;
            }
            return advice;
        }

        public IDictionary<string, object> GetDataAsDictionary(bool create)
        {
            var data = DataAsDictionary;
            if (create && data == null)
            {
                data = new Dictionary<string, object>();
                this[MessageFields.DATA_FIELD] = data;
            }
            return data;
        }

        public IDictionary<string, object> GetExt(bool create)
        {
            var ext = Ext;
            if (create && ext == null)
            {
                ext = new Dictionary<string, object>();
                this[MessageFields.EXT_FIELD] = ext;
            }
            return ext;
        }

        public override string ToString()
        {
            return Json;
        }

        protected DictionaryMessage(SerializationInfo serializationInfo,
            StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }
}
