using System;
using System.Collections.Generic;
using System.Linq;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.logging.exception;
using ch.wuerth.tobias.mux.Core.logging.information;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.plugins.PluginChromaprint;
using ch.wuerth.tobias.mux.plugins.PluginImport;

namespace ch.wuerth.tobias.mux.App
{
    public class Program
    {
        protected Program(String[] args)
        {
            LoggerBundle logger = PrepareLogger(args);

            try
            {
                // validate call
                if (args.Length < 1)
                {
                    logger.Information.Log("Usage: app <logging destination> <plugin name> [<arg1> <arg2> ...]");
                    logger.Information.Log("Logging destinations: console, file");
                    return;
                }

                List<PluginBase> pluginsToLoad = LoadPlugins(logger);
                Dictionary<String, PluginBase> plugins = InitializePlugin(pluginsToLoad, logger);

                String pluginName = args[0].ToLower();
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
                plugins[pluginName].Work(args.Skip(1).ToArray() /* skip command name */);
                logger.Information.Log("Execution finished.");
            }
            catch (Exception ex)
            {
                logger.Exception.Log(ex);
            }
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

        private static List<PluginBase> LoadPlugins(LoggerBundle logger)
        {
            return new List<PluginBase> {new PluginImport(logger), new PluginChromaprint(logger)};

            // does not work currently, should load plugins from /plugins/ folder instead of hardlink reference in this solution/project 
            // it only works when built with the same referenced version of core and data is used in all the projects and plugins respectively 

            //if (!Directory.Exists(Location.PluginsDirectoryPath))
            //{
            //    logger?.Information?.Log(
            //        $"Directory '{Location.PluginsDirectoryPath}' not found. Trying to create it...");

            //    Directory.CreateDirectory(Location.PluginsDirectoryPath);
            //    logger?.Information?.Log($"Directory '{Location.PluginsDirectoryPath}' created");
            //}

            //logger?.Information?.Log($"Searching '{Location.PluginsDirectoryPath}' for plugins...");
            //List<String> dllFiles = Directory.GetFiles(Location.PluginsDirectoryPath).Where(x => x.EndsWith(".dll"))
            //    .ToList();
            //logger?.Information?.Log($"{dllFiles.Count} potential plugins found");

            //List<PluginBase> plugins = new List<PluginBase>();

            //foreach (String file in dllFiles)
            //{
            //    try
            //    {
            //        logger?.Information?.Log($"Checking file {file}...");
            //        Assembly a = Assembly.LoadFrom(file);
            //        Type[] types = a.GetTypes();
            //        foreach (Type t in types)
            //        {
            //            Boolean isAssignableFrom = typeof(PluginBase).IsAssignableFrom(t);
            //            if (!isAssignableFrom)
            //            {
            //                continue;
            //            }

            //            PluginBase plugin = (PluginBase) Activator.CreateInstance(t);
            //            logger?.Information?.Log($"Found plugin '{t.FullName}'");
            //            plugins.Add(plugin);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        logger?.Exception?.Log(ex);
            //        logger?.Information?.Log($"Skipping this file");
            //    }
            //}

            //return plugins;
        }

        private static LoggerBundle PrepareLogger(String[] args)
        {
            ConsoleRethrowCallback cb = new ConsoleRethrowCallback();

            Boolean toFile = args.FirstOrDefault()?.Equals("file", StringComparison.InvariantCultureIgnoreCase) ??
                             false;
            LoggerBundle loggers = new LoggerBundle
            {
                Exception = toFile ? (ExceptionLogger) new ExceptionFileLogger(cb) : new ExceptionConsoleLogger(cb),
                Information =
                    toFile ? (InformationLogger) new InformationFileLogger(cb) : new InformationConsoleLogger(cb)
            };
            loggers.Information.Log("Logger initialized");
            return loggers;
        }

        public static void Main(String[] args)
        {
            Program program = new Program(args);
        }
    }
}