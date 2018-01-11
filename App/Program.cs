using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.logging.exception;
using ch.wuerth.tobias.mux.Core.logging.information;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.plugins.PluginAcoustId;
using ch.wuerth.tobias.mux.plugins.PluginChromaprint;
using ch.wuerth.tobias.mux.plugins.PluginImport;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz;
using clipr;
using global::ch.wuerth.tobias.mux.Core.global;

namespace ch.wuerth.tobias.mux.App
{
    public class Program
    {
        private static readonly LoggerBundle Logger = new LoggerBundle
        {
            Information = new InformationConsoleLogger(null),
            Exception = new ExceptionConsoleLogger(null)
        };

        private Program(String[] args)
        {
            (Boolean success, ProgramConfig config) pa = ProcessProgramArguments(args, Logger);
            if (!pa.success)
            {
                return;
            }

            try
            {
                CreateGlobalDirectories();

                List<PluginBase> pluginsToLoad = LoadPlugins(Logger);
                Dictionary<String, PluginBase> plugins = InitializePlugin(pluginsToLoad, Logger);

                String pluginName = pa.config.PluginName.ToLower();
                if (!plugins.ContainsKey(pluginName))
                {
                    Logger.Information.Log($"No active plugin with name '{pluginName}' found");
                    Logger.Information.Log("The following plugin names were registered on startup: ");
                    foreach (String key in plugins.Keys)
                    {
                        Logger.Information.Log($" - {key}");
                    }

                    return;
                }

                Logger.Information.Log("Executing plugin...");
                plugins[pluginName].Work(pa.config.Args.ToArray());
                Logger.Information.Log("Execution finished.");
            }
            catch (Exception ex)
            {
                Logger.Exception.Log(ex);
            }
        }

        private static (Boolean success, ProgramConfig config) ProcessProgramArguments(String[] args,
            LoggerBundle logger)
        {
            ProgramConfig config;
            try
            {
                config = CliParser.Parse<ProgramConfig>(args);
            }
            catch (Exception ex)
            {
                logger.Exception.Log(ex);
                logger.Information.Log("Usage: app [options...] <plugin name> [arguments...]");
                logger.Information.Log("Options:");
                logger.Information.Log("-i | --log-information\t\t console, file");
                logger.Information.Log("-e | --log-exception\t\t console, file");
                logger.Information.Log(
                    "sample:\t app -i console --log-exception file import 'C:\\User\\Bob\\Music' 'C:\\User\\Foo\\Music'");
                return (false, null);
            }

            // additional adjustments based on successful argument parsing
            Int32 fileLoggerId = new ProgramConfig.LogDestination.FileDestination().Id;
            if (config.LogInformation.Id.Equals(fileLoggerId))
            {
                logger.Information = new InformationFileLogger(null);
            }
            if (config.LogException.Id.Equals(fileLoggerId))
            {
                logger.Exception = new ExceptionFileLogger(null);
            }

            return (true, config);
        }

        private static void CreateGlobalDirectories()
        {
            List<String> paths = new List<String>
            {
                Location.ApplicationDataDirectoryPath,
                Location.PluginsDirectoryPath,
                Location.LogsDirectoryPath
            };

            paths.Where(x => !Directory.Exists(x)).ToList().ForEach(x => Directory.CreateDirectory(x));
        }

        private static Dictionary<String, PluginBase> InitializePlugin(List<PluginBase> pluginsToLoad,
            LoggerBundle loggers)
        {
            Dictionary<String, PluginBase> plugins = new Dictionary<String, PluginBase>();

            pluginsToLoad.ForEach(x =>
            {
                // initialize
                Boolean initialized = x.Initialize();

                if (!initialized)
                {
                    loggers.Information.Log(
                        $"Plugin '{x.Name}' cannot be initialized. This plugin will be deactivated.");
                    return; // continue in linq
                }

                loggers.Information.Log($"Plugin '{x.Name}' initialized successfully. Validating...");

                // validate
                String pcName = x.Name.ToLower().Trim();
                if (plugins.ContainsKey(pcName))
                {
                    loggers.Information.Log(
                        $"Plugin '{x.Name}' does not pass validation because a plugin with the same name has already been registered. This plugin will be deactivated.");
                }
                loggers.Information.Log($"Plugin '{x.Name}' passed validation");

                // add to plugin registry
                plugins.Add(pcName, x);
                loggers.Information.Log($"Plugin '{x.Name}' activated");
            });
            return plugins;
        }

        private static List<PluginBase> LoadPlugins(LoggerBundle logger)
        {
            return new List<PluginBase>
            {
                new PluginImport(logger),
                new PluginChromaprint(logger),
                new PluginAcoustId(logger),
                new PluginMusicBrainz(logger)
            };

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

            Boolean toFile = args.Length > 1 &&
                             args.Skip(1).First().Equals("file", StringComparison.InvariantCultureIgnoreCase);
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
#if DEBUG
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
#endif
        }
    }
}