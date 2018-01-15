using System;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;

namespace ch.wuerth.tobias.mux.plugins.PluginUserMgmt
{
    public class PluginUserMgmt : PluginBase
    {
        public PluginUserMgmt(String pluginName, LoggerBundle logger) : base(pluginName, logger) { }

        protected override void Process(String[] args)
        {
            throw new NotImplementedException();
        }
    }
}