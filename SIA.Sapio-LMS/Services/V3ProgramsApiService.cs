using System.Net.Http.Json;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Services
{
    public class V3ProgramsApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public V3ProgramsApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        private string GetBaseUrl()
        {
            return _configuration["InternalUsersApi:BaseUrl"]
                   ?? throw new InvalidOperationException("InternalUserApi:BaseUrl is not configured.");
        }

        private string GetApiKey()
        {
            return _configuration["InternalUsersApi:ApiKey"]
                   ?? throw new InvalidOperationException("InternalUserApi:ApiKey is not configured.");
        }

        public async Task<List<V3ProgramDto>> GetProgramsByVrstaAsync(int vrstaId)
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/V3Programs?vrstaId={vrstaId}");

            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var programs = await response.Content.ReadFromJsonAsync<List<V3ProgramDto>>();

            return programs ?? new List<V3ProgramDto>();
        }

        public async Task<List<V3ProgramDto>> GetAllProgramsAsync()
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/V3Programs");

            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var programs = await response.Content.ReadFromJsonAsync<List<V3ProgramDto>>();

            return programs ?? new List<V3ProgramDto>();
        }
    }
}