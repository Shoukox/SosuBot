using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SosuBot.Database.Database.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json.Serialization;

namespace SosuBot.Services
{
    public sealed class ReplayRenderService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
        private static readonly ConcurrentDictionary<int, byte> _cancelledJobs = new();
        private readonly Uri _serverUri;
        private readonly int _clientId;
        private readonly string _clientSecret;
        private readonly ILogger<ReplayRenderService> _logger;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);
        private JwtTokenResponse? _jwtToken;
        private DateTimeOffset _jwtTokenExpiresAt = DateTimeOffset.MinValue;

        public ReplayRenderService(Uri serverUri, int clientId, string clientSecret, ILogger<ReplayRenderService> logger)
        {
            _serverUri = serverUri;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _logger = logger;
        }

        public Uri ServerUri => _serverUri;

        private async Task<string?> GetAccessToken()
        {
            if (_jwtToken is { AccessToken: not null } && DateTimeOffset.UtcNow < _jwtTokenExpiresAt)
                return _jwtToken.AccessToken;

            await _tokenLock.WaitAsync();
            try
            {
                if (_jwtToken is { AccessToken: not null } && DateTimeOffset.UtcNow < _jwtTokenExpiresAt)
                    return _jwtToken.AccessToken;

                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_serverUri, "jwt"))
                {
                    Content = JsonContent.Create(new
                    {
                        client_id = _clientId,
                        client_secret = _clientSecret,
                        grant_type = "client_credentials",
                        scope = "sosubot"
                    })
                };

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                _jwtToken = await response.Content.ReadFromJsonAsync<JwtTokenResponse>();
                if (_jwtToken?.AccessToken == null)
                    return null;

                var expiresIn = _jwtToken.ExpiresIn > 60 ? _jwtToken.ExpiresIn - 60 : Math.Max(1, _jwtToken.ExpiresIn / 2);
                _jwtTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
                return _jwtToken.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReplayRenderService token acquisition failed");
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<HttpRequestMessage?> CreateRequest(
            HttpMethod method,
            HttpContent? content,
            string relativePath,
            IDictionary<string, string>? headers = null,
            bool authorizeAsSosuBot = false)
        {
            var request = new HttpRequestMessage(method, new Uri(_serverUri, relativePath))
            {
                Content = content
            };

            if (authorizeAsSosuBot)
            {
                var accessToken = await GetAccessToken();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    request.Dispose();
                    return null;
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            if (headers != null)
            {
                foreach (var (key, value) in headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            return request;
        }

        private async Task<T?> MakeRequest<T>(
            HttpMethod method,
            HttpContent? content,
            string relativePath,
            IDictionary<string, string>? headers = null,
            bool authorizeAsSosuBot = false)
        {
            try
            {
                using var request = await CreateRequest(method, content, relativePath, headers, authorizeAsSosuBot);
                if (request == null)
                    return default;

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return default;

                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReplayRenderService");
                return default;
            }
        }

        private async Task<bool> MakeRequestSuccess(
            HttpMethod method,
            HttpContent? content,
            string relativePath,
            IDictionary<string, string>? headers = null,
            bool authorizeAsSosuBot = false)
        {
            try
            {
                using var request = await CreateRequest(method, content, relativePath, headers, authorizeAsSosuBot);
                if (request == null)
                    return false;

                using var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReplayRenderService");
                return false;
            }
        }

        public async Task<RenderQueuedResponse?> QueueReplay(Stream replayFile, DanserConfiguration danserConfiguration, string requestedBy)
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

            return await MakeRequest<RenderQueuedResponse>(
                HttpMethod.Post,
                multipart,
                "render/queue-replay",
                headers,
                authorizeAsSosuBot: true);
        }

        public async Task<SkinUploadResponse?> UploadSkin(Stream skinFile, string skinName)
        {
            var multipart = new MultipartFormDataContent()
            {
                { new StreamContent(skinFile), "skinFile", skinName }
            };

            return await MakeRequest<SkinUploadResponse>(
                HttpMethod.Post,
                multipart,
                "skins/upload-skin");
        }

        public async Task<RenderJob?> GetRenderJobInfo(int jobId)
        {
            return await MakeRequest<RenderJob>(
                HttpMethod.Post,
                null,
                $"render/get-render-job-info?job-id={jobId}");
        }

        public async Task<bool> CancelRender(int jobId)
        {
            var result = await MakeRequestSuccess(
                HttpMethod.Post,
                null,
                $"render/cancel?job-id={jobId}",
                authorizeAsSosuBot: true);

            if (result)
                _cancelledJobs[jobId] = 0;

            return result;
        }

        public bool IsRenderCancelled(int jobId)
        {
            return _cancelledJobs.ContainsKey(jobId);
        }

        public void ClearCancelledRender(int jobId)
        {
            _cancelledJobs.TryRemove(jobId, out _);
        }

        public async Task<OnlineRenderer[]?> GetOnlineRenderers()
        {
            return await MakeRequest<OnlineRenderer[]>(
                HttpMethod.Get,
                null,
                "render/get-online-renderers");
        }

        public async Task<int> GetWaitqueueLength(int jobId)
        {
            return await MakeRequest<int>(
                HttpMethod.Get,
                null,
                $"render/get-waitqueue-length?job-id={jobId}");
        }

        public async Task<IEnumerable<string>?> GetAvailableSkins()
        {
            return await MakeRequest<IEnumerable<string>>(
                HttpMethod.Get,
                null,
                "skins/get-available-skins");
        }

        private sealed class JwtTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
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
            public string Location { get; set; } = string.Empty;
        }

        public class OnlineRenderer
        {
            public int RendererId { get; set; }
            public DateTime LastSeen { get; set; } = DateTime.MinValue;
            public string RendererName { get; set; } = "undefined";
            public string UsedGPU { get; set; } = "undefined";
        }

        public class RenderJob
        {
            public int JobId { get; set; }
            public string VideoUri { get; set; } = string.Empty;
            public string ReplayPath { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }
            public string RequestedBy { get; set; } = null!;
            public int RenderingBy { get; set; } = -1;
            public DateTime RenderingStartedAt { get; set; }
            public DateTime RenderingLastUpdate { get; set; }
            public double ProgressPercent { get; set; } = 0;
            public bool IsComplete { get; set; } = false;
            public bool IsSuccess { get; set; } = false;
            public string RenderSkin { get; set; } = "default";
            public string FailureReason { get; set; } = string.Empty;
        }
    }
}
