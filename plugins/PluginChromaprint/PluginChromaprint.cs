using System;
using System.Collections.Generic;
using System.Text;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint
{
    // https://github.com/acoustid/chromaprint
    public class PluginChromaprint : PluginBase
    {
        public PluginChromaprint(LoggerBundle logger) : base(logger) { }
        protected override void ConfigurePlugin(PluginConfigurator configurator)
        {
            configurator.RegisterName("chromaprint");
        }

        protected override void Process(String[] args)
        {
            throw new NotImplementedException();
        }
    }
}
