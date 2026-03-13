using FlowAgent.Core;
using FlowAgent.Core.Models;
using FlowAgent.Core.Tools;

namespace FlowAgent.Examples;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("FlowAgent - 从零开始构建智能体");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();

        // 第一步: 创建智能体
        Console.WriteLine("📝 第一步: 创建智能体");
        Console.WriteLine();

        var config = new AgentConfig
        {
            Name = "数学助手",
            SystemPrompt = "你是一个数学助手，可以帮助用户解决数学问题。",
            EnableTools = true
        };

        var agent = new Agent(config);
        Console.WriteLine($"✓ 创建智能体: {config.Name}");
        Console.WriteLine($"  系统提示: {config.SystemPrompt}");
        Console.WriteLine();

        // 第二步: 注册工具
        Console.WriteLine("📝 第二步: 注册工具");
        Console.WriteLine();

        agent.RegisterTool(new CalculatorTool());
        Console.WriteLine();

        // 第三步: 模拟对话
        Console.WriteLine("📝 第三步: 模拟用户对话");
        Console.WriteLine();

        // 添加用户消息
        var userMessage = "请帮我计算 25 + 17";
        Console.WriteLine($"👤 用户: {userMessage}");
        agent.AddUserMessage(userMessage);
        Console.WriteLine();

        // 模拟智能体决定调用工具
        Console.WriteLine("🤖 智能体: 我需要使用计算器工具来计算这个问题...");
        Console.WriteLine();

        // 执行工具调用
        var toolResult = await agent.ExecuteToolAsync(
            "calculator",
            @"{""operation"": ""add"", ""a"": 25, ""b"": 17}"
        );
        Console.WriteLine();

        // 添加助手回复
        var assistantResponse = $"根据计算结果，25 + 17 = 42";
        Console.WriteLine($"🤖 智能体: {assistantResponse}");
        agent.AddAssistantMessage(assistantResponse);
        Console.WriteLine();

        // 第四步: 查看对话历史
        Console.WriteLine("📝 第四步: 查看对话历史");
        Console.WriteLine();
        Console.WriteLine(agent.GetHistorySummary());
        Console.WriteLine();

        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("对话历史详情:");
        Console.WriteLine("=".PadRight(60, '='));
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

            Console.WriteLine($"{emoji} [{msg.Role}] {msg.Content}");
        }

        Console.WriteLine();
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("演示完成！");
        Console.WriteLine("=".PadRight(60, '='));
    }
}
