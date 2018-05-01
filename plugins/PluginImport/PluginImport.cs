using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ch.wuerth.tobias.mux.Core.data;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginImport
{
    public class PluginImport : PluginBase
    {
        private Config _config;

        public PluginImport() : base("import") { }

        protected override String GetHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Usage: app {Name} <path1> [<path2>...]");
            sb.Append(Environment.NewLine);
            sb.Append("> path-n | Path to directory to import from");
            return sb.ToString();
        }

        protected override void OnInitialize()
        {
            LoggerBundle.Debug($"Initializing plugin '{Name}'...");

            LoggerBundle.Trace("Requesting config...");
            _config = RequestConfig<Config>();
            LoggerBundle.Trace("Done.");
        }

        protected override void Process(String[] args)
        {
            OnProcessStarting();
            TriggerActions(args.ToList());

            List<String> paths = args.Distinct().Select(x => x.Trim()).ToList();
            if (paths.Count.Equals(0))
            {
                LoggerBundle.Fatal(new ArgumentException("no argument given"));
                Environment.Exit(1);
            }

            foreach (String path in paths)
            {
                LoggerBundle.Inform($"Processing path '{path}'...");
                if (!Directory.Exists(path))
                {
                    LoggerBundle.Warn($"Path '{path}' not found. Skipping.");
                    continue;
                }

                LoggerBundle.Debug("Preloading data...");
                List<String> tracks;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    tracks = dataContext.SetTracks.AsNoTracking().Select(x => x.Path).ToList();
                }
                sw.Stop();
                LoggerBundle.Debug($"Getting data finished in {sw.ElapsedMilliseconds}ms");

                List<String> buffer = new List<String>();
                DataSource<String> ds = new PathDataSource(path, _config.Extensions);

                LoggerBundle.Inform($"Start to crawl path '{path}'...");
                foreach (String file in ds.Get())
                {
                    buffer.Add(file);

                    Int32 bufferCount = buffer.Count;
                    if (bufferCount < _config.BufferSize)
                    {
                        if (bufferCount % (_config.BufferSize < 1337 ? _config.BufferSize : 1337) == 0)
                        {
                            LoggerBundle.Trace($"Adding files to buffer [{bufferCount}/{_config.BufferSize}] ...");
                        }
                        continue;
                    }

                    ProcessBuffer(ref buffer, ref tracks);
                }

                ProcessBuffer(ref buffer, ref tracks);
            }
        }

        private void ProcessBuffer(ref List<String> buffer, ref List<String> tracks)
        {
            LoggerBundle.Debug("Buffer full. Searching new entries...");
            List<String> newPaths = buffer.Except(tracks).ToList();
            Int32 newPathsCount = newPaths.Count;
            LoggerBundle.Debug($"{newPathsCount} new files found");

            LoggerBundle.Debug("Saving to database...");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (DataContext dataContext = DataContextFactory.GetInstance())
            {
                dataContext.ChangeTracker.AutoDetectChangesEnabled = false;
                // todo disable validation on save

                for (Int32 i = 0 ; i < newPathsCount ; i++)
                {
                    dataContext.SetTracks.Add(new Track
                    {
                        Path = newPaths[i]
                    });
                    if (i % 1337 != 0)
                    {
                        continue;
                    }

                    dataContext.SaveChanges();
                    LoggerBundle.Trace($"Saved {i + 1}/{newPathsCount}...");
                }

                dataContext.SaveChanges();
            }

            sw.Stop();
            Int64 elms = sw.ElapsedMilliseconds;
            LoggerBundle.Debug($"Saved {newPathsCount} items in {elms}ms ({(Double) elms / newPathsCount}ms per item average)");

            tracks.AddRange(newPaths);
            buffer.Clear();
            LoggerBundle.Debug("Finished processing buffer. Returning.");
        }
    }
}