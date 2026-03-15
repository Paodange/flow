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
│       │   ├── RandomTool.cs     # 随机工具
│       │   ├── FileOperationTool.cs # 文件操作工具（读/写/删除/复制/移动/目录管理）
│       │   ├── WebRequestTool.cs    # 网络请求工具
│       │   ├── WebSearchTool.cs     # 网页搜索工具（DuckDuckGo）
│       │   ├── DatabaseQueryTool.cs # 内存数据库工具
│       │   └── SqliteDatabaseTool.cs# SQLite 数据库工具（持久化）
│       ├── LLM/                  # LLM 集成
│       │   ├── ILlmClient.cs     # LLM 客户端接口（含流式）
│       │   ├── LlmResponse.cs    # LLM 响应模型
│       │   └── OpenAiClient.cs   # OpenAI 兼容客户端
│       ├── Agent.cs              # 智能体核心类（含持久化/重试/并行工具）
│       └── AgentOrchestrator.cs  # 多智能体编排器
├── examples/
│   └── FlowAgent.Examples/      # 示例项目
│       └── Program.cs            # 演示程序（5个示例）
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

启动后直接进入交互式聊天模式，支持通过配置文件管理模型设置（Microsoft.Extensions.AI）：

```bash
export OPENAI_API_KEY=sk-...          # 必填
export OPENAI_API_BASE=https://...    # 可选，支持 DeepSeek / 通义千问等
export OPENAI_MODEL=gpt-4o-mini       # 可选，默认 gpt-4o-mini
dotnet run --project examples/FlowAgent.Examples/FlowAgent.Examples.csproj
```

启动后可直接提问，也支持以下内置命令：

| 命令 | 说明 |
|------|------|
| `/help` | 显示帮助信息 |
| `/tools` | 列出所有可用工具 |
| `/clear` | 清空对话历史，开始新对话 |
| `/history` | 查看对话历史统计 |
| `/save` | 保存对话历史到 JSON 文件 |
| `/exit` | 退出聊天 |

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

### 📁 FileOperationTool（文件操作）
读取、写入、追加、删除文件，创建目录，移动/复制文件，获取文件信息

```csharp
// 构造时可指定根目录（防止目录遍历）
var fileTool = new FileOperationTool("/safe/base/dir");
await agent.ExecuteToolAsync("file_operation",
    @"{""operation"": ""write"", ""path"": ""output.txt"", ""content"": ""Hello!""}");
// 创建目录
await agent.ExecuteToolAsync("file_operation",
    @"{""operation"": ""mkdir"", ""path"": ""logs""}");
// 复制文件
await agent.ExecuteToolAsync("file_operation",
    @"{""operation"": ""copy"", ""path"": ""output.txt"", ""destination"": ""backup/output.txt""}");
// 移动/重命名文件
await agent.ExecuteToolAsync("file_operation",
    @"{""operation"": ""move"", ""path"": ""output.txt"", ""destination"": ""archive/output.txt""}");
// 获取文件信息
await agent.ExecuteToolAsync("file_operation",
    @"{""operation"": ""info"", ""path"": ""output.txt""}");
```

### 🌐 WebRequestTool（网络请求）
发送 HTTP GET / POST 请求

```csharp
await agent.ExecuteToolAsync("web_request",
    @"{""url"": ""https://api.example.com/data"", ""method"": ""GET""}");
```

### 🔍 WebSearchTool（网页搜索）
使用 DuckDuckGo 搜索网络内容，返回摘要、直接答案和相关链接

```csharp
var searchTool = new WebSearchTool();
await agent.ExecuteToolAsync("web_search",
    @"{""query"": "".NET 10 新特性"", ""max_results"": 5}");
```

### 🗄️ DatabaseQueryTool（内存数据库）
操作内存表：创建表、插入、查询、删除数据

```csharp
// 创建表
await agent.ExecuteToolAsync("database_query",
    @"{""operation"": ""create_table"", ""table"": ""users"", ""columns"": [""name"", ""age""]}");
// 插入数据
await agent.ExecuteToolAsync("database_query",
    @"{""operation"": ""insert"", ""table"": ""users"", ""row"": {""name"": ""张三"", ""age"": ""28""}}");
// 查询（支持 where 过滤）
await agent.ExecuteToolAsync("database_query",
    @"{""operation"": ""select"", ""table"": ""users"", ""where"": {""age"": ""28""}}");
```

### 🗃️ SqliteDatabaseTool（SQLite 数据库）
持久化 SQLite 数据库：完整 CRUD 操作（含 UPDATE）和自定义 SQL 语句

```csharp
// 使用文件数据库（持久化）
var dbTool = new SqliteDatabaseTool("mydata.db");
// 或使用内存数据库（不持久化）
var dbTool = new SqliteDatabaseTool();

// 创建表（支持完整的列类型定义）
await agent.ExecuteToolAsync("sqlite_database",
    @"{""operation"": ""create_table"", ""table"": ""users"",
      ""columns"": [{""name"": ""id"", ""type"": ""INTEGER PRIMARY KEY AUTOINCREMENT""},
                   {""name"": ""name"", ""type"": ""TEXT""},
                   {""name"": ""age"", ""type"": ""INTEGER""}]}");
// 插入
await agent.ExecuteToolAsync("sqlite_database",
    @"{""operation"": ""insert"", ""table"": ""users"", ""row"": {""name"": ""张三"", ""age"": ""28""}}");
// 查询（支持 where 和 limit）
await agent.ExecuteToolAsync("sqlite_database",
    @"{""operation"": ""select"", ""table"": ""users"", ""where"": {""age"": ""28""}, ""limit"": 10}");
// 更新
await agent.ExecuteToolAsync("sqlite_database",
    @"{""operation"": ""update"", ""table"": ""users"", ""set"": {""age"": ""29""}, ""where"": {""name"": ""张三""}}");
// 自定义 SQL
await agent.ExecuteToolAsync("sqlite_database",
    @"{""operation"": ""execute_sql"", ""sql"": ""SELECT * FROM users ORDER BY age DESC""}");
```

