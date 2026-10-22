using System.Net.Http.Json;
using System.Text.Json;
using RequestsManagement.Dto.Common;

namespace RequestsManagement.IntegrationTests.Infrastructure;

internal static class HttpExtensions
{
    public static async Task<T> ReadDataAsync<T>(this HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<T>>(ApiFactory.Json);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success, envelope.Error);
        return envelope.Data!;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsJsonAsync(url, body, ApiFactory.Json);
}
