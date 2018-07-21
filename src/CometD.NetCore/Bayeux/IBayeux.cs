using System.Collections.Generic;

namespace CometD.NetCore.Bayeux
{
    /// <summary> <p>The {@link Bayeux} interface is the common API for both client-side and
    /// server-side configuration and usage of the Bayeux object.</p>
    /// <p>The {@link Bayeux} object handles configuration options and a set of
    /// transports that is negotiated with the server.</p>
    /// </summary>
    /// <seealso cref="ITransport">
    /// </seealso>
    public interface IBayeux
    {
        /// <returns> the set of known transport names of this {@link Bayeux} object.
        /// </returns>
        /// <seealso cref="getAllowedTransports()">
        /// </seealso>
        ICollection<string> KnownTransportNames { get; }

        /// <param name="transport">the transport name
        /// </param>
        /// <returns> the transport with the given name or null
        /// if no such transport exist
        /// </returns>
        ITransport GetTransport(string transport);

        /// <returns> the ordered list of transport names that will be used in the
        /// negotiation of transports with the other peer.
        /// </returns>
        /// <seealso cref="getKnownTransportNames()">
        /// </seealso>
        IList<string> AllowedTransports { get; }

        /// <param name="qualifiedName">the configuration option name
        /// </param>
        /// <returns> the configuration option with the given {@code qualifiedName}
        /// </returns>
        /// <seealso cref="SetOption(string, object)">
        /// </seealso>
        /// <seealso cref="getOptionNames()">
        /// </seealso>
        object GetOption(string qualifiedName);

        /// <param name="qualifiedName">the configuration option name
        /// </param>
        /// <param name="value">the configuration option value
        /// </param>
        /// <seealso cref="GetOption(string)">
        /// </seealso>
        void SetOption(string qualifiedName, object value);

        /// <returns> the set of configuration options
        /// </returns>
        /// <seealso cref="GetOption(string)">
        /// </seealso>
        ICollection<string> OptionNames { get; }

        /// <returns> the set of configuration options
        /// </returns>
        /// <seealso cref="GetOption(string)">
        /// </seealso>
        IDictionary<string, object> Options { get; }
    }

    /// <summary> <p>The common base interface for Bayeux listeners.</p>
    /// <p>Specific sub-interfaces define what kind of events listeners will be notified.</p>
    /// </summary>
    public interface IBayeuxListener
    {
    }
}