## 当前实现的功能

- ✅ 基础消息模型（Message, MessageRole, ToolCall）
- ✅ 工具接口定义（ITool）
- ✅ 4个实用工具示例（Calculator, DateTime, TextProcess, Random）
- ✅ 智能体核心类（Agent）
- ✅ 对话历史管理（自动清理）
- ✅ 工具注册和执行
- ✅ 配置管理（AgentConfig）
- ✅ 完整的示例程序（5个演示）
- ✅ 详细的教程文档
- ✅ HTTP 客户端支持（OpenAiClient）
- ✅ OpenAI API 兼容接口（支持 DeepSeek、通义千问等）
- ✅ 流式响应（`ChatStreamAsync` / `StreamCompleteAsync`）
- ✅ 自动工具调用判断（`ChatAsync` LLM 驱动循环）
- ✅ 文件操作工具（FileOperationTool：读写/追加/删除/列目录/创建目录/复制/移动/文件信息）
- ✅ 网络请求工具（WebRequestTool）
- ✅ 网页搜索工具（WebSearchTool：DuckDuckGo 即时答案 API）
- ✅ 数据库查询工具（DatabaseQueryTool，内存表）
- ✅ SQLite 数据库工具（SqliteDatabaseTool：持久化、完整 CRUD、自定义 SQL）
- ✅ 对话历史持久化（`SaveHistoryAsync` / `LoadHistoryAsync`）
- ✅ 工具链式调用优化（支持并行工具执行）
- ✅ 多智能体协作（AgentOrchestrator + SubAgentTool）
- ✅ 错误处理和重试机制（指数退避重试策略）
- ✅ **插件化工具系统**（`PluginManager`：自动发现、热加载/卸载、工具启用/禁用，无需重启）

## 下一步计划

### 进阶特性
- [ ] 支持 Azure OpenAI 专属鉴权（API-Key Header 方式）
- [ ] 工具执行结果缓存
- [ ] 智能体状态快照与恢复

## 高级特性

### 🌊 流式响应
```csharp
await foreach (var chunk in agent.ChatStreamAsync("给我写一首小诗"))
{
    Console.Write(chunk);  // 实时打印每个 token
}
```

### 💾 对话历史持久化
```csharp
// 保存
await agent.SaveHistoryAsync("history.json");

// 加载（下次启动时恢复上下文）
await agent.LoadHistoryAsync("history.json");
```

### ⚡ 并行工具执行
```csharp
var config = new AgentConfig
{
    ParallelToolExecution = true  // 同一轮多工具并行执行
};
```

### 🤝 多智能体协作
```csharp
var orchestrator = new AgentOrchestrator();
orchestrator.RegisterAgent("math", mathAgent);
orchestrator.RegisterAgent("text", textAgent);

// 为协调者提供调用其他智能体的能力
coordinator.RegisterTool(orchestrator.CreateSubAgentTool("coordinator"));
```

### 🔄 错误处理和重试机制
```csharp
var config = new AgentConfig
{
    LlmMaxRetries = 3,      // 最多重试 3 次
    LlmRetryDelayMs = 1000  // 首次重试等待 1 秒（指数退避）
};
```

### 🔌 插件化工具系统

工具现在支持以**插件 DLL** 形式动态加载，无需重启即可热插拔，并可随时启用/禁用任意工具。

#### 创建插件

新建一个 `.NET` 类库项目，引用 `FlowAgent.Core` 并实现 `ITool`：

```csharp
// MyPlugin/MyTool.cs
public class MyTool : ITool
{
    public string Name => "my_tool";
    public string Description => "我的自定义工具";
    public string ParametersSchema => @"{ ""type"": ""object"", ""properties"": {} }";
    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
        => Task.FromResult("执行结果");
}
```

编译后将 `MyPlugin.dll` 放入插件目录即可自动加载，无需修改主程序。

#### 使用 PluginManager

```csharp
using FlowAgent.Core.Plugins;

// 创建插件管理器，监视 ./plugins 目录
var pluginManager = new PluginManager("./plugins");

// 发现工具时自动注册到智能体
pluginManager.ToolDiscovered += (info, tool) => agent.RegisterTool(tool);
// 插件卸载时自动注销工具
pluginManager.ToolRemoved    += (info, tool) => agent.UnregisterTool(tool.Name);

// 启动：扫描已有插件并开始监视目录（无阻塞）
await pluginManager.StartAsync();

// 将 MyPlugin.dll 放入 ./plugins —— 自动热加载，无需重启 ✨
```

#### 运行时启用 / 禁用工具

```csharp
// 禁用工具（工具仍注册，但不传递给 LLM）
agent.DisableTool("calculator");

// 重新启用工具
agent.EnableTool("calculator");

// 彻底注销工具
agent.UnregisterTool("calculator");

// 查看当前激活的工具列表
var active = agent.GetActiveTools();
```

#### 手动加载 / 卸载插件

```csharp
// 手动加载指定 DLL
await pluginManager.LoadPluginAsync("/path/to/MyPlugin.dll");

// 卸载（触发 ToolRemoved 事件，自动注销工具）
pluginManager.UnloadPlugin("/path/to/MyPlugin.dll");

// 热替换：先卸载旧版本，再加载新版本
await pluginManager.ReloadPluginAsync("/path/to/MyPlugin.dll");
```

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
