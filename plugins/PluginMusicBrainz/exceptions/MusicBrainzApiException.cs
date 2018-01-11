using System;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.exceptions
{
    public class MusicBrainzApiException : Exception
    {
        public MusicBrainzApiException(String message) : base(message) { }
    }
}