namespace CometD.NetCore.Bayeux
{
    public class ChannelFields
    {
        public const string META = "/meta";
        public const string META_CONNECT = META + "/connect";
        public const string META_DISCONNECT = META + "/disconnect";
        public const string META_HANDSHAKE = META + "/handshake";
        public const string META_SUBSCRIBE = META + "/subscribe";
        public const string META_UNSUBSCRIBE = META + "/unsubscribe";
    }
}
