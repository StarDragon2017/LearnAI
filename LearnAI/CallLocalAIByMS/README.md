# CallLocalAIByMS - 使用 Microsoft.Extensions.AI 调用本地大模型

## 项目说明

本项目演示如何使用 `Microsoft.Extensions.AI.OpenAI` 库通过 OpenAI 兼容 API 调用本地部署的 Ollama 大模型。

## 与 CallLocalAIApi 项目的对比

### CallLocalAIApi（原始 HttpClient 方式）
- **特点**: 直接使用 HttpClient 手动构建和解析 HTTP 请求
- **优点**: 
  - 完全控制 HTTP 请求的细节
  - 无需额外依赖
  - 适合理解和学习 API 原理
- **缺点**:
  - 需要手动管理数据模型
  - 错误处理较复杂
  - 代码量较大
  - 不易扩展到其他 AI 服务

### CallLocalAIByMS（Microsoft.Extensions.AI 方式）
- **特点**: 使用 Microsoft.Extensions.AI 提供的抽象层
- **优点**:
  - 代码简洁，易于理解
  - 统一的 API 接口，易于切换不同的 AI 服务
  - 内置流式输出支持
  - 类型安全
  - 更好的错误处理
  - 支持依赖注入
- **缺点**:
  - 需要学习额外的抽象概念
  - 依赖额外的 NuGet 包

## 核心代码对比

### 1. 初始化配置

**CallLocalAIApi**:
```csharp
const string OLLAMA_BASE_URL = "http://localhost:11434";
using var httpClient = new HttpClient { BaseAddress = new Uri(OLLAMA_BASE_URL) };
```

**CallLocalAIByMS**:
```csharp
const string OLLAMA_BASE_URL = "http://localhost:11434/v1"; // OpenAI 兼容端点
var openAIClient = new OpenAIClient(apiKey: "ollama", endpoint: new Uri(OLLAMA_BASE_URL));
var chatClient = openAIClient.AsChatClient(MODEL_NAME);
```

### 2. 发送请求

**CallLocalAIApi**:
```csharp
var requestBody = new OllamaChatRequest
{
    Model = MODEL_NAME,
    Messages = messages,
    Stream = false,
    Options = new OllamaOptions { Temperature = 0.7, TopP = 0.9 }
};
var jsonContent = JsonSerializer.Serialize(requestBody);
var response = await httpClient.PostAsync("/api/chat", content);
var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseString);
```

**CallLocalAIByMS**:
```csharp
var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "你好") };
var response = await chatClient.GetResponseAsync(messages);
Console.WriteLine(response.Message.Content);
```

### 3. 流式输出

**CallLocalAIApi**:
```csharp
using var stream = await streamResponse.Content.ReadAsStreamAsync();
using var reader = new StreamReader(stream);
while (!reader.EndOfStream) {
    var line = await reader.ReadLineAsync();
    var chunk = JsonSerializer.Deserialize<JsonElement>(line);
    if (chunk.TryGetProperty("response", out var response)) {
        Console.Write(response.GetString());
    }
}
```

**CallLocalAIByMS**:
```csharp
await foreach (var update in chatClient.GetStreamingResponseAsync(messages)) {
    if (update.Text is { Length: > 0 }) {
        Console.Write(update.Text);
    }
}
```

## 使用前准备

### 1. 安装 Ollama
访问 https://ollama.com 下载并安装 Ollama

### 2. 启动 Ollama 服务
```bash
ollama serve
```

### 3. 拉取模型（如果还没有）
```bash
ollama pull qwen3.5:2b
```

## 配置说明

### 关键配置项
- `OLLAMA_BASE_URL`: Ollama 服务地址
  - 原始 API: `http://localhost:11434`
  - OpenAI 兼容 API: `http://localhost:11434/v1`
- `MODEL_NAME`: 使用的模型名称
- `API_KEY`: Ollama 不需要真实 API Key，但需提供占位符

## 依赖包

```xml
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="1.0.0-preview.1.25063.1" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0-preview.1.25064.1" />
```

## 运行项目

```bash
dotnet run
```

## 示例功能

本程序包含以下四个示例：

1. **基础对话示例**: 单轮对话，获取完整响应
2. **多轮对话示例**: 维护对话历史，实现上下文连续性
3. **流式输出示例**: 实时显示 AI 的生成过程
4. **参数配置示例**: 自定义温度、最大 tokens 等参数

## 技术要点

### IChatClient 接口
Microsoft.Extensions.AI 的核心抽象，提供统一的大模型调用接口：
- `GetResponseAsync()`: 获取完整响应
- `GetStreamingResponseAsync()`: 获取流式响应

### ChatMessage 类
表示对话消息：
- `Role`: 角色
- `Content`: 消息内容

### ChatOptions 类
配置生成参数：
- `Temperature`: 随机性（0-1）
- `MaxTokens`: 最大生成 token 数
- `TopP`: 核采样参数

## 扩展性

使用 Microsoft.Extensions.AI 的优势在于可以轻松切换不同的 AI 服务提供商：

```csharp
// 切换到 Azure OpenAI
var azureClient = new AzureOpenAIClient(endpoint, credential);

// 切换到 OpenAI
var openAIClient = new OpenAIClient(apiKey);

// 都使用相同的 IChatClient 接口
var chatClient = openAIClient.AsChatClient(MODEL_NAME);
```

## 注意事项

1. 确保 Ollama 服务已启动
2. 确保模型已下载
3. OpenAI 兼容端点需要 Ollama 0.1.32 或更高版本
4. Microsoft.Extensions.AI 目前处于预览阶段，API 可能会有变化