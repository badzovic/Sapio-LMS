using System.Net.Http.Json;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Services
{
    public class InternalUsersApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public InternalUsersApiService(HttpClient httpClient, IConfiguration configuration)
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

        public async Task<List<InternalUserDto>> GetUsersAsync()
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/InternalUsers");

            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HRM API greška. " +
                    $"Status: {(int)response.StatusCode} {response.StatusCode}. " +
                    $"Odgovor: {responseBody}");
            }

            var users = System.Text.Json.JsonSerializer.Deserialize<List<InternalUserDto>>(
                responseBody,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return users ?? new List<InternalUserDto>();
        }

        public async Task<InternalUserDto?> GetUserByHrmIdAsync(int hrmId)
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/InternalUsers/{hrmId}");

            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HRM API greška za HRM_ID={hrmId}. " +
                    $"Status: {(int)response.StatusCode} {response.StatusCode}. " +
                    $"Odgovor: {responseBody}");
            }

            return System.Text.Json.JsonSerializer.Deserialize<InternalUserDto>(
                responseBody,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
    }
}