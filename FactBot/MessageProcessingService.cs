using System.Collections.Concurrent;
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
                Console.WriteLine($"Processing message from {msg.Author.Username}#{msg.Author.Discriminator}: {msg.Content}");
                string result = await GPTService.instance.GetResponse(msg.Content);
                Console.WriteLine($"Response: {result}");
                await msg.Channel.SendMessageAsync(result);
            }
            await Task.Yield();
        }
    }
}