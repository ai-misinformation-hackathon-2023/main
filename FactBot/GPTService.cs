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
    private object m_Mutex = new object();
    private volatile bool m_ResetInProgress = false;
    private object m_ResetInProgressMutex = new object();
    
    
    private volatile int m_GetResponseCallCount = 0;
    private object m_GetResponseCallCountMutex = new object();

    public GPTServiceManager(OpenAIAPI api)
    {
        m_MisinfoService = new GPTMisinformationCheckService(api);
        m_InputValidationService = new GPTInputValidationService(api);
        s_Instance = this;
    }

    public async Task<(GPTResponse, string)> TryGetResponse(string message)
    {
        try
        {
            Monitor.Enter(m_ResetInProgressMutex);
            if (m_ResetInProgress)
                return (GPTResponse.Timeout, "Timeout occurred.");

            try
            {
                Monitor.Enter(m_GetResponseCallCountMutex);
                Interlocked.Increment(ref m_GetResponseCallCount);
            }
            finally
            {
                Monitor.Exit(m_GetResponseCallCountMutex);
            }
        }
        finally
        {
            Monitor.Exit(m_ResetInProgressMutex);
        }

        try
        {
            return await m_InputValidationService.GetResponse(message);
        }
        finally
        {
            try
            {
                Monitor.Enter(m_GetResponseCallCountMutex);
                Interlocked.Decrement(ref m_GetResponseCallCount);
            }
            finally
            {
                Monitor.Exit(m_GetResponseCallCountMutex);
            }
        }
    }

    public async Task<(GPTResponse, string)> GetResponse(string message)
    {
        (GPTResponse response, string msg) = await m_InputValidationService.GetResponse(message);
        Console.WriteLine($"Input validation check: {response}");
        if (response is GPTResponse.Failed or GPTResponse.Invalid or GPTResponse.Timeout)
            return (response, msg);

        (response, msg) = await m_MisinfoService.GetResponse(message);
        Console.WriteLine($"Misinformation check: {response}");
        return (response, msg);
    }

    public async Task Initialize()
    {
        await m_InputValidationService.Initialize();
        await m_MisinfoService.Initialize();
    }

    public async Task Reset()
    {
        try
        {
            Monitor.Enter(m_Mutex);
            try
            {
                Monitor.Enter(m_ResetInProgressMutex);
                if (m_ResetInProgress)
                {
                    return;
                }
                m_ResetInProgress = true;
            }
            finally
            {
                Monitor.Exit(m_ResetInProgress);
            }
            
            while (true)
            {
                try
                {
                    Monitor.Enter(m_GetResponseCallCountMutex);
                    if (m_GetResponseCallCount == 0)
                        break;
                }
                finally
                {
                    Monitor.Exit(m_GetResponseCallCountMutex);
                }
                await Task.Yield();
            }
            await m_InputValidationService.Reset();
            await m_MisinfoService.Reset();
        }
        finally
        {
            Monitor.Exit(m_Mutex);
        }
    }
}

public enum GPTResponse : int
{
    Passed,
    Failed,
    Maybe,
    Invalid,
    Timeout
}

public interface IGPTService
{
    Task<(GPTResponse, string)> GetResponse(string message);
    Task Initialize();
    Task Reset();
}

public class GPTInputValidationService : IGPTService
{
    private OpenAIAPI m_OpenAI;
    private const string PROMPT = """
You're tasked with detecing if the user input is grammatically correct or is a valid mathematical equation.
If the user input is mostly grammatically correct, respond with YES.
If the user input is a syntactically correct mathematical equation, respond with YES.
If the user input mostly makes sense but has a few grammatical errors, respond with YES.
If the user input mostly makes sense but has spelling errors, respond with YES.
If the user input is a bunch of random words or characters, respond with INVALID.
If the user input contains obviously harmful information, respond with NO.
If the user input is a bit weird, but is still mostly grammatical, respond with MAYBE.
If the user input makes no sense logically but is grammatically correct, respond with MAYBE.
The first word of your response can either be YES, NO, MAYBE or INVALID. It is strictly forbidden for the first word of your response to be anything else.
You then MUST give a reason for your response. The reason MUST be a single sentence.
""";
    private List<ChatMessage> m_Messages = new List<ChatMessage>();
    
