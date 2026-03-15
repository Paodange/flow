using FlowAgent.Core;
using FlowAgent.Core.Configuration;
using FlowAgent.Core.Plugins;
using FlowAgent.Core.Skills;
using FlowAgent.Core.Tools;

namespace FlowAgent.Examples;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        await RunInteractiveChatAsync();
    }


    /// <summary>
    /// 交互式智能体聊天 - 使用配置文件管理模型设置，支持插件热加载
    /// </summary>
    static async Task RunInteractiveChatAsync()
    {
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "flowagent.settings.json");
        var pluginDir = Path.Combine(Directory.GetCurrentDirectory(), "plugins");

        Console.WriteLine("╔" + "═".PadRight(58, '═') + "╗");
        Console.WriteLine("║" + " FlowAgent 智能体助手 ".PadRight(58) + "║");
        Console.WriteLine("╚" + "═".PadRight(58, '═') + "╝");
        Console.WriteLine();
        Console.WriteLine($"📄 配置文件: {settingsPath}");
        Console.WriteLine($"🔌 插件目录: {pluginDir}");
        Console.WriteLine();

        // ── 加载或初始化模型配置 ──────────────────────────────────────────────
        ModelSettings settings;

        if (File.Exists(settingsPath))
        {
            settings = await ModelSettingsManager.LoadAsync(settingsPath);
            Console.WriteLine($"✅ 已从配置文件加载模型设置:");
        }
        else
        {
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
                Console.Write("   请输入 API Key（留空退出）: ");
                var inputKey = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(inputKey))
                {
                    Console.WriteLine("未提供 API Key，退出。");
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
        Console.WriteLine();

        // ── 创建 LLM 客户端 ───────────────────────────────────────────────────
        FlowAgent.Core.LLM.MicrosoftAiClient llmClient;
        try
        {
            llmClient = ModelSettingsManager.CreateLlmClient(settings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建 LLM 客户端失败: {ex.Message}");
            Console.WriteLine("   请检查配置文件中的 API Key 和 Endpoint 是否正确。");
            return;
        }

        using var _ = llmClient;

        // ── 临时目录（用于文件工具和会话保存）────────────────────────────────
        var tmpDir = Path.Combine(Path.GetTempPath(), "flowagent_chat");
        Directory.CreateDirectory(tmpDir);

        // ── 创建智能体 ────────────────────────────────────────────────────────
        var config = new AgentConfig
        {
            Name = "FlowAgent 助手",
            SystemPrompt = """
                你是一个全能的 AI 助手，可以借助多种工具和技能为用户提供准确的回答：
                - calculator：执行数学计算（加减乘除）
                - datetime：获取当前时间、日期加减计算
                - text_process：文本处理（大小写转换、长度统计、单词计数等）
                - random：生成随机数或从列表中随机选择
                - file_operation：读写文件、创建目录、复制移动文件
                - web_request：发送 HTTP GET/POST 请求
                - web_search：使用 DuckDuckGo 搜索网络内容
                - database_query：操作内存数据库（创建表、插入、查询数据）
                - sqlite_database：操作 SQLite 数据库（支持持久化、完整 CRUD 和自定义 SQL）
                - json_process：处理 JSON 数据（格式化、查询、设置字段值等）
                - environment：获取系统环境信息（环境变量、系统信息、主机名等）
                - web_research：【技能】对指定主题进行多轮网络调研并汇总报告
                - 插件工具：通过插件系统动态加载的额外工具（可在运行时热加载）

                遇到相关问题时，请主动调用对应工具来获取准确结果，而不是凭记忆回答。
                """,
            MaxIterations = 10,
            LlmMaxRetries = 3,
            LlmRetryDelayMs = 500
        };
        var agent = new Agent(config, llmClient);

        // ── 注册内置工具 ──────────────────────────────────────────────────────
        Console.Write("📦 正在注册内置工具...");
        agent.RegisterTool(new CalculatorTool());
        agent.RegisterTool(new DateTimeTool());
        agent.RegisterTool(new TextProcessTool());
        agent.RegisterTool(new RandomTool());
        agent.RegisterTool(new FileOperationTool(tmpDir));
        agent.RegisterTool(new WebRequestTool());
        agent.RegisterTool(new WebSearchTool());
        agent.RegisterTool(new DatabaseQueryTool());
        agent.RegisterTool(new SqliteDatabaseTool(Path.Combine(tmpDir, "chat.db")));
        agent.RegisterTool(new JsonProcessTool());
        agent.RegisterTool(new EnvironmentTool());
        Console.WriteLine($" {agent.Tools.Count} 个工具已就绪");

        // ── 注册内置技能 ──────────────────────────────────────────────────────
        Console.Write("🧠 正在注册内置技能...");
        agent.RegisterSkill(new WebResearchSkill());
        Console.WriteLine($" {agent.Skills.Count} 个技能已就绪");

        // ── 启动插件管理器 ────────────────────────────────────────────────────
        Directory.CreateDirectory(pluginDir);
        using var pluginManager = new PluginManager(pluginDir);

        pluginManager.ToolDiscovered += (pluginInfo, tool) =>
        {
            try
            {
                agent.RegisterTool(tool);
                Console.WriteLine($"🔌 [插件] 已注册工具 [{tool.Name}] 来自: {Path.GetFileName(pluginInfo.FilePath)}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"⚠️  [插件] {ex.Message}");
            }
        };

        pluginManager.ToolRemoved += (pluginInfo, tool) =>
        {
            agent.UnregisterTool(tool.Name);
            Console.WriteLine($"🔌 [插件] 已注销工具 [{tool.Name}] 来自: {Path.GetFileName(pluginInfo.FilePath)}");
        };

        pluginManager.PluginLoadFailed += (filePath, ex) =>
        {
            Console.WriteLine($"❌ [插件] 加载失败 {Path.GetFileName(filePath)}: {ex.Message}");
        };

        await pluginManager.StartAsync();

        // ── 欢迎界面 ──────────────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("┌" + "─".PadRight(58, '─') + "┐");
        Console.WriteLine("│ 💬 智能体聊天已启动！输入问题开始对话             │");
        Console.WriteLine("└" + "─".PadRight(58, '─') + "┘");
        Console.WriteLine();
        Console.WriteLine("  内置命令：");
        Console.WriteLine("    /help    - 显示帮助信息");
        Console.WriteLine("    /tools   - 列出所有可用工具和技能");
        Console.WriteLine("    /plugins - 查看已加载的插件");
        Console.WriteLine("    /clear   - 清空对话历史，开始新对话");
        Console.WriteLine("    /history - 查看对话历史摘要");
        Console.WriteLine("    /save    - 保存对话历史到文件");
        Console.WriteLine("    /exit    - 退出");
        Console.WriteLine();
        Console.WriteLine($"💡 提示：将插件 DLL 放入插件目录可自动热加载: {pluginDir}");
        Console.WriteLine($"💡 提示：可编辑配置文件切换 AI 提供商: {settingsPath}");
        Console.WriteLine("─".PadRight(60, '─'));
        Console.WriteLine();

        // ── 交互主循环 ────────────────────────────────────────────────────────
        while (true)
        {
            Console.Write("👤 你: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.StartsWith('/'))
            {
                var shouldExit = await HandleCommand(input, agent, pluginManager, tmpDir, settingsPath);
                if (shouldExit) return;
                continue;
            }

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
    /// 处理 / 命令。返回 true 表示应退出。
    /// </summary>
    static async Task<bool> HandleCommand(string command, Agent agent, PluginManager pluginManager, string tmpDir, string settingsPath)
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
                Console.WriteLine("  直接输入问题，助手会自动判断是否需要调用工具。");
                Console.WriteLine();
                Console.WriteLine("  命令：");
                Console.WriteLine("    /help    - 显示此帮助");
                Console.WriteLine("    /tools   - 列出所有已注册工具及技能");
                Console.WriteLine("    /plugins - 查看已加载的插件列表");
                Console.WriteLine("    /clear   - 清空对话历史，开始全新对话");
                Console.WriteLine("    /history - 查看当前对话的历史统计");
                Console.WriteLine("    /save    - 将对话历史保存到 JSON 文件");
                Console.WriteLine("    /exit    - 退出");
                Console.WriteLine();
                Console.WriteLine($"  插件目录: {pluginManager.PluginDirectory}");
                Console.WriteLine($"  配置文件: {settingsPath}");
                Console.WriteLine();
                break;

            case "/tools":
                Console.WriteLine();
                Console.WriteLine("  🔧 已注册工具：");
                Console.WriteLine("  " + "─".PadRight(58, '─'));
                var activeTools = agent.GetActiveTools();
                foreach (var tool in agent.Tools.Values)
                {
                    // 跳过技能（技能单独显示）
                    if (agent.Skills.ContainsKey(tool.Name))
                        continue;
                    var status = activeTools.ContainsKey(tool.Name) ? "✅" : "⏸️ ";
                    Console.WriteLine($"  {status} {tool.Name,-22} {tool.Description}");
                }
                if (agent.Skills.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  🧠 已注册技能：");
                    Console.WriteLine("  " + "─".PadRight(58, '─'));
                    foreach (var skill in agent.Skills.Values)
                    {
                        var status = activeTools.ContainsKey(skill.Name) ? "✅" : "⏸️ ";
                        Console.WriteLine($"  {status} {skill.Name,-22} {skill.Description}");
                    }
                }
                Console.WriteLine();
                break;

            case "/plugins":
                Console.WriteLine();
                Console.WriteLine("  🔌 已加载插件：");
                Console.WriteLine("  " + "─".PadRight(58, '─'));
                var plugins = pluginManager.LoadedPlugins;
                if (plugins.Count == 0)
                {
                    Console.WriteLine("  （暂无已加载的插件）");
                    Console.WriteLine($"  将插件 DLL 放入以下目录可自动加载：");
                    Console.WriteLine($"  {pluginManager.PluginDirectory}");
                }
                else
                {
                    foreach (var p in plugins)
                    {
                        Console.WriteLine($"  📦 {Path.GetFileName(p.FilePath)}");
                        foreach (var t in p.Tools)
                        {
                            Console.WriteLine($"     🔧 {t.Name} - {t.Description}");
                        }
                    }
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
}
