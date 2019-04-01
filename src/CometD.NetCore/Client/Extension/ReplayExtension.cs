using System.Collections.Generic;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;

namespace CometD.NetCore.Client.Extension
{
    public class ReplayExtension : IExtension
    {
        private const string EXTENSION_NAME = "replay";

        private volatile bool _serverSupportsReplay = false;

        public bool Receive(IClientSession session, IMutableMessage message)
        {
            // can retrieve actual replay ids for messages here if needed.
            //var ext = (Dictionary<string, object>)message.GetExt(false);
            //var e = ext[Message_Fields.EVENT_FIELD];

            return true;
        }

        public bool ReceiveMeta(IClientSession session, IMutableMessage message)
        {
            if (ChannelFields.META_HANDSHAKE.Equals(message.Channel))
            {
                var ext = (Dictionary<string, object>)message.GetExt(false);
                _serverSupportsReplay = ext != null && true.Equals(ext[EXTENSION_NAME]);
            }
           
            return true;
        }

        public bool Send(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool SendMeta(IClientSession session, IMutableMessage message)
        {
            if (ChannelFields.META_SUBSCRIBE.Equals(message.Channel) ||
                ChannelFields.META_UNSUBSCRIBE.Equals(message.Channel))
            {
                var value = new Dictionary<string, object>
                {
                    { message.Subscription, message.ReplayId }
                };

                message.GetExt(true)[EXTENSION_NAME] = value;
            }
            return true;
        }
    }
}
