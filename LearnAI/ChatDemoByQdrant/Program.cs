using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Net.Http.Json;

// ---------- 加载配置 ----------
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var dashScopeApiKey = configuration["DashScope:ApiKey"];
var qdrantHost = configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.TryParse(configuration["Qdrant:Port"], out var port) ? port : 6333;

// ---------- 初始化 ----------
var qdrantClient = new QdrantClient(host: qdrantHost, port: (ushort)qdrantPort);
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {dashScopeApiKey}");

// 辅助方法：调用阿里云百炼嵌入API
async Task<float[]> GetEmbedding(string text)
{
    var requestBody = new
    {
        model = "text-embedding-v3",
        input = new
        {
            texts = new[] { text }
        },
        parameters = new
        {
            text_type = "document"
        }
    };

    var response = await httpClient.PostAsJsonAsync("https://dashscope.aliyuncs.com/api/v1/services/embeddings/text-embedding/text-embedding", requestBody);
    var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
    var embeddings = jsonResponse.GetProperty("output").GetProperty("embeddings")[0].GetProperty("embedding");
    var result = new float[embeddings.GetArrayLength()];
    for (int i = 0; i < result.Length; i++)
    {
        result[i] = embeddings[i].GetSingle();
    }
    return result;
}

// 辅助方法：调用阿里云百炼聊天API
async Task<string> ChatAsync(string systemPrompt, string userQuestion)
{
    var requestBody = new
    {
        model = "qwen-turbo",
        input = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userQuestion }
            }
        }
    };

    var response = await httpClient.PostAsJsonAsync("https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation", requestBody);
    var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
    return jsonResponse.GetProperty("output").GetProperty("text").GetString() ?? string.Empty;
}

// ---------- 1. 创建集合（如果不存在） ----------
const string collectionName = "knowledge_base";
const int vectorDimension = 1024; // 阿里云百炼 text-embedding-v3 模型的向量维度
try
{
    await qdrantClient.CreateCollectionAsync(
        collectionName: collectionName,
        vectorsConfig: new VectorParams
        {
            Size = vectorDimension,
            Distance = Distance.Cosine
        }
    );
    Console.WriteLine("集合已创建");
}
catch (Exception ex)
{
    // 集合可能已存在，忽略异常
    Console.WriteLine("集合可能已存在，继续...");
}

// ---------- 2. 准备文档并插入 ----------
var documents = new List<(string Id, string Text)>
{
    ("doc1", "纬星智能科技成立于 2021 年，总部位于武汉。"),
    ("doc2", "AI 代码助手支持 C#、Python、Java 等语言，提供代码补全和错误检测。"),
    ("doc3", "客服联系方式：support@wistarits.com，工作时间 9:00-18:00。")
};

var points = new List<PointStruct>();
ulong idCounter = 1;
foreach (var doc in documents)
{
    var vector = await GetEmbedding(doc.Text);
    var point = new PointStruct
    {
        Id = idCounter++,
        Vectors = vector,
        Payload = { { "text", doc.Text } }
    };
    points.Add(point);
}
await qdrantClient.UpsertAsync(collectionName, points);
Console.WriteLine($"已插入 {points.Count} 条知识");

// ---------- 3. 用户提问并检索 ----------
string userQuestion = "你们的联系方式是什么？";
var questionVector = await GetEmbedding(userQuestion);

var searchResult = await qdrantClient.SearchAsync(
    collectionName: collectionName,
    vector: questionVector,
    limit: 2
);

var contexts = new List<string>();
foreach (var point in searchResult)
{
    if (point.Payload.TryGetValue("text", out var textObj))
        contexts.Add(textObj.ToString());
}
string context = string.Join("\n", contexts);

Console.WriteLine($"检索到的知识：\n{context}\n");

// ---------- 4. 调用大模型生成回答 ----------
var systemPrompt = $@"你是一个客服助手。请基于以下知识回答用户的问题。如果知识里没有相关信息，就说不知道。
知识库内容：
{context}";

string answer = await ChatAsync(systemPrompt, userQuestion);
Console.WriteLine($"AI 回答：{answer}");
Console.ReadLine();
