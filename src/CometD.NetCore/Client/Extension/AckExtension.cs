using System.Collections.Generic;

using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;
using CometD.NetCore.Common;

namespace CometD.NetCore.Client.Extension
{
    /// <summary> AckExtension
    ///
    /// This client-side extension enables the client to acknowledge to the server
    /// the messages that the client has received.
    /// For the acknowledgement to work, the server must be configured with the
    /// correspondent server-side ack extension. If both client and server support
    /// the ack extension, then the ack functionality will take place automatically.
    /// By enabling this extension, all messages arriving from the server will arrive
    /// via the long poll, so the comet communication will be slightly chattier.
    /// The fact that all messages will return via long poll means also that the
    /// messages will arrive with total order, which is not guaranteed if messages
    /// can arrive via both long poll and normal response.
    /// Messages are not acknowledged one by one, but instead a group of messages is
    /// acknowledged when long poll returns.
    ///
    /// </summary>
    public class AckExtension : IExtension
    {
        public const string EXT_FIELD = "ack";

        private volatile bool _serverSupportsAcks = false;
        private volatile int _ackId = -1;

        public ChannelFields Channel_Fields { get; private set; }

        public bool Receive(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool ReceiveMeta(IClientSession session, IMutableMessage message)
        {
            if (ChannelFields.META_HANDSHAKE.Equals(message.Channel))
            {
                var ext = (Dictionary<string, object>)message.GetExt(false);
                _serverSupportsAcks = ext != null && true.Equals(ext[EXT_FIELD]);
            }
            else if (_serverSupportsAcks
                && true.Equals(message[MessageFields.SUCCESSFUL_FIELD])
                && ChannelFields.META_CONNECT.Equals(message.Channel))
            {
                var ext = (Dictionary<string, object>)message.GetExt(false);
                if (ext != null)
                {
                    ext.TryGetValue(EXT_FIELD, out var ack);
                    _ackId = ObjectConverter.ToInt32(ack, _ackId);
                }
            }

            return true;
        }

        public bool Send(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool SendMeta(IClientSession session, IMutableMessage message)
        {
            if (ChannelFields.META_HANDSHAKE.Equals(message.Channel))
            {
                message.GetExt(true)[EXT_FIELD] = true;
                _ackId = -1;
            }
            else if (_serverSupportsAcks && ChannelFields.META_CONNECT.Equals(message.Channel))
            {
                message.GetExt(true)[EXT_FIELD] = _ackId;
            }

            return true;
        }
    }
}
