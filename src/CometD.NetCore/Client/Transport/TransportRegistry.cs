using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CometD.NetCore.Client.Transport
{
    /// <summary>
    /// Represents a registry of <see cref="ClientTransport"/>s.
    /// </summary>
    public class TransportRegistry
    {
        private readonly IDictionary<string, ClientTransport> _transports = new Dictionary<string, ClientTransport>();
        private readonly List<string> _allowed = new List<string>();

        /// <summary>
        /// Adds a new <see cref="ClientTransport"/> into this registry.
        /// </summary>
        public void Add(ClientTransport transport)
        {
            if (transport != null)
            {
                _transports[transport.Name] = transport;
                _allowed.Add(transport.Name);
            }
        }

        /// <summary>
        /// Returns unmodifiable collection of known transports.
        /// </summary>
        public IList<string> KnownTransports
        {
            get
            {
                var newList = new List<string>(_transports.Keys.Count);
                foreach (var key in _transports.Keys)
                {
                    newList.Add(key);
                }
                return newList.AsReadOnly();
            }
        }

        /// <summary>
        /// Returns unmodifiable list of allowed transports.
        /// </summary>
        public IList<string> AllowedTransports => _allowed.AsReadOnly();

        /// <summary>
        /// Returns a list of requested transports that exists in this registry.
        /// </summary>
        public IList<ClientTransport> Negotiate(IEnumerable<object> requestedTransports, string bayeuxVersion)
        {
            var list = new List<ClientTransport>();

            foreach (var transportName in _allowed)
            {
                foreach (JValue requestedTransportName in requestedTransports)
                {
                    if (requestedTransportName.Value.Equals(transportName))
                    {
                        var transport = GetTransport(transportName);
                        if (transport.Accept(bayeuxVersion))
                        {
                            list.Add(transport);
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Returns an existing <see cref="ClientTransport"/> in this registry.
        /// </summary>
        /// <param name="transport">The transport name.</param>
        /// <returns>Return null if the <paramref name="transport"/> does not exist.</returns>
        public virtual ClientTransport GetTransport(string transport)
        {
            if (string.IsNullOrEmpty(transport))
            {
                throw new ArgumentNullException(nameof(transport));
            }

            return _transports.TryGetValue(transport, out var val) ? val : null;
        }

        /// <summary>
        /// Used to debug.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendFormat(CultureInfo.InvariantCulture, "{{{0}  AllowedTransports: [", Environment.NewLine);
            lock (_allowed)
            {
                var allowedTransports = new string[_allowed.Count];
                _allowed.CopyTo(allowedTransports, 0);

                sb.AppendFormat(CultureInfo.InvariantCulture, "'{0}'", string.Join("', '", allowedTransports));
            }

            sb.AppendFormat(CultureInfo.InvariantCulture, "]{0}  KnownTransports: [", Environment.NewLine);
            lock (_transports)
            {
                foreach (var t in _transports)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}    +- '{1}': {2}", Environment.NewLine,
                        t.Key, t.Value.ToString().Replace(Environment.NewLine, Environment.NewLine + "       "));
                }
            }

            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}  ]{0}}}", Environment.NewLine);
            return sb.ToString();
        }
    }
}
