using System.Diagnostics;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace FactBot;

public class GPTService
{
    private Conversation m_Chat;
    private OpenAIAPI m_OpenAI;
    private static GPTService? s_Instance;
    private List<ChatMessage> m_Messages = new List<ChatMessage>();
    private const string PROMPT = """
You're tasked with finding misinformation in the user input. Your response can either be YES, NO or MAYBE. It is strictly forbidden to respond with anything else.
Here are some sample inputs and their expected outputs:
""";
    
    public GPTService(string apiKey)
    {
        m_OpenAI = new OpenAIAPI(new APIAuthentication(apiKey));
        m_Messages = new List<ChatMessage>(
            new[]
            {
                new ChatMessage(ChatMessageRole.System, PROMPT),
                new ChatMessage(ChatMessageRole.User, "Covid 19 is a hoax"),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, "The earth is flat"),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, "The moon landing was faked"),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, "The earth is round"),
                new ChatMessage(ChatMessageRole.Assistant, "YES"),
            }
        );
        s_Instance = this;
    }
    
    public async Task Initialize()
    {
        ChatResult result = await m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.8,
            MaxTokens = 2000,
            Messages = new List<ChatMessage>
            {
                new ChatMessage(ChatMessageRole.System, PROMPT)
            }
        });
        Console.WriteLine(result.ToString());
    }
    
    public static GPTService instance => s_Instance ?? throw new Exception("GPTService not initialized");
    
    public async Task<string> GetResponse(string message)
    {
        m_Messages.Add(new ChatMessage(ChatMessageRole.User, message));
        ChatResult result = await m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.5,
            MaxTokens = 2000,
            Messages = m_Messages
        });
        string resultString = result.ToString();
        m_Messages.Add(new ChatMessage(ChatMessageRole.Assistant, resultString));
        return resultString;
    }
}
