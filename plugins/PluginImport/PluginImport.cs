using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ch.wuerth.tobias.mux.Core.data;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginImport
{
    public class PluginImport : PluginBase
    {
        private const Int32 BUFFER_INSERT_THRESHOLD = 25000;

        private readonly List<String> _extensionFilter =
            new List<String> {".mp3", ".m4a", ".flac", ".wav", ".ape", ".m4v", ".wma"};

        protected override void ConfigurePlugin(PluginConfigurator configurator)
        {
            configurator.RegisterName("import");
        }

        protected override void OnProcessStarted()
        {
            base.OnProcessStarted();
            Logger?.Information?.Log("Plugin Import has started a new process");
        }

        protected override void OnProcessStopped()
        {
            base.OnProcessStopped();
            Logger?.Information?.Log("Plugin Import has stopped a process");
        }

        protected override void Process(params String[] args)
        {
            // every arg should be a directory path

            List<String> paths = args.Distinct().Select(x => x.Trim()).ToList();
            if (paths.Count.Equals(0))
            {
                Logger?.Exception?.Log(new ArgumentException("Usage: exe <path.1> [<path.2> ...]"));
                return;
            }

            foreach (String path in paths)
            {
                if (!Directory.Exists(path))
                {
                    Logger?.Information?.Log($"Path '{path}' not found. Skipping.");
                    continue;
                }

                Logger?.Information?.Log("Preloading data...");
                List<String> tracks;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                using (DataContext context = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    tracks = context.SetTracks.AsNoTracking().Select(x => x.Path).ToList();
                }
                sw.Stop();
                Logger?.Information?.Log($"Getting data finished in {sw.ElapsedMilliseconds}ms");

                List<String> buffer = new List<String>();
                DataSource<String> ds = new PathDataSource(path, _extensionFilter);

                Logger?.Information?.Log($"Start to crawl path '{path}'...");
                foreach (String file in ds.Get())
                {
                    buffer.Add(file);

                    Int32 bufferCount = buffer.Count;
                    if (bufferCount < BUFFER_INSERT_THRESHOLD)
                    {
                        if (bufferCount % 5000 == 0)
                        {
                            Logger?.Information?.Log(
                                $"Adding files to buffer [{bufferCount}/{BUFFER_INSERT_THRESHOLD}] ...");
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
            Logger?.Information?.Log("Buffer full. Starting to process...");
            List<String> newPaths = buffer.Except(tracks).ToList();
            Int32 newPathsCount = newPaths.Count;
            Logger?.Information?.Log($"{newPathsCount} new files found in buffer");

            Logger?.Information?.Log("Saving to database...");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (DataContext context = new DataContext(new DbContextOptions<DataContext>(), Logger))
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                // todo disable validation on save (haven't found feature in EF .net core yet)

                for (Int32 i = 0;
                    i < newPathsCount;
                    i++)
                {
                    context.SetTracks.Add(new Track {Path = newPaths[i]});
                    if (i % 1000 != 0)
                    {
                        continue;
                    }

                    context.SaveChanges();
                    Logger?.Information?.Log($"Saved {i + 1}/{newPathsCount}...");
                }

                context.SaveChanges();
            }

            sw.Stop();
            Int64 elms = sw.ElapsedMilliseconds;
            Logger?.Information?.Log($"Saved {newPathsCount} in {elms}ms ({elms / newPathsCount} per item average)");

            tracks.AddRange(newPaths);
            buffer.Clear();
            Logger?.Information?.Log("Finished processing");
        }
    }
}