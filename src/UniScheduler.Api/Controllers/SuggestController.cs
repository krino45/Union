using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuggestController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public SuggestController(IHttpClientFactory factory, IConfiguration config)
    {
        _http    = factory.CreateClient();
        _apiKey  = config["YandexSuggestApiKey"] ?? string.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> Get(
        [FromQuery] string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3 || string.IsNullOrEmpty(_apiKey))
            return Ok(Array.Empty<string>());

        var url = $"https://suggest-maps.yandex.ru/v1/suggest" +
                  $"?apikey={_apiKey}" +
                  $"&text={Uri.EscapeDataString(text)}" +
                  $"&lang=ru_RU&results=5&types=geo,biz";

        try
        {
            var response = await _http.GetFromJsonAsync<YandexSuggestResponse>(url, ct);
            var results  = response?.Results?.Select(r =>
                string.IsNullOrEmpty(r.Subtitle?.Text)
                    ? r.Title.Text
                    : $"{r.Title.Text} ({r.Subtitle.Text})"
            ) ?? [];
            return Ok(results);
        }
        catch
        {
            return Ok(Array.Empty<string>());
        }
    }
}

internal record YandexSuggestResponse(
    [property: JsonPropertyName("results")] List<YandexSuggestItem>? Results);

internal record YandexSuggestItem(
    [property: JsonPropertyName("title")]    YandexTextField  Title,
    [property: JsonPropertyName("subtitle")] YandexTextField? Subtitle);

internal record YandexTextField(
    [property: JsonPropertyName("text")] string Text);
