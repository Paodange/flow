using FlowAgent.Core;
using FlowAgent.Core.Configuration;
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

        await Example7_ModelSettingsAndMicrosoftAI();
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
        Console.WriteLine($"🌐 API 地址: {baseUrl}");
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
            MaxIterations = 5,
            LlmMaxRetries = 3,
            LlmRetryDelayMs = 500
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

    /// <summary>
    /// 示例5: 演示所有新功能 - 文件操作、数据库、Web请求、对话持久化、多智能体协作
    /// </summary>
    static async Task Example5_NewFeatures()
    {
        PrintSectionTitle("示例5: 新功能演示");

        // ── 5a. 文件操作工具 ─────────────────────────────────────────────────
        Console.WriteLine("📁 5a. 文件操作工具");

        var tmpDir = Path.Combine(Path.GetTempPath(), "flowagent_demo");
        Directory.CreateDirectory(tmpDir);

        var agent = new Agent(new AgentConfig { Name = "文件助手" });
        agent.RegisterTool(new FileOperationTool(tmpDir));

        var writeResult = await agent.ExecuteToolAsync("file_operation",
            $@"{{""operation"":""write"",""path"":""hello.txt"",""content"":""Hello, FlowAgent!\n这是一个文件操作示例。""}}");
        Console.WriteLine($"   {writeResult}");

        var readResult = await agent.ExecuteToolAsync("file_operation",
            @"{""operation"":""read"",""path"":""hello.txt""}");
        Console.WriteLine($"   {readResult}");

        // mkdir + copy + move + info
        var mkdirResult = await agent.ExecuteToolAsync("file_operation",
            @"{""operation"":""mkdir"",""path"":""subdir""}");
        Console.WriteLine($"   {mkdirResult}");

        var copyResult = await agent.ExecuteToolAsync("file_operation",
            @"{""operation"":""copy"",""path"":""hello.txt"",""destination"":""subdir/hello_copy.txt""}");
        Console.WriteLine($"   {copyResult}");

        var infoResult = await agent.ExecuteToolAsync("file_operation",
            @"{""operation"":""info"",""path"":""hello.txt""}");
        Console.WriteLine($"   {infoResult}");

        var listResult = await agent.ExecuteToolAsync("file_operation",
            @"{""operation"":""list_dir"",""path"":"".""}");
        Console.WriteLine($"   {listResult}");

        Console.WriteLine();

        // ── 5b. 数据库查询工具 ───────────────────────────────────────────────
        Console.WriteLine("🗄️  5b. 内存数据库工具");

        var dbAgent = new Agent(new AgentConfig { Name = "数据库助手" });
        dbAgent.RegisterTool(new DatabaseQueryTool());

        var createResult = await dbAgent.ExecuteToolAsync("database_query",
            @"{""operation"":""create_table"",""table"":""users"",""columns"":[""name"",""age"",""city""]}");
        Console.WriteLine($"   {createResult}");

        await dbAgent.ExecuteToolAsync("database_query",
            @"{""operation"":""insert"",""table"":""users"",""row"":{""name"":""张三"",""age"":""28"",""city"":""北京""}}");
        await dbAgent.ExecuteToolAsync("database_query",
            @"{""operation"":""insert"",""table"":""users"",""row"":{""name"":""李四"",""age"":""35"",""city"":""上海""}}");
        await dbAgent.ExecuteToolAsync("database_query",
            @"{""operation"":""insert"",""table"":""users"",""row"":{""name"":""王五"",""age"":""28"",""city"":""北京""}}");

        var selectAll = await dbAgent.ExecuteToolAsync("database_query",
            @"{""operation"":""select"",""table"":""users""}");
        Console.WriteLine($"   {selectAll}");

        var selectFiltered = await dbAgent.ExecuteToolAsync("database_query",
            @"{""operation"":""select"",""table"":""users"",""where"":{""city"":""北京""}}");
        Console.WriteLine($"   {selectFiltered}");

        Console.WriteLine();

        // ── 5b2. SQLite 数据库工具 ────────────────────────────────────────────
        Console.WriteLine("🗄️  5b2. SQLite 数据库工具（持久化）");

        var dbFile = Path.Combine(tmpDir, "demo.db");
        var sqliteAgent = new Agent(new AgentConfig { Name = "SQLite助手" });
        sqliteAgent.RegisterTool(new SqliteDatabaseTool(dbFile));

        var sqlCreateResult = await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""create_table"",""table"":""products"",""columns"":[{""name"":""id"",""type"":""INTEGER PRIMARY KEY AUTOINCREMENT""},{""name"":""name"",""type"":""TEXT""},{""name"":""price"",""type"":""REAL""},{""name"":""stock"",""type"":""INTEGER""}]}");
        Console.WriteLine($"   {sqlCreateResult}");

        await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""insert"",""table"":""products"",""row"":{""name"":""苹果"",""price"":""3.5"",""stock"":""100""}}");
        await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""insert"",""table"":""products"",""row"":{""name"":""香蕉"",""price"":""2.0"",""stock"":""200""}}");
        await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""insert"",""table"":""products"",""row"":{""name"":""橙子"",""price"":""4.5"",""stock"":""80""}}");

        var sqlSelectAll = await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""select"",""table"":""products""}");
        Console.WriteLine($"   {sqlSelectAll}");

        // 更新价格
        var sqlUpdateResult = await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""update"",""table"":""products"",""set"":{""price"":""5.0""},""where"":{""name"":""苹果""}}");
        Console.WriteLine($"   {sqlUpdateResult}");

        // 自定义 SQL 查询
        var sqlCustomResult = await sqliteAgent.ExecuteToolAsync("sqlite_database",
            @"{""operation"":""execute_sql"",""sql"":""SELECT name, price FROM products ORDER BY price DESC""}");
        Console.WriteLine($"   {sqlCustomResult}");

        Console.WriteLine();

        // ── 5c. 对话历史持久化 ───────────────────────────────────────────────
        Console.WriteLine("💾 5c. 对话历史持久化");

        var historyAgent = new Agent(new AgentConfig
        {
            Name = "持久化测试",
            SystemPrompt = "你是一个测试助手。"
        });
        historyAgent.AddUserMessage("你好！");
        historyAgent.AddAssistantMessage("你好！有什么可以帮你的？");
        historyAgent.AddUserMessage("今天天气真好。");
        historyAgent.AddAssistantMessage("是的，好天气让人心情愉快！");

        var historyFile = Path.Combine(tmpDir, "history.json");
        await historyAgent.SaveHistoryAsync(historyFile);

        // 创建一个新智能体并加载历史
        var restoredAgent = new Agent(new AgentConfig { Name = "已恢复的助手" });
        await restoredAgent.LoadHistoryAsync(historyFile);
        Console.WriteLine($"   加载后历史条数: {restoredAgent.ConversationHistory.Count}");
        Console.WriteLine($"   最后一条消息: [{restoredAgent.ConversationHistory[^1].Role}] {restoredAgent.ConversationHistory[^1].Content}");

        Console.WriteLine();

        // ── 5d. 多智能体协作 ─────────────────────────────────────────────────
        Console.WriteLine("🤝 5d. 多智能体协作");

        var orchestrator = new AgentOrchestrator();

        // 创建数学专家智能体
        var mathAgent = new Agent(new AgentConfig
        {
            Name = "数学专家",
            SystemPrompt = "你是一个数学专家，擅长数学计算。"
        });
        mathAgent.RegisterTool(new CalculatorTool());
        orchestrator.RegisterAgent("math", mathAgent);

        // 创建文本专家智能体
        var textAgent = new Agent(new AgentConfig
        {
            Name = "文本专家",
            SystemPrompt = "你是一个文本处理专家。"
        });
        textAgent.RegisterTool(new TextProcessTool());
        orchestrator.RegisterAgent("text", textAgent);

        // 创建协调智能体并为其提供 SubAgentTool
        var coordinatorConfig = new AgentConfig
        {
            Name = "协调者",
            SystemPrompt = "你是一个协调者，可以调用其他专业智能体。"
        };
        var coordinator = new Agent(coordinatorConfig);
        coordinator.RegisterTool(orchestrator.CreateSubAgentTool("coordinator"));

        // 手动演示子智能体工具调用
        var mathResult = await coordinator.ExecuteToolAsync("call_agent",
            @"{""agent_name"":""math"",""message"":""帮我计算 99 * 99""}");
        Console.WriteLine($"   数学专家回复: {mathResult}");

        Console.WriteLine();
        Console.WriteLine("✅ 所有新功能演示完成！");
    }

    /// <summary>
    /// 示例6: 交互式聊天 —— 用户直接与大模型对话，大模型自主决定调用哪些工具。
    /// 运行此示例需要设置环境变量 OPENAI_API_KEY（或兼容接口的密钥）。
    /// </summary>
    static async Task Example6_InteractiveChat()
    {
        PrintSectionTitle("示例6: 交互式智能体聊天");

        // ── 读取 API Key ──────────────────────────────────────────────────────
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("⚠️  未检测到 OPENAI_API_KEY 环境变量。");
            Console.WriteLine("   可通过以下方式设置：");
            Console.WriteLine("   export OPENAI_API_KEY=sk-...");
            Console.WriteLine("   也支持 DeepSeek、通义千问等兼容 OpenAI 格式的接口，");
            Console.WriteLine("   同时设置 OPENAI_API_BASE 与 OPENAI_MODEL 即可。");
            Console.WriteLine();
            Console.Write("   或者在此直接输入 API Key（留空则退出）: ");
            apiKey = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("未提供 API Key，退出交互式聊天。");
                return;
            }
        }

        var baseUrl = Environment.GetEnvironmentVariable("OPENAI_API_BASE") ?? "https://api.openai.com/v1";
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

        Console.WriteLine($"🔑 使用模型: {model}");
        Console.WriteLine($"🌐 API 地址: {baseUrl}");
        Console.WriteLine();

        // ── 创建 LLM 客户端 ───────────────────────────────────────────────────
        using var llmClient = new OpenAiClient(apiKey, model, baseUrl);

        // ── 准备文件工具的临时目录 ─────────────────────────────────────────────
        var tmpDir = Path.Combine(Path.GetTempPath(), "flowagent_chat");
        Directory.CreateDirectory(tmpDir);

        // ── 创建智能体 ────────────────────────────────────────────────────────
        var config = new AgentConfig
        {
            Name = "全能助手",
            SystemPrompt = """
                你是一个全能的AI助手，可以借助多种工具为用户提供准确的回答：
                - calculator：执行数学计算（加减乘除）
                - datetime：获取当前时间、日期加减计算
                - text_process：文本处理（大小写转换、长度统计、单词计数等）
                - random：生成随机数或从列表中随机选择
                - file_operation：读写文件、创建目录、复制移动文件（文件存储在临时目录下）
                - web_request：发送 HTTP GET/POST 请求
                - web_search：使用 DuckDuckGo 搜索网络内容
                - database_query：操作内存数据库（创建表、插入、查询数据）
                - sqlite_database：操作 SQLite 数据库（支持持久化、完整 CRUD 和自定义 SQL）

                遇到相关问题时，请主动调用对应工具来获取准确结果，而不是凭记忆回答。
                """,
            MaxIterations = 10,
            LlmMaxRetries = 3,
            LlmRetryDelayMs = 500
        };
        var agent = new Agent(config, llmClient);

        // ── 注册所有工具 ──────────────────────────────────────────────────────
        Console.WriteLine("📦 正在注册工具...");
        agent.RegisterTool(new CalculatorTool());
        agent.RegisterTool(new DateTimeTool());
        agent.RegisterTool(new TextProcessTool());
        agent.RegisterTool(new RandomTool());
        agent.RegisterTool(new FileOperationTool(tmpDir));
        agent.RegisterTool(new WebRequestTool());
        agent.RegisterTool(new WebSearchTool());
        agent.RegisterTool(new DatabaseQueryTool());
        agent.RegisterTool(new SqliteDatabaseTool(Path.Combine(tmpDir, "chat.db")));

        // ── 欢迎界面 ──────────────────────────────────────────────────────────
        Console.WriteLine();
        PrintSectionTitle("💬 交互式智能体聊天已启动！");
        Console.WriteLine("  输入你的问题或指令，智能体会自动调用合适的工具。");
        Console.WriteLine("  示例问题：");
        Console.WriteLine("    • 计算 999 乘以 123");
        Console.WriteLine("    • 今天是星期几？再过 100 天是哪天？");
        Console.WriteLine("    • 帮我把 'hello flowagent' 转成大写");
        Console.WriteLine("    • 从苹果、香蕉、草莓中随机选一个");
        Console.WriteLine("    • 把这段话写入文件 notes.txt：FlowAgent 真好用");
        Console.WriteLine("    • 搜索一下 .NET 10 的新特性");
        Console.WriteLine();
        Console.WriteLine("  内置命令：");
        Console.WriteLine("    /help    - 显示帮助信息");
        Console.WriteLine("    /tools   - 列出所有可用工具");
        Console.WriteLine("    /clear   - 清空对话历史，开始新对话");
        Console.WriteLine("    /history - 查看对话历史摘要");
        Console.WriteLine("    /save    - 保存对话历史到文件");
        Console.WriteLine("    /exit    - 退出聊天");
        Console.WriteLine();
        Console.WriteLine("─".PadRight(60, '─'));
        Console.WriteLine();

        // ── 交互主循环 ────────────────────────────────────────────────────────
        while (true)
        {
            Console.Write("👤 你: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            // 处理内置命令
            if (input.StartsWith('/'))
            {
                var shouldExit = await HandleInteractiveCommand(input, agent, tmpDir);
                if (shouldExit) return;
                continue;
            }

            // 发送消息给 LLM，LLM 自主决定是否调用工具
            Console.WriteLine();
            try
            {
                var reply = await agent.ChatAsync(input);
                Console.WriteLine();
                Console.WriteLine($"🤖 助手: {reply}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("操作已取消。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("─".PadRight(60, '─'));
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 处理交互式聊天中的 / 命令。返回 true 表示应退出聊天循环。
    /// </summary>
    static async Task<bool> HandleInteractiveCommand(string command, Agent agent, string tmpDir)
    {
        switch (command.ToLowerInvariant())
        {
            case "/exit":
            case "/quit":
                Console.WriteLine("再见！👋");
                return true;

            case "/help":
                Console.WriteLine();
                Console.WriteLine("  📖 帮助信息");
                Console.WriteLine("  " + "─".PadRight(58, '─'));
                Console.WriteLine("  直接输入问题，智能体会自动判断是否需要调用工具。");
                Console.WriteLine();
                Console.WriteLine("  命令：");
                Console.WriteLine("    /help    - 显示此帮助");
                Console.WriteLine("    /tools   - 列出所有可用工具及描述");
                Console.WriteLine("    /clear   - 清空对话历史，开始全新对话");
                Console.WriteLine("    /history - 查看当前对话的历史统计");
                Console.WriteLine("    /save    - 将对话历史保存到 JSON 文件");
                Console.WriteLine("    /exit    - 退出交互式聊天");
                Console.WriteLine();
                break;

            case "/tools":
                Console.WriteLine();
                Console.WriteLine("  🔧 可用工具：");
                Console.WriteLine("  " + "─".PadRight(58, '─'));
                foreach (var tool in agent.Tools.Values)
                {
                    Console.WriteLine($"  • {tool.Name,-20} {tool.Description}");
                }
                Console.WriteLine();
                break;

            case "/clear":
                agent.ClearHistory();
                Console.WriteLine("✓ 对话历史已清空，可以开始新的对话。");
                Console.WriteLine();
                break;

            case "/history":
                Console.WriteLine();
                Console.WriteLine(agent.GetHistorySummary());
                Console.WriteLine();
                break;

            case "/save":
                var fileName = $"chat_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var savePath = Path.Combine(tmpDir, fileName);
                await agent.SaveHistoryAsync(savePath);
                Console.WriteLine();
                break;

            default:
                Console.WriteLine($"未知命令: {command}。输入 /help 查看可用命令。");
                Console.WriteLine();
                break;
        }

        return false;
    }

    /// <summary>
    /// 示例7: 使用 Microsoft.Extensions.AI 和模型配置文件管理 AI 客户端。
    /// 演示如何通过 ModelSettings 配置文件灵活切换不同的 AI 模型提供商。
    /// </summary>
    static async Task Example7_ModelSettingsAndMicrosoftAI()
    {
        PrintSectionTitle("示例7: Microsoft.Extensions.AI + 模型配置文件");

        // ── 配置文件路径（当前目录下的 flowagent.settings.json）──────────────
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "flowagent.settings.json");

        Console.WriteLine($"📄 配置文件路径: {settingsPath}");
        Console.WriteLine();

        // ── 加载或初始化配置 ──────────────────────────────────────────────────
        ModelSettings settings;

        if (File.Exists(settingsPath))
        {
            settings = await ModelSettingsManager.LoadAsync(settingsPath);
            Console.WriteLine($"✅ 已从配置文件加载模型设置:");
        }
        else
        {
            // 首次运行：尝试从环境变量读取，并保存为配置文件
            var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var envBaseUrl = Environment.GetEnvironmentVariable("OPENAI_API_BASE");
            var envModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");

            settings = new ModelSettings
            {
                Provider = ModelProvider.OpenAI,
                ApiKey = envApiKey,
                Endpoint = string.IsNullOrWhiteSpace(envBaseUrl) ? null : envBaseUrl,
                ModelId = envModel ?? "gpt-4o-mini",
                MaxOutputTokens = 2048,
                Temperature = 0.7
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                await ModelSettingsManager.SaveAsync(settings, settingsPath);
                Console.WriteLine($"✅ 已从环境变量创建配置文件:");
            }
            else
            {
                Console.WriteLine("⚠️  未找到配置文件，且未设置 OPENAI_API_KEY 环境变量。");
                Console.WriteLine();
                Console.Write("   请输入 API Key（留空跳过本示例）: ");
                var inputKey = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(inputKey))
                {
                    Console.WriteLine("未提供 API Key，跳过本示例。");
                    Console.WriteLine();
                    Console.WriteLine("提示: 可手动创建 flowagent.settings.json，内容示例：");
                    Console.WriteLine("""
                    {
                      "provider": "OpenAI",
                      "apiKey": "sk-...",
                      "endpoint": null,
                      "modelId": "gpt-4o-mini",
                      "maxOutputTokens": 2048,
                      "temperature": 0.7
                    }
                    """);
                    return;
                }

                settings.ApiKey = inputKey;
                await ModelSettingsManager.SaveAsync(settings, settingsPath);
                Console.WriteLine($"✅ 配置已保存到: {settingsPath}");
            }
        }

        Console.WriteLine($"   提供商:   {settings.Provider}");
        Console.WriteLine($"   模型:     {settings.ModelId}");
        Console.WriteLine($"   Endpoint: {settings.Endpoint ?? "（默认）"}");
        Console.WriteLine($"   最大输出: {settings.MaxOutputTokens} tokens");
        Console.WriteLine($"   温度:     {settings.Temperature}");
        Console.WriteLine();

        // ── 使用 Microsoft.Extensions.AI 创建客户端 ──────────────────────────
        ILlmClient llmClient;
        try
        {
            llmClient = ModelSettingsManager.CreateLlmClient(settings);
            Console.WriteLine("🤖 已通过 Microsoft.Extensions.AI 创建客户端（MicrosoftAiClient）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建客户端失败: {ex.Message}");
            return;
        }

        using var disposableLlmClient = llmClient as IDisposable;

        // ── 创建带工具的智能体 ────────────────────────────────────────────────
        var config = new AgentConfig
        {
            Name = "AI助手（Microsoft.Extensions.AI）",
            SystemPrompt = """
                你是一个全能的AI助手，可以借助多种工具为用户提供准确的回答：
                - calculator：执行数学计算（加减乘除）
                - datetime：获取当前时间、日期加减计算
                - text_process：文本处理（大小写转换、长度统计、单词计数等）

                遇到相关问题时，请主动调用对应工具来获取准确结果，而不是凭记忆回答。
                """,
            MaxIterations = 10,
            LlmMaxRetries = 3,
            LlmRetryDelayMs = 500
        };
        var agent = new Agent(config, llmClient);

        Console.WriteLine("📦 正在注册工具...");
        agent.RegisterTool(new CalculatorTool());
        agent.RegisterTool(new DateTimeTool());
        agent.RegisterTool(new TextProcessTool());

        // ── 欢迎界面 ──────────────────────────────────────────────────────────
        Console.WriteLine();
        PrintSectionTitle("💬 交互式智能体聊天已启动！");
        Console.WriteLine("  输入你的问题或指令，智能体会自动调用合适的工具。");
        Console.WriteLine("  示例问题：");
        Console.WriteLine("    • 计算 256 乘以 48");
        Console.WriteLine("    • 今天是星期几？再过 100 天是哪天？");
        Console.WriteLine("    • 帮我把 'hello flowagent' 转成大写");
        Console.WriteLine();
        Console.WriteLine("  内置命令：");
        Console.WriteLine("    /help    - 显示帮助信息");
        Console.WriteLine("    /tools   - 列出所有可用工具");
        Console.WriteLine("    /clear   - 清空对话历史，开始新对话");
        Console.WriteLine("    /history - 查看对话历史摘要");
        Console.WriteLine("    /save    - 保存对话历史到文件");
        Console.WriteLine("    /exit    - 退出聊天");
        Console.WriteLine();
        Console.WriteLine($"💡 提示：可编辑配置文件切换 AI 提供商：{settingsPath}");
        Console.WriteLine("─".PadRight(60, '─'));
        Console.WriteLine();

        var tmpDir = Path.Combine(Path.GetTempPath(), "flowagent_chat");
        Directory.CreateDirectory(tmpDir);

        // ── 交互主循环 ────────────────────────────────────────────────────────
        while (true)
        {
            Console.Write("👤 你: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            // 处理内置命令
            if (input.StartsWith('/'))
            {
                var shouldExit = await HandleInteractiveCommand(input, agent, tmpDir);
                if (shouldExit) return;
                continue;
            }

            // 发送消息给 LLM，LLM 自主决定是否调用工具
            Console.WriteLine();
            try
            {
                var reply = await agent.ChatAsync(input);
                Console.WriteLine();
                Console.WriteLine($"🤖 助手: {reply}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("操作已取消。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("─".PadRight(60, '─'));
            Console.WriteLine();
        }
    }
}
