using System;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint.exceptions
{
    public class CalculationException : Exception

    {
        public CalculationException(String message) : base(message) { }
    }
}