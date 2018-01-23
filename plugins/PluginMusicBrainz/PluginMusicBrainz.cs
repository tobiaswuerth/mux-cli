using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private MusicBrainzApiHandler _api;
        private Config _config;

        public PluginMusicBrainz(LoggerBundle logger) : base("musicbrainz", logger) { }

        protected override void OnInitialize()
        {
            _config = RequestConfig<Config>();
        }

        protected override void OnProcessStarting()
        {
            base.OnProcessStarting();
            _api = new MusicBrainzApiHandler(Logger);
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
                        .Include(x => x.MusicBrainzAliasMusicBrainzRecords)
                        .ThenInclude(x => x.MusicBrainzAlias)
                        .Include(x => x.MusicBrainzArtistCreditMusicBrainzRecords)
                        .ThenInclude(x => x.MusicBrainzArtistCredit)
                        .Include(x => x.MusicBrainzReleaseMusicBrainzRecords)
                        .ThenInclude(x => x.MusicBrainzRelease)
                        .Include(x => x.MusicBrainzTagMusicBrainzRecords)
                        .ThenInclude(x => x.MusicBrainzTag)
                        .OrderBy(x => x.UniqueId)
                        .Take(_config.BatchSize)
                        .ToList();

                    Logger?.Information?.Log($"Batch containing: {data.Count} entries");

                    foreach (MusicBrainzRecord mbr in data)
                    {
                        try
                        {
                            Logger?.Information?.Log($"Processing '{mbr}'...");

                            DateTime requestTime = DateTime.Now;
                            Object o = _api.Get(mbr.MusicbrainzId);

                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            switch (o)
                            {
                                case JsonMusicBrainzRequest req:
                                    HandleResponse(mbr, req, context);
                                    break;
                                case JsonErrorMusicBrainz err:
                                    mbr.MusicBrainzApiCallError = err.Error?.Trim() ?? "<unknown>";
                                    Logger?.Exception?.Log(
                                        new MusicBrainzApiException($"Error: {mbr.MusicBrainzApiCallError}"));
                                    break;
                            }

                            mbr.LastMusicBrainzApiCall = requestTime;
                            context.SaveChanges();

                            sw.Stop();
                            Logger?.Information?.Log($"Saved and finished processing in {sw.ElapsedMilliseconds}ms");
                        }
                        catch (Exception ex)
                        {
                            Logger?.Exception?.Log(ex);
                        }
                    }
                }
            }
            while (data.Count > 0);
        }

        private void HandleResponse(MusicBrainzRecord mbr, JsonMusicBrainzRequest json, DataContext context)
        {
            mbr.Disambiguation = json.Disambiguation;
            mbr.Length = json.Length;
            mbr.Title = json.Title;
            mbr.Video = json.Video;

            // aliases
            List<MusicBrainzAlias> aliases = json.Aliases?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzAlias>();

            List<MusicBrainzAliasMusicBrainzRecord> aliasRecord = aliases.Select(x => new MusicBrainzAliasMusicBrainzRecord
                {
                    MusicBrainzRecord = mbr
                    , MusicBrainzRecordUniqueId = mbr.UniqueId
                    , MusicBrainzAlias = x
                    , MusicBrainzAliasUniqueId = x.UniqueId
                })
                .ToList();

            mbr.MusicBrainzAliasMusicBrainzRecords = mbr.MusicBrainzAliasMusicBrainzRecords.Concat(aliasRecord.Where(x
                    => !mbr.MusicBrainzAliasMusicBrainzRecords.Any(y
                        => y.MusicBrainzAliasUniqueId.Equals(x.MusicBrainzAliasUniqueId)
                            && y.MusicBrainzRecordUniqueId.Equals(x.MusicBrainzRecordUniqueId))))
                .ToList();

            // artist credits
            List<MusicBrainzArtistCredit> credits = json.ArtistCredit?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzArtistCredit>();

            List<MusicBrainzArtistCreditMusicBrainzRecord> artistCreditRecords = credits.Select(x
                    => new MusicBrainzArtistCreditMusicBrainzRecord
                    {
                        MusicBrainzRecord = mbr
                        , MusicBrainzRecordUniqueId = mbr.UniqueId
                        , MusicBrainzArtistCredit = x
                        , MusicBrainzArtistCreditUniqueId = x.UniqueId
                    })
                .ToList();

            mbr.MusicBrainzArtistCreditMusicBrainzRecords = mbr.MusicBrainzArtistCreditMusicBrainzRecords.Concat(
                    artistCreditRecords.Where(x => !mbr.MusicBrainzArtistCreditMusicBrainzRecords.Any(y
                        => y.MusicBrainzArtistCreditUniqueId.Equals(x.MusicBrainzArtistCreditUniqueId)
                            && y.MusicBrainzRecordUniqueId.Equals(x.MusicBrainzRecordUniqueId))))
                .ToList();

            // releases
            List<MusicBrainzRelease> releases = json.Releases?.Select(x => MusicBrainzMapper.Map(context, x, Logger)).ToList()
                ?? new List<MusicBrainzRelease>();

            List<MusicBrainzReleaseMusicBrainzRecord> releaseRecords = releases.Select(x
                    => new MusicBrainzReleaseMusicBrainzRecord
                    {
                        MusicBrainzRecord = mbr
                        , MusicBrainzRecordUniqueId = mbr.UniqueId
                        , MusicBrainzRelease = x
                        , MusicBrainzReleaseUniqueId = x.UniqueId
                    })
                .ToList();

            mbr.MusicBrainzReleaseMusicBrainzRecords = mbr.MusicBrainzReleaseMusicBrainzRecords.Concat(releaseRecords.Where(x
                    => !mbr.MusicBrainzReleaseMusicBrainzRecords.Any(y
                        => y.MusicBrainzReleaseUniqueId.Equals(x.MusicBrainzReleaseUniqueId)
                            && y.MusicBrainzRecordUniqueId.Equals(x.MusicBrainzRecordUniqueId))))
                .ToList();

            // tags
            List<MusicBrainzTag> tags = json.Tags?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzTag>();

            List<MusicBrainzTagMusicBrainzRecord> tagRecords = tags.Select(x => new MusicBrainzTagMusicBrainzRecord
                {
                    MusicBrainzRecord = mbr
                    , MusicBrainzRecordUniqueId = mbr.UniqueId
                    , MusicBrainzTag = x
                    , MusicBrainzTagUniqueId = x.UniqueId
                })
                .ToList();

            mbr.MusicBrainzTagMusicBrainzRecords = mbr.MusicBrainzTagMusicBrainzRecords.Concat(tagRecords.Where(x
                    => !mbr.MusicBrainzTagMusicBrainzRecords.Any(y
                        => y.MusicBrainzTagUniqueId.Equals(x.MusicBrainzTagUniqueId)
                            && y.MusicBrainzRecordUniqueId.Equals(x.MusicBrainzRecordUniqueId))))
                .ToList();
        }
    }
}