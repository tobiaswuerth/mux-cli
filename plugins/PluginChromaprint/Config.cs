using System;
using System.Collections.Generic;
using System.Text;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint
{
    public class Config
    {
        public Int32 ParallelProcesses { get; set; }
        public Int32 BatchSize { get; set; }

        public Config()
        {
            ParallelProcesses = 3;
            BatchSize = 3000;
        }
    }
}
