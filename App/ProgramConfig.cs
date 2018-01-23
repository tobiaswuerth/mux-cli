using System;
using System.Collections.Generic;
using clipr;

namespace ch.wuerth.tobias.mux.App
{
    public class ProgramConfig
    {
        [ PositionalArgument(0, Description = "The name of the plugin to execute") ]
        public String PluginName { get; set; }

        [ PositionalArgument(1, Constraint = NumArgsConstraint.AtLeast, NumArgs = 0, Description = "Additional arguments") ]
        public List<String> Args { get; set; } = new List<String>();
    }
}