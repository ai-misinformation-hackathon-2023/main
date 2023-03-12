using Discord.WebSocket;
using Newtonsoft.Json;
using log4net;

namespace FactBot;

using Discord;

public class Program
{
    private DiscordSocketClient m_Client;
    private Secrets m_Secrets;

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

        m_Client.MessageReceived += MessageReceived;

        await m_Client.LoginAsync(TokenType.Bot, m_Secrets.token);
        await m_Client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task MessageReceived(SocketMessage msg)
    {
        var message = (msg as SocketUserMessage)!;
        Console.WriteLine($"Received message from {message.Author.Username}: {message.Content}");
    }
}
