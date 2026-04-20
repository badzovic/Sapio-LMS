using System.Net.Http.Json;
using System.Text.Json;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Services
{
    public class ExternalUsersApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ExternalUsersApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        private string GetBaseUrl()
        {
            return _configuration["InternalUsersApi:BaseUrl"]
                   ?? throw new InvalidOperationException("InternalUsersApi:BaseUrl is not configured.");
        }

        private string GetApiKey()
        {
            return _configuration["InternalUsersApi:ApiKey"]
                   ?? throw new InvalidOperationException("InternalUsersApi:ApiKey is not configured.");
        }

        public async Task<List<ExternalUsersVM>> GetUsersAsync()
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/ExternalUsers");
            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var users = await response.Content.ReadFromJsonAsync<List<ExternalUsersVM>>();
            return users ?? new List<ExternalUsersVM>();
        }

        public async Task<ExternalUsersVM?> GetUserByExternalIdAsync(int externalId)
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/ExternalUsers/{externalId}");
            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ExternalUsersVM>();
        }
    }
}