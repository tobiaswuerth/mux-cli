using System;
using System.Runtime.Serialization;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.exceptions
{
    public class MusicBrainzApiException : Exception
    {
        public MusicBrainzApiException() { }

        public MusicBrainzApiException(String message) : base(message) { }

        public MusicBrainzApiException(String message, Exception innerException) : base(message, innerException) { }

        protected MusicBrainzApiException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}