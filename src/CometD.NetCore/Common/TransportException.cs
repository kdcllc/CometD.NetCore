using System;
using System.Runtime.Serialization;

namespace CometD.NetCore.Common
{
    /// <summary>
    /// Defines the base class for Bayeux transport exceptions.
    /// </summary>
    [Serializable]
    public class TransportException : SystemException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransportException"/> class.
        /// </summary>
        public TransportException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportException"/> class
        /// with a specified error message.
        /// </summary>
        public TransportException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportException"/> class
        /// with a specified error message and a reference to the inner exception
        /// that is the cause of this exception.
        /// </summary>
        public TransportException(string message, Exception cause)
            : base(message, cause)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportException"/> class with serialized data.
        /// </summary>
        protected TransportException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
