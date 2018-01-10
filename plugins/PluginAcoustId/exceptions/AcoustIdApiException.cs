using System;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId.exceptions
{
    public class AcoustIdApiException : Exception
    {
        public AcoustIdApiException(String message) : base(message) { }
    }
}