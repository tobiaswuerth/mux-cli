using System;
using System.Collections.Generic;

namespace ch.wuerth.tobias.mux.plugins.PluginImport
{
    public class Config
    {
        public Config()
        {
            Extensions = new List<String> {".mp3", ".m4a", ".flac", ".wav", ".ape", ".m4v", ".wma"};
            BufferSize = 25000;
        }

        public List<String> Extensions { get; set; }
        public Int32 BufferSize { get; set; }
    }
}