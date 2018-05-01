using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using ch.wuerth.tobias.mux.plugins.PluginChromaprint.dto;
using ch.wuerth.tobias.mux.plugins.PluginChromaprint.exceptions;
using ch.wuerth.tobias.occ.Hasher;
using ch.wuerth.tobias.occ.Hasher.sha;
using global::ch.wuerth.tobias.mux.Core.global;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ch.wuerth.tobias.mux.plugins.PluginChromaprint
{
    // https://github.com/acoustid/chromaprint
    public class PluginChromaprint : PluginBase
    {
        private readonly List<Track> _buffer = new List<Track>();
        private readonly IHasher _hasher = new Sha512Hasher();

        private Config _config;
        private Boolean _includeFailed;
        public PluginChromaprint() : base("Chromaprint") { }

        private static String FingerprintCalculationExecutablePath
        {
            get
            {
                return Path.Combine(Location.PluginsDirectoryPath, "fpcalc.exe");
            }
        }

        protected override String GetHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Usage: app {Name} [<options>...]");
            sb.Append(Environment.NewLine);
            sb.Append("Options:");
            sb.Append(Environment.NewLine);
            sb.Append(
                "> include-failed | includes records which have previously been processed but have failed (disabled by default)");
            return sb.ToString();
        }

        private void HandleErrOutput(DataReceivedEventArgs arguments, Track track)
        {
            LoggerBundle.Trace($"Processing error response of track '{track}'...");
            try
            {
                String output = arguments?.Data?.Trim();
                if (String.IsNullOrEmpty(output))
                {
                    return;
                }

                LoggerBundle.Warn(new CalculationException(output));
                track.FingerprintError = output;
                track.LastFingerprintCalculation = DateTime.Now;

                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    LoggerBundle.Trace($"Saving track '{track}'...");
                    dataContext.SetTracks.Attach(track);
                    dataContext.Entry(track).State = EntityState.Modified;
                    dataContext.SaveChanges();
                    LoggerBundle.Debug($"Successfully updated track '{track}'...");
                }
            }
            catch (Exception ex)
            {
                LoggerBundle.Error(ex);
            }
            finally
            {
                _buffer.Remove(track);
            }
        }

        private void HandleStdOutput(DataReceivedEventArgs arguments, Track track)
        {
            LoggerBundle.Trace($"Processing response of track '{track}'...");
            try
            {
                String output = arguments?.Data?.Trim();
                if (String.IsNullOrEmpty(output))
                {
                    return;
                }

                LoggerBundle.Debug(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine
                    , $"Trying to serialize computation output of file '{track}'...");
                JsonFingerprint jfp = JsonConvert.DeserializeObject<JsonFingerprint>(output);
                LoggerBundle.Debug(Logger.DefaultLogFlags & ~LogFlags.PrefixLoggerType & ~LogFlags.PrefixTimeStamp, "Ok.");

                track.LastFingerprintCalculation = DateTime.Now;
                track.FingerprintHash = _hasher.Compute(jfp.Fingerprint);
                track.FingerprintError = null;

                LoggerBundle.Trace($"Fingerprint hash: {track.FingerprintHash} for fingerprint {jfp.Fingerprint}");

                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    LoggerBundle.Trace($"Checking for duplicates for file '{track}'...");
                    if (dataContext.SetTracks.AsNoTracking().Any(x => x.FingerprintHash.Equals(track.FingerprintHash)))
                    {
                        LoggerBundle.Debug($"File with same fingerprint already in database. Path '{track}' will be skipped");
                        track.FingerprintError = "duplicate";
                    }
                    else
                    {
                        LoggerBundle.Trace($"No duplicate found for file '{track}'");
                        track.Duration = jfp.Duration;
                        track.Fingerprint = jfp.Fingerprint;
                        LoggerBundle.Trace($"New meta data duration '{track.Duration}' and fingerprint '{jfp.Fingerprint}'");
                    }

                    LoggerBundle.Trace($"Saving file '{track.Path}'...");
                    dataContext.SetTracks.Attach(track);
                    dataContext.Entry(track).State = EntityState.Modified;
                    dataContext.SaveChanges();
                    LoggerBundle.Debug($"Successfully saved file '{track}'...");
                }
            }
            catch (Exception ex)
            {
                LoggerBundle.Error(ex);
            }
            finally
            {
                _buffer.Remove(track);
            }
        }

        protected override void OnInitialize()
        {
            LoggerBundle.Debug($"Initializing plugin '{Name}'...");

            if (!File.Exists(FingerprintCalculationExecutablePath))
            {
                LoggerBundle.Fatal(new FileNotFoundException(
                    $"File '{FingerprintCalculationExecutablePath}' not found. Visit https://github.com/acoustid/chromaprint/releases to download the latest version."
                    , FingerprintCalculationExecutablePath));
                Environment.Exit(1);
            }

            LoggerBundle.Trace("Requesting config...");
            _config = RequestConfig<Config>();
            LoggerBundle.Trace("Done");

            RegisterAction("include-failed", () => _includeFailed = true);
        }

        protected override void Process(String[] args)
        {
            OnProcessStarting();
            TriggerActions(args.ToList());

            List<Track> tracks;

            do
            {
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    LoggerBundle.Debug("Preloading data...");
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    tracks = _includeFailed
                        ? dataContext.SetTracks.Where(x => !x.LastFingerprintCalculation.HasValue || null != x.FingerprintError)
                            .Take(_config.BufferSize)
                            .ToList()
                        : dataContext.SetTracks.Where(x => !x.LastFingerprintCalculation.HasValue)
                            .Take(_config.BufferSize)
                            .ToList();

                    sw.Stop();
                    LoggerBundle.Debug($"Getting data finished in {sw.ElapsedMilliseconds}ms");
                }
                LoggerBundle.Inform($"Batch contains {tracks.Count} record(s).");

                foreach (Track track in tracks)
                {
                    LoggerBundle.Trace($"Initializing process for track '{track}'...");
                    while (_buffer.Count >= _config.ParallelProcesses)
                    {
                        Thread.Sleep(1);
                    }

                    LoggerBundle.Trace($"Starting process for track '{track}'...");
                    Process p = new Process
                    {
                        StartInfo =
                        {
                            FileName = FingerprintCalculationExecutablePath
                            , Arguments = $"-json \"{track.Path}\""
                            , CreateNoWindow = true
                            , RedirectStandardError = true
                            , RedirectStandardInput = true
                            , RedirectStandardOutput = true
                            , UseShellExecute = false
                        }
                    };
                    p.OutputDataReceived += (_, arguments) => HandleStdOutput(arguments, track);
                    p.ErrorDataReceived += (_, arguments) => HandleErrOutput(arguments, track);
                    _buffer.Add(track);
                    LoggerBundle.Trace($"Starting computation process for file '{track}'...");
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    LoggerBundle.Debug($"Computation process started for file '{track}'");
                }
            }
            while (tracks.Count > 0);
        }
    }
}