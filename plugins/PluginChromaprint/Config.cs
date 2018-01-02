using System;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint
{
    public class Config
    {
        public Config()
        {
            ParallelProcesses = 3;
            BufferSize = 3000;
        }

        public Int32 ParallelProcesses { get; set; }
        public Int32 BufferSize { get; set; }
    }
}