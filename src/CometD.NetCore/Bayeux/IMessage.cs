using System.Collections.Generic;

namespace CometD.NetCore.Bayeux
{
    public interface IMessage : IDictionary<string, object>
    {
        /// <summary> Convenience method to retrieve the {@link #ADVICE_FIELD}</summary>
        /// <returns> the advice of the message
        /// </returns>
        IDictionary<string, object> Advice { get; }

        /// <summary> Convenience method to retrieve the {@link #CHANNEL_FIELD}.
        /// Bayeux message always have a non null channel.
        /// </summary>
        /// <returns> the channel of the message
        /// </returns>
        string Channel { get; }

        /// <summary>
        /// Convenience method to retrieve the {@link #SUBSCRIPTION_FIELD}.
        /// </summary>
        string Subscription { get; }

        long ReplayId { get; }
        /// <summary> Convenience method to retrieve the {@link #CHANNEL_FIELD}.
        /// Bayeux message always have a non null channel.
        /// </summary>
        /// <returns> the channel of the message
        /// </returns>
        ChannelId ChannelId { get; }

        /// <summary> Convenience method to retrieve the {@link #CLIENT_ID_FIELD}</summary>
        /// <returns> the client id of the message
        /// </returns>
        string ClientId { get; }

        /// <summary> Convenience method to retrieve the {@link #DATA_FIELD}</summary>
        /// <returns> the data of the message
        /// </returns>
        /// <seealso cref="getDataAsMap()">
        /// </seealso>
        object Data { get; }

        /// <returns> the data of the message as a map
        /// </returns>
        /// <seealso cref="getData()">
        /// </seealso>
        IDictionary<string, object> DataAsDictionary { get; }

        /// <summary> Convenience method to retrieve the {@link #EXT_FIELD}</summary>
        /// <returns> the ext of the message
        /// </returns>
        IDictionary<string, object> Ext { get; }

        /// <summary> Convenience method to retrieve the {@link #ID_FIELD}</summary>
        /// <returns> the id of the message
        /// </returns>
        string Id { get; }

        /// <returns> this message as a JSON string
        /// </returns>
        string Json { get; }

        /// <summary> A messages that has a meta channel is dubbed a "meta message".</summary>
        /// <returns> whether the channel's message is a meta channel
        /// </returns>
        bool Meta { get; }

        /// <summary> Convenience method to retrieve the {@link #SUCCESSFUL_FIELD}</summary>
        /// <returns> whether the message is successful
        /// </returns>
        bool Successful { get; }
    }
}
