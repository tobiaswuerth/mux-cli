using System;
using System.Collections.Generic;

namespace ch.wuerth.tobias.mux.plugins.PluginImport
{
    public class Config
    {
        public List<String> Extensions { get; set; } = new List<String>
        {
            ".mp3"
            , ".m4a"
            , ".flac"
            , ".wav"
            , ".ape"
            , ".m4v"
            , ".wma"
        };

        public Int32 BufferSize { get; set; } = 25000;
    }
}