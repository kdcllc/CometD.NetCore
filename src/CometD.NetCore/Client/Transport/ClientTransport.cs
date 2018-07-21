using System.Collections.Generic;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Common;

namespace CometD.NetCore.Client.Transport
{
    /// <version> $Revision: 902 $ $Date: 2011-03-10 15:02:59 +0100 (Thu, 10 Mar 2011) $
    /// </version>
    public abstract class ClientTransport : AbstractTransport
    {
        public const string INTERVAL_OPTION = "interval";
        public const string MAX_NETWORK_DELAY_OPTION = "maxNetworkDelay";
        public const string TIMEOUT_OPTION = "timeout";
        public ClientTransport(string name, IDictionary<string, object> options)
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
        public abstract void Send(ITransportListener listener, IList<IMutableMessage> messages);
    }
}
