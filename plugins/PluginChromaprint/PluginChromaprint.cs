using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
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
            List<Track> tracks;

            do
            {
                using (DataContext dc = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log("Preloading data...");
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    tracks = dc.SetTracks.Where(x => !x.LastFingerprintCalculation.HasValue).Take(_config.BufferSize)
                        .ToList();
                    sw.Stop();
                    Logger?.Information?.Log($"Getting data finished in {sw.ElapsedMilliseconds}ms");
                }
                Logger?.Information?.Log($"Batch contains {tracks.Count} record(s).");

                foreach (Track obj in tracks)
                {
                    while (_buffer.Count >= _config.ParallelProcesses)
                    {
                        Thread.Sleep(1);
                    }

                    Logger?.Information?.Log($"Initializing process for file '{obj.Path}'...");
                    Process p = new Process
                    {
                        StartInfo =
                        {
                            FileName = FingerprintCalculationExecutablePath,
                            Arguments = $"-json \"{obj.Path}\"",
                            CreateNoWindow = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        }
                    };
                    p.OutputDataReceived += (_, arguments) => HandleStdOutput(arguments, obj);
                    p.ErrorDataReceived += (_, arguments) => HandleErrOutput(arguments, obj);
                    _buffer.Add(obj);
                    Logger?.Information?.Log($"Starting computation process for file '{obj.Path}'...");
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    Logger?.Information?.Log($"Process started for file '{obj.Path}'");
                }
            } while (tracks.Count > 0);
        }

        private void HandleErrOutput(DataReceivedEventArgs arguments, Track obj)
        {
            try
            {
                String output = arguments?.Data?.Trim();
                if (String.IsNullOrEmpty(output))
                {
                    return;
                }

                Logger?.Exception?.Log(new CalculationException(output));
                obj.FingerprintError = output;
                obj.LastFingerprintCalculation = DateTime.Now;

                using (DataContext dc = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log($"Saving file '{obj.Path}'...");
                    dc.Entry(obj).State = EntityState.Modified;
                    dc.SaveChanges();
                    Logger?.Information?.Log($"Successfully saved file '{obj.Path}'");
                }
            }
            catch (Exception ex)
            {
                Logger?.Exception?.Log(ex);
            }
            finally
            {
                _buffer.Remove(obj);
            }
        }

        private void HandleStdOutput(DataReceivedEventArgs arguments, Track obj)
        {
            try
            {
                String output = arguments?.Data?.Trim();
                if (String.IsNullOrEmpty(output))
                {
                    return;
                }

                Logger?.Information?.Log($"Process done for file '{obj.Path}'");
                Logger?.Information?.Log($"Trying to serialize computation output of file '{obj.Path}'");
                JsonFingerprint jfp = JsonConvert.DeserializeObject<JsonFingerprint>(output);
                obj.LastFingerprintCalculation = DateTime.Now;
                obj.FingerprintHash = _hasher.Compute(jfp.Fingerprint);

                using (DataContext dc = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log($"Checking for duplicates for file '{obj.Path}'...");
                    if (dc.SetTracks.AsNoTracking().Any(x => x.FingerprintHash.Equals(obj.FingerprintHash)))
                    {
                        Logger?.Information?.Log(
                            $"File with same fingerprint already in database. Path '{obj.Path}' will be skipped");
                        obj.FingerprintError = "duplicate";
                    }
                    else
                    {
                        Logger?.Information?.Log($"No duplicate found for file '{obj.Path}.");
                        obj.Duration = jfp.Duration;
                        obj.Fingerprint = jfp.Fingerprint;
                    }

                    Logger?.Information?.Log($"Saving file '{obj.Path}'...");
                    dc.Entry(obj).State = EntityState.Modified;
                    dc.SaveChanges();
                    Logger?.Information?.Log($"Successfully saved file '{obj.Path}'...");
                }
            }
            catch (Exception ex)
            {
                Logger?.Exception?.Log(ex);
            }
            finally
            {
                _buffer.Remove(obj);
            }
        }
    }
}