using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// 本地Ollama配置
const string OLLAMA_BASE_URL = "http://localhost:11434"; // Ollama默认端口
const string MODEL_NAME = "qwen3.5:2b"; // 指定使用的模型

// 创建HttpClient
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(OLLAMA_BASE_URL)
};
httpClient.DefaultRequestHeaders.Add("User-Agent", "CallLocalAIApi/1.0");

// 1. 检查Ollama服务是否运行
Console.WriteLine("正在检查Ollama服务状态...");
try
{
    var response = await httpClient.GetAsync("/api/tags");
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"无法连接到Ollama服务 (状态码: {response.StatusCode})");
        Console.WriteLine("请确保Ollama服务已启动，运行命令: ollama serve");
        return;
    }
    Console.WriteLine("✓ Ollama服务运行正常");
}
catch (HttpRequestException)
{
    Console.WriteLine("✗ 无法连接到Ollama服务");
    Console.WriteLine("请确保Ollama已安装并运行: ollama serve");
    return;
}

// 2. 检查模型是否存在
Console.WriteLine($"正在检查模型 {MODEL_NAME} 是否存在...");
var tagsResponse = await httpClient.GetAsync("/api/tags");
var tagsJson = await tagsResponse.Content.ReadAsStringAsync();
var tagsResult = JsonSerializer.Deserialize<OllamaTagsResponse>(tagsJson);

var modelExists = tagsResult?.Models?.Any(m => m.Name == MODEL_NAME) ?? false;
if (!modelExists)
{
    Console.WriteLine($"✗ 模型 {MODEL_NAME} 不存在");
    Console.WriteLine($"请先拉取模型: ollama pull {MODEL_NAME}");
    return;
}
Console.WriteLine($"✓ 模型 {MODEL_NAME} 已安装");

// 3. 创建聊天请求
Console.WriteLine("=== 开始对话 ===");

var messages = new[]
{
    new OllamaMessage
    {
        Role = "user",
        Content = "你好，请介绍一下你自己"
    }
};

var requestBody = new OllamaChatRequest
{
    Model = MODEL_NAME,
    Messages = messages,
    Stream = false, // 不使用流式输出
    Options = new OllamaOptions
    {
        Temperature = 0.7,
        TopP = 0.9,
        NumPredict = 2000 // 最大生成token数
    }
};

try
{
    Console.WriteLine("正在发送请求到Ollama...");

    var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
    var chatResponse = await httpClient.PostAsync("/api/chat", content);
    var chatResponseString = await chatResponse.Content.ReadAsStringAsync();

    if (chatResponse.IsSuccessStatusCode)
    {
        var chatResult = JsonSerializer.Deserialize<OllamaChatResponse>(chatResponseString);

        if (chatResult?.Message?.Content != null)
        {
            Console.WriteLine("=== AI 响应 ===");
            Console.WriteLine(chatResult.Message.Content);
            Console.WriteLine($"=== 模型信息 ===");
            Console.WriteLine($"模型: {chatResult.Model}");
            Console.WriteLine($"创建时间: {chatResult.CreatedAt}");
            Console.WriteLine($"完成原因: {chatResult.DoneReason}");

            if (chatResult.EvalCount > 0)
            {
                Console.WriteLine($"=== 性能统计 ===");
                Console.WriteLine($"评估tokens: {chatResult.EvalCount}");
                Console.WriteLine($"评估耗时: {chatResult.EvalDuration}ms");
                if (chatResult.PromptEvalCount > 0)
                {
                    Console.WriteLine($"提示tokens: {chatResult.PromptEvalCount}");
                    Console.WriteLine($"提示耗时: {chatResult.PromptEvalDuration}ms");
                    Console.WriteLine($"总耗时: {chatResult.TotalDuration}ms");
                }
            }
        }
    }
    else
    {
        Console.WriteLine($"✗ 请求失败: {chatResponse.StatusCode}");
        Console.WriteLine($"错误信息: {chatResponseString}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ 发生异常: {ex.Message}");
    Console.WriteLine($"详细信息: {ex.StackTrace}");
}

// 4. 流式输出示例（使用 /api/generate 接口）
Console.WriteLine("=== 开始流式对话 ===");

var streamRequestBody = new
{
    model = MODEL_NAME,
    prompt = "你好，你能帮我写一段C#代码吗？",
    stream = true
};

try
{
    Console.WriteLine("正在发送流式请求到Ollama...");
    Console.WriteLine($"提问内容：{streamRequestBody.prompt}");
    Console.WriteLine("=== AI 实时响应 ===");

    var streamJsonContent = JsonSerializer.Serialize(streamRequestBody, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var streamContent = new StringContent(streamJsonContent, Encoding.UTF8, "application/json");

    // 使用 SendAsync + HttpCompletionOption.ResponseHeadersRead 实现真正的流式处理
    // 这样可以立即获取响应头，然后边接收数据边处理，而不必等待整个响应完成
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
    {
        Content = streamContent
    };
    using var streamResponse = await httpClient.SendAsync(request,
        HttpCompletionOption.ResponseHeadersRead);

    if (streamResponse.IsSuccessStatusCode)
    {
        using var stream = await streamResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                // 解析每一行JSON数据（包含 thinking 和 response 字段）
                var chunk = JsonSerializer.Deserialize<JsonElement>(line);

                // 优先输出 response 字段（实际回答内容）
                if (chunk.TryGetProperty("response", out var response) && 
                    response.ValueKind != JsonValueKind.Null &&
                    response.GetString() != "")
                {
                    Console.Write(response.GetString());
                }

                // 检查是否完成
                if (chunk.TryGetProperty("done", out var done) && done.GetBoolean())
                {
                    Console.WriteLine($"=== 流式输出完成 ===");
                    if (chunk.TryGetProperty("eval_count", out var evalCount))
                    {
                        Console.WriteLine($"总生成tokens: {evalCount.GetInt32()}");
                    }
                    if (chunk.TryGetProperty("total_duration", out var totalDuration))
                    {
                        Console.WriteLine($"总耗时: {totalDuration.GetInt64() / 1_000_000}ms");
                    }
                    break;
                }
            }
            catch (JsonException)
            {
                // 忽略解析错误，继续处理下一行
                continue;
            }
        }
    }
    else
    {
        Console.WriteLine($"✗ 流式请求失败: {streamResponse.StatusCode}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ 流式输出发生异常: {ex.Message}");
}
           

// Ollama API 数据模型
class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public OllamaModel[]? Models { get; set; }
}

class OllamaModel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("modified_at")]
    public DateTime? ModifiedAt { get; set; }
    
    [JsonPropertyName("size")]
    public long? Size { get; set; }
}

class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("messages")]
    public OllamaMessage[]? Messages { get; set; }
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
    
    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

class OllamaMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }
    
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalData { get; set; }
}

class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
    
    [JsonPropertyName("top_p")]
    public double TopP { get; set; }
    
    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; }
}

class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }
    
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
    
    [JsonPropertyName("done")]
    public bool? Done { get; set; }
    
    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }
    
    [JsonPropertyName("context")]
    public int[]? Context { get; set; }
    
    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }
    
    [JsonPropertyName("prompt_eval_duration")]
    public long? PromptEvalDuration { get; set; }
    
    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }
    
    [JsonPropertyName("eval_duration")]
    public long? EvalDuration { get; set; }
}
