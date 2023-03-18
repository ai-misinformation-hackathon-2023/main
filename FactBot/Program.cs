using Discord.WebSocket;
using Newtonsoft.Json;
using log4net;
using OpenAI_API;

namespace FactBot;

using Discord;
using OpenAI_API.Chat;

public class Program
{
    private DiscordSocketClient m_Client;
    private Secrets m_Secrets;
    private MessageProcessingService m_MsgProcessor;

    public static async Task Main(string[] args)
    {
        await new Program().ProgramMain();
    }

    private Program()
    {
        
    }

    public void DebugLog(string msg)
    {
        Console.WriteLine(msg);
    }

    private async Task ProgramMain()
    {
        m_Secrets = JsonConvert.DeserializeObject<Secrets>(await File.ReadAllTextAsync("secrets.json"));
        
        OpenAIAPI openAI = new OpenAIAPI(new APIAuthentication(m_Secrets.openaiKey));
        
        
        DiscordSocketConfig socketConfig = new()
        {
            GatewayIntents = GatewayIntents.All
        };
        m_Client = new DiscordSocketClient(socketConfig);
        m_Client.Log += async message =>
        {
            Console.WriteLine(message);
            await Task.Yield();
        };

        m_MsgProcessor = new MessageProcessingService();

        m_Client.MessageReceived += MessageReceived;

        await m_Client.LoginAsync(TokenType.Bot, m_Secrets.token);
        await m_Client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task MessageReceived(SocketMessage msg)
    {
        var message = (msg as SocketUserMessage)!;
        m_MsgProcessor.AcceptMessage(message);
        await Task.Yield();
    }
}