    private const string SHORT_PROMPT = "Is the sentence grammtically correct WITHOUT considering the meaning (YES for grammatical, INVALID for ungrammatical (without considering the correctness of the content), NO for harmful, MAYBE for unsure): ";
    
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
            new ChatMessage(ChatMessageRole.Assistant, "YES. The syntax is correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("1 + 1 = 2")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The syntax is correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("The quick brown fox jumps over the lazy dog")),
            new ChatMessage(ChatMessageRole.Assistant, "YES. The sentence is grammatically correct."),
            new ChatMessage(ChatMessageRole.User, Preprocess("Pfizer manufactured the COVID virus.")),
            new ChatMessage(ChatMessageRole.Assistant, "NO. The sentence is grammatically correct but it contains harmful information."),
        };
    }

    public async Task<(GPTResponse, string)> GetResponse(string message)
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
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(20))) != resultTask)
        {
            return (GPTResponse.Timeout, "");
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
            return (GPTResponse.Passed, resultString);
        }
        if (resultString.StartsWith("NO"))
        {
            return (GPTResponse.Failed, resultString);
        }
        if (resultString.StartsWith("MAYBE"))
        {
            return (GPTResponse.Maybe, resultString);
        }
        if (resultString.StartsWith("INVALID"))
        {
            return (GPTResponse.Invalid, resultString);
        }
        return (GPTResponse.Invalid, resultString);
    }

    public async Task Initialize()
    {
        BEGIN:
        Task<ChatResult> resultTask = m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.5,
            MaxTokens = 2000,
            Messages = new List<ChatMessage>
            {
                new ChatMessage(ChatMessageRole.System, PROMPT)
            }
        });
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(60))) != resultTask)
        {
            Console.WriteLine("Timeout");
            goto BEGIN;
        }
        ChatResult result = resultTask.Result;
        Console.WriteLine(result.ToString());
    }

    public async Task Reset()
    {
        
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
It is strictly forbidden to detect any correct mathematical formulas or equations as misinformation.
It is strictly forbidden to detect any opinionated statements as misinformation.
You then MUST give a reason for your response. The reason MUST be a single sentence.
Here are some sample inputs and their expected outputs:
""";
    private const string SHORT_PROMPT = "YES for misinformation, NO for not misinformation, MAYBE for unsure.";
    
    private string Preprocess(string message)
    {
        return SHORT_PROMPT + message;
    }
    
    public GPTMisinformationCheckService(OpenAIAPI api)
    {
        m_OpenAI = api;
        m_Messages = new List<ChatMessage>(
            new[]
            {
                new ChatMessage(ChatMessageRole.System, PROMPT),
                new ChatMessage(ChatMessageRole.User, Preprocess("Covid 19 is a hoax")),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, Preprocess("The earth is flat")),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, Preprocess("The moon landing was faked")),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, Preprocess("The earth is round")),
                new ChatMessage(ChatMessageRole.Assistant, "YES"),
                new ChatMessage(ChatMessageRole.User, Preprocess("1 + 1 = 2")),
                new ChatMessage(ChatMessageRole.Assistant, "YES"),
                new ChatMessage(ChatMessageRole.User, Preprocess("1 + 1 = 3")),
                new ChatMessage(ChatMessageRole.Assistant, "NO"),
                new ChatMessage(ChatMessageRole.User, Preprocess("red is a color")),
                new ChatMessage(ChatMessageRole.Assistant, "YES"),
                new ChatMessage(ChatMessageRole.User, Preprocess("red is the best color")),
                new ChatMessage(ChatMessageRole.Assistant, "MAYBE"),
            }
        );
        s_Instance = this;
    }
    
    public async Task Initialize()
    {
        BEGIN:
        Task<ChatResult> resultTask = m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.8,
            MaxTokens = 2000,
            Messages = new List<ChatMessage>
            {
                new ChatMessage(ChatMessageRole.System, PROMPT)
            }
        });
        
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(60))) != resultTask)
        {
            Console.WriteLine("Timeout");
            goto BEGIN;
        }
        ChatResult result = resultTask.Result;
        Console.WriteLine(result.ToString());
    }

    public async Task Reset()
    {
        
    }

    public static GPTMisinformationCheckService instance => s_Instance ?? throw new Exception("GPTService not initialized");
    
    public async Task<(GPTResponse, string)> GetResponse(string message)
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
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(20))) != resultTask)
        {
            return (GPTResponse.Timeout, "");
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
            return (GPTResponse.Passed, resultString);
        }
        if (resultString.StartsWith("NO"))
        {
            return (GPTResponse.Failed, resultString);
        }
        if (resultString.StartsWith("MAYBE"))
        {
            return (GPTResponse.Maybe, resultString);
        }
        return (GPTResponse.Invalid, resultString);
    }
}
