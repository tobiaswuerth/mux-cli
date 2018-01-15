using System;
using System.Collections.Generic;
using clipr;

namespace ch.wuerth.tobias.mux.App
{
    public class ProgramConfig
    {
        public ProgramConfig()
        {
            LogInformation = LogDestination.Console;
            LogException = LogDestination.Console;
            Args = new List<String>();
        }

        [ PositionalArgument(0, Description = "The name of the plugin to execute") ]
        public String PluginName { get; set; }

        [ NamedArgument('i', "log-information", Description = "Destination to log information to", Required = false) ]
        public LogDestination LogInformation { get; set; }

        [ NamedArgument('e', "log-exception", Description = "Destination to log exceptions to", Required = false) ]
        public LogDestination LogException { get; set; }

        [ PositionalArgument(1, Constraint = NumArgsConstraint.AtLeast, NumArgs = 0, Description = "Additional arguments") ]
        public List<String> Args { get; set; }

        [ StaticEnumeration ]
        public abstract class LogDestination
        {
            public static readonly LogDestination Console = new ConsoleDestination();
            public static readonly LogDestination File = new FileDestination();

            // must be unique
            public abstract Int32 Id { get; }

            public class ConsoleDestination : LogDestination
            {
                public override Int32 Id
                {
                    get
                    {
                        return 1 << 0;
                    }
                }
            }

            public class FileDestination : LogDestination
            {
                public override Int32 Id
                {
                    get
                    {
                        return 1 << 1;
                    }
                }
            }
        }
    }
}