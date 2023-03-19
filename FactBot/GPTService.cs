using System.Diagnostics;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace FactBot;

public class GPTServiceManager : IGPTService
{
    private readonly GPTInputValidationService m_InputValidationService;
    private readonly GPTMisinformationCheckService m_MisinfoService;
    public static GPTServiceManager? s_Instance { get; private set; }

    public GPTServiceManager(OpenAIAPI api)
    {
        m_MisinfoService = new GPTMisinformationCheckService(api);
        m_InputValidationService = new GPTInputValidationService(api);
        s_Instance = this;
    }

    public async Task<GPTResponse> GetResponse(string message)
    {
        GPTResponse response = await m_InputValidationService.GetResponse(message);
        Console.WriteLine($"Input validation response: {response}");
        if (response is GPTResponse.No or GPTResponse.Invalid or GPTResponse.Timeout)
            return response;

        response = await m_MisinfoService.GetResponse(message);
        Console.WriteLine($"Misinfo response: {response}");
        return response;
    }

    public async Task Initialize()
    {
        await m_InputValidationService.Initialize();
        await m_MisinfoService.Initialize();
    }
}

public enum GPTResponse : int
{
    Yes,
    No,
    Maybe,
    Invalid,
    Timeout
}

public interface IGPTService
{
    Task<GPTResponse> GetResponse(string message);
    Task Initialize();
}

public class GPTInputValidationService : IGPTService
{
    private OpenAIAPI m_OpenAI;
    private const string PROMPT = """
You're tasked with detecing if the user input is grammatically correct.
If the user input is mostly grammatical, respond with YES.
If the user input is a bunch of random words, respond with INVALID.
If the user input contains obviously harmful information, respond with NO.
If the user input is a bit weird, but is still mostly grammatical, respond with MAYBE.
If the user input makes no sense logically but is grammatically correct, respond with MAYBE.
If the user input mostly makes sense but has a few grammatical errors, respond with YES.
If the user input mostly makes sense but has spelling errors, respond with YES.
The first word of your response can either be YES, NO, MAYBE or INVALID. It is strictly forbidden for the first word of your response to be anything else.
You then MUST give a reason for your response. The reason MUST be a single sentence.
""";
    private List<ChatMessage> m_Messages = new List<ChatMessage>();
    
    private const string SHORT_PROMPT = "Is the sentence grammtically correct without considering the meaning : ";
    
    private string Preprocess(string message)
    {
        return SHORT_PROMPT + message;
    }
    
    public GPTInputValidationService(OpenAIAPI api)
    {
        m_OpenAI = api;
        m_Messages = new List<ChatMessage>
        {
            new ChatMessage(ChatMessageRole.System, PROMPT),
            new ChatMessage(ChatMessageRole.User, Preprocess("I am a windows computer")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The sentence is grammatically correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("They currently hold valid temporary status or have left Canada.")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The sentence is grammatically correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("Colorless green ideas sleep furiously.")),
            new ChatMessage(ChatMessageRole.Assistant, "MAYBE. The sentence is grammatically correct but the meaning is unclear."),
            new ChatMessage(ChatMessageRole.User, Preprocess("Hi")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The sentence is grammatically correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("asdogihawpeodfijasdkglas")),
            new ChatMessage(ChatMessageRole.Assistant, "INVALID. It's a bunch of random letters."),
            new ChatMessage(ChatMessageRole.User, Preprocess("Do yes hello hear yes me that i am is hey want go school home work")),
            new ChatMessage(ChatMessageRole.Assistant, "INVALID. It's a bunch of random words."),
            new ChatMessage(ChatMessageRole.User, Preprocess("1 + 1 = 3")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The syntax correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("1 + 1 = 2")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The syntax correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("The quick brown fox jumps over the lazy dog")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The sentence is grammatically correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("Pfizer manufactured the COVID virus.")),
            new ChatMessage(ChatMessageRole.Assistant, "NO. The sentence is grammatically correct but it contains harmful information."),
        };
    }

    public async Task<GPTResponse> GetResponse(string message)
    {
        List<ChatMessage> messages;
        lock (m_Messages)
        {
            messages = new List<ChatMessage>(m_Messages);
        }
        
        messages.Add(new ChatMessage(ChatMessageRole.User, message));

        Task<ChatResult> resultTask = m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.2,
            MaxTokens = 2000,
            Messages = messages
        });
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(10))) != resultTask)
        {
            return GPTResponse.Timeout;
        }
        ChatResult result = resultTask.Result;
        string resultString = result.ToString();
        
        messages.Add(new ChatMessage(ChatMessageRole.Assistant, resultString));
        Console.WriteLine(resultString);
        lock (m_Messages)
        { 
            m_Messages.Add(messages[^2]);
            m_Messages.Add(messages[^1]);
        }
        if (resultString.StartsWith("YES"))
        {
            return GPTResponse.Yes;
        }
        if (resultString.StartsWith("NO"))
        {
            return GPTResponse.No;
        }
        if (resultString.StartsWith("MAYBE"))
        {
            return GPTResponse.Maybe;
        }
        if (resultString.StartsWith("INVALID"))
        {
            return GPTResponse.Invalid;
        }
        return GPTResponse.Invalid;
    }

    public async Task Initialize()
    {
        ChatResult result = await m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.5,
            MaxTokens = 2000,
            Messages = new List<ChatMessage>
            {
                new ChatMessage(ChatMessageRole.System, PROMPT)
            }
        });
        Console.WriteLine(result.ToString());
    }
}

public class GPTMisinformationCheckService : IGPTService
{
    private Conversation m_Chat;
    private OpenAIAPI m_OpenAI;
    private static GPTMisinformationCheckService? s_Instance;
    private List<ChatMessage> m_Messages = new List<ChatMessage>();
    private const string PROMPT = """
You're tasked with finding misinformation in the user input. The first word of your response can either be YES, NO or MAYBE.
It is strictly forbidden for the first word of your response to be anything else.
You then MUST give a reason for your response. The reason MUST be a single sentence.
Here are some sample inputs and their expected outputs:
""";
    
    public GPTMisinformationCheckService(OpenAIAPI api)
    {
        m_OpenAI = api;
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
    
    public static GPTMisinformationCheckService instance => s_Instance ?? throw new Exception("GPTService not initialized");
    
    public async Task<GPTResponse> GetResponse(string message)
    {
        List<ChatMessage> messages;
        lock (m_Messages)
        {
            messages = new List<ChatMessage>(m_Messages);
        }
        
        messages.Add(new ChatMessage(ChatMessageRole.User, PROMPT + ":\n" + message));

        Task<ChatResult> resultTask = m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.5,
            MaxTokens = 2000,
            Messages = messages
        });
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(10))) != resultTask)
        {
            return GPTResponse.Timeout;
        }
        ChatResult result = await resultTask;
        string resultString = result.ToString();
        
        messages.Add(new ChatMessage(ChatMessageRole.Assistant, resultString));
        lock (m_Messages)
        { 
            m_Messages.Add(messages[^2]);
            m_Messages.Add(messages[^1]);
        }
        if (resultString.StartsWith("YES"))
        {
            return GPTResponse.Yes;
        }
        if (resultString.StartsWith("NO"))
        {
            return GPTResponse.No;
        }
        if (resultString.StartsWith("MAYBE"))
        {
            return GPTResponse.Maybe;
        }
        return GPTResponse.Invalid;
    }
}