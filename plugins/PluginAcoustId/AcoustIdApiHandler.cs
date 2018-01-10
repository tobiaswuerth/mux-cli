using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using ch.wuerth.tobias.mux.core.json;
using Newtonsoft.Json;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId
{
    public class AcoustIdApiHandler
    {
        private const String API_ENDPOINT = "https://api.acoustid.org/v2/lookup";
        private readonly HttpClient _client = new HttpClient();

        public AcoustIdApiHandler(String apiKey)
        {
            ApiKey = apiKey;
        }

        private String ApiKey { get; }

        public Object Post(Double duration, String fingerprint)
        {
            Dictionary<String, String> values = new Dictionary<String, String>
            {
                {"client", ApiKey},
                {"duration", $"{(Int32) duration}"},
                {"fingerprint", fingerprint},
                {"meta", "recordingids"}
            };

            FormUrlEncodedContent content = new FormUrlEncodedContent(values);
            Task<HttpResponseMessage> response = _client.PostAsync(API_ENDPOINT, content);
            Task<String> responseString = response.Result.Content.ReadAsStringAsync();
            String result = responseString.Result.Trim();

            JsonStatus status = JsonConvert.DeserializeObject<JsonStatus>(result);

            if (status.Status.ToLower().Trim().Equals("ok"))
            {
                return JsonConvert.DeserializeObject<JsonAcoustIdRequest>(result);
            }

            return JsonConvert.DeserializeObject<JsonErrorAcoustId>(result);
        }
    }
}