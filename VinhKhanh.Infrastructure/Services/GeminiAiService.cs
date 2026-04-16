using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VinhKhanh.Infrastructure.Services;

public class GeminiAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"]?.Trim() ?? string.Empty;
        _logger = logger;
    }

    public async Task<GeminiTranslationResult?> GenerateTranslationsAsync(string vietnameseName, string vietnameseDescription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình Gemini:ApiKey trong appsettings hoặc biến môi trường.");
        }

        // Chuyển sang gemini-2.5-flash vì Google đã nâng cấp model
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

        var prompt = $@"
Bạn là một trợ lý ảo chuyên dịch thuật dữ liệu du lịch về Phố Ẩm Thực Vĩnh Khánh.
Dựa vào tên và mô tả được cung cấp bằng tiếng Việt, hãy dịch chúng sang tiếng Anh (EN) và tiếng Nhật (JA) với văn phong hấp dẫn, tự nhiên, và chuyên nghiệp.

Dữ liệu đầu vào:
Tên quán (VI): {vietnameseName}
Mô tả (VI): {vietnameseDescription}

Vui lòng TRẢ VỀ ĐÚNG ĐỊNH DẠNG JSON MÀ KHÔNG CÓ BẤT KỲ VĂN BẢN NÀO KHÁC BÊN NGOÀI, không dùng markdown ```json:
{{
  ""en"": {{
    ""name"": ""English Name"",
    ""description"": ""English Description""
  }},
  ""ja"": {{
    ""name"": ""Japanese Name"",
    ""description"": ""Japanese Description""
  }}
}}
";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                responseMimeType = "application/json"
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API Error: {statusCode} - {error}", response.StatusCode, responseString);
                throw new InvalidOperationException($"Gemini API lỗi {(int)response.StatusCode}: {responseString}");
            }

            var jsonDocument = JsonDocument.Parse(responseString);
            var textResult = jsonDocument.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(textResult))
            {
                throw new InvalidOperationException("Gemini không trả về nội dung dịch.");
            }

            var jsonPayload = ExtractJsonPayload(textResult);
            var parsed = JsonSerializer.Deserialize<GeminiTranslationResult>(jsonPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                throw new InvalidOperationException("Không parse được JSON dịch thuật từ Gemini.");
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Gemini API");
            throw;
        }
    }

    private static string ExtractJsonPayload(string rawText)
    {
        var trimmed = rawText.Trim();

        if (trimmed.StartsWith("```") && trimmed.EndsWith("```"))
        {
            trimmed = Regex.Replace(trimmed, "^```(?:json)?\\s*", string.Empty, RegexOptions.IgnoreCase);
            trimmed = Regex.Replace(trimmed, "\\s*```$", string.Empty, RegexOptions.IgnoreCase);
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }
}

public class GeminiTranslationResult
{
    public TranslationData En { get; set; } = new();
    public TranslationData Ja { get; set; } = new();
}

public class TranslationData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
