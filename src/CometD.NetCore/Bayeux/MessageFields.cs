namespace CometD.NetCore.Bayeux
{
    /// <summary> <p>The Bayeux protocol exchange information by means of messages.</p>
    /// <p>This interface represents the API of a Bayeux message, and consists
    /// mainly of convenience methods to access the known fields of the message map.</p>
    /// <p>This interface comes in both an immutable and {@link Mutable mutable} versions.<br/>
    /// Mutability may be deeply enforced by an implementation, so that it is not correct
    /// to cast a passed Message, to a Message.Mutable, even if the implementation
    /// allows this.</p>
    ///
    /// </summary>
    public static class MessageFields
    {
        public const string ADVICE_FIELD = "advice";
        public const string CHANNEL_FIELD = "channel";
        public const string CLIENT_ID_FIELD = "clientId";
        public const string CONNECTION_TYPE_FIELD = "connectionType";
        public const string DATA_FIELD = "data";
        public const string EVENT_FIELD = "event";
        public const string REPLAY_ID_FIELD = "replayId";
        public const string ERROR_FIELD = "error";
        public const string EXT_FIELD = "ext";
        public const string ID_FIELD = "id";
        public const string INTERVAL_FIELD = "interval";
        public const string MIN_VERSION_FIELD = "minimumVersion";
        public const string RECONNECT_FIELD = "reconnect";
        public const string RECONNECT_HANDSHAKE_VALUE = "handshake";
        public const string RECONNECT_NONE_VALUE = "none";
        public const string RECONNECT_RETRY_VALUE = "retry";
        public const string SUBSCRIPTION_FIELD = "subscription";
        public const string SUCCESSFUL_FIELD = "successful";
        public const string SUPPORTED_CONNECTION_TYPES_FIELD = "supportedConnectionTypes";
        public const string TIMEOUT_FIELD = "timeout";
        public const string TIMESTAMP_FIELD = "timestamp";
        public const string TRANSPORT_FIELD = "transport";
        public const string VERSION_FIELD = "version";
    }
}
