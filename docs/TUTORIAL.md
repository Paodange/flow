# 智能体开发教程 - 循序渐进

本教程将一步一步地带你了解如何从零开始开发一个智能体系统。

## 目录

1. [智能体是什么](#1-智能体是什么)
2. [核心组件详解](#2-核心组件详解)
3. [创建你的第一个智能体](#3-创建你的第一个智能体)
4. [开发自定义工具](#4-开发自定义工具)
5. [高级特性](#5-高级特性)

## 1. 智能体是什么？

智能体（Agent）是一个能够感知环境、做出决策并采取行动的自主实体。在AI领域，智能体通常是一个可以：

- 📨 **接收输入**：理解用户的需求和问题
- 🤔 **思考决策**：分析问题，决定使用什么工具或方法
- 🔧 **调用工具**：执行具体的任务（计算、查询、处理数据等）
- 💬 **生成回复**：向用户提供答案和反馈

### 智能体的基本工作流程

```
用户输入 → 智能体理解 → 决定行动 → 调用工具 → 生成回复 → 用户
   ↑                                                    ↓
   └──────────────── 可能的多轮对话 ─────────────────────┘
```

## 2. 核心组件详解

### 2.1 Message（消息）

消息是对话的基本单位。每条消息都有：

```csharp
public class Message
{
    public string Id { get; set; }           // 唯一标识
    public MessageRole Role { get; set; }    // 角色（System/User/Assistant/Tool）
    public string Content { get; set; }      // 消息内容
    public DateTime CreatedAt { get; set; }  // 创建时间
    public List<ToolCall>? ToolCalls { get; set; }  // 工具调用信息
}
```

**消息角色说明：**

- **System（系统）**：定义智能体的行为规则和身份
  ```
  示例："你是一个数学助手，专门帮助用户解决数学问题。"
  ```

- **User（用户）**：用户的输入和问题
  ```
  示例："请帮我计算 25 + 17"
  ```

- **Assistant（助手）**：智能体的回复
  ```
  示例："根据计算，25 + 17 = 42"
  ```

- **Tool（工具）**：工具执行的结果
  ```
  示例："计算结果: 25 + 17 = 42"
  ```

### 2.2 Tool（工具）

工具是智能体可以调用的功能。每个工具必须实现 `ITool` 接口：

```csharp
public interface ITool
{
    string Name { get; }              // 工具名称（唯一标识）
    string Description { get; }       // 工具功能描述
    string ParametersSchema { get; }  // 参数定义（JSON Schema）

    Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken);
}
```

**为什么需要 ParametersSchema？**

参数 Schema 使用 JSON Schema 格式，告诉智能体：
- 工具需要什么参数
- 参数的类型和格式
- 哪些参数是必需的
- 参数的有效值范围

示例：
```json
{
  "type": "object",
  "properties": {
    "operation": {
      "type": "string",
      "enum": ["add", "subtract", "multiply", "divide"],
      "description": "要执行的运算类型"
    },
    "a": {
      "type": "number",
      "description": "第一个数字"
    },
    "b": {
      "type": "number",
      "description": "第二个数字"
    }
  },
  "required": ["operation", "a", "b"]
}
```

### 2.3 Agent（智能体）

智能体是核心类，负责协调所有组件：

```csharp
public class Agent
{
    // 配置信息
    public AgentConfig Config { get; }

    // 对话历史
    public IReadOnlyList<Message> ConversationHistory { get; }

    // 已注册的工具
    public IReadOnlyDictionary<string, ITool> Tools { get; }

    // 核心方法
    public void RegisterTool(ITool tool);                    // 注册工具
    public void AddUserMessage(string content);              // 添加用户消息
    public void AddAssistantMessage(string content);         // 添加助手消息
    public Task<string> ExecuteToolAsync(string toolName, string arguments);  // 执行工具
}
```

## 3. 创建你的第一个智能体

### 步骤 1：创建智能体实例

```csharp
// 配置智能体
var config = new AgentConfig
{
    Name = "数学助手",
    SystemPrompt = "你是一个数学助手，可以帮助用户解决数学问题。",
    EnableTools = true,
    MaxHistoryLength = 100  // 最多保留100条历史消息
};

// 创建智能体
var agent = new Agent(config);
```

### 步骤 2：注册工具

```csharp
// 注册计算器工具
agent.RegisterTool(new CalculatorTool());

// 可以注册多个工具
agent.RegisterTool(new DateTimeTool());
agent.RegisterTool(new TextProcessTool());
```

### 步骤 3：处理用户输入

```csharp
// 用户输入
string userInput = "请帮我计算 25 + 17";
agent.AddUserMessage(userInput);

// 智能体决策：需要使用计算器工具
// （在真实场景中，这个决策由LLM完成）

// 执行工具
var result = await agent.ExecuteToolAsync(
    "calculator",
    @"{""operation"": ""add"", ""a"": 25, ""b"": 17}"
);

// 生成回复
agent.AddAssistantMessage($"根据计算结果，25 + 17 = 42");
```

### 步骤 4：查看对话历史

```csharp
// 获取统计信息
Console.WriteLine(agent.GetHistorySummary());

// 遍历所有消息
foreach (var message in agent.ConversationHistory)
{
    Console.WriteLine($"[{message.Role}] {message.Content}");
}
```

## 4. 开发自定义工具

### 4.1 工具开发模板

```csharp
using System.Text.Json;

public class MyCustomTool : ITool
{
    // 1. 定义工具名称（必须唯一）
    public string Name => "my_tool";

    // 2. 描述工具功能（告诉AI这个工具是干什么的）
    public string Description => "这个工具的功能描述";

    // 3. 定义参数结构（使用JSON Schema）
    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""param1"": {
                ""type"": ""string"",
                ""description"": ""参数1的描述""
            }
        },
        ""required"": [""param1""]
    }";

    // 4. 实现执行逻辑
    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            // 解析参数
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var args = JsonSerializer.Deserialize<MyArgs>(arguments, options);

            // 执行业务逻辑
            var result = DoSomething(args);

            // 返回结果
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    // 5. 定义参数类
    private class MyArgs
    {
        public string Param1 { get; set; } = string.Empty;
    }

    private string DoSomething(MyArgs args)
    {
        // 你的业务逻辑
        return "执行结果";
    }
}
```

### 4.2 实战示例：天气查询工具

```csharp
public class WeatherTool : ITool
{
    public string Name => "weather";

    public string Description => "查询指定城市的天气信息";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""city"": {
                ""type"": ""string"",
                ""description"": ""城市名称，如：北京、上海""
            }
        },
        ""required"": [""city""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<WeatherArgs>(arguments, options);

            if (args == null || string.IsNullOrEmpty(args.City))
            {
                return Task.FromResult("错误: 请提供城市名称");
            }

            // 模拟天气查询（实际应调用天气API）
            var weather = new
            {
                City = args.City,
                Temperature = new Random().Next(-10, 35),
                Condition = new[] { "晴天", "多云", "阴天", "小雨" }[new Random().Next(4)]
            };

            return Task.FromResult(
                $"{weather.City}的天气：{weather.Condition}，温度 {weather.Temperature}°C"
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    private class WeatherArgs
    {
        public string City { get; set; } = string.Empty;
    }
}
```

## 5. 高级特性

### 5.1 对话历史管理

智能体会自动管理对话历史，并在达到最大长度时自动清理旧消息：

```csharp
var config = new AgentConfig
{
    MaxHistoryLength = 50  // 最多保留50条消息
};

var agent = new Agent(config);

// 添加很多消息后，旧消息会自动被清理
// 但系统消息会被保留
```

### 5.2 手动清理历史

```csharp
// 清空对话历史（保留系统消息）
agent.ClearHistory();
```

### 5.3 工具链式调用

智能体可以在一次对话中调用多个工具：

```csharp
// 1. 用户询问
agent.AddUserMessage("今天是几号？7天后是几号？");

// 2. 调用日期工具获取当前日期
var currentDate = await agent.ExecuteToolAsync("datetime", @"{""action"": ""current""}");

// 3. 调用日期工具计算7天后
var futureDate = await agent.ExecuteToolAsync("datetime", @"{""action"": ""add_days"", ""days"": 7}");

// 4. 生成综合回复
agent.AddAssistantMessage($"当前日期：{currentDate}，7天后：{futureDate}");
```

### 5.4 错误处理

工具应该优雅地处理错误：

```csharp
public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
{
    try
    {
        // 业务逻辑
        return Task.FromResult("成功");
    }
    catch (JsonException ex)
    {
        return Task.FromResult($"参数解析错误: {ex.Message}");
    }
    catch (ArgumentException ex)
    {
        return Task.FromResult($"参数无效: {ex.Message}");
    }
    catch (Exception ex)
    {
        return Task.FromResult($"执行错误: {ex.Message}");
    }
}
```

## 下一步学习

1. **集成真实的LLM**：将 OpenAI、Claude 或其他 LLM API 集成到智能体中
2. **实现自动工具选择**：让 LLM 自动决定何时调用哪个工具
3. **添加流式响应**：实现实时流式输出
4. **多智能体协作**：创建多个智能体相互配合完成复杂任务
5. **持久化存储**：将对话历史保存到数据库

## 常见问题

### Q1: 什么时候需要创建新工具？

当你需要智能体执行某个特定任务时，比如：
- 查询数据库
- 调用外部 API
- 文件操作
- 复杂计算
- 数据转换

### Q2: 工具的粒度应该多大？

工具应该是**单一职责**的：
- ✅ 好：`CalculatorTool`（执行数学运算）
- ✅ 好：`WeatherTool`（查询天气）
- ❌ 差：`UtilityTool`（做各种不相关的事情）

### Q3: 如何让智能体更智能？

1. **编写清晰的 System Prompt**：告诉智能体它的角色和能力
2. **提供详细的工具描述**：让智能体知道何时使用哪个工具
3. **设计合理的参数 Schema**：确保参数类型和格式清晰
4. **处理边界情况**：在工具中妥善处理各种异常情况

### Q4: 为什么需要对话历史？

对话历史让智能体能够：
- 理解上下文
- 记住之前的交互
- 提供连贯的对话体验
- 支持多轮对话

## 总结

构建智能体的关键步骤：

1. ✅ **定义智能体配置**（名称、角色、提示词）
2. ✅ **创建工具**（实现 ITool 接口）
3. ✅ **注册工具**到智能体
4. ✅ **管理对话流**（用户输入 → 工具调用 → 生成回复）
5. ✅ **维护对话历史**

现在你已经掌握了智能体开发的基础知识，开始创建你自己的智能体吧！ 🚀
