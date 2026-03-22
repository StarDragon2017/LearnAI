using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.ClientModel;
using OpenAI;

// 本地Ollama配置（兼容OpenAI API）
const string OLLAMA_BASE_URL = "http://localhost:11434/v1"; // Ollama的OpenAI兼容端点
const string MODEL_NAME = "qwen3.5:2b"; // 指定使用的模型
const string API_KEY = "ollama"; // Ollama不需要真实的API Key，但需要提供一个占位符

// 创建服务容器和配置OpenAI客户端（指向Ollama）
var services = new ServiceCollection();

services.AddSingleton(sp =>
{
    // 配置OpenAI客户端指向本地Ollama服务
    return new OpenAIClient(new ApiKeyCredential(API_KEY), new OpenAIClientOptions
    {
        Endpoint = new Uri(OLLAMA_BASE_URL)
    });
});

// 注册IChatClient
services.AddSingleton<IChatClient>(sp =>
{
    var openAIClient = sp.GetRequiredService<OpenAIClient>();
    return openAIClient.AsChatClient(MODEL_NAME);
});

var serviceProvider = services.BuildServiceProvider();
var chatClient = serviceProvider.GetRequiredService<IChatClient>();

Console.WriteLine("=== 检查服务状态 ===");
Console.WriteLine($"目标端点: {OLLAMA_BASE_URL}");
Console.WriteLine($"使用模型: {MODEL_NAME}");
Console.WriteLine("✓ 配置完成");
Console.WriteLine();

// 1. 基础对话示例
Console.WriteLine("=== 开始对话 ===");

try
{
    var messages = new List<ChatMessage>
    {
         new ChatMessage(ChatRole.User, "我叫小明，是一名程序员，我喜欢C#。")
        //new ChatMessage(ChatRole.User, "你好，请介绍一下你自己")
    };

    Console.WriteLine($"提问内容：{messages.FirstOrDefault().Text}");
    Console.WriteLine("=== AI 响应 ===");

    var response = await chatClient.CompleteAsync(messages);
    Console.WriteLine(response.Message.Text);
    Console.WriteLine();

    // 第二轮对话，但messages中只有当前这一条（没有历史）
    var messages2 = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, "我叫什么名字？")
    };
    Console.WriteLine($"提问内容：{messages2.FirstOrDefault().Text}");
    Console.WriteLine("=== AI 响应 ===");
    var response2 = await chatClient.CompleteAsync(messages2);
    Console.WriteLine($"AI: {response2.Message.Text}");

    Console.Read();
}
catch (Exception ex)
{
    Console.WriteLine($"✗ 请求失败: {ex.Message}");
    Console.WriteLine($"详细信息: {ex.StackTrace}");
    return;
}

// 2. 多轮对话示例
Console.WriteLine("=== 多轮对话示例 ===");

try
{
    var conversationHistory = new List<ChatMessage>
    {
        //new ChatMessage(ChatRole.System, "你是一个友好的C#编程助手。"),
        //new ChatMessage(ChatRole.User, "什么是.NET？"),
         new ChatMessage(ChatRole.User, "我叫小明，是一名程序员，我喜欢C#。")
    };

    // 第一轮
    Console.WriteLine("=== 第一轮 ===");

    var response1 = await chatClient.CompleteAsync(conversationHistory);
    Console.WriteLine($"提问内容：{conversationHistory.FirstOrDefault().Text}");
    Console.WriteLine($"AI: {response1.Message.Text}");

    // 将AI的回复加入历史
    conversationHistory.Add(new ChatMessage(ChatRole.Assistant, response1.Message.Text));

    // 第二轮
    Console.WriteLine("=== 第二轮 ===");
    conversationHistory.Add(new ChatMessage(ChatRole.User, "我叫什么名字？"));
    var response2 = await chatClient.CompleteAsync(conversationHistory);
    Console.WriteLine($"AI: {response2.Message.Text}");
    Console.Read();
}
catch (Exception ex)
{
    Console.WriteLine($"✗ 多轮对话失败: {ex.Message}");
}

// 3. 流式输出示例
Console.WriteLine("=== 流式输出示例 ===");

try
{
    var streamMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, "你好，你能帮我写一段简单的C#代码吗？")
    };

    Console.WriteLine("提问内容：你好，你能帮我写一段简单的C#代码吗？");
    Console.WriteLine("=== AI 实时响应 ===");

    await foreach (var update in chatClient.CompleteStreamingAsync(streamMessages))
    {
        if (update.Text is { Length: > 0 })
        {
            Console.Write(update.Text);
        }
    }
    Console.WriteLine("=== 流式输出完成 ===");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ 流式输出失败: {ex.Message}");
}

// 4. 带参数配置的对话示例
Console.WriteLine("=== 参数配置示例 ===");

try
{
    var options = new ChatOptions
    {
        Temperature = 0.7f,
        MaxOutputTokens = 500,
        TopP = 0.9f
    };

    var paramMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.User, "用一句话介绍人工智能")
    };

    Console.WriteLine($"提问内容：用一句话介绍人工智能");
    Console.WriteLine($"温度参数: {options.Temperature}");
    Console.WriteLine($"最大tokens: {options.MaxOutputTokens}");
    Console.WriteLine("=== AI 响应 ===");

    var paramResponse = await chatClient.CompleteAsync(paramMessages, options);
    Console.WriteLine(paramResponse.Message.Text);
}
catch (Exception ex)
{
    Console.WriteLine($"✗ 参数配置对话失败: {ex.Message}");
}

Console.WriteLine("=== 所有示例完成 ===");
