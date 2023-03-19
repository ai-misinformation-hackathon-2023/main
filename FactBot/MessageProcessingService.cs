using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;

namespace FactBot;

public class MessageProcessingService
{
    private readonly ConcurrentQueue<SocketUserMessage> m_MessageProcessingQueue;
    private Task m_Task;

    public MessageProcessingService()
    {
        m_MessageProcessingQueue = new ConcurrentQueue<SocketUserMessage>();
        m_Task = Task.Run(WorkerRoutine);
    }

    public void AcceptMessage(SocketUserMessage message)
    {
        m_MessageProcessingQueue.Enqueue(message);
    }

    private async void WorkerRoutine()
    {
        while (true)
        {
            if (m_MessageProcessingQueue.TryDequeue(out SocketUserMessage? msg))
            {
                if (msg.Author.IsBot)
                    continue;
                Task t = Task.Run(async () =>
                {
                    Console.WriteLine($"Processing message from {msg.Author.Username}#{msg.Author.Discriminator}: {msg.Content}");
                    (GPTResponse result, string message) = await GPTServiceManager.s_Instance!.GetResponse(msg.Content);
                    Console.WriteLine($"Response: {result}, {message}");
                    if (result is GPTResponse.Failed)
                    {
                        await msg.ReplyAsync($"Misinformation detected!\nResponse from the bot: {message}");
                        await Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            await msg.DeleteAsync();
                        });
                    }
                    else if (result is GPTResponse.Timeout)
                    {
                        Console.WriteLine("Timeout occurred.");
                        m_MessageProcessingQueue.Enqueue(msg);
                    }
                });
            }
            await Task.Yield();
        }
    }
}