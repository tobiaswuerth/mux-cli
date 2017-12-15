using System;
using System.Collections.Generic;
using System.Text;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginImport
{
    public class PluginImport : PluginBase
    {
        protected override void ConfigurePlugin(PluginConfigurator configurator)
        {
            configurator.RegisterName("import");
        }

        protected override void Process(params String[] args)
        {
            // every arg should be a directory path
            

        }
    }
}
