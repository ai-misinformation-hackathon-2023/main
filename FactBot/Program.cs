using Discord.WebSocket;
using Newtonsoft.Json;

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

    private async Task ProgramMain()
    {
        m_Secrets = JsonConvert.DeserializeObject<Secrets>(await File.ReadAllTextAsync("secrets.json"));
        m_Client = new DiscordSocketClient();
        m_Client.Log += async message =>
        {
            Console.WriteLine(message);
            await Task.Yield();
        };

        await m_Client.LoginAsync(TokenType.Bot, m_Secrets.token);
        await m_Client.StartAsync();

        await Task.Delay(-1);
    }
}
