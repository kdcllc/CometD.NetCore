namespace CometD.NetCore.Bayeux.Client
{
    /// <summary> <p>A client side channel representation.</p>
    /// <p>A {@link ClientSessionChannel} is scoped to a particular {@link ClientSession}
    /// that is obtained by a call to {@link ClientSession#getChannel(String)}.</p>
    /// <p>Typical usage examples are:</p>
    /// <pre>
    /// clientSession.getChannel("/foo/bar").subscribe(mySubscriptionListener);
    /// clientSession.getChannel("/foo/bar").publish("Hello");
    /// clientSession.getChannel("/meta/*").addListener(myMetaChannelListener);
    /// </pre>
    ///
    /// </summary>
    /// <version>  $Revision: 1295 $ $Date: 2010-06-18 11:57:20 +0200 (Fri, 18 Jun 2010) $
    /// </version>
    public interface IClientSessionChannel : IChannel
    {
        /// <returns> the client session associated with this channel
        /// </returns>
        IClientSession Session { get; }

        /// <param name="listener">the listener to add
        /// </param>
        void AddListener(IClientSessionChannelListener listener);

        /// <summary> Equivalent to {@link #publish(Object, Object) publish(data, null)}.</summary>
        /// <param name="data">the data to publish
        /// </param>
        void Publish(object data);

        /// <summary> Publishes the given {@code data} to this channel,
        /// optionally specifying the {@code messageId} to set on the
        /// publish message.
        /// </summary>
        /// <param name="data">the data to publish
        /// </param>
        /// <param name="messageId">the message id to set on the message, or null to let the
        /// implementation choose the message id.
        /// </param>
        /// <seealso cref="IMessage.getId()">
        /// </seealso>
        void Publish(object data, string messageId);

        /// <param name="listener">the listener to remove
        /// </param>
        void RemoveListener(IClientSessionChannelListener listener);

        void Subscribe(IMessageListener listener);

        void Unsubscribe(IMessageListener listener);

        void Unsubscribe();
    }

    /// <summary> <p>Represents a listener on a {@link ClientSessionChannel}.</p>
    /// <p>Sub-interfaces specify the exact semantic of the listener.</p>
    /// </summary>
    public interface IClientSessionChannelListener : IBayeuxListener
    {
    }
}
