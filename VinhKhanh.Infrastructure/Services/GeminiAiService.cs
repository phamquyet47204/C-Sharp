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

        // Danh sach model thu tu de fallback neu gap loi 503 (Ban) hoac 429 (Het han muc)
        // Danh sach model thu tu de fallback
        var modelNames = new[] { "gemini-1.5-flash", "gemini-2.0-flash", "gemini-1.5-pro", "gemini-1.0-pro" };
        
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
                temperature = 0.4
            }
        };

        foreach (var modelName in modelNames)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}";
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            int maxRetries = 2;
            int delayMs = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.PostAsync(url, content, cancellationToken);
                    var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonDocument = JsonDocument.Parse(responseString);
                        var textResult = jsonDocument.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        if (string.IsNullOrWhiteSpace(textResult))
                            throw new InvalidOperationException("Gemini không trả về nội dung.");

                        var jsonPayload = ExtractJsonPayload(textResult);
                        var parsed = JsonSerializer.Deserialize<GeminiTranslationResult>(jsonPayload, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        return parsed ?? throw new InvalidOperationException("Lỗi parse JSON.");
                    }

                    // Neu loi 503 (Ban) hoac 429 (Quota), ta co the thu lai hoac doi model
                    if (((int)response.StatusCode == 503 || (int)response.StatusCode == 429) && attempt < maxRetries)
                    {
                        _logger.LogWarning("Model {model} dang ban (503) hoac het quota (429), thu lai lan {attempt}...", modelName, attempt);
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs *= 2;
                        continue;
                    }
                    
                    // Neu van loi sau khi thu lai, hoac la loi 404 (khong tim thay model) / 400 (model khong ho tro config), ta break de chuyen model tiep theo
                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 429 || (int)response.StatusCode == 404 || (int)response.StatusCode == 400)
                    {
                        _logger.LogWarning("Model {model} khong kha dung ({status}), dang thu chuyen sang model tiep theo...", modelName, response.StatusCode);
                        break; 
                    }

                    _logger.LogError("Gemini API Error ({model}): {statusCode} - {error}", modelName, response.StatusCode, responseString);
                    throw new InvalidOperationException($"Gemini API lỗi {(int)response.StatusCode} trên model {modelName}");
                }
                catch (Exception ex) when (attempt < maxRetries && ex is not InvalidOperationException)
                {
                    _logger.LogWarning(ex, "Loi ket noi Gemini model {model}, attempt {attempt}", modelName, attempt);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException("Tất cả các model Gemini bận. Vui lòng thử lại sau.");
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
