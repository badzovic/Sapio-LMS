using SIA.Sapio_LMS.Models;
using System.Net.Http.Json;

namespace SIA.Sapio_LMS.Services
{
    public class V3MatrixApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public V3MatrixApiService(HttpClient httpClient, IConfiguration configuration)
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

        public async Task<V3RadnikMatrixDto?> GetMatrixByRadnikAsync(int radnikId)
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/V3Matrix/by-radnik/{radnikId}"
            );

            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<V3RadnikMatrixDto>();
        }

        public async Task<V3RadnikProgramStatusResponse?> GetRadniciProgramStatusAsync(int programId)
        {
            var baseUrl = GetBaseUrl().TrimEnd('/');
            var apiKey = GetApiKey();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/V3Matrix/radnici-by-program-status/{programId}"
            );

            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await _httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"V3Matrix API error. Status: {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}"
                );
            }

            return System.Text.Json.JsonSerializer.Deserialize<V3RadnikProgramStatusResponse>(
                responseBody,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
    }
}