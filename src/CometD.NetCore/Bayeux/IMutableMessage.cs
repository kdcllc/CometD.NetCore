using System.Collections.Generic;

namespace CometD.NetCore.Bayeux
{
    /// <summary> The mutable version of a {@link Message}.</summary>
    public interface IMutableMessage : IMessage
    {
        /// <summary>
        /// Channel.
        /// </summary>
        /// <param name="channel">the channel of this message.
        /// </param>
        new string Channel { get; set; }

        /// <summary>
        /// Subscription.
        /// </summary>
        /// <param name="subscription">the subscription of this message.
        /// </param>
        new string Subscription { get; set; }

        /// <summary>
        /// ReplayId.
        /// </summary>
        /// <param name="replayid">the replayid of this message.
        /// </param>
        new long ReplayId { get; set; }

        /// <summary>
        /// ClientId.
        /// </summary>
        /// <param name="clientId">the client id of this message.
        /// </param>
        new string ClientId { get; set; }

        /// <summary>
        /// Data.
        /// </summary>
        /// <param name="data">the data of this message.
        /// </param>
        new object Data { get; set; }

        /// <summary>
        /// Id.
        /// </summary>
        /// <param name="id">the id of this message.
        /// </param>
        new string Id { get; set; }

        /// <summary>
        /// The successfulness of this message.
        /// </summary>
        new bool Successful { get; set; }

        /// <summary> Convenience method to retrieve the {@link #ADVICE_FIELD} and create it if it does not exist.</summary>
        /// <param name="create">whether to create the advice field if it does not exist.
        /// </param>
        /// <returns> the advice of the message.
        /// </returns>
        IDictionary<string, object> GetAdvice(bool create);

        /// <summary> Convenience method to retrieve the {@link #DATA_FIELD} and create it if it does not exist.</summary>
        /// <param name="create">whether to create the data field if it does not exist.
        /// </param>
        /// <returns> the data of the message.
        /// </returns>
        IDictionary<string, object> GetDataAsDictionary(bool create);

        /// <summary> Convenience method to retrieve the {@link #EXT_FIELD} and create it if it does not exist.</summary>
        /// <param name="create">whether to create the ext field if it does not exist.
        /// </param>
        /// <returns> the ext of the message.
        /// </returns>
        IDictionary<string, object> GetExt(bool create);
    }
}
