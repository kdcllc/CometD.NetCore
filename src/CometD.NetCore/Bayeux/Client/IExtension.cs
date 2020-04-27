namespace CometD.NetCore.Bayeux.Client
{
    /// <summary> <p>Extension API for client session.</p>
    /// <p>An extension allows user code to interact with the Bayeux protocol as late
    /// as messages are sent or as soon as messages are received.</p>
    /// <p>Messages may be modified, or state held, so that the extension adds a
    /// specific behavior simply by observing the flow of Bayeux messages.</p>
    ///
    /// </summary>
    /// <seealso cref="IClientSession.AddExtension(IExtension)">
    /// </seealso>
    public interface IExtension
    {
        /// <summary> Callback method invoked every time a normal message is received.</summary>
        /// <param name="session">the session object that is receiving the message.
        /// </param>
        /// <param name="message">the message received.
        /// </param>
        /// <returns> true if message processing should continue, false if it should stop.
        /// </returns>
        bool Receive(IClientSession session, IMutableMessage message);

        /// <summary> Callback method invoked every time a meta message is received.</summary>
        /// <param name="session">the session object that is receiving the meta message.
        /// </param>
        /// <param name="message">the meta message received.
        /// </param>
        /// <returns> true if message processing should continue, false if it should stop.
        /// </returns>
        bool ReceiveMeta(IClientSession session, IMutableMessage message);

        /// <summary> Callback method invoked every time a normal message is being sent.</summary>
        /// <param name="session">the session object that is sending the message.
        /// </param>
        /// <param name="message">the message being sent.
        /// </param>
        /// <returns> true if message processing should continue, false if it should stop.
        /// </returns>
        bool Send(IClientSession session, IMutableMessage message);

        /// <summary> Callback method invoked every time a meta message is being sent.</summary>
        /// <param name="session">the session object that is sending the message.
        /// </param>
        /// <param name="message">the meta message being sent.
        /// </param>
        /// <returns> true if message processing should continue, false if it should stop.
        /// </returns>
        bool SendMeta(IClientSession session, IMutableMessage message);
    }
}
