using System;
using System.Runtime.Serialization;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint.exceptions
{
    public class CalculationException : Exception
    {
        public CalculationException() { }

        public CalculationException(String message) : base(message) { }

        public CalculationException(String message, Exception innerException) : base(message, innerException) { }

        protected CalculationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}