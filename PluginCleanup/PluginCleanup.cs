using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;

namespace ch.wuerth.tobias.mux.plugins.PluginCleanup
{
    public class PluginCleanup : PluginBase
    {
        private Config _config;

        public PluginCleanup() : base("cleanup") { }

        private void CleanDuplicates()
        {
            List<Track> data;

            do
            {
                LoggerBundle.Inform("Removing erroneous tracks");
                LoggerBundle.Debug("Getting data...");
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    data = dataContext.SetTracks.Where(x => null != x.FingerprintError).OrderBy(x => x.UniqueId).Take(_config.BufferSize).ToList();

                    LoggerBundle.Inform($"Batch containing: {data.Count} entries");

                    foreach (Track track in data)
                    {
                        try
                        {
                            LoggerBundle.Debug($"Processing track '{track}'...");

                            if (File.Exists(track.Path))
                            {
                                File.Delete(track.Path);
                            }

                            dataContext.SetTracks.Remove(track);
                            dataContext.SaveChanges();

                            LoggerBundle.Trace("Processing done");
                        }
                        catch (Exception ex)
                        {
                            LoggerBundle.Error(ex);
                        }
                    }
                }
            }
            while (data.Count > 0);
        }

        private void CleanInvisible()
        {
            List<Track> data;

            do
            {
                LoggerBundle.Inform("Removing invisible data");
                LoggerBundle.Debug("Getting data...");
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    data = dataContext.SetTracks.Where(x => null != x.LastAcoustIdApiCall).Where(x => 1 > x.AcoustIdResults.Count).OrderBy(x => x.UniqueId).Take(_config.BufferSize).ToList();

                    LoggerBundle.Inform($"Batch containing: {data.Count} entries");

                    foreach (Track track in data)
                    {
                        try
                        {
                            LoggerBundle.Debug($"Processing track '{track}'...");

                            if (File.Exists(track.Path))
                            {
                                File.Delete(track.Path);
                            }

                            dataContext.SetTracks.Remove(track);
                            dataContext.SaveChanges();

                            LoggerBundle.Trace("Processing done");
                        }
                        catch (Exception ex)
                        {
                            LoggerBundle.Error(ex);
                        }
                    }
                }
            }
            while (data.Count > 0);
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

            if (_config.RemoveUnusedDuplicates)
            {
                CleanDuplicates();
            }

            if (_config.RemoveInvisible)
            {
                CleanInvisible();
            }
        }
    }
}
