using System;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;

namespace CometD.NetCore.Client.Extension
{
    public class ErrorExtension : IExtension
    {
        public ErrorExtension()
        {
        }

        public event EventHandler<string> ConnectionMessage;

        /// <summary>
        /// Check for (message["error"].ToString().ToLower() == "403::unknown client")
        /// </summary>
        public event EventHandler<string> ConnectionError;

        public event EventHandler<Exception> ConnectionException;

        public bool Receive(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        /// <summary>
        /// Receives all of the message payloads.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <example>
        /// {
        ///    "clientId": "2d71lgayxukaalrq17ryle2pyeeu6", 
        ///    "advice": {
        ///     "interval": 0, 
        ///     "reconnect": "none"
        ///    }, 
        ///   "channel": "/meta/connect", 
        ///   "id": "365", 
        ///   "error": "403::Unknown client", 
        ///   "successful": false, 
        ///   "action": "connect"
        ///    }
        /// </example>
        public bool ReceiveMeta(IClientSession session, IMutableMessage message)
        {
            Console.WriteLine($"ReceiveMeta: {message}");

            if (message.Successful)
            {
                OnConnectionSucess(message?.Json);
            }

            if (message.ContainsKey("exception"))
            {
                var ex = (Exception)message["exception"];
                OnConnectionException(ex);
            }

            if (message.ContainsKey("error"))
            {
                OnConnectionError(message["error"].ToString());

                //if (message["error"].ToString().ToLower() == "403::unknown client")
                //{
                //    this.OnConnectionError(message["error"].ToString());
                //}
            }

            return true;
        }

        public bool Send(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool SendMeta(IClientSession session, IMutableMessage message)
        {
            Console.WriteLine($"SendMeta: {message}");

            return true;
        }

        private void OnConnectionError(string message)
        {
            ConnectionError?.Invoke(this, message);
        }

        private void OnConnectionException(Exception exception)
        {
            ConnectionException?.Invoke(this, exception);
        }

        private void OnConnectionSucess(string message)
        {
            ConnectionMessage?.Invoke(this, message);
        }
    }
}
