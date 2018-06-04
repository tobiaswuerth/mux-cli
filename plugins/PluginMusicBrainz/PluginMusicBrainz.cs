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

        public PluginMusicBrainz() : base("musicbrainz") { }

        private void HandleResponse(MusicBrainzRecord mbr, JsonMusicBrainzRequest json, DataContext context)
        {
            LoggerBundle.Trace("Handling response...");

            mbr.Disambiguation = json.Disambiguation;
            mbr.Length = json.Length;
            mbr.Title = json.Title;
            mbr.Video = json.Video;

            // aliases
            List<MusicBrainzAlias> aliases = json.Aliases?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzAlias>();
            IEnumerable<Int32> existingAliasIds =
                mbr.MusicBrainzAliasMusicBrainzRecords.Select(x => x.MusicBrainzAliasUniqueId);
            IEnumerable<Int32> newAliasIds = aliases.Select(x => x.UniqueId).Except(existingAliasIds).Distinct();
            IEnumerable<MusicBrainzAliasMusicBrainzRecord> newAliases = aliases.Where(x => newAliasIds.Contains(x.UniqueId))
                .Select(x => MusicBrainzMapper.NewShadow(mbr, x));
            mbr.MusicBrainzAliasMusicBrainzRecords.AddRange(newAliases);

            // artist credits
            List<MusicBrainzArtistCredit> credits = json.ArtistCredit?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzArtistCredit>();
            IEnumerable<Int32> existingCreditIds =
                mbr.MusicBrainzArtistCreditMusicBrainzRecords.Select(x => x.MusicBrainzArtistCreditUniqueId);
            IEnumerable<Int32> newCreditIds = credits.Select(x => x.UniqueId).Except(existingCreditIds).Distinct();
            IEnumerable<MusicBrainzArtistCreditMusicBrainzRecord> newCredits = credits
                .Where(x => newCreditIds.Contains(x.UniqueId))
                .Select(x => MusicBrainzMapper.NewShadow(mbr, x));
            mbr.MusicBrainzArtistCreditMusicBrainzRecords.AddRange(newCredits);

            // releases
            List<MusicBrainzRelease> releases = json.Releases?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzRelease>();
            IEnumerable<Int32> existingReleaseIds =
                mbr.MusicBrainzReleaseMusicBrainzRecords.Select(x => x.MusicBrainzReleaseUniqueId);
            IEnumerable<Int32> newReleaseIds = releases.Select(x => x.UniqueId).Except(existingReleaseIds).Distinct();
            IEnumerable<MusicBrainzReleaseMusicBrainzRecord> newReleases = releases
                .Where(x => newReleaseIds.Contains(x.UniqueId))
                .Select(x => MusicBrainzMapper.NewShadow(mbr, x));
            mbr.MusicBrainzReleaseMusicBrainzRecords.AddRange(newReleases);

            // tags
            List<MusicBrainzTag> tags = json.Tags?.Select(x => MusicBrainzMapper.Map(context, x)).ToList()
                ?? new List<MusicBrainzTag>();
            IEnumerable<Int32> existingTagIds = mbr.MusicBrainzTagMusicBrainzRecords.Select(x => x.MusicBrainzTagUniqueId);
            IEnumerable<Int32> newTagIds = tags.Select(x => x.UniqueId).Except(existingTagIds).Distinct();
            IEnumerable<MusicBrainzTagMusicBrainzRecord> newTags = tags.Where(x => newTagIds.Contains(x.UniqueId))
                .Select(x => MusicBrainzMapper.NewShadow(mbr, x));
            mbr.MusicBrainzTagMusicBrainzRecords.AddRange(newTags);
        }

        protected override void OnInitialize()
        {
            LoggerBundle.Debug($"Initializing plugin '{Name}'...");

            LoggerBundle.Trace("Requesting config...");
            _config = RequestConfig<Config>();
            LoggerBundle.Trace("Done.");
        }

        protected override void OnProcessStarting()
        {
            base.OnProcessStarting();
            _api = new MusicBrainzApiHandler();
        }

        protected override void Process(String[] args)
        {
            OnProcessStarting();
            TriggerActions(args.ToList());

            List<MusicBrainzRecord> data;

            do
            {
                LoggerBundle.Debug("Getting data...");
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    data = dataContext.SetMusicBrainzRecords.Where(x => null == x.LastMusicBrainzApiCall)
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

                    LoggerBundle.Inform($"Batch containing: {data.Count} entries");

                    foreach (MusicBrainzRecord mbr in data)
                    {
                        try
                        {
                            LoggerBundle.Debug($"Processing record '{mbr}'...");

                            DateTime requestTime = DateTime.Now;
                            Object o = _api.Get(mbr.MusicbrainzId);

                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            switch (o)
                            {
                                case JsonMusicBrainzRequest req:
                                    HandleResponse(mbr, req, dataContext);
                                    break;
                                case JsonErrorMusicBrainz err:
                                    mbr.MusicBrainzApiCallError = err.Error?.Trim() ?? "<unknown>";
                                    LoggerBundle.Warn(new MusicBrainzApiException($"Error: {mbr.MusicBrainzApiCallError}"));
                                    break;
                            }

                            mbr.LastMusicBrainzApiCall = requestTime;
                            dataContext.SaveChanges();

                            sw.Stop();
                            LoggerBundle.Debug($"Processing done in {sw.ElapsedMilliseconds}ms");
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
    }
}