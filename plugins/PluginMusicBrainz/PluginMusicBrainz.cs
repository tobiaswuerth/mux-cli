using System;
using System.Collections.Generic;
using System.Linq;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using ch.wuerth.tobias.mux.Data.models.shadowentities;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.dto;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.exceptions;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz
{
    public class PluginMusicBrainz : PluginBase
    {
        private readonly MusicBrainzApiHandler _api = new MusicBrainzApiHandler();
        private Config _config;

        public PluginMusicBrainz(LoggerBundle logger) : base("musicbrainz", logger) { }

        protected override void OnInitialize()
        {
            _config = RequestConfig<Config>();
        }

        protected override void OnProcessStarting()
        {
            base.OnProcessStarting();
            Logger?.Information?.Log(
                "Notice: The API used is throttled to allow a maximum of one request per second. This is due to the MusicBrainz policy.");
        }

        protected override void Process(String[] args)
        {
            List<MusicBrainzRecord> data;

            do
            {
                Logger?.Information?.Log("Getting data...");
                using (DataContext context = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    data = context.SetMusicBrainzRecords.Where(x => null == x.LastMusicBrainzApiCall)
                        .OrderBy(x => x.UniqueId).Take(_config.BatchSize).ToList();

                    Logger?.Information?.Log($"Batch containing: {data.Count} entries");

                    foreach (MusicBrainzRecord mbr in data)
                    {
                        try
                        {
                            Logger?.Information?.Log($"Processing '{mbr}'...");

                            mbr.LastMusicBrainzApiCall = DateTime.Now;
                            Object o = _api.Get(mbr.MusicbrainzId);

                            switch (o)
                            {
                                case JsonMusicBrainzRequest req:
                                    HandleResponse(mbr, req, context);
                                    break;
                                case JsonErrorMusicBrainz err:
                                    mbr.MusicBrainzApiCallError = err.Error?.Trim() ?? "<unknown>";
                                    Logger?.Exception?.Log(
                                        new MusicBrainzApiException($"Error: {mbr.MusicBrainzApiCallError}"));
                                    context.SaveChanges();
                                    break;
                            }

                            Logger?.Information?.Log("Saved and finished processing");
                        }
                        catch (Exception ex)
                        {
                            Logger?.Exception?.Log(ex);
                        }
                    }
                }
            } while (data.Count > 0);
        }

        private void HandleResponse(MusicBrainzRecord mbr, JsonMusicBrainzRequest json, DataContext context)
        {
            mbr.Disambiguation = json.Disambiguation;
            mbr.Length = json.Length;
            mbr.Title = json.Title;
            mbr.Video = json.Video;

            context.SaveChanges();

            // aliases
            List<MusicBrainzAlias> aliases = json.Aliases?.Select(x => MusicBrainzMapper.Map(context, x)).ToList() ??
                                             new List<MusicBrainzAlias>();
            List<MusicBrainzAliasMusicBrainzRecord> mbambr = aliases.Select(x => new MusicBrainzAliasMusicBrainzRecord
            {
                MusicBrainzRecord = mbr,
                MusicBrainzRecordUniqueId = mbr.UniqueId,
                MusicBrainzAlias = x,
                MusicBrainzAliasUniqueId = x.UniqueId
            }).ToList();
            mbr.MusicBrainzAliasMusicBrainzRecords = mbambr;

            context.SaveChanges();

            // artist credits
            List<MusicBrainzArtistCredit> credits =
                json.ArtistCredit?.Select(x => MusicBrainzMapper.Map(context, x)).ToList() ??
                new List<MusicBrainzArtistCredit>();
            mbr.MusicBrainzArtistCreditMusicBrainzRecords = credits.Select(x =>
                new MusicBrainzArtistCreditMusicBrainzRecord
                {
                    MusicBrainzRecord = mbr,
                    MusicBrainzRecordUniqueId = mbr.UniqueId,
                    MusicBrainzArtistCredit = x,
                    MusicBrainzArtistCreditUniqueId = x.UniqueId
                }).ToList();

            // releases
            List<MusicBrainzRelease> releases =
                json.Releases?.Select(x => MusicBrainzMapper.Map(context, x, Logger)).ToList() ??
                new List<MusicBrainzRelease>();
            mbr.MusicBrainzReleaseMusicBrainzRecords = releases.Select(x =>
                new MusicBrainzReleaseMusicBrainzRecord
                {
                    MusicBrainzRecord = mbr,
                    MusicBrainzRecordUniqueId = mbr.UniqueId,
                    MusicBrainzRelease = x,
                    MusicBrainzReleaseUniqueId = x.UniqueId
                }).ToList();

            // tags
            List<MusicBrainzTag> tags = json.Tags?.Select(x => MusicBrainzMapper.Map(context, x)).ToList() ??
                                        new List<MusicBrainzTag>();
            mbr.MusicBrainzTagMusicBrainzRecords = tags.Select(x => new MusicBrainzTagMusicBrainzRecord
            {
                MusicBrainzRecord = mbr,
                MusicBrainzRecordUniqueId = mbr.UniqueId,
                MusicBrainzTag = x,
                MusicBrainzTagUniqueId = x.UniqueId
            }).ToList();

            context.SaveChanges();
        }
    }
}