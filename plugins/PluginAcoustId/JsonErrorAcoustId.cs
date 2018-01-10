using System;

namespace ch.wuerth.tobias.mux.core.json
{
    public class JsonErrorAcoustId
    {
        public ClaError Error { get; set; }

        public class ClaError
        {
            public String Message { get; set; }
            public Int32 Code { get; set; }
        }
    }
}