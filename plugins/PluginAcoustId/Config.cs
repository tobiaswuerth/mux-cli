using System;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId
{
    public class Config
    {
        public String ApiKey { get; set; } = "YOUR_API_KEY";
        public Int32 BufferSize { get; set; } = 3000;
    }
}