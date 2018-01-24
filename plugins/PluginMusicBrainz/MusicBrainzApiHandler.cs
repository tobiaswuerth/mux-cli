using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.plugins.PluginMusicBrainz.dto;
using Newtonsoft.Json;

namespace ch.wuerth.tobias.mux.plugins.PluginMusicBrainz
{
    // https://musicbrainz.org/doc/Development/XML_Web_Service/Version_2
    public class MusicBrainzApiHandler
    {
        private const String URI_API_MUSICBRAINZ =
            "https://musicbrainz.org/ws/2/recording/{0}?inc=artists+releases+artist-credits+aliases+tags&fmt=json";

        private const Int32 MAX_REQUESTS_PER_SECOND = 1; // as requested by policy
        private const Int32 DELAY_BETWEEN_REQUESTS = 1000 / MAX_REQUESTS_PER_SECOND; // in milliseconds

        private readonly HttpClient _client = new HttpClient();
        private readonly Guid _guid = Guid.NewGuid();

        private DateTime _lastRequest = DateTime.MinValue;

        public MusicBrainzApiHandler()
        {
            LoggerBundle.Inform(
                $"Notice: The MusicBrainz API is throttled to a maximum of {MAX_REQUESTS_PER_SECOND} requests per second due to their policy.");
        }

        public Object Get(String id)
        {
            LoggerBundle.Debug($"Get request for id '{id}'...");
            while ((DateTime.Now - _lastRequest).TotalMilliseconds < DELAY_BETWEEN_REQUESTS)
            {
                Thread.Sleep(1);
            }

            String url = String.Format(URI_API_MUSICBRAINZ, id ?? String.Empty);
            LoggerBundle.Trace($"Posting to '{url}'");

            Assembly coreAssembly = typeof(PluginBase).Assembly;
            Version coreVersion = coreAssembly.GetName().Version;

            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);

            String userAgent = $"Mux/{coreVersion} ( mail @fooo.ooo ) - Instance {_guid}";
            req.Headers.Add("User-Agent", userAgent);
            LoggerBundle.Trace($"User-Agent: {userAgent}");

            _lastRequest = DateTime.Now;
            LoggerBundle.Trace(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine, "Sending async request...");
            Task<HttpResponseMessage> response = _client.SendAsync(req);
            LoggerBundle.Trace(Logger.DefaultLogFlags & ~LogFlags.PrefixTimeStamp & ~LogFlags.PrefixLoggerType, "Ok.");

            Task<String> responseString = response.Result.Content.ReadAsStringAsync();
            String responseBody = responseString.Result.Trim();
            LoggerBundle.Trace($"Response: {responseBody}");

            LoggerBundle.Trace(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine, "Trying to deserialize object...");
            JsonErrorMusicBrainz status = JsonConvert.DeserializeObject<JsonErrorMusicBrainz>(responseBody);
            LoggerBundle.Trace(Logger.DefaultLogFlags & ~LogFlags.PrefixTimeStamp & ~LogFlags.PrefixLoggerType, "Ok.");

            if (null == status.Error)
            {
                // no error found
                return JsonConvert.DeserializeObject<JsonMusicBrainzRequest>(responseBody);
            }

            return status;
        }
    }
}