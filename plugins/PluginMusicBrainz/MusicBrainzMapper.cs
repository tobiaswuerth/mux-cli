using System;
using System.Collections.Generic;
using System.Linq;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.processor;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using ch.wuerth.tobias.mux.Data.models.shadowentities;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.dto;
using ch.wuerth.tobias.occ.ObjectContentComparator;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz
{
    public static class MusicBrainzMapper
    {
        private static readonly DateTimeStringProcessor DateTimeProcessor = new DateTimeStringProcessor();

        public static MusicBrainzRelease Map(DataContext context, JsonMusicBrainzRequest.Release json,
            LoggerBundle logger)
        {
            (DateTime? parsedDate, Boolean _) = DateTimeProcessor.Handle(json.Date, logger);

            MusicBrainzRelease mbr = new MusicBrainzRelease
            {
                Date = parsedDate,
                Status = json.Status,
                Id = json.Id,
                Barcode = json.Barcode,
                Country = json.Country,
                Disambiguation = json.Disambiguation,
                PackagingId = json.PackagingId,
                Quality = json.Quality,
                StatusId = json.StatusId,
                Title = json.Title
            };
            mbr.UniqueHash = Comparator.ComputeContentHash(mbr);

            MusicBrainzRelease dbObj = context.SetReleases.FirstOrDefault(x => x.UniqueHash.Equals(mbr.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            // save to generate primary key
            context.SetReleases.Add(mbr);
            context.SaveChanges();
            context.SetReleases.Attach(mbr);

            // credits
            List<MusicBrainzArtistCredit> credits = json.ArtistCredit?.Select(x => Map(context, x)).ToList() ??
                                                    new List<MusicBrainzArtistCredit>();
            mbr.MusicBrainzReleaseMusicBrainzArtistCredits = credits.Select(x =>
                new MusicBrainzReleaseMusicBrainzArtistCredit
                {
                    MusicBrainzRelease = mbr,
                    MusicBrainzReleaseUniqueId = mbr.UniqueId,
                    MusicBrainzArtistCredit = x,
                    MusicBrainzArtistCreditUniqueId = x.UniqueId
                }).ToList();

            // release events
            List<MusicBrainzReleaseEvent> releaseEvents =
                json.ReleaseEvents?.Select(x => Map(context, x, logger)).ToList() ??
                new List<MusicBrainzReleaseEvent>();
            mbr.MusicBrainzReleaseEventMusicBrainzReleases = releaseEvents.Select(x =>
                new MusicBrainzReleaseEventMusicBrainzRelease
                {
                    MusicBrainzRelease = mbr,
                    MusicBrainzReleaseUniqueId = mbr.UniqueId,
                    MusicBrainzReleaseEvent = x,
                    MusicBrainzReleaseEventUniqueId = x.UniqueId
                }).ToList();

            // aliases
            List<MusicBrainzAlias> aliases =
                json.Aliases?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzAlias>();
            mbr.MusicBrainzReleaseMusicBrainzAliases = aliases.Select(x => new MusicBrainzReleaseMusicBrainzAlias
            {
                MusicBrainzRelease = mbr,
                MusicBrainzReleaseUniqueId = mbr.UniqueId,
                MusicBrainzAlias = x,
                MusicBrainzAliasUniqueId = x.UniqueId
            }).ToList();

            mbr.TextRepresentation = null == json.TextRepresentation ? null : Map(context, json.TextRepresentation);

            context.SaveChanges();
            return mbr;
        }

        private static MusicBrainzTextRepresentation Map(DataContext context,
            JsonMusicBrainzRequest.Release.ClaTextRepresentation json)
        {
            MusicBrainzTextRepresentation obj =
                new MusicBrainzTextRepresentation {Language = json.Language, Script = json.Script};
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzTextRepresentation dbObj =
                context.SetTextRepresentations.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetTextRepresentations.Add(obj);
            context.SaveChanges();
            context.SetTextRepresentations.Attach(obj);

            return obj;
        }

        public static MusicBrainzArtistCredit Map(DataContext context, JsonMusicBrainzRequest.ClaArtistCredit json)
        {
            MusicBrainzArtistCredit obj = new MusicBrainzArtistCredit {Name = json.Name, Joinphrase = json.Joinphrase};
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzArtistCredit dbObj =
                context.SetArtistCredits.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            obj.Artist = null == json.Artist ? null : Map(context, json.Artist);

            context.SetArtistCredits.Add(obj);
            context.SaveChanges();
            context.SetArtistCredits.Attach(obj);

            return obj;
        }

        private static MusicBrainzArtist Map(DataContext context, JsonMusicBrainzRequest.ClaArtistCredit.ClaArtist json)
        {
            MusicBrainzArtist artist = new MusicBrainzArtist
            {
                Name = json.Name,
                Disambiguation = json.Disambiguation,
                SortName = json.SortName
            };
            artist.UniqueHash = Comparator.ComputeContentHash(artist);

            List<MusicBrainzAlias> aliases =
                json.Aliases?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzAlias>();
            MusicBrainzArtist dbArtist = context.SetArtists.FirstOrDefault(x => x.UniqueHash.Equals(artist.UniqueHash));

            if (null != dbArtist)
            {
                // already in db

                if (json.Aliases?.Count.Equals(0) ?? true)
                {
                    // no reference check needed
                    return dbArtist;
                }

                if (null == dbArtist.MusicBrainzArtistMusicBrainzAliases)
                {
                    dbArtist.MusicBrainzArtistMusicBrainzAliases = new List<MusicBrainzArtistMusicBrainzAlias>();
                }

                // check if any new references were added
                List<String> dbAliasHashes = dbArtist.MusicBrainzArtistMusicBrainzAliases
                    .Select(x => x.MusicBrainzAlias.UniqueHash).ToList();
                List<String> newRefsHashes = aliases.Select(x => x.UniqueHash).Except(dbAliasHashes).ToList();
                List<MusicBrainzAlias> newRefs = aliases.Where(x => newRefsHashes.Contains(x.UniqueHash)).ToList();

                if (newRefs.Count.Equals(0))
                {
                    // no new references found
                    return dbArtist;
                }

                // new references found
                dbArtist.MusicBrainzArtistMusicBrainzAliases.AddRange(newRefs.Select(x =>
                    new MusicBrainzArtistMusicBrainzAlias
                    {
                        MusicBrainzArtist = dbArtist,
                        MusicBrainzArtistUniqueId = dbArtist.UniqueId,
                        MusicBrainzAlias = x,
                        MusicBrainzAliasUniqueId = x.UniqueId
                    }));

                context.SaveChanges();

                return dbArtist;
            }

            // create new object 

            // to generate new primary key
            context.SetArtists.Add(artist);
            context.SaveChanges();
            context.SetArtists.Attach(artist);

            // add references
            artist.MusicBrainzArtistMusicBrainzAliases = aliases.Select(x =>
                new MusicBrainzArtistMusicBrainzAlias
                {
                    MusicBrainzArtist = artist,
                    MusicBrainzArtistUniqueId = artist.UniqueId,
                    MusicBrainzAlias = x,
                    MusicBrainzAliasUniqueId = x.UniqueId
                }).ToList();

            context.SaveChanges();

            return artist;
        }

        public static MusicBrainzAlias Map(DataContext context, JsonMusicBrainzRequest.Alias json)
        {
            MusicBrainzAlias obj = new MusicBrainzAlias
            {
                Name = json.Name,
                Begin = json.Begin,
                TypeId = json.TypeId,
                End = json.End,
                Primary = json.Primary,
                ShortName = json.ShortName,
                Type = json.Type,
                Locale = json.Locale,
                Ended = json.Ended
            };
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzAlias dbObj = context.SetAliases.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetAliases.Add(obj);
            context.SaveChanges();
            context.SetAliases.Attach(obj);

            return obj;
        }

        private static MusicBrainzReleaseEvent Map(DataContext context,
            JsonMusicBrainzRequest.Release.ReleaseEvent json, LoggerBundle logger)
        {
            (DateTime? parsedDate, Boolean _) = DateTimeProcessor.Handle(json.Date, logger);

            MusicBrainzReleaseEvent obj = new MusicBrainzReleaseEvent
            {
                Date = parsedDate,
                Area = null == json.Area ? null : Map(context, json.Area)
            };
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzReleaseEvent dbObj =
                context.SetReleaseEvents.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetReleaseEvents.Add(obj);
            context.SaveChanges();
            context.SetReleaseEvents.Attach(obj);

            return obj;
        }

        private static MusicBrainzArea Map(DataContext context,
            JsonMusicBrainzRequest.Release.ReleaseEvent.ClaArea json)
        {
            MusicBrainzArea obj = context.SetAreas.FirstOrDefault(x => x.Id.Equals(json.Id));

            if (null != obj)
            {
                // already in db
                return obj;
            }

            obj = new MusicBrainzArea
            {
                Name = json.Name,
                Id = json.Id,
                Disambiguation = json.Disambiguation,
                SortName = json.SortName
            };

            // to generate new primary key
            context.SetAreas.Add(obj);
            context.SaveChanges();
            context.SetAreas.Attach(obj);

            List<MusicBrainzIsoCode> isoCodes = json.Iso31661Codes?.Select(x => Map(context, x)).ToList() ??
                                                new List<MusicBrainzIsoCode>();
            obj.MusicBrainzIsoCodeMusicBrainzAreas = isoCodes.Select(x => new MusicBrainzIsoCodeMusicBrainzArea
            {
                MusicBrainzArea = obj,
                MusicBrainzAreaUniqueId = obj.UniqueId,
                MusicBrainzIsoCode = x,
                MusicBrainzIsoCodeUniqueId = x.UniqueId
            }).ToList();

            context.SaveChanges();
            return obj;
        }

        private static MusicBrainzIsoCode Map(DataContext context, String s)
        {
            MusicBrainzIsoCode obj = context.SetIsoCodes.FirstOrDefault(x => x.Code.Equals(s));

            if (null != obj)
            {
                // already in db
                return obj;
            }

            obj = new MusicBrainzIsoCode {Code = s};

            context.SetIsoCodes.Add(obj);
            context.SaveChanges();
            context.SetIsoCodes.Attach(obj);

            return obj;
        }

        public static MusicBrainzTag Map(DataContext context, JsonMusicBrainzRequest.Tag json)
        {
            MusicBrainzTag obj = new MusicBrainzTag {Name = json.Name, Count = json.Count};
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzTag dbObj = context.SetTags.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetTags.Add(obj);
            context.SaveChanges();
            context.SetTags.Attach(obj);

            return obj;
        }
    }
}