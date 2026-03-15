# 阿里云百炼平台 API 调用示例

本项目演示如何使用 C# HttpClient 调用阿里云百炼平台的聊天对话接口。

## 前置准备

1. 在 [阿里云百炼平台](https://bailian.console.aliyun.com/) 注册账号
2. 创建 API-KEY（Dashscope API-KEY）
3. 确保你的账户有足够的调用额度

## 配置说明

在 `Program.cs` 中修改以下配置：

```csharp
const string API_KEY = "your-api-key-here"; // 替换为你的 API Key
const string MODEL = "qwen-turbo";         // 选择模型
```

## 支持的模型

- `qwen-turbo` - 通义千问 Turbo（性价比高）
- `qwen-plus` - 通义千问 Plus（平衡性能）
- `qwen-max` - 通义千问 Max（最强性能）
- `qwen-max-longcontext` - 支持长上下文

## 请求参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| model | string | 模型名称 |
| messages | array | 对话消息列表 |
| temperature | float | 温度参数，控制随机性 (0-2) |
| top_p | float | 核采样参数 (0-1) |

## 响应字段说明

- `id`: 请求 ID
- `choices`: AI 回复列表
- `usage`: Token 使用统计
  - `prompt_tokens`: 提示词 Token 数
  - `completion_tokens`: 回复 Token 数
  - `total_tokens`: 总 Token 数

## 运行项目

```bash
dotnet run
```

## 注意事项

1. 请妥善保管 API Key，不要泄露
2. 注意 API 调用频率限制
3. 根据实际需求选择合适的模型
4. 生产环境建议添加重试机制和错误处理

## 参考文档

[阿里云百炼平台 API 文档](https://help.aliyun.com/zh/dashscope/)
