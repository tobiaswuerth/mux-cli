using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.dto
{
    public class JsonMusicBrainzRequest
    {
        public String Disambiguation { get; set; }
        public List<Release> Releases { get; set; }
        public Int32? Length { get; set; }
        public List<Alias> Aliases { get; set; }
        public String Title { get; set; }
        public List<Tag> Tags { get; set; }

        public String Id { get; set; }

        [ JsonProperty("Artist-Credit") ]
        public List<ClaArtistCredit> ArtistCredit { get; set; }

        public Boolean Video { get; set; }

        public class Tag
        {
            public String Name { get; set; }
            public Int32 Count { get; set; }
        }

        public class ClaArtistCredit
        {
            public String Name { get; set; }
            public String Joinphrase { get; set; }
            public ClaArtist Artist { get; set; }

            public class ClaArtist
            {
                public String Id { get; set; }

                [ JsonProperty("Sort-Name") ]
                public String SortName { get; set; }

                public String Disambiguation { get; set; }
                public String Name { get; set; }
                public List<Alias> Aliases { get; set; }
            }
        }

        public class Alias
        {
            public String Begin { get; set; }
            public String Locale { get; set; }

            [ JsonProperty("Type-Id") ]
            public String TypeId { get; set; }

            public String End { get; set; }
            public String Name { get; set; }
            public String Type { get; set; }

            [ JsonProperty("Short-Name") ]
            public String ShortName { get; set; }

            public String Primary { get; set; }
            public Boolean Ended { get; set; }
        }

        public class Release
        {
            [ JsonProperty("Release-Events") ]
            public List<ReleaseEvent> ReleaseEvents { get; set; }

            public List<Alias> Aliases { get; set; }
            public String Title { get; set; }
            public String Status { get; set; }
            public String Quality { get; set; }
            public String Country { get; set; }

            [ JsonProperty("Status-Id") ]
            public String StatusId { get; set; }

            [ JsonProperty("Artist-Credit") ]
            public List<ClaArtistCredit> ArtistCredit { get; set; }

            public String Id { get; set; }
            public String Barcode { get; set; }

            [ JsonProperty("Text-Representation") ]
            public ClaTextRepresentation TextRepresentation { get; set; }

            [ JsonProperty("Packaging-Id") ]
            public String PackagingId { get; set; }

            public String Date { get; set; }
            public String Disambiguation { get; set; }

            public class ClaTextRepresentation
            {
                public String Script { get; set; }
                public String Language { get; set; }
            }

            public class ReleaseEvent
            {
                public String Date { get; set; }
                public ClaArea Area { get; set; }

                public class ClaArea
                {
                    public String Id { get; set; }

                    [ JsonProperty("Sort-Name") ]
                    public String SortName { get; set; }

                    [ JsonProperty("Iso-3166-1-Codes") ]
                    public List<String> Iso31661Codes { get; set; }

                    public String Disambiguation { get; set; }
                    public String Name { get; set; }
                }
            }
        }
    }
}