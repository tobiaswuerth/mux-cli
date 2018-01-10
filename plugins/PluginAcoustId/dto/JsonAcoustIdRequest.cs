using System;
using System.Collections.Generic;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId.dto
{
    public class JsonAcoustIdRequest
    {
        public List<JsonResult> Results { get; set; }

        public class JsonResult
        {
            public List<JsonRecordingId> Recordings { get; set; }
            public Double Score { get; set; }
            public String Id { get; set; }

            public class JsonRecordingId
            {
                public String Id { get; set; }
            }
        }
    }
}