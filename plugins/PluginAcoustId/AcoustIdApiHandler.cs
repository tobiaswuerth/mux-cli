using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.plugins.PluginAcoustId.dto;
using Newtonsoft.Json;

namespace ch.wuerth.tobias.mux.plugins.PluginAcoustId
{
    // https://acoustid.org/webservice
    public class AcoustIdApiHandler
    {
        private const String API_ENDPOINT = "https://api.acoustid.org/v2/lookup";
        private const Int32 MAX_REQUESTS_PER_SECOND = 3; // as requested by policy
        private const Int32 DELAY_BETWEEN_REQUESTS = 1000 / MAX_REQUESTS_PER_SECOND; // in milliseconds
        private readonly String _apiKey;
        private readonly HttpClient _client = new HttpClient();

        private DateTime _lastRequest = DateTime.MinValue;

        public AcoustIdApiHandler(LoggerBundle logger, String apiKey)
        {
            _apiKey = apiKey;

            logger?.Information?.Log($"Notice: The AcoustId API is throttled to a maximum of {MAX_REQUESTS_PER_SECOND} requests per second due to their policy.");
        }

        public Object Post(Double duration, String fingerprint)
        {
            while ((DateTime.Now - _lastRequest).TotalMilliseconds < DELAY_BETWEEN_REQUESTS)
            {
                Thread.Sleep(1);
            }

            Dictionary<String, String> values = new Dictionary<String, String>
            {
                {
                    "client", _apiKey
                }
                ,
                {
                    "duration", $"{(Int32) duration}"
                }
                ,
                {
                    "fingerprint", fingerprint
                }
                ,
                // ReSharper disable once StringLiteralTypo
                {
                    "meta", "recordingids"
                }
            };

            FormUrlEncodedContent content = new FormUrlEncodedContent(values);
            _lastRequest = DateTime.Now;
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