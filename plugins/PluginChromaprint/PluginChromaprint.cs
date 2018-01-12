﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private const String ARG_KEY_INCLUDE_FAILED = "include-failed";
        private Boolean _includeFailed;

        private Config _config;
        public PluginChromaprint(LoggerBundle logger) : base("Chromaprint", logger) { }

        private static String FingerprintCalculationExecutablePath
        {
            get { return Path.Combine(Location.PluginsDirectoryPath, "fpcalc.exe"); }
        }

        protected override void OnInitialize()
        {
            if (!File.Exists(FingerprintCalculationExecutablePath))
            {
                throw new FileNotFoundException(
                    $"File '{FingerprintCalculationExecutablePath}' not found. Visit https://github.com/acoustid/chromaprint/releases to download the latest version.",
                    FingerprintCalculationExecutablePath);
            }

            _config = RequestConfig<Config>();
        }

        protected override void Process(String[] args)
        {
            _includeFailed = args.Select(x => x.ToLower()).Contains(ARG_KEY_INCLUDE_FAILED);

            List<Track> tracks;

            do
            {
                using (DataContext dataContext = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log("Preloading data...");
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    tracks = _includeFailed
                        ? dataContext.SetTracks
                            .Where(x => !x.LastFingerprintCalculation.HasValue || null != x.FingerprintError)
                            .Take(_config.BufferSize).ToList()
                        : dataContext.SetTracks
                            .Where(x => !x.LastFingerprintCalculation.HasValue)
                            .Take(_config.BufferSize).ToList();

                    sw.Stop();
                    Logger?.Information?.Log($"Getting data finished in {sw.ElapsedMilliseconds}ms");
                }
                Logger?.Information?.Log($"Batch contains {tracks.Count} record(s).");

                foreach (Track track in tracks)
                {
                    while (_buffer.Count >= _config.ParallelProcesses)
                    {
                        Thread.Sleep(1);
                    }

                    Logger?.Information?.Log($"Initializing process for file '{track.Path}'...");
                    Process p = new Process
                    {
                        StartInfo =
                        {
                            FileName = FingerprintCalculationExecutablePath,
                            Arguments = $"-json \"{track.Path}\"",
                            CreateNoWindow = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        }
                    };
                    p.OutputDataReceived += (_, arguments) => HandleStdOutput(arguments, track);
                    p.ErrorDataReceived += (_, arguments) => HandleErrOutput(arguments, track);
                    _buffer.Add(track);
                    Logger?.Information?.Log($"Starting computation process for file '{track.Path}'...");
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    Logger?.Information?.Log($"Process started for file '{track.Path}'");
                }
            } while (tracks.Count > 0);
        }

        private void HandleErrOutput(DataReceivedEventArgs arguments, Track track)
        {
            try
            {
                String output = arguments?.Data?.Trim();
                if (String.IsNullOrEmpty(output))
                {
                    return;
                }

                Logger?.Exception?.Log(new CalculationException(output));
                track.FingerprintError = output;
                track.LastFingerprintCalculation = DateTime.Now;

                using (DataContext dataContext = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log($"Saving file '{track.Path}'...");
                    dataContext.SetTracks.Attach(track);
                    dataContext.Entry(track).State = EntityState.Modified;
                    dataContext.SaveChanges();
                    Logger?.Information?.Log($"Successfully saved file '{track.Path}'");
                }
            }
            catch (Exception ex)
            {
                Logger?.Exception?.Log(ex);
            }
            finally
            {
                _buffer.Remove(track);
            }
        }

        private void HandleStdOutput(DataReceivedEventArgs arguments, Track track)
        {
            try
            {
                String output = arguments?.Data?.Trim();
                if (String.IsNullOrEmpty(output))
                {
                    return;
                }

                Logger?.Information?.Log($"Process done for file '{track.Path}'");
                Logger?.Information?.Log($"Trying to serialize computation output of file '{track.Path}'");
                JsonFingerprint jfp = JsonConvert.DeserializeObject<JsonFingerprint>(output);
                track.LastFingerprintCalculation = DateTime.Now;
                track.FingerprintHash = _hasher.Compute(jfp.Fingerprint);
                track.FingerprintError = null;

                using (DataContext dataContext = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log($"Checking for duplicates for file '{track.Path}'...");
                    if (dataContext.SetTracks.AsNoTracking().Any(x => x.FingerprintHash.Equals(track.FingerprintHash)))
                    {
                        Logger?.Information?.Log(
                            $"File with same fingerprint already in database. Path '{track.Path}' will be skipped");
                        track.FingerprintError = "duplicate";
                    }
                    else
                    {
                        Logger?.Information?.Log($"No duplicate found for file '{track.Path}.");
                        track.Duration = jfp.Duration;
                        track.Fingerprint = jfp.Fingerprint;
                    }

                    Logger?.Information?.Log($"Saving file '{track.Path}'...");
                    dataContext.SetTracks.Attach(track);
                    dataContext.Entry(track).State = EntityState.Modified;
                    dataContext.SaveChanges();
                    Logger?.Information?.Log($"Successfully saved file '{track.Path}'...");
                }
            }
            catch (Exception ex)
            {
                Logger?.Exception?.Log(ex);
            }
            finally
            {
                _buffer.Remove(track);
            }
        }
    }
}