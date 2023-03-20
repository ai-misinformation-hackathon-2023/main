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
        Console.WriteLine("Reset in progress.");
        lock (m_ResetInProgressMutex)
        {
            if (m_ResetInProgress)
            {
                return;
            }
            m_ResetInProgress = true;
        }

        while (true)
        {
            lock (m_GetResponseCallCountMutex)
            {
                if (m_GetResponseCallCount == 0)
                    break;
            }
            await Task.Yield();
        }
        await m_InputValidationService.Reset();
        await m_MisinfoService.Reset();
        lock (m_ResetInProgressMutex)
        {
            m_ResetInProgress = false;
        }
        Console.WriteLine("Reset complete.");
    }
}

public enum GPTResponse : int
{
    Timeout,
    Grammatical,
    Ungrammatical,
    Harmful,
    Unsure,
    ContainsMisinformation,
    DoesNotContainMisinformation,
    ContainsOpinion,
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
        m_Messages = new List<ChatMessage>
        {
            new ChatMessage(ChatMessageRole.System, PROMPT)
        };
        BEGIN:
        Task<ChatResult> resultTask = m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.5,
            MaxTokens = 2000,
            Messages = m_Messages
        });
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(20))) != resultTask)
        {
            Console.WriteLine("Timeout");
            goto BEGIN;
        }
        ChatResult result = resultTask.Result;
        Console.WriteLine(result.ToString());
    }

    public async Task Reset()
    {
        await Initialize();
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
        s_Instance = this;
    }
    
    public async Task Initialize()
    {
        m_Messages = new List<ChatMessage>(
        new[]
        {
            new ChatMessage(ChatMessageRole.System, PROMPT)
        }
        );
        BEGIN:
        Task<ChatResult> resultTask = m_OpenAI.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.8,
            MaxTokens = 2000,
            Messages = m_Messages
        });
        
        if (await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(20))) != resultTask)
        {
            Console.WriteLine("Timeout");
            goto BEGIN;
        }
        ChatResult result = resultTask.Result;
        Console.WriteLine(result.ToString());
    }

    public async Task Reset()
    {
        await Initialize();
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
