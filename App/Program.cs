using System;
using System.Collections.Generic;
using System.Linq;
using ch.wuerth.tobias.mux.Core.events;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.logging.exception;
using ch.wuerth.tobias.mux.Core.logging.information;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.plugins.PluginImport;

namespace ch.wuerth.tobias.mux.App
{
    public class Program
    {
        private Program(String[] args)
        {
            LoggerBundle logger = PrepareLogger();

            // validate call
            if (args.Length < 2)
            {
                // args should contain executable name an all parameters passed
                // e.g. [ ch.wuerth.tobias.mux.App.dll, arg1, arg2, arg3, .. ]
                logger.Information.Log($"Usage: {args[0]} <plugin name> [<arg1> <arg2> ...]");
                return;
            }

            List<PluginBase> pluginsToLoad = LoadPlugins(logger);
            Dictionary<String, PluginBase> plugins = InitializePlugin(pluginsToLoad, logger);

            String pluginName = args[1].ToLower();
            if (!plugins.ContainsKey(pluginName))
            {
                logger.Information.Log($"No active plugin with name '{pluginName}' found");
                logger.Information.Log("The following plugin names were registered on startup: ");
                foreach (String key in plugins.Keys)
                {
                    logger.Information.Log($" - {key}");
                }

                return;
            }

            logger.Information.Log("Executing plugin...");
            plugins[pluginName].Work(new ArraySegment<String>(args, 2, args.Length - 2).Array);
            logger.Information.Log("Execution finished.");
        }

        private static Dictionary<String, PluginBase> InitializePlugin(List<PluginBase> pluginsToLoad,
            LoggerBundle loggers)
        {
            Dictionary<String, PluginBase> plugins = new Dictionary<String, PluginBase>();

            pluginsToLoad.ForEach(x =>
            {
                // initialize
                PluginConfigurator pc = new PluginConfigurator();
                Boolean initialized = x.Initialize(pc);

                String pluginTypeName = x.GetType().FullName;
                if (!initialized)
                {
                    loggers.Information.Log(
                        $"Plugin '{pluginTypeName}' cannot be initialized. This plugin will be deactivated.");
                    return; // continue in linq
                }

                loggers.Information.Log($"Plugin '{pluginTypeName}' initialized successfully. Validating...");

                // validate
                String pcName = pc.Name.ToLower();
                if (plugins.ContainsKey(pcName))
                {
                    loggers.Information.Log(
                        $"Plugin '{pluginTypeName}' does not pass validation because a plugin with the same name has already been registered. This plugin will be deactivated.");
                }
                loggers.Information.Log($"Plugin '{pluginTypeName}' passed validation");

                // add to plugin registry
                plugins.Add(pcName, x);
                loggers.Information.Log($"Plugin '{pluginTypeName}' activated");
            });
            return plugins;
        }

        private static List<PluginBase> LoadPlugins(LoggerBundle loggers)
        {
            // all plugins
            // todo load dynamically based on dll file in /plugins/ folder or so
            List<PluginBase> pluginsToLoad = new List<PluginBase> {new PluginImport()};
            return pluginsToLoad;
        }

        private static LoggerBundle PrepareLogger()
        {
            ConsoleRethrowCallback cb = new ConsoleRethrowCallback();
            LoggerBundle loggers = new LoggerBundle
            {
                Exception = new ExceptionConsoleLogger(cb),
                Information = new InformationConsoleLogger(cb)
            };
            loggers.Information.Log("Logger initialized");
            return loggers;
        }

        private static void Main(params String[] args)
        {
            new Program(args);
        }
    }
}