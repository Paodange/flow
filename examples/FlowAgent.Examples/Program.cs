using FlowAgent.Core;
using FlowAgent.Core.LLM;
using FlowAgent.Core.Models;
using FlowAgent.Core.Tools;

namespace FlowAgent.Examples;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        PrintHeader();

        // 示例1: 基础智能体（手动工具调用，无需 LLM API Key）
        await Example1_BasicAgent();

        Console.WriteLine("\n按任意键继续下一个示例...");
        Console.ReadKey();
        Console.Clear();

        // 示例2: 多工具智能体
        await Example2_MultiToolAgent();

        Console.WriteLine("\n按任意键继续下一个示例...");
        Console.ReadKey();
        Console.Clear();

        // 示例3: 对话历史管理
        await Example3_ConversationHistory();

        Console.WriteLine("\n按任意键继续下一个示例...");
        Console.ReadKey();
        Console.Clear();

        // 示例4: LLM 驱动的智能体（自动工具调用循环）
        await Example4_LlmDrivenAgent();

        Console.WriteLine("\n\n所有示例演示完成！");
    }

    static void PrintHeader()
    {
        Console.WriteLine("╔" + "═".PadRight(58, '═') + "╗");
        Console.WriteLine("║" + " FlowAgent - 从零开始构建智能体 ".PadRight(58) + "║");
        Console.WriteLine("╚" + "═".PadRight(58, '═') + "╝");
        Console.WriteLine();
    }

    static void PrintSectionTitle(string title)
    {
        Console.WriteLine();
        Console.WriteLine("┌" + "─".PadRight(58, '─') + "┐");
        Console.WriteLine("│ " + title.PadRight(57) + "│");
        Console.WriteLine("└" + "─".PadRight(58, '─') + "┘");
        Console.WriteLine();
    }

    /// <summary>
    /// 示例1: 创建一个基础的智能体，使用计算器工具
    /// </summary>
    static async Task Example1_BasicAgent()
    {
        PrintSectionTitle("示例1: 基础智能体 - 计算器");

        Console.WriteLine("📝 步骤1: 创建智能体");
        var config = new AgentConfig
        {
            Name = "数学助手",
            SystemPrompt = "你是一个数学助手，可以帮助用户解决数学问题。"
        };
        var agent = new Agent(config);
        Console.WriteLine($"   ✓ 智能体名称: {config.Name}");
        Console.WriteLine();

        Console.WriteLine("📝 步骤2: 注册计算器工具");
        agent.RegisterTool(new CalculatorTool());
        Console.WriteLine();

        Console.WriteLine("📝 步骤3: 执行数学计算");

        var calculations = new[]
        {
            ("加法", "add", 25, 17),
            ("减法", "subtract", 100, 42),
            ("乘法", "multiply", 6, 7),
            ("除法", "divide", 144, 12)
        };

        foreach (var (name, op, a, b) in calculations)
        {
            Console.WriteLine($"\n   🔢 {name}: {a} 和 {b}");
            var result = await agent.ExecuteToolAsync(
                "calculator",
                $@"{{""operation"": ""{op}"", ""a"": {a}, ""b"": {b}}}"
            );
            Console.WriteLine($"      结果: {result}");
        }
    }

    /// <summary>
    /// 示例2: 创建一个拥有多个工具的智能体
    /// </summary>
    static async Task Example2_MultiToolAgent()
    {
        PrintSectionTitle("示例2: 多工具智能体");

        Console.WriteLine("📝 创建多功能助手");
        var config = new AgentConfig
        {
            Name = "多功能助手",
            SystemPrompt = "你是一个多功能AI助手，可以处理各种任务。"
        };
        var agent = new Agent(config);
        Console.WriteLine();

        Console.WriteLine("📝 注册多个工具");
        agent.RegisterTool(new CalculatorTool());
        agent.RegisterTool(new DateTimeTool());
        agent.RegisterTool(new TextProcessTool());
        agent.RegisterTool(new RandomTool());
        Console.WriteLine();

        Console.WriteLine("📝 测试各种工具功能");

        Console.WriteLine("\n   ⏰ 日期时间工具:");
        var dateResult = await agent.ExecuteToolAsync("datetime", @"{""action"": ""current""}");
        Console.WriteLine($"      {dateResult}");

        var addDaysResult = await agent.ExecuteToolAsync("datetime", @"{""action"": ""add_days"", ""days"": 7}");
        Console.WriteLine($"      {addDaysResult}");

        Console.WriteLine("\n   📝 文本处理工具:");
        var text = "Hello World";
        var upperResult = await agent.ExecuteToolAsync("text_process",
            $@"{{""operation"": ""upper"", ""text"": ""{text}""}}");
        Console.WriteLine($"      {upperResult}");

        var lengthResult = await agent.ExecuteToolAsync("text_process",
            $@"{{""operation"": ""length"", ""text"": ""{text}""}}");
        Console.WriteLine($"      {lengthResult}");

        Console.WriteLine("\n   🎲 随机工具:");
        var randomNumResult = await agent.ExecuteToolAsync("random",
            @"{""type"": ""number"", ""min"": 1, ""max"": 100}");
        Console.WriteLine($"      {randomNumResult}");

        var choiceResult = await agent.ExecuteToolAsync("random",
            @"{""type"": ""choice"", ""choices"": [""苹果"", ""香蕉"", ""橙子"", ""葡萄""]}");
        Console.WriteLine($"      {choiceResult}");

        Console.WriteLine("\n📊 工具统计:");
        Console.WriteLine(agent.GetHistorySummary());
    }

    /// <summary>
    /// 示例3: 演示对话历史管理
    /// </summary>
    static async Task Example3_ConversationHistory()
    {
        PrintSectionTitle("示例3: 对话历史管理");

        var config = new AgentConfig
        {
            Name = "对话助手",
            SystemPrompt = "你是一个友好的AI助手。",
            MaxHistoryLength = 10
        };
        var agent = new Agent(config);

        Console.WriteLine("📝 模拟多轮对话");
        Console.WriteLine();

        var conversations = new[]
        {
            ("你好！", "你好！很高兴见到你。有什么我可以帮助的吗？"),
            ("今天天气怎么样？", "我是一个AI助手，无法直接获取天气信息。你可以使用天气工具查询。"),
            ("你能做什么？", "我可以帮你进行计算、处理文本、获取时间信息等多种任务。"),
            ("谢谢！", "不客气！随时为你服务。")
        };

        foreach (var (userMsg, assistantMsg) in conversations)
        {
            Console.WriteLine($"👤 用户: {userMsg}");
            agent.AddUserMessage(userMsg);

            await Task.Delay(300);

            Console.WriteLine($"🤖 助手: {assistantMsg}");
            agent.AddAssistantMessage(assistantMsg);
            Console.WriteLine();
        }

        Console.WriteLine("📊 对话历史统计:");
        Console.WriteLine(agent.GetHistorySummary());
        Console.WriteLine();

        Console.WriteLine("💬 完整对话记录:");
        Console.WriteLine("─".PadRight(60, '─'));
        foreach (var msg in agent.ConversationHistory)
        {
            var emoji = msg.Role switch
            {
                MessageRole.System => "⚙️",
                MessageRole.User => "👤",
                MessageRole.Assistant => "🤖",
                MessageRole.Tool => "🔧",
                _ => "•"
            };

            var role = msg.Role switch
            {
                MessageRole.System => "系统",
                MessageRole.User => "用户",
                MessageRole.Assistant => "助手",
                MessageRole.Tool => "工具",
                _ => "未知"
            };

            Console.WriteLine($"{emoji} [{role}] {msg.Content}");
        }
        Console.WriteLine("─".PadRight(60, '─'));
    }

    /// <summary>
    /// 示例4: LLM 驱动的智能体（使用 ChatAsync 自动完成推理→工具调用→回复循环）。
    /// 运行此示例需要设置环境变量 OPENAI_API_KEY（或兼容接口的密钥）。
    /// </summary>
    static async Task Example4_LlmDrivenAgent()
    {
        PrintSectionTitle("示例4: LLM 驱动的智能体（ChatAsync）");

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("⚠️  未设置 OPENAI_API_KEY 环境变量，跳过本示例。");
            Console.WriteLine("   请设置环境变量后重新运行：");
            Console.WriteLine("   export OPENAI_API_KEY=sk-...");
            Console.WriteLine();
            Console.WriteLine("   也可通过兼容 OpenAI 格式的接口（如 DeepSeek、通义千问等）使用，");
            Console.WriteLine("   同时设置 OPENAI_API_BASE 环境变量指定自定义 Base URL。");
            return;
        }

        var baseUrl = Environment.GetEnvironmentVariable("OPENAI_API_BASE") ?? "https://api.openai.com/v1";
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

        Console.WriteLine($"🔑 使用模型: {model}");
        Console.WriteLine($"�� API 地址: {baseUrl}");
        Console.WriteLine();

        // 创建 LLM 客户端
        using var llmClient = new OpenAiClient(
            apiKey: apiKey,
            model: model,
            baseUrl: baseUrl);

        // 创建带工具的智能体
        var config = new AgentConfig
        {
            Name = "全能助手",
            SystemPrompt = "你是一个有用的AI助手，擅长数学计算和文本处理。遇到计算问题时请使用 calculator 工具；遇到日期时间问题时请使用 datetime 工具；遇到文本处理问题时请使用 text_process 工具。",
            MaxIterations = 5
        };
        var agent = new Agent(config, llmClient);
        agent.RegisterTool(new CalculatorTool());
        agent.RegisterTool(new DateTimeTool());
        agent.RegisterTool(new TextProcessTool());

        Console.WriteLine("📝 开始与 LLM 驱动的智能体对话：");
        Console.WriteLine();

        var questions = new[]
        {
            "请帮我计算 123 乘以 456 等于多少？",
            "今天是几号？再过 30 天是哪天？",
            "请把 'Hello FlowAgent' 转成大写。"
        };

        foreach (var question in questions)
        {
            Console.WriteLine($"👤 用户: {question}");
            Console.WriteLine();

            try
            {
                var reply = await agent.ChatAsync(question);
                Console.WriteLine($"🤖 助手: {reply}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("─".PadRight(60, '─'));
            Console.WriteLine();
        }

        Console.WriteLine("📊 对话历史统计:");
        Console.WriteLine(agent.GetHistorySummary());
    }
}
