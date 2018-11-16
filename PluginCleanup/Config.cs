using System;

namespace ch.wuerth.tobias.mux.plugins.PluginCleanup
{
    public class Config
    {
        public Boolean RemoveUnusedDuplicates { get; set; } = true;
        public Boolean RemoveInvisible { get; set; } = true;
        public Int32 BufferSize { get; set; } = 5000;
    }
}
