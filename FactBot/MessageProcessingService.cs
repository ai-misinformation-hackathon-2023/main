using System.Collections.Concurrent;
using System.Globalization;
using Discord;
using Discord.WebSocket;

namespace FactBot;

public class MessageProcessingService
{
    private readonly ConcurrentQueue<SocketUserMessage> m_MessageProcessingQueue;
    private Task m_Task;
    private int m_QueryCount = 0;

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
                
                m_QueryCount++;

                if (m_QueryCount > 10)
                {
                    await Task.Run(async () =>
                    {
                        await GPTServiceManager.s_Instance!.Reset();
                    });
                    m_QueryCount = 0;
                }
                
                Task t = Task.Run(async () =>
                {
                    Console.WriteLine($"Processing message from {msg.Author.Username}#{msg.Author.Discriminator}: {msg.Content}");
                    (GPTResponse result, string message) = await GPTServiceManager.s_Instance!.TryGetResponse(msg.Content);
                    Console.WriteLine($"Response: {result}, {message}");
                    if (result is GPTResponse.Harmful or GPTResponse.ContainsMisinformation)
                    {
                        await msg.ReplyAsync($"Misinformation detected!\nResponse from the bot: {message}\n" +
                                             "If you believe this is a mistake, please contact the bot owner.\n" +
                                             "Message deleted :\n" +
                                             $"From : {msg.Author.Username}#{msg.Author.Discriminator}\n" +
                                             $"To : {msg.Channel.Name}\n" +
                                             $"Sent : {msg.Timestamp.ToLocalTime():F}\n" +
                                             $"Content :\n{msg.Content}");
                        await Task.Run(async () =>
                        {
                            await Task.Yield();
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