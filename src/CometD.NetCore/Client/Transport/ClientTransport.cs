using System.Collections.Generic;

using CometD.NetCore.Bayeux;
using CometD.NetCore.Common;

namespace CometD.NetCore.Client.Transport
{
    public abstract class ClientTransport : AbstractTransport
    {
        public const int DEFAULT_TIMEOUT = 120000;
        public const string INTERVAL_OPTION = "interval";
        public const string MAX_NETWORK_DELAY_OPTION = "maxNetworkDelay";
        public const string TIMEOUT_OPTION = "timeout";

        protected ClientTransport(string name, IDictionary<string, object> options)
            : base(name, options)
        {
        }

        public abstract bool IsSending { get; }

        public abstract void Abort();

        public abstract bool Accept(string version);

        public virtual void Init()
        {
        }

        public abstract void Reset();

        /// <summary>
        /// Send request over the transport.
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="messages"></param>
        /// <param name="requestTimeOut">Default timeout for the request is 2min or 120000 seconds.</param>
        public abstract void Send(ITransportListener listener, IList<IMutableMessage> messages, int requestTimeOut);
    }
}
