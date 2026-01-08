using Newtonsoft.Json;
using SosuBot.Database.Database.Models;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json.Serialization;

namespace SosuBot.Services
{
    public sealed class ReplayRenderService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
        private readonly Uri _serverUri;

        public ReplayRenderService(Uri serverUri)
        {
            _serverUri = serverUri;
        }

        public Uri ServerUri => _serverUri;

        private async Task<T?> MakeRequest<T>(
            HttpMethod method,
            HttpContent? content,
            string relativePath,
            IDictionary<string, string>? headers = null)
        {
            using var request = new HttpRequestMessage(method, new Uri(_serverUri, relativePath))
            {
                Content = content
            };

            if (headers != null)
            {
                foreach (var (key, value) in headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return default;

            return await response.Content.ReadFromJsonAsync<T>();
        }

        public async Task<RenderQueuedResponse?> QueueReplay(
            Stream replayFile, DanserConfiguration danserConfiguration)
        {
            var multipart = new MultipartFormDataContent()
            {
                { new StreamContent(replayFile), "file", "replay.osr" },
                { new StringContent(JsonConvert.SerializeObject(danserConfiguration, Formatting.None), System.Text.Encoding.UTF8, MediaTypeNames.Application.Json), "config" }
            };
            var headers = new Dictionary<string, string>()
            {
                ["Requested-By"] = "sosubot!!!"
            };
            var result = await MakeRequest<RenderQueuedResponse>(
                HttpMethod.Post,
                multipart,
                "render/queue-replay",
                headers);

            return result;
        }

        public async Task<SkinUploadResponse?> UploadSkin(
           Stream skinFile, string skinName)
        {
            var multipart = new MultipartFormDataContent()
            {
                { new StreamContent(skinFile), "skinFile", skinName }
            };
            var result = await MakeRequest<SkinUploadResponse>(
                HttpMethod.Post,
                multipart,
                "skins/upload-skin",
                null);

            return result;
        }


        public async Task<RenderJob?> GetRenderJobInfo(int jobId)
        {
            var result = await MakeRequest<RenderJob>(
                HttpMethod.Post,
                null,
                $"render/get-render-job-info?job-id={jobId}",
                null);

            return result;
        }

        public async Task<OnlineRenderer[]?> GetOnlineRenderers()
        {
            var result = await MakeRequest<OnlineRenderer[]>(
                HttpMethod.Get,
                null,
                "render/get-online-renderers",
                null);

            return result;
        }

        public async Task<IEnumerable<string>?> GetAvailableSkins()
        {
            var result = await MakeRequest<IEnumerable<string>>(
                HttpMethod.Get,
                null,
                "skins/get-available-skins",
                null);

            return result;
        }

        public class RenderQueuedResponse
        {
            [JsonPropertyName("jobId")]
            public int JobId { get; set; } = -1;

            [JsonPropertyName("status")]
            public string Status { get; set; } = null!;
        }

        public class SkinUploadResponse
        {
            [JsonPropertyName("location")]
            public string Location { get; set; } = "";
        }

        public class OnlineRenderer
        {
            public int RendererId { get; set; }
            public DateTime LastSeen { get; set; } = DateTime.MinValue;
        }

        public class RenderJob
        {
            public int JobId { get; set; }
            public string VideoUri { get; set; } = "";
            public string ReplayPath { get; set; } = "";
            public DateTime RequestedAt { get; set; }
            public string RequestedBy { get; set; } = null!;
            public int RenderingBy { get; set; } = -1;
            public DateTime RenderingStartedAt { get; set; }
            public DateTime RenderingLastUpdate { get; set; }
            public double ProgressPercent { get; set; } = 0; // 0.00 ... 1.00
            public bool IsComplete { get; set; } = false;
            public bool IsSuccess { get; set; } = false;
            public string RenderSkin { get; set; } = "default";
            public string FailureReason { get; set; } = "";
        }
    }
}
