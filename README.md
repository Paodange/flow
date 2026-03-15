# FlowAgent - 从零开始构建智能体

这是一个循序渐进的教程项目，展示如何使用 C# 和 .NET 10 从零开始构建一个智能体系统。

> 📚 **完整教程**：查看 [docs/TUTORIAL.md](docs/TUTORIAL.md) 了解详细的开发指南

## ✨ 特性

- 🎯 **简单易懂**：从零开始，循序渐进
- 🔧 **工具系统**：可扩展的工具框架
- 💬 **对话管理**：自动管理对话历史
- 📦 **开箱即用**：包含多个实用工具示例
- 🚀 **现代化**：使用 .NET 10 和 C# 13

## 项目结构

```
flow/
├── src/
│   └── FlowAgent.Core/          # 核心类库
│       ├── Models/               # 数据模型
│       │   └── Message.cs        # 消息模型
│       ├── Tools/                # 工具系统
│       │   ├── ITool.cs          # 工具接口
│       │   ├── CalculatorTool.cs # 计算器工具
│       │   ├── DateTimeTool.cs   # 日期时间工具
│       │   ├── TextProcessTool.cs# 文本处理工具
│       │   └── RandomTool.cs     # 随机工具
│       └── Agent.cs              # 智能体核心类
├── examples/
│   └── FlowAgent.Examples/      # 示例项目
│       └── Program.cs            # 演示程序（3个示例）
└── docs/
    └── TUTORIAL.md               # 详细教程文档
```

## 核心概念

### 1. Message（消息）

消息是对话系统的基本单元，包含以下属性：
- **Role（角色）**：System（系统）、User（用户）、Assistant（助手）、Tool（工具）
- **Content（内容）**：消息的文本内容
- **ToolCalls（工具调用）**：如果消息包含工具调用请求
- **ToolCallId（工具调用ID）**：如果消息是工具执行结果

### 2. Tool（工具）

工具是智能体可以调用的功能，通过 `ITool` 接口定义：
- **Name**：工具的唯一名称
- **Description**：工具的功能描述
- **ParametersSchema**：参数的 JSON Schema 定义
- **ExecuteAsync**：异步执行方法

### 3. Agent（智能体）

智能体是核心类，负责：
- 管理对话历史
- 注册和调用工具
- 维护配置信息
- 处理消息流

## 快速开始

### 前置要求

- .NET 10 SDK
- Visual Studio 2022 或 VS Code

### 构建项目

```bash
dotnet build
```

### 运行示例

```bash
dotnet run --project examples/FlowAgent.Examples/FlowAgent.Examples.csproj
```

示例程序包含三个演示：
1. **基础智能体** - 使用计算器工具进行数学运算
2. **多工具智能体** - 展示多个工具的协同使用
3. **对话历史管理** - 演示多轮对话和历史记录

## 示例代码

```csharp
// 1. 创建智能体
var config = new AgentConfig
{
    Name = "数学助手",
    SystemPrompt = "你是一个数学助手，可以帮助用户解决数学问题。",
    EnableTools = true
};

var agent = new Agent(config);

// 2. 注册工具
agent.RegisterTool(new CalculatorTool());
agent.RegisterTool(new DateTimeTool());
agent.RegisterTool(new TextProcessTool());
agent.RegisterTool(new RandomTool());

// 3. 添加用户消息
agent.AddUserMessage("请帮我计算 25 + 17");

// 4. 执行工具调用
var result = await agent.ExecuteToolAsync(
    "calculator",
    @"{""operation"": ""add"", ""a"": 25, ""b"": 17}"
);

// 5. 添加助手回复
agent.AddAssistantMessage($"根据计算结果，25 + 17 = 42");
```

## 内置工具

### 🔢 CalculatorTool（计算器）
执行基本的数学运算：加、减、乘、除

```csharp
await agent.ExecuteToolAsync("calculator",
    @"{""operation"": ""add"", ""a"": 25, ""b"": 17}");
```

### ⏰ DateTimeTool（日期时间）
获取当前时间、日期计算、格式化

```csharp
await agent.ExecuteToolAsync("datetime",
    @"{""action"": ""add_days"", ""days"": 7}");
```

### 📝 TextProcessTool（文本处理）
文本转换、长度计算、单词统计

```csharp
await agent.ExecuteToolAsync("text_process",
    @"{""operation"": ""upper"", ""text"": ""hello""}");
```

### 🎲 RandomTool（随机工具）
生成随机数、随机选择

```csharp
await agent.ExecuteToolAsync("random",
    @"{""type"": ""choice"", ""choices"": [""A"", ""B"", ""C""]}");
```

## 当前实现的功能

- ✅ 基础消息模型（Message, MessageRole, ToolCall）
- ✅ 工具接口定义（ITool）
- ✅ 4个实用工具示例（Calculator, DateTime, TextProcess, Random）
- ✅ 智能体核心类（Agent）
- ✅ 对话历史管理（自动清理）
- ✅ 工具注册和执行
- ✅ 配置管理（AgentConfig）
- ✅ 完整的示例程序（3个演示）
- ✅ 详细的教程文档

## 下一步计划

### 集成大语言模型（LLM）
- [ ] 添加 HTTP 客户端支持
- [ ] 实现与 OpenAI API 兼容的接口
- [ ] 支持流式响应
- [ ] 自动工具调用判断

### 更多工具
- [ ] 文件操作工具
- [ ] 网络请求工具
- [ ] 数据库查询工具

### 高级特性
- [ ] 对话历史持久化
- [ ] 工具链式调用优化
- [ ] 多智能体协作
- [ ] 错误处理和重试机制

## 创建自定义工具

创建自己的工具非常简单：

```csharp
public class MyCustomTool : ITool
{
    public string Name => "my_tool";
    public string Description => "我的自定义工具";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""param"": {""type"": ""string"", ""description"": ""参数说明""}
        },
        ""required"": [""param""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        // 实现你的逻辑
        return Task.FromResult("执行结果");
    }
}
```

查看 [docs/TUTORIAL.md](docs/TUTORIAL.md) 了解更多细节。

## 学习目标

通过这个项目，你将学习：
1. 智能体的基本架构和设计模式
2. 如何定义和实现工具系统
3. 对话历史的管理
4. 如何集成大语言模型（下一阶段）
5. 异步编程和错误处理
6. C# 现代特性的应用

## 技术栈

- .NET 10
- C# 13
- System.Text.Json（JSON 序列化）
- 异步编程（async/await）

## 资源

- 📚 [完整教程](docs/TUTORIAL.md)
- 💻 [示例代码](examples/FlowAgent.Examples/Program.cs)
- 🔧 [工具示例](src/FlowAgent.Core/Tools/)

## 许可证

MIT License
