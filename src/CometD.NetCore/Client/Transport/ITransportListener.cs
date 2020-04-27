using System;
using System.Collections.Generic;

using CometD.NetCore.Bayeux;

namespace CometD.NetCore.Client.Transport
{
    /// <summary>
    /// Represents a listener on a <see cref="ITransport"/>.
    /// </summary>
    public interface ITransportListener
    {
        /// <summary>
        /// Callback method invoked when the given messages have hit the network towards the Bayeux server.
        /// </summary>
        /// <remarks>
        /// The messages may not be modified, and any modification will be useless
        /// because the message have already been sent.
        /// </remarks>
        /// <param name="messages">The messages sent.</param>
        void OnSending(IList<IMessage> messages);

        /// <summary>
        /// Callback method invoke when the given messages have just arrived from the Bayeux server.
        /// </summary>
        /// <param name="messages">The messages arrived.</param>
        void OnMessages(IList<IMutableMessage> messages);

        /// <summary>
        /// Callback method invoked when the given messages have failed to be sent
        /// because of a HTTP connection exception.
        /// </summary>
        /// <param name="ex">The exception that caused the failure.</param>
        /// <param name="messages">The messages being sent.</param>
        void OnConnectException(Exception ex, IList<IMessage> messages);

        /// <summary>
        /// Callback method invoked when the given messages have failed to be sent
        /// because of a Web exception.
        /// </summary>
        /// <param name="ex">The exception that caused the failure.</param>
        /// <param name="messages">The messages being sent.</param>
        void OnException(Exception ex, IList<IMessage> messages);

        /// <summary>
        /// Callback method invoked when the given messages have failed to be sent
        /// because of a HTTP request timeout.
        /// </summary>
        /// <param name="messages">The messages being sent.</param>
        void OnExpire(IList<IMessage> messages);

        /// <summary>
        /// Callback method invoked when the given messages have failed to be sent
        /// because of an unexpected Bayeux server exception was thrown.
        /// </summary>
        /// <param name="info">Bayeux server error message.</param>
        /// <param name="messages">The messages being sent.</param>
        void OnProtocolError(string info, IList<IMessage> messages);
    }
}
