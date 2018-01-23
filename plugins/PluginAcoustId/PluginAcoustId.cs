using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using ch.wuerth.tobias.mux.Data.models.shadowentities;
using ch.wuerth.tobias.mux.plugins.PluginAcoustId.dto;
using ch.wuerth.tobias.mux.plugins.PluginAcoustId.exceptions;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId
{
    public class PluginAcoustId : PluginBase
    {
        private AcoustIdApiHandler _apiHandler;
        private Config _config;

        private Boolean _includeFailed;

        public PluginAcoustId(LoggerBundle logger) : base("AcoustId", logger) { }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _config = RequestConfig<Config>();

            RegisterAction("include-failed", () => _includeFailed = true);
        }

        protected override void OnProcessStarting()
        {
            base.OnProcessStarting();
            _apiHandler = new AcoustIdApiHandler(Logger, _config.ApiKey);
        }

        private static void HandleResponse(DataContext context, Track track, JsonAcoustIdRequest air)
        {
            air.Results?.ForEach(x =>
            {
                AcoustId dbAid = HandleAcoustId(context, x);
                HandleResult(context, track, dbAid, x);
                HandleRecordings(context, x, dbAid);
            });
        }

        private static void HandleRecordings(DataContext context, JsonAcoustIdRequest.JsonResult json, AcoustId ai)
        {
            json.Recordings?.ForEach(recording =>
            {
                MusicBrainzRecord mbr = context.SetMusicBrainzRecords.Include(x => x.MusicBrainzRecordAcoustIds).ThenInclude(x => x.AcoustId).FirstOrDefault(x => x.MusicbrainzId.Equals(recording.Id));

                if (null == mbr)
                {
                    // not found in database
                    mbr = new MusicBrainzRecord
                    {
                        MusicbrainzId = recording.Id
                        , MusicBrainzRecordAcoustIds = new List<MusicBrainzRecordAcoustId>()
                    };
                    context.SetMusicBrainzRecords.Add(mbr);
                    context.SaveChanges();
                    mbr.MusicBrainzRecordAcoustIds.Add(new MusicBrainzRecordAcoustId
                    {
                        AcoustIdUniqueId = ai.UniqueId
                        , AcoustId = ai
                        , MusicBrainzRecord = mbr
                        , MusicBrainzRecordUniqueId = mbr.UniqueId
                    });
                    context.SaveChanges();

                    return; // continue in foreach
                }

                if (mbr.MusicBrainzRecordAcoustIds.Any(x => x.AcoustId.Id.Equals(ai.Id)))
                {
                    // reference already exists
                    return;
                }

                // no reference yet -> create one
                mbr.MusicBrainzRecordAcoustIds.Add(new MusicBrainzRecordAcoustId
                {
                    AcoustIdUniqueId = ai.UniqueId
                    , AcoustId = ai
                    , MusicBrainzRecord = mbr
                    , MusicBrainzRecordUniqueId = mbr.UniqueId
                });
                context.SaveChanges();
            });
        }

        private static void HandleResult(DataContext context, Track track, AcoustId dbAid, JsonAcoustIdRequest.JsonResult json)
        {
            context.SetAcoustIdResults.Add(new AcoustIdResult
            {
                Track = track
                , AcoustId = dbAid
                , Score = json.Score
            });
            context.SaveChanges();
        }

        private static AcoustId HandleAcoustId(DataContext context, JsonAcoustIdRequest.JsonResult json)
        {
            AcoustId dbAid = context.SetAcoustIds.FirstOrDefault(y => y.Id.Equals(json.Id));

            if (null != dbAid)
            {
                return dbAid;
            }

            // does not exist in database yet
            dbAid = new AcoustId
            {
                Id = json.Id
            };
            context.SetAcoustIds.Add(dbAid);
            context.SaveChanges();

            return dbAid;
        }

        protected override void OnActionHelp(StringBuilder sb)
        {
            sb.Append($"Usage: app {Name} [<options>...]");
            sb.Append(Environment.NewLine);
            sb.Append("Options:");
            sb.Append(Environment.NewLine);
            sb.Append("> include-failed | includes records which have previously been processed but have failed (disabled by default)");
        }

        protected override void Process(String[] args)
        {
            TriggerActions(args.ToList());

            List<Track> data;
            do
            {
                using (DataContext context = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log("Loading batch...");

                    data = _includeFailed ? context.SetTracks.Where(x => null != x.LastFingerprintCalculation && null == x.FingerprintError && null == x.LastAcoustIdApiCall || x.LastAcoustIdApiCall.HasValue && null != x.AcoustIdApiError).Take(_config.BufferSize).ToList() : context.SetTracks.Where(x => null != x.LastFingerprintCalculation && null == x.FingerprintError && null == x.LastAcoustIdApiCall).Take(_config.BufferSize).ToList();

                    Logger?.Information?.Log($"Batch containing {data.Count} entries");

                    foreach (Track track in data)
                    {
                        Logger?.Information?.Log($"Processing '{track}'...");

                        track.LastAcoustIdApiCall = DateTime.Now;

                        Object response = _apiHandler.Post(track.Duration ?? 0d, track.Fingerprint);

                        switch (response)
                        {
                            case JsonErrorAcoustId jea:
                            {
                                Logger?.Exception?.Log(new AcoustIdApiException($"Error {jea.Error.Code}: {jea.Error.Message}"));
                                track.AcoustIdApiError = jea.Error.Message;
                                track.AcoustIdApiErrorCode = jea.Error.Code;
                                break;
                            }
                            case JsonAcoustIdRequest air:
                            {
                                Logger?.Information?.Log("Process response...");
                                HandleResponse(context, track, air);
                                Logger?.Information?.Log("Processing done");
                                break;
                            }
                            default:
                            {
                                Logger?.Exception?.Log(new AcoustIdApiException($"Unknown response: {response}"));
                                break;
                            }
                        }

                        context.SaveChanges();
                    }
                }
            }
            while (data.Count > 0);
        }
    }
}