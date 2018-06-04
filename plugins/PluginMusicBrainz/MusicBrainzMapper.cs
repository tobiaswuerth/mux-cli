using System;
using System.Collections.Generic;
using System.Linq;
using ch.wuerth.tobias.mux.Core.processing;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using ch.wuerth.tobias.mux.Data.models.shadowentities;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.dto;
using ch.wuerth.tobias.occ.ObjectContentComparator;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz
{
    public static class MusicBrainzMapper
    {
        private static readonly DateTimeParserPipe DateTimeParserPipe = new DateTimeParserPipe();

        public static MusicBrainzRelease Map(DataContext context, JsonMusicBrainzRequest.Release json)
        {
            // references
            List<MusicBrainzArtistCredit> credits = json.ArtistCredit?.Select(x => Map(context, x)).ToList()
                ?? new List<MusicBrainzArtistCredit>();
            List<MusicBrainzReleaseEvent> events = json.ReleaseEvents?.Select(x => Map(context, x)).ToList()
                ?? new List<MusicBrainzReleaseEvent>();
            List<MusicBrainzAlias> aliases =
                json.Aliases?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzAlias>();

            // main object
            DateTime? parsedDate = DateTimeParserPipe.Process(json.Date);

            MusicBrainzRelease release = new MusicBrainzRelease
            {
                Date = parsedDate
                , Status = json.Status
                , Id = json.Id
                , Barcode = json.Barcode
                , Country = json.Country
                , Disambiguation = json.Disambiguation
                , PackagingId = json.PackagingId
                , Quality = json.Quality
                , StatusId = json.StatusId
                , Title = json.Title
                , TextRepresentation = null == json.TextRepresentation ? null : Map(context, json.TextRepresentation)
            };
            release.UniqueHash = Comparator.ComputeContentHash(release);

            MusicBrainzRelease dbRelease = context.SetReleases.Include(x => x.MusicBrainzReleaseEventMusicBrainzReleases)
                .ThenInclude(x => x.MusicBrainzReleaseEvent)
                .Include(x => x.MusicBrainzReleaseMusicBrainzAliases)
                .ThenInclude(x => x.MusicBrainzAlias)
                .Include(x => x.MusicBrainzReleaseMusicBrainzArtistCredits)
                .ThenInclude(x => x.MusicBrainzArtistCredit)
                .FirstOrDefault(x => x.UniqueHash.Equals(release.UniqueHash));

            if (null == dbRelease)
            {
                // new entry

                // save to generate primary key
                context.SetReleases.Add(release);
                context.SaveChanges();

                // references
                release.MusicBrainzReleaseMusicBrainzArtistCredits = credits.Select(x => NewShadow(release, x)).ToList();
                release.MusicBrainzReleaseEventMusicBrainzReleases = events.Select(x => NewShadow(release, x)).ToList();
                release.MusicBrainzReleaseMusicBrainzAliases = aliases.Select(x => NewShadow(release, x)).ToList();

                context.SaveChanges();
                return release;
            }

            // already exists, only check for new references

            // credits
            IEnumerable<Int32> existingCreditIds =
                dbRelease.MusicBrainzReleaseMusicBrainzArtistCredits.Select(x => x.MusicBrainzArtistCredit.UniqueId);
            IEnumerable<Int32> newCreditIds = credits.Select(x => x.UniqueId).Except(existingCreditIds).Distinct();
            IEnumerable<MusicBrainzReleaseMusicBrainzArtistCredit> newCredits =
                credits.Where(x => newCreditIds.Contains(x.UniqueId)).Select(x => NewShadow(dbRelease, x));
            dbRelease.MusicBrainzReleaseMusicBrainzArtistCredits.AddRange(newCredits);

            // release events
            IEnumerable<Int32> existingEventIds =
                dbRelease.MusicBrainzReleaseEventMusicBrainzReleases.Select(x => x.MusicBrainzReleaseEvent.UniqueId);
            IEnumerable<Int32> newEventIds = events.Select(x => x.UniqueId).Except(existingEventIds).Distinct();
            IEnumerable<MusicBrainzReleaseEventMusicBrainzRelease> newEvents =
                events.Where(x => newEventIds.Contains(x.UniqueId)).Select(x => NewShadow(dbRelease, x));
            dbRelease.MusicBrainzReleaseEventMusicBrainzReleases.AddRange(newEvents);

            // aliases
            IEnumerable<Int32> existingAliasIds =
                dbRelease.MusicBrainzReleaseMusicBrainzAliases.Select(x => x.MusicBrainzAlias.UniqueId);
            IEnumerable<Int32> newAliasIds = aliases.Select(x => x.UniqueId).Except(existingAliasIds).Distinct();
            IEnumerable<MusicBrainzReleaseMusicBrainzAlias> newAliases =
                aliases.Where(x => newAliasIds.Contains(x.UniqueId)).Select(x => NewShadow(dbRelease, x));
            dbRelease.MusicBrainzReleaseMusicBrainzAliases.AddRange(newAliases);

            context.SaveChanges();

            return dbRelease;
        }

        private static MusicBrainzTextRepresentation Map(DataContext context
            , JsonMusicBrainzRequest.Release.ClaTextRepresentation json)
        {
            MusicBrainzTextRepresentation text = new MusicBrainzTextRepresentation
            {
                Language = json.Language
                , Script = json.Script
            };
            text.UniqueHash = Comparator.ComputeContentHash(text);

            MusicBrainzTextRepresentation dbText =
                context.SetTextRepresentations.FirstOrDefault(x => x.UniqueHash.Equals(text.UniqueHash));

            if (null != dbText)
            {
                // already in db
                return dbText;
            }

            context.SetTextRepresentations.Add(text);
            context.SaveChanges();

            return text;
        }

        public static MusicBrainzArtistCredit Map(DataContext context, JsonMusicBrainzRequest.ClaArtistCredit json)
        {
            MusicBrainzArtistCredit credit = new MusicBrainzArtistCredit
            {
                Name = json.Name
                , Joinphrase = json.Joinphrase
                , Artist = null == json.Artist ? null : Map(context, json.Artist)
            };
            credit.UniqueHash = Comparator.ComputeContentHash(credit);

            MusicBrainzArtistCredit dbCredit =
                context.SetArtistCredits.FirstOrDefault(x => x.UniqueHash.Equals(credit.UniqueHash));

            if (null != dbCredit)
            {
                // already in db
                return dbCredit;
            }

            context.SetArtistCredits.Add(credit);
            context.SaveChanges();

            return credit;
        }

        private static MusicBrainzArtist Map(DataContext context, JsonMusicBrainzRequest.ClaArtistCredit.ClaArtist json)
        {
            // references
            List<MusicBrainzAlias> aliases =
                json.Aliases?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzAlias>();

            // main object
            MusicBrainzArtist artist = new MusicBrainzArtist
            {
                Name = json.Name
                , Disambiguation = json.Disambiguation
                , SortName = json.SortName
            };
            artist.UniqueHash = Comparator.ComputeContentHash(artist);

            MusicBrainzArtist dbArtist = context.SetArtists.Include(x => x.MusicBrainzArtistMusicBrainzAliases)
                .ThenInclude(x => x.MusicBrainzAlias)
                .FirstOrDefault(x => x.UniqueHash.Equals(artist.UniqueHash));

            if (null == dbArtist)
            {
                // new entry

                // save to generate primary key
                context.SetArtists.Add(artist);
                context.SaveChanges();

                // references
                artist.MusicBrainzArtistMusicBrainzAliases = aliases.Select(x => NewShadow(artist, x)).ToList();
                context.SaveChanges();

                return artist;
            }

            // already exists, only check for new references 
            IEnumerable<Int32> existingAliasIds =
                dbArtist.MusicBrainzArtistMusicBrainzAliases.Select(x => x.MusicBrainzAlias.UniqueId);
            IEnumerable<Int32> newAliasIds = aliases.Select(x => x.UniqueId).Except(existingAliasIds).Distinct();
            IEnumerable<MusicBrainzArtistMusicBrainzAlias> newAliases =
                aliases.Where(x => newAliasIds.Contains(x.UniqueId)).Select(x => NewShadow(dbArtist, x));
            dbArtist.MusicBrainzArtistMusicBrainzAliases.AddRange(newAliases);

            context.SaveChanges();

            return dbArtist;
        }

        public static MusicBrainzAlias Map(DataContext context, JsonMusicBrainzRequest.Alias json)
        {
            MusicBrainzAlias alias = new MusicBrainzAlias
            {
                Name = json.Name
                , Begin = json.Begin
                , TypeId = json.TypeId
                , End = json.End
                , Primary = json.Primary
                , ShortName = json.ShortName
                , Type = json.Type
                , Locale = json.Locale
                , Ended = json.Ended
            };
            alias.UniqueHash = Comparator.ComputeContentHash(alias);

            MusicBrainzAlias dbAlias = context.SetAliases.FirstOrDefault(x => x.UniqueHash.Equals(alias.UniqueHash));

            if (null != dbAlias)
            {
                // already in db
                return dbAlias;
            }

            context.SetAliases.Add(alias);
            context.SaveChanges();

            return alias;
        }

        private static MusicBrainzReleaseEvent Map(DataContext context, JsonMusicBrainzRequest.Release.ReleaseEvent json)
        {
            DateTime? parsedDate = DateTimeParserPipe.Process(json.Date);

            MusicBrainzReleaseEvent events = new MusicBrainzReleaseEvent
            {
                Date = parsedDate
                , Area = null == json.Area ? null : Map(context, json.Area)
            };
            events.UniqueHash = Comparator.ComputeContentHash(events);

            MusicBrainzReleaseEvent dbEvent =
                context.SetReleaseEvents.FirstOrDefault(x => x.UniqueHash.Equals(events.UniqueHash));

            if (null != dbEvent)
            {
                // already in db
                return dbEvent;
            }

            context.SetReleaseEvents.Add(events);
            context.SaveChanges();

            return events;
        }

        private static MusicBrainzArea Map(DataContext context, JsonMusicBrainzRequest.Release.ReleaseEvent.ClaArea json)
        {
            MusicBrainzArea area = context.SetAreas.FirstOrDefault(x => x.Id.Equals(json.Id));

            if (null != area)
            {
                // already in db
                return area;
            }

            area = new MusicBrainzArea
            {
                Name = json.Name
                , Id = json.Id
                , Disambiguation = json.Disambiguation
                , SortName = json.SortName
            };

            // to generate new primary key
            context.SetAreas.Add(area);
            context.SaveChanges();

            List<MusicBrainzIsoCode> isoCodes =
                json.Iso31661Codes?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzIsoCode>();
            area.MusicBrainzIsoCodeMusicBrainzAreas = isoCodes.Select(x => new MusicBrainzIsoCodeMusicBrainzArea
                {
                    MusicBrainzArea = area
                    , MusicBrainzAreaUniqueId = area.UniqueId
                    , MusicBrainzIsoCode = x
                    , MusicBrainzIsoCodeUniqueId = x.UniqueId
                })
                .ToList();

            context.SaveChanges();
            return area;
        }

        private static MusicBrainzIsoCode Map(DataContext context, String s)
        {
            MusicBrainzIsoCode iso = context.SetIsoCodes.FirstOrDefault(x => x.Code.Equals(s));

            if (null != iso)
            {
                // already in db
                return iso;
            }

            iso = new MusicBrainzIsoCode
            {
                Code = s
            };

            context.SetIsoCodes.Add(iso);
            context.SaveChanges();

            return iso;
        }

        public static MusicBrainzTag Map(DataContext context, JsonMusicBrainzRequest.Tag json)
        {
            MusicBrainzTag tag = new MusicBrainzTag
            {
                Name = json.Name
                , Count = json.Count
            };
            tag.UniqueHash = Comparator.ComputeContentHash(tag);

            MusicBrainzTag dbTag = context.SetTags.FirstOrDefault(x => x.UniqueHash.Equals(tag.UniqueHash));

            if (null != dbTag)
            {
                // already in db
                return dbTag;
            }

            context.SetTags.Add(tag);
            context.SaveChanges();

            return tag;
        }

        private static MusicBrainzReleaseMusicBrainzAlias NewShadow(MusicBrainzRelease release, MusicBrainzAlias alias)
        {
            return new MusicBrainzReleaseMusicBrainzAlias
            {
                MusicBrainzRelease = release
                , MusicBrainzReleaseUniqueId = release.UniqueId
                , MusicBrainzAlias = alias
                , MusicBrainzAliasUniqueId = alias.UniqueId
            };
        }

        private static MusicBrainzReleaseEventMusicBrainzRelease NewShadow(MusicBrainzRelease release
            , MusicBrainzReleaseEvent releaseEvent)
        {
            return new MusicBrainzReleaseEventMusicBrainzRelease
            {
                MusicBrainzRelease = release
                , MusicBrainzReleaseUniqueId = release.UniqueId
                , MusicBrainzReleaseEvent = releaseEvent
                , MusicBrainzReleaseEventUniqueId = releaseEvent.UniqueId
            };
        }

        private static MusicBrainzReleaseMusicBrainzArtistCredit NewShadow(MusicBrainzRelease release
            , MusicBrainzArtistCredit artistCredit)
        {
            return new MusicBrainzReleaseMusicBrainzArtistCredit
            {
                MusicBrainzRelease = release
                , MusicBrainzReleaseUniqueId = release.UniqueId
                , MusicBrainzArtistCredit = artistCredit
                , MusicBrainzArtistCreditUniqueId = artistCredit.UniqueId
            };
        }

        private static MusicBrainzArtistMusicBrainzAlias NewShadow(MusicBrainzAlias alias, MusicBrainzArtist artist)
        {
            return new MusicBrainzArtistMusicBrainzAlias
            {
                MusicBrainzAlias = alias
                , MusicBrainzAliasUniqueId = alias.UniqueId
                , MusicBrainzArtist = artist
                , MusicBrainzArtistUniqueId = artist.UniqueId
            };
        }

        private static MusicBrainzArtistMusicBrainzAlias NewShadow(MusicBrainzArtist artist, MusicBrainzAlias alias)
        {
            return new MusicBrainzArtistMusicBrainzAlias
            {
                MusicBrainzArtist = artist
                , MusicBrainzArtistUniqueId = artist.UniqueId
                , MusicBrainzAlias = alias
                , MusicBrainzAliasUniqueId = alias.UniqueId
            };
        }

        public static MusicBrainzTagMusicBrainzRecord NewShadow(MusicBrainzRecord record, MusicBrainzTag tag)
        {
            return new MusicBrainzTagMusicBrainzRecord
            {
                MusicBrainzRecord = record
                , MusicBrainzRecordUniqueId = record.UniqueId
                , MusicBrainzTag = tag
                , MusicBrainzTagUniqueId = tag.UniqueId
            };
        }

        public static MusicBrainzReleaseMusicBrainzRecord NewShadow(MusicBrainzRecord record, MusicBrainzRelease release)
        {
            return new MusicBrainzReleaseMusicBrainzRecord
            {
                MusicBrainzRecord = record
                , MusicBrainzRecordUniqueId = record.UniqueId
                , MusicBrainzRelease = release
                , MusicBrainzReleaseUniqueId = release.UniqueId
            };
        }

        public static MusicBrainzArtistCreditMusicBrainzRecord NewShadow(MusicBrainzRecord record
            , MusicBrainzArtistCredit artistCredit)
        {
            return new MusicBrainzArtistCreditMusicBrainzRecord
            {
                MusicBrainzRecord = record
                , MusicBrainzRecordUniqueId = record.UniqueId
                , MusicBrainzArtistCredit = artistCredit
                , MusicBrainzArtistCreditUniqueId = artistCredit.UniqueId
            };
        }

        public static MusicBrainzAliasMusicBrainzRecord NewShadow(MusicBrainzRecord record, MusicBrainzAlias alias)
        {
            return new MusicBrainzAliasMusicBrainzRecord
            {
                MusicBrainzRecord = record
                , MusicBrainzRecordUniqueId = record.UniqueId
                , MusicBrainzAlias = alias
                , MusicBrainzAliasUniqueId = alias.UniqueId
            };
        }
    }
}