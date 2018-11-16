using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.plugins.PluginAcoustId;
using ch.wuerth.tobias.mux.plugins.PluginChromaprint;
using ch.wuerth.tobias.mux.plugins.PluginCleanup;
using ch.wuerth.tobias.mux.plugins.PluginImport;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz;
using ch.wuerth.tobias.mux.plugins.PluginUserMgmt;
using clipr;
using global::ch.wuerth.tobias.mux.Core.global;

namespace ch.wuerth.tobias.mux.App
{
    public class Program
    {
        private Program(String[] args)
        {
            LoggerBundle.Inform("Initializing...");

            (Boolean success, ProgramConfig config) = ProcessProgramArguments(args);
            if (!success)
            {
                LoggerBundle.Fatal("Program arguments could not be parsed correctly");
                Environment.Exit(1);
            }

            try
            {
                CreateGlobalDirectories();

                List<PluginBase> pluginsToLoad = LoadPlugins();
                Dictionary<String, PluginBase> plugins = InitializePlugin(pluginsToLoad);

                String pluginName = config.PluginName.ToLower();
                if (!plugins.ContainsKey(pluginName))
                {
                    LoggerBundle.Warn($"No active plugin with name '{pluginName}' found");
                    LoggerBundle.Inform("The following plugin names were registered on startup: ");
                    plugins.Keys.ToList().ForEach(x => LoggerBundle.Inform($"+ {x}"));
                    return;
                }

                LoggerBundle.Inform("Executing plugin...");
                plugins[pluginName].Work(config.Args.ToArray());
                LoggerBundle.Inform("Execution finished.");
            }
            catch (Exception ex)
            {
                LoggerBundle.Error(ex);
            }
        }

        private static void CreateGlobalDirectories()
        {
            List<String> paths = new List<String>
            {
                Location.ApplicationDataDirectoryPath
                , Location.PluginsDirectoryPath
                , Location.LogsDirectoryPath
            };

            paths.Where(x => !Directory.Exists(x))
                .ToList()
                .ForEach(x =>
                {
                    LoggerBundle.Debug(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine, $"Trying to create directory '{x}'...");
                    Directory.CreateDirectory(x);
                    LoggerBundle.Debug(Logger.DefaultLogFlags & ~LogFlags.PrefixLoggerType & ~LogFlags.PrefixTimeStamp, "Ok.");
                });
        }

        private static Dictionary<String, PluginBase> InitializePlugin(List<PluginBase> pluginsToLoad)
        {
            Dictionary<String, PluginBase> plugins = new Dictionary<String, PluginBase>();

            pluginsToLoad.ForEach(x =>
            {
                // initialize
                Boolean initialized = x.Initialize();

                if (!initialized)
                {
                    LoggerBundle.Warn($"Plugin '{x.Name}' cannot be initialized. This plugin will be deactivated.");
                    return;
                }

                LoggerBundle.Debug($"Plugin '{x.Name}' initialized successfully. Validating...");

                // validate
                String pcName = x.Name.ToLower().Trim();
                if (plugins.ContainsKey(pcName))
                {
                    LoggerBundle.Warn($"Plugin '{x.Name}' does not pass validation because a plugin with the same name has already been registered. This plugin will be deactivated.");
                }

                LoggerBundle.Debug($"Plugin '{x.Name}' passed validation");

                // add to plugin registry
                plugins.Add(pcName, x);
                LoggerBundle.Inform($"Plugin '{x.Name}' activated");
            });
            return plugins;
        }

        private static List<PluginBase> LoadPlugins()
        {
            return new List<PluginBase>
            {
                new PluginImport()
                , new PluginChromaprint()
                , new PluginAcoustId()
                , new PluginMusicBrainz()
                , new PluginUserMgmt()
                , new PluginCleanup()
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

        public static void Main(String[] args)
        {
            Program program = new Program(args);
#if DEBUG
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
#endif
        }

        private static (Boolean success, ProgramConfig config) ProcessProgramArguments(String[] args)
        {
            ProgramConfig config;
            try
            {
                LoggerBundle.Trace("Trying to parse to following arguments:");
                args.ToList().ForEach(x => LoggerBundle.Trace($"+ '{x}'"));
                config = CliParser.Parse<ProgramConfig>(args);
            }
            catch (Exception ex)
            {
                LoggerBundle.Error(ex);
                return (false, null);
            }

            return (true, config);
        }
    }
}
