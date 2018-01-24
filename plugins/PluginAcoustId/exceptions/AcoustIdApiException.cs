using System;
using System.Runtime.Serialization;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId.exceptions
{
    public class AcoustIdApiException : Exception
    {
        public AcoustIdApiException() { }

        public AcoustIdApiException(String message) : base(message) { }

        public AcoustIdApiException(String message, Exception innerException) : base(message, innerException) { }

        protected AcoustIdApiException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}