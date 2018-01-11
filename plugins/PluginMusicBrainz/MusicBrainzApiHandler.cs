using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.dto;
using global::ch.wuerth.tobias.mux.Core.global;
using Newtonsoft.Json;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz
{
    public class MusicBrainzApiHandler
    {
        private const String URI_API_MUSICBRAINZ =
            "https://musicbrainz.org/ws/2/recording/{0}?inc=artists+releases+artist-credits+aliases+tags&fmt=json";

        private readonly HttpClient _client = new HttpClient();
        private readonly Guid _guid = Guid.NewGuid();

        private DateTime _lastRequest = DateTime.MinValue;

        public Object Get(String id)
        {
            while ((DateTime.Now - _lastRequest).TotalMilliseconds < 1000)
            {
                // throttling to max 1 request per second 
                // like requested by MusicBrainz policy
                Thread.Sleep(1);
            }

            String url = String.Format(URI_API_MUSICBRAINZ, id ?? String.Empty);

            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent",
                $"Mux/{typeof(Location).Assembly.ImageRuntimeVersion} ( mail @fooo.ooo ) - Instance {_guid}");

            _lastRequest = DateTime.Now;
            Task<HttpResponseMessage> response = _client.SendAsync(req);
            Task<String> responseString = response.Result.Content.ReadAsStringAsync();
            String result = responseString.Result.Trim();

            JsonErrorMusicBrainz status = JsonConvert.DeserializeObject<JsonErrorMusicBrainz>(result);

            if (null == status.Error)
            {
                // no error found
                return JsonConvert.DeserializeObject<JsonMusicBrainzRequest>(result);
            }

            return status;
        }
    }
}