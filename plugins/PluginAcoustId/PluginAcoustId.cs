using System;
using System.Collections.Generic;
using System.Linq;
using ch.wuerth.tobias.mux.core.json;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using ch.wuerth.tobias.mux.Data.models.shadowentities;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId
{
    // https://acoustid.org/webservice
    public class PluginAcoustId : PluginBase
    {
        private AcoustIdApiHandler _apiHandler;
        private Config _config;

        public PluginAcoustId(LoggerBundle logger) : base("acoustid", logger) { }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _config = RequestConfig<Config>();
            _apiHandler = new AcoustIdApiHandler(_config.ApiKey);
        }

        private void HandleResponse(DataContext context, Track track, JsonAcoustIdRequest air)
        {
            air.Results?.ForEach(x =>
            {
                AcoustId dbair = HandleAcoustId(context, x);
                HandleResult(context, track, dbair, x);
                HandleRecordings(context, x, dbair);
            });
        }

        private static void HandleRecordings(DataContext context, JsonAcoustIdRequest.JsonResult json, AcoustId ai)
        {
            json.Recordings?.ForEach(recording =>
            {
                MusicBrainzRecord mbr =
                    context.SetMusicBrainzRecords.FirstOrDefault(x => x.MusicbrainzId.Equals(recording.Id));

                if (null == mbr)
                {
                    // not found in database
                    mbr = new MusicBrainzRecord
                    {
                        MusicbrainzId = recording.Id,
                        MusicBrainzRecordAcoustIds = new List<MusicBrainzRecordAcoustId>()
                    };
                    context.SetMusicBrainzRecords.Add(mbr);
                    context.SaveChanges();
                    mbr.MusicBrainzRecordAcoustIds.Add(new MusicBrainzRecordAcoustId
                    {
                        AcoustIdUniqueId = ai.UniqueId,
                        AcoustId = ai,
                        MusicBrainzRecord = mbr,
                        MusicBrainzRecordUniqueId = mbr.UniqueId
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
                    AcoustIdUniqueId = ai.UniqueId,
                    AcoustId = ai,
                    MusicBrainzRecord = mbr,
                    MusicBrainzRecordUniqueId = mbr.UniqueId
                });
                context.SaveChanges();
            });
        }

        private static void HandleResult(DataContext context, Track track, AcoustId dbair,
            JsonAcoustIdRequest.JsonResult json)
        {
            context.SetAcoustIdResults.Add(new AcoustIdResult {Track = track, AcoustId = dbair, Score = json.Score});
            context.SaveChanges();
        }

        private static AcoustId HandleAcoustId(DataContext context, JsonAcoustIdRequest.JsonResult json)
        {
            AcoustId dbair = context.SetAcoustIds.FirstOrDefault(y => y.Id.Equals(json.Id));

            if (null != dbair)
            {
                return dbair;
            }

            // does not exist in database yet
            dbair = new AcoustId {Id = json.Id};
            context.SetAcoustIds.Add(dbair);
            context.SaveChanges();

            return dbair;
        }

        protected override void Process(String[] args)
        {
            List<Track> data;
            do
            {
                using (DataContext context = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    Logger?.Information?.Log("Loading batch...");

                    data = context.SetTracks
                        .Where(x => null != x.LastFingerprintCalculation && null == x.FingerprintError &&
                                    null == x.LastAcoustIdApiCall)
                        .OrderBy(x => x.UniqueId) // todo might need to optimize
                        .Take(_config.BufferSize).ToList();

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
                                Logger?.Exception?.Log(
                                    new AcoustIdApiException($"Error {jea.Error.Code}: {jea.Error.Message}"));
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
                                Logger?.Exception?.Log(new NotImplementedException("Unknown response type"));
                                break;
                            }
                        }

                        context.SaveChanges();
                    }
                }
            } while (data.Count > 0);
        }
    }
}