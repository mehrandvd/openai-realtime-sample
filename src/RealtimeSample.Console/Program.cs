using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Realtime;
using RealtimeSample.Console; // for AIExtensions
using RealtimeSample.Console.Utility;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

#pragma warning disable OPENAI002

Console.WriteLine("This is a comprehensive OpenAI Realtime API sample!");

var endpoint = Environment.GetEnvironmentVariable("AzureOpenAI:gpt4o-realtime:Endpoint")
    ?? throw new InvalidOperationException("Endpoint not found");

var apiKey = Environment.GetEnvironmentVariable("AzureOpenAI:gpt4o-realtime:Key")
    ?? throw new InvalidOperationException("API key not found");

var deployment = Environment.GetEnvironmentVariable("AzureOpenAI:gpt4o-realtime:Deployment")
             ?? throw new InvalidOperationException("Deployment not found");

var client = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new ApiKeyCredential(apiKey));

var realtimeClient = client.GetRealtimeClient();

await using
var micStream = ConsoleMicrophone.Start();
var speaker = new ConsoleSpeaker();

using RealtimeSession session = await realtimeClient.StartConversationSessionAsync(
    model: deployment);

// Build AIFunction list and convert to conversation tools
var tools = GetTools();
var conversationTools = tools.Select(t => t.ConversationTool()).ToArray();

ConversationSessionOptions sessionOptions = new()
{
    Instructions = "Answer the questions happily. Prefer to call tools whenever applicable.",
    Voice = ConversationVoice.Alloy,
    ContentModalities = RealtimeContentModalities.Text | RealtimeContentModalities.Audio,
    InputAudioFormat = RealtimeAudioFormat.Pcm16,
    OutputAudioFormat = RealtimeAudioFormat.Pcm16,
    InputTranscriptionOptions = new() { Model = "whisper-1" },
    TurnDetectionOptions = TurnDetectionOptions.CreateServerVoiceActivityTurnDetectionOptions(
        detectionThreshold: 0.4f,
        silenceDuration: TimeSpan.FromMilliseconds(1000)),
};

foreach (var tool in conversationTools)
{
    sessionOptions.Tools.Add(tool);
}

await session.ConfigureConversationSessionAsync(sessionOptions);

await foreach (RealtimeUpdate update in session.ReceiveUpdatesAsync())
{
    Console.WriteLine($"--- Update received: {update.GetType().Name}");

    if (update is ConversationSessionStartedUpdate sessionStartedUpdate)
    {
        Console.WriteLine($"<<< Session started. ID: {sessionStartedUpdate.SessionId}");
        Console.WriteLine();
        _ = Task.Run(async () => await session.SendInputAudioAsync(micStream));
    }

    if (update is InputAudioSpeechStartedUpdate speechStartedUpdate)
    {
        Console.WriteLine(
            $"  -- Voice activity detection started at {speechStartedUpdate.AudioStartTime}");
    }

    if (update is InputAudioSpeechFinishedUpdate speechFinishedUpdate)
    {
        Console.WriteLine(
            $"  -- Voice activity detection ended at {speechFinishedUpdate.AudioEndTime}");
    }

    if (update is OutputDeltaUpdate deltaUpdate)
    {
        Console.Write(deltaUpdate.AudioTranscript);
        Console.Write(deltaUpdate.Text);
        Console.Write(deltaUpdate.FunctionArguments);
        if (deltaUpdate.AudioBytes is not null)
        {
            if (speaker != null && deltaUpdate.AudioBytes is not null)
            {
                await speaker.EnqueueAsync(deltaUpdate.AudioBytes.ToArray(), deltaUpdate.Text);
            }
        }
    }

    if (update is OutputStreamingFinishedUpdate itemStreamingFinishedUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"  -- Item streaming finished, item_id={itemStreamingFinishedUpdate.ItemId}");

        if (itemStreamingFinishedUpdate.FunctionCallId is not null)
        {
            // Process function/tool invocation
            var callId = itemStreamingFinishedUpdate.FunctionCallId;
            var functionName = itemStreamingFinishedUpdate.FunctionName;
            var functionCallArguments = itemStreamingFinishedUpdate.FunctionCallArguments;

            JsonNode node = JsonNode.Parse(functionCallArguments)!;
            AIFunctionArguments functionArgs = ToAIFunctionArguments(node);

            string output;
            try
            {
                var tool = tools.First(t => t.Name == functionName);
                var result = await tool.InvokeAsync(functionArgs);
                output = result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                output = $"Tool execution failed: {ex.Message}";
            }

            RealtimeItem functionOutputItem = RealtimeItem.CreateFunctionCallOutput(
                callId: callId,
                output: output);

            await session.AddItemAsync(functionOutputItem);
        }
        else if (itemStreamingFinishedUpdate.MessageContentParts?.Count > 0)
        {
            Console.Write($"    + [{itemStreamingFinishedUpdate.MessageRole}]: ");
            foreach (ConversationContentPart contentPart in itemStreamingFinishedUpdate.MessageContentParts)
            {
                Console.Write(contentPart.AudioTranscript);
            }
            Console.WriteLine();
        }
    }

    if (update is InputAudioTranscriptionFinishedUpdate transcriptionCompletedUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"  -- User audio transcript: {transcriptionCompletedUpdate.Transcript}");
        Console.WriteLine();
    }

    if (update is ResponseFinishedUpdate turnFinishedUpdate)
    {
        Console.WriteLine($"  -- Model turn generation finished. Status: {turnFinishedUpdate.Status}");

        if (turnFinishedUpdate.CreatedItems.Any(item => item.FunctionName?.Length > 0))
        {
            Console.WriteLine($"  -- Ending client turn for pending tool responses");
            await session.StartResponseAsync();
        }
    }

    if (update is RealtimeErrorUpdate errorUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"ERROR: {errorUpdate.Message}");
        break;
    }
}

Console.ReadLine();

static AIFunctionArguments ToAIFunctionArguments(JsonNode node)
{
    var args = new AIFunctionArguments();
    if (node is JsonObject obj)
    {
        foreach (var prop in obj)
        {
            object? value = prop.Value switch
            {
                JsonValue v when v.TryGetValue<decimal>(out var dec) => dec,
                JsonValue v when v.TryGetValue<bool>(out var b) => b,
                JsonValue v when v.TryGetValue<string?>(out var s) => s,
                JsonObject o => ToAIFunctionArguments(o),
                JsonArray a => a.Where(o => o is not null)
                                .Select(o => ToAIFunctionArguments(o!)).ToArray(),
                _ => null
            };
            args.Add(prop.Key, value);
        }
    }
    return args;
}

static AIFunction[] GetTools()
{
    return
    [
        AIFunctionFactory.Create(GetWeather)
    ];
}

[Description("gets the weather for a location")]
static string GetWeather(
    [Description("The city and state, e.g. San Francisco, CA")] string location,
    Unit unit)
{
    return unit switch
    {
        Unit.C => $"The weather in {location} is 21 degrees Celsius and sunny.",
        Unit.F => $"The weather in {location} is 70 degrees Fahrenheit and sunny.",
        _ => $"The weather in {location} is sunny.",
    };
}

enum Unit
{
    C,
    F
}