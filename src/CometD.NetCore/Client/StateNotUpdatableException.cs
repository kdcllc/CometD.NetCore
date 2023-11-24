using System;
using System.Runtime.Serialization;

namespace CometD.NetCore.Client
{
    [Serializable]
    internal class StateNotUpdatableException : Exception
    {
        public StateNotUpdatableException()
        {
        }

        public StateNotUpdatableException(string message) : base(message)
        {
        }

        public StateNotUpdatableException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected StateNotUpdatableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}