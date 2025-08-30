using Azure.AI.OpenAI;
using OpenAI.Realtime;
using RealtimeSample.Console.Utility;
using System.ClientModel;
using System.Threading;

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

 ConversationSessionOptions sessionOptions = new()
 {
     Instructions = "Answer the questions happily. "
         + "Prefer to call tools whenever applicable.",
     Voice = ConversationVoice.Alloy,
     Tools = { CreateSampleWeatherTool() },
     ContentModalities = RealtimeContentModalities.Text | RealtimeContentModalities.Audio,
     InputAudioFormat = RealtimeAudioFormat.Pcm16,
     OutputAudioFormat = RealtimeAudioFormat.Pcm16,
     InputTranscriptionOptions = new() { Model = "whisper-1" },
     TurnDetectionOptions = TurnDetectionOptions.CreateServerVoiceActivityTurnDetectionOptions(
         detectionThreshold: 0.4f,
         silenceDuration: TimeSpan.FromMilliseconds(1000)),
 };

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

    // Item started updates notify that the model generation process will insert a new item into
    // the conversation and begin streaming its content via content updates.
    if (update is OutputStreamingStartedUpdate itemStreamingStartedUpdate)
    {
        Console.WriteLine($"  -- Begin streaming of new item");
        if (!string.IsNullOrEmpty(itemStreamingStartedUpdate.FunctionName))
        {
            Console.Write($"    {itemStreamingStartedUpdate.FunctionName}: ");
        }
    }

    if (update is OutputDeltaUpdate deltaUpdate)
    {
        // With audio output enabled, the audio transcript of the delta update contains an approximation of
        // the words spoken by the model. Without audio output, the text of the delta update will contain
        // the segments making up the text content of a message.
        Console.Write(deltaUpdate.AudioTranscript);
        Console.Write(deltaUpdate.Text);
        Console.Write(deltaUpdate.FunctionArguments);
        if (deltaUpdate.AudioBytes is not null)
        {
            // if (!outputAudioStreamsById.TryGetValue(deltaUpdate.ItemId, out Stream value))
            // {
            //     string filename = $"output_{sessionOptions.OutputAudioFormat}_{deltaUpdate.ItemId}.raw";
            //     value = File.OpenWrite(filename);
            //     outputAudioStreamsById[deltaUpdate.ItemId] = value;
            // }
            //
            // value.Write(deltaUpdate.AudioBytes);
            if (speaker != null && deltaUpdate.AudioBytes is not null)
            {
                await speaker.EnqueueAsync(deltaUpdate.AudioBytes.ToArray(), deltaUpdate.Text);
            }
        }
    }



    // Item finished updates arrive when all streamed data for an item has arrived and the
    // accumulated results are available. In the case of function calls, this is the point
    // where all arguments are expected to be present.
    if (update is OutputStreamingFinishedUpdate itemStreamingFinishedUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"  -- Item streaming finished, item_id={itemStreamingFinishedUpdate.ItemId}");

        if (itemStreamingFinishedUpdate.FunctionCallId is not null)
        {
            Console.WriteLine($"    + Responding to tool invoked by item: {itemStreamingFinishedUpdate.FunctionName}");
            RealtimeItem functionOutputItem = RealtimeItem.CreateFunctionCallOutput(
                callId: itemStreamingFinishedUpdate.FunctionCallId,
                output: "70 degrees Fahrenheit and sunny");
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
    
        // Here, if we processed tool calls in the course of the model turn, we finish the
        // client turn to resume model generation. The next model turn will reflect the tool
        // responses that were already provided.
        // if (turnFinishedUpdate.CreatedItems.Any(item => item.FunctionName?.Length > 0))
        // {
        //     Console.WriteLine($"  -- Ending client turn for pending tool responses");
        //     await session.StartResponseAsync();
        // }
        // else
        // {
        //     break;
        // }
    }

    if (update is RealtimeErrorUpdate errorUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"ERROR: {errorUpdate.Message}");
        break;
    }
}

Console.ReadLine();

static ConversationFunctionTool CreateSampleWeatherTool()
{
    return new ConversationFunctionTool("get_weather_for_location")
    {
        Description = "gets the weather for a location",
        Parameters = BinaryData.FromString("""
                                           {
                                             "type": "object",
                                             "properties": {
                                               "location": {
                                                 "type": "string",
                                                 "description": "The city and state, e.g. San Francisco, CA"
                                               },
                                               "unit": {
                                                 "type": "string",
                                                 "enum": ["c","f"]
                                               }
                                             },
                                             "required": ["location","unit"]
                                           }
                                           """)
    };
}