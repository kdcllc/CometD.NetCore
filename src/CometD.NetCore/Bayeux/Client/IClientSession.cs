using System.Collections.Generic;

namespace CometD.NetCore.Bayeux.Client
{
    /// <summary> <p>This interface represents the client side Bayeux session.</p>
    /// <p>In addition to the {@link Session common Bayeux session}, this
    /// interface provides method to configure extension, access channels
    /// and to initiate the communication with a Bayeux server(s).</p>
    ///
    /// </summary>
    public interface IClientSession : ISession
    {
        /// <summary> Adds an extension to this session.</summary>
        /// <param name="extension">the extension to add.
        /// </param>
        /// <seealso cref="RemoveExtension(IExtension)">
        /// </seealso>
        void AddExtension(IExtension extension);

        /// <summary> Removes an extension from this session.</summary>
        /// <param name="extension">the extension to remove.
        /// </param>
        /// <seealso cref="AddExtension(IExtension)">
        /// </seealso>
        void RemoveExtension(IExtension extension);

        /// <summary> <p>Equivalent to {@link #handshake(Map) handshake(null)}.</p></summary>
        void Handshake();

        /// <summary> <p>Initiates the bayeux protocol handshake with the server(s).</p>
        /// <p>The handshake initiated by this method is asynchronous and
        /// does not wait for the handshake response.</p>
        ///
        /// </summary>
        /// <param name="template">additional fields to add to the handshake message.
        /// </param>
        void Handshake(IDictionary<string, object> template);

        /// <summary> <p>Returns a client side channel scoped by this session.</p>
        /// <p>The channel name may be for a specific channel (e.g. "/foo/bar")
        /// or for a wild channel (e.g. "/meta/**" or "/foo/*").</p>
        /// <p>This method will always return a channel, even if the
        /// the channel has not been created on the server side.  The server
        /// side channel is only involved once a publish or subscribe method
        /// is called on the channel returned by this method.</p>
        /// <p>Typical usage examples are:</p>
        /// <pre>
        /// clientSession.getChannel("/foo/bar").subscribe(mySubscriptionListener);
        /// clientSession.getChannel("/foo/bar").publish("Hello");
        /// clientSession.getChannel("/meta/*").addListener(myMetaChannelListener);
        /// </pre>
        /// </summary>
        /// <param name="channelName">specific or wild channel name.
        /// </param>
        /// <returns> a channel scoped by this session.
        /// </returns>
        IClientSessionChannel GetChannel(string channelName);

        IClientSessionChannel GetChannel(string channelName, long replayId);
    }
}
