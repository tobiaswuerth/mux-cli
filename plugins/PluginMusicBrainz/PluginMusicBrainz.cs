using System;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz
{
    public class PluginMusicBrainz : PluginBase
    {
        private Config _config;

        public PluginMusicBrainz(LoggerBundle logger) : base("musicbrainz", logger) { }

        protected override void OnInitialize()
        {
            _config = RequestConfig<Config>();
        }

        protected override void Process(String[] args)
        {
            throw new NotImplementedException();
        }
    }
}