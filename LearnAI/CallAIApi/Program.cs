using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// 阿里云百炼平台配置
const string API_KEY =  "your-api-key-here"; // 替换为你的API Key
const string API_URL = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation";

// 创建HttpClient
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
//httpClient.DefaultRequestHeaders.Add("User-Agent", "CallAIApi/1.0");

// 创建请求
var requestBody = new
{
    model = "qwen-plus", // 可选: qwen-turbo, qwen-plus, qwen-max 等
    input = new
    {
        messages = new[]
        {
            //new
            //{
            //    role = "system",
            //    content = "你是一个专业的C#编程助手，回答问题简洁明了。"
            //},
            new
            {
                role = "user",
                content = "你好，请介绍一下你自己"
            }
        }
    },
    parameters = new
    {
        temperature = 0.7,
        top_p = 0.8,
        max_tokens = 1500
    }
};

try
{
    Console.WriteLine($"用户提出内容：{requestBody.input.messages.FirstOrDefault().content}\n");

    Console.WriteLine("正在调用阿里云百炼平台API...\n");

    // 发送请求
    var jsonContent = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
    
    var response = await httpClient.PostAsync(API_URL, content);
    var responseString = await response.Content.ReadAsStringAsync();

    Console.WriteLine("阿里云返回内容:\n");
    Console.WriteLine(responseString);
    if (response.IsSuccessStatusCode)
    {
        // 解析响应
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        var result = JsonSerializer.Deserialize<ApiResponse>(responseString, options);
        
        if (result?.Output?.Text != null && result.Output.Text.Length > 0)
        {
            Console.WriteLine("=== AI 响应 ===");
            Console.WriteLine(result.Output.Text);
            Console.WriteLine("\n=== 响应详情 ===");
            Console.WriteLine($"模型: {result.Model}");
            Console.WriteLine($"Token使用: {result.Usage.TotalTokens} (输入: {result.Usage.InputTokens}, 输出: {result.Usage.OutputTokens})");
        }
    }
    else
    {
        Console.WriteLine($"请求失败: {response.StatusCode}");
        Console.WriteLine($"错误信息: {responseString}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"发生异常: {ex.Message}");
    Console.WriteLine($"详细信息: {ex.StackTrace}");
}
Console.ReadLine();

// API响应模型
class ApiResponse
{
    [JsonPropertyName("output")]
    public Output? Output { get; set; }

    [JsonPropertyName("usage")]
    public Usage? Usage { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

class Output
{
    [JsonPropertyName("choices")]
    public Choice[]? Choices { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

class Choice
{
    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

class Message
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

class Usage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}