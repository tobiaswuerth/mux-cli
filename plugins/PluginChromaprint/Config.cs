using System;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint
{
    public class Config
    {
        public Int32 ParallelProcesses { get; set; } = 3;
        public Int32 BufferSize { get; set; } = 3000;
    }
}