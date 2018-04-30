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
            List<MusicBrainzReleaseEvent> releaseEvents = json.ReleaseEvents?.Select(x => Map(context, x)).ToList()
                ?? new List<MusicBrainzReleaseEvent>();
            List<MusicBrainzAlias> aliases =
                json.Aliases?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzAlias>();

            // main object
            DateTime? parsedDate = DateTimeParserPipe.Process(json.Date);

            MusicBrainzRelease obj = new MusicBrainzRelease
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
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzRelease dbObj = context.SetReleases.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null == dbObj)
            {
                // new entry

                // save to generate primary key
                context.SetReleases.Add(obj);
                context.SaveChanges();

                // references
                obj.MusicBrainzReleaseMusicBrainzArtistCredits = credits.Select(x => NewShadow(obj, x)).ToList();
                obj.MusicBrainzReleaseEventMusicBrainzReleases = releaseEvents.Select(x => NewShadow(obj, x)).ToList();
                obj.MusicBrainzReleaseMusicBrainzAliases = aliases.Select(x => NewShadow(obj, x)).ToList();

                context.SaveChanges();
                return obj;
            }

            // already exists, only check for new references

            // credits
            IEnumerable<MusicBrainzArtistCredit> existingCredits =
                dbObj.MusicBrainzReleaseMusicBrainzArtistCredits.Select(x => x.MusicBrainzArtistCredit);
            IEnumerable<MusicBrainzArtistCredit> newCredits = credits.Except(existingCredits);
            IEnumerable<MusicBrainzReleaseMusicBrainzArtistCredit> newCreditShadows =
                newCredits.Select(x => NewShadow(dbObj, x));
            dbObj.MusicBrainzReleaseMusicBrainzArtistCredits.AddRange(newCreditShadows);

            // release events
            IEnumerable<MusicBrainzReleaseEvent> existingEvents =
                dbObj.MusicBrainzReleaseEventMusicBrainzReleases.Select(x => x.MusicBrainzReleaseEvent);
            IEnumerable<MusicBrainzReleaseEvent> newEvents = releaseEvents.Except(existingEvents);
            IEnumerable<MusicBrainzReleaseEventMusicBrainzRelease> newEventShadows = newEvents.Select(x => NewShadow(dbObj, x));
            dbObj.MusicBrainzReleaseEventMusicBrainzReleases.AddRange(newEventShadows);

            // aliases
            IEnumerable<MusicBrainzAlias> existingAliases =
                dbObj.MusicBrainzReleaseMusicBrainzAliases.Select(x => x.MusicBrainzAlias);
            IEnumerable<MusicBrainzAlias> newAliases = aliases.Except(existingAliases);
            IEnumerable<MusicBrainzReleaseMusicBrainzAlias> newAliasShadows = newAliases.Select(x => NewShadow(dbObj, x));
            dbObj.MusicBrainzReleaseMusicBrainzAliases.AddRange(newAliasShadows);

            context.SaveChanges();

            return dbObj;
        }

        private static MusicBrainzTextRepresentation Map(DataContext context
            , JsonMusicBrainzRequest.Release.ClaTextRepresentation json)
        {
            MusicBrainzTextRepresentation obj = new MusicBrainzTextRepresentation
            {
                Language = json.Language
                , Script = json.Script
            };
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

            return obj;
        }

        public static MusicBrainzArtistCredit Map(DataContext context, JsonMusicBrainzRequest.ClaArtistCredit json)
        {
            MusicBrainzArtistCredit obj = new MusicBrainzArtistCredit
            {
                Name = json.Name
                , Joinphrase = json.Joinphrase
                , Artist = null == json.Artist ? null : Map(context, json.Artist)
            };
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzArtistCredit dbObj = context.SetArtistCredits.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetArtistCredits.Add(obj);
            context.SaveChanges();

            return obj;
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
            IEnumerable<MusicBrainzAlias> existingAliases =
                dbArtist.MusicBrainzArtistMusicBrainzAliases.Select(x => x.MusicBrainzAlias);
            IEnumerable<MusicBrainzAlias> newAliases = aliases.Except(existingAliases);
            IEnumerable<MusicBrainzArtistMusicBrainzAlias> newAliasShadows = newAliases.Select(x => NewShadow(x, dbArtist));
            dbArtist.MusicBrainzArtistMusicBrainzAliases.AddRange(newAliasShadows);

            context.SaveChanges();

            return dbArtist;
        }

        public static MusicBrainzAlias Map(DataContext context, JsonMusicBrainzRequest.Alias json)
        {
            MusicBrainzAlias obj = new MusicBrainzAlias
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
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzAlias dbObj = context.SetAliases.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetAliases.Add(obj);
            context.SaveChanges();

            return obj;
        }

        private static MusicBrainzReleaseEvent Map(DataContext context, JsonMusicBrainzRequest.Release.ReleaseEvent json)
        {
            DateTime? parsedDate = DateTimeParserPipe.Process(json.Date);

            MusicBrainzReleaseEvent obj = new MusicBrainzReleaseEvent
            {
                Date = parsedDate
                , Area = null == json.Area ? null : Map(context, json.Area)
            };
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzReleaseEvent dbObj = context.SetReleaseEvents.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetReleaseEvents.Add(obj);
            context.SaveChanges();

            return obj;
        }

        private static MusicBrainzArea Map(DataContext context, JsonMusicBrainzRequest.Release.ReleaseEvent.ClaArea json)
        {
            MusicBrainzArea obj = context.SetAreas.FirstOrDefault(x => x.Id.Equals(json.Id));

            if (null != obj)
            {
                // already in db
                return obj;
            }

            obj = new MusicBrainzArea
            {
                Name = json.Name
                , Id = json.Id
                , Disambiguation = json.Disambiguation
                , SortName = json.SortName
            };

            // to generate new primary key
            context.SetAreas.Add(obj);
            context.SaveChanges();

            List<MusicBrainzIsoCode> isoCodes =
                json.Iso31661Codes?.Select(x => Map(context, x)).ToList() ?? new List<MusicBrainzIsoCode>();
            obj.MusicBrainzIsoCodeMusicBrainzAreas = isoCodes.Select(x => new MusicBrainzIsoCodeMusicBrainzArea
                {
                    MusicBrainzArea = obj
                    , MusicBrainzAreaUniqueId = obj.UniqueId
                    , MusicBrainzIsoCode = x
                    , MusicBrainzIsoCodeUniqueId = x.UniqueId
                })
                .ToList();

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

            obj = new MusicBrainzIsoCode
            {
                Code = s
            };

            context.SetIsoCodes.Add(obj);
            context.SaveChanges();

            return obj;
        }

        public static MusicBrainzTag Map(DataContext context, JsonMusicBrainzRequest.Tag json)
        {
            MusicBrainzTag obj = new MusicBrainzTag
            {
                Name = json.Name
                , Count = json.Count
            };
            obj.UniqueHash = Comparator.ComputeContentHash(obj);

            MusicBrainzTag dbObj = context.SetTags.FirstOrDefault(x => x.UniqueHash.Equals(obj.UniqueHash));

            if (null != dbObj)
            {
                // already in db
                return dbObj;
            }

            context.SetTags.Add(obj);
            context.SaveChanges();

            return obj;
        }

        private static MusicBrainzReleaseMusicBrainzAlias NewShadow(MusicBrainzRelease obj, MusicBrainzAlias x)
        {
            return new MusicBrainzReleaseMusicBrainzAlias
            {
                MusicBrainzRelease = obj
                , MusicBrainzReleaseUniqueId = obj.UniqueId
                , MusicBrainzAlias = x
                , MusicBrainzAliasUniqueId = x.UniqueId
            };
        }

        private static MusicBrainzReleaseEventMusicBrainzRelease NewShadow(MusicBrainzRelease obj, MusicBrainzReleaseEvent x)
        {
            return new MusicBrainzReleaseEventMusicBrainzRelease
            {
                MusicBrainzRelease = obj
                , MusicBrainzReleaseUniqueId = obj.UniqueId
                , MusicBrainzReleaseEvent = x
                , MusicBrainzReleaseEventUniqueId = x.UniqueId
            };
        }

        private static MusicBrainzReleaseMusicBrainzArtistCredit NewShadow(MusicBrainzRelease obj, MusicBrainzArtistCredit x)
        {
            return new MusicBrainzReleaseMusicBrainzArtistCredit
            {
                MusicBrainzRelease = obj
                , MusicBrainzReleaseUniqueId = obj.UniqueId
                , MusicBrainzArtistCredit = x
                , MusicBrainzArtistCreditUniqueId = x.UniqueId
            };
        }

        private static MusicBrainzArtistMusicBrainzAlias NewShadow(MusicBrainzAlias x, MusicBrainzArtist dbArtist)
        {
            return new MusicBrainzArtistMusicBrainzAlias
            {
                MusicBrainzAlias = x
                , MusicBrainzAliasUniqueId = x.UniqueId
                , MusicBrainzArtist = dbArtist
                , MusicBrainzArtistUniqueId = dbArtist.UniqueId
            };
        }

        private static MusicBrainzArtistMusicBrainzAlias NewShadow(MusicBrainzArtist artist, MusicBrainzAlias x)
        {
            return new MusicBrainzArtistMusicBrainzAlias
            {
                MusicBrainzArtist = artist
                , MusicBrainzArtistUniqueId = artist.UniqueId
                , MusicBrainzAlias = x
                , MusicBrainzAliasUniqueId = x.UniqueId
            };
        }
    }
}