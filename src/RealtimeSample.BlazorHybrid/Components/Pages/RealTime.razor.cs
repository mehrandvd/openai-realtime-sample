using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Realtime;
using RealtimeSample.BlazorHybrid.Services.Contracts;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Bit.BlazorUI;
using Microsoft.AspNetCore.Components;

namespace RealtimeSample.BlazorHybrid.Components.Pages
{
    [Experimental("OPENAI002")]
    public partial class RealTime(IMicrophoneService microphoneService, ISpeakerService speakerService)
    {
        List<UpdateContainer> Updates { get; } = new();
        List<UpdateContainer> RelatedUpdates { get; set; } = new();
        List<UpdateGroup> UpdateGroups { get; } = new();
        public UpdateContainer? SelectedUpdate { get; set; }
        public UpdateContainer HoveredUpdate { get; set; }
        private async Task OnStartClicked(bool arg)
        {
            _ = Task.Run(StartRealTimeTalk);
        }

        private async Task StartRealTimeTalk()
        {
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

            var micStream = microphoneService.GetStream();
            var speaker = speakerService;// new ConsoleSpeaker();

            RealtimeSession session = await realtimeClient.StartConversationSessionAsync(
                model: deployment);

            var tools = GetTools();
            var conversationTools = tools.Select(t => t.ConversationTool()).ToArray();

            ConversationSessionOptions sessionOptions = new()
            {
                Instructions = "Answer the questions happily. "
                               + "Prefer to call tools whenever applicable.",
                Voice = ConversationVoice.Alloy,
                ContentModalities = RealtimeContentModalities.Text | RealtimeContentModalities.Audio,
                InputAudioFormat = RealtimeAudioFormat.Pcm16,
                OutputAudioFormat = RealtimeAudioFormat.Pcm16,
                InputTranscriptionOptions = new() { Model = "whisper-1" },
                TurnDetectionOptions = TurnDetectionOptions.CreateServerVoiceActivityTurnDetectionOptions(
                    detectionThreshold: 0.4f,
                    silenceDuration: TimeSpan.FromMilliseconds(1000)),
            };

            foreach(var tool in conversationTools)
            {
                sessionOptions.Tools.Add(tool);
            }

            await session.ConfigureConversationSessionAsync(sessionOptions);

            await foreach (RealtimeUpdate update in session.ReceiveUpdatesAsync())
            {
                AddRealtimeUpdate(update);
                await InvokeAsync(StateHasChanged);

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
                    // With audio output enabled, the audio transcript of the delta update contains an approximation of
                    // the words spoken by the model. Without audio output, the text of the delta update will contain
                    // the segments making up the text content of a message.
                    if (deltaUpdate.AudioBytes is not null)
                    {
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
                    if (itemStreamingFinishedUpdate.FunctionCallId is not null)
                    {
                        var callId = itemStreamingFinishedUpdate.FunctionCallId;
                        var functionName = itemStreamingFinishedUpdate.FunctionName;
                        var functionCallArguments = itemStreamingFinishedUpdate.FunctionCallArguments;

                        JsonNode node = JsonNode.Parse(functionCallArguments)!;
                        AIFunctionArguments args = ToAIFunctionArguments(node);

                        string output;
                        try
                        {
                            var tool = tools.First(t => t.Name == functionName);
                            var result = await tool.InvokeAsync(args);
                            output = result?.ToString() ?? "";
                        }
                        catch(Exception ex)
                        {
                            output = $"Something went wrong when calling the tool. {ex.ToString()}";
                        }

                        RealtimeItem functionOutputItem = RealtimeItem.CreateFunctionCallOutput(
                            callId: itemStreamingFinishedUpdate.FunctionCallId,
                            output: output);

                        await session.AddItemAsync(functionOutputItem);
                    }
                }

                if (update is ResponseFinishedUpdate turnFinishedUpdate)
                {
                    // Here, if we processed tool calls in the course of the model turn, we finish the
                    // client turn to resume model generation. The next model turn will reflect the tool
                    // responses that were already provided.
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

            await Task.Delay(-1);
        }

        public static AIFunctionArguments ToAIFunctionArguments(JsonNode node)
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
                                        .Select(o=>ToAIFunctionArguments(o!)).ToArray(),
                        _ => null
                    };
                    args.Add(prop.Key, value);
                }
            }
            return args;
        }

        private static string FormatJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json ?? string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var formatted = JsonSerializer.Serialize(
                    doc.RootElement,
                    new JsonSerializerOptions { WriteIndented = true });

                return formatted;
            }
            catch
            {
                // Not valid JSON; return original
                return json!;
            }
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
            [Description("The city and state, e.g. San Francisco, CA")]
            string location,
            Unit unit)
        {
            // Fake weather data; in a real scenario this would call out to a weather service.
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

        int LastOrderNo = 0;

        private void AddRealtimeUpdate(RealtimeUpdate update)
        {
            LastOrderNo++;
            var container = new UpdateContainer(update);
            container.OrderNo = LastOrderNo;
            Updates.Add(container);

            using var doc = JsonDocument.Parse(update.GetRawContent());

            if (doc.RootElement.TryGetProperty("type", out var eventTypeElement))
            {
                container.EventType = eventTypeElement.GetString();
            }

            if (
                doc.RootElement.TryGetProperty("item_id", out JsonElement itemIdElement)
                || 
                (doc.RootElement.TryGetProperty("item", out var itemElement) && itemElement.TryGetProperty("id", out itemIdElement))
                )
            {
                var itemId = itemIdElement.GetString();
                container.ItemId = itemId;
            }

            if (
                doc.RootElement.TryGetProperty("response_id", out JsonElement responseIdElement)
                ||
                (doc.RootElement.TryGetProperty("response", out var responseElement) && responseElement.TryGetProperty("id", out responseIdElement))
            )
            {
                var responseId = responseIdElement.GetString();
                container.ResponseId = responseId;
            }

            var groupId = container.ItemId ?? Guid.NewGuid().ToString();

            var group = UpdateGroups.FirstOrDefault(i =>
                i.GroupId == groupId
                // || 
                // (i.ItemId is not null && i.ItemId == container.ItemId)
                // || (i.ResponseId is not null && i.ResponseId == container.ResponseId)
            );

            if (group == null)
            {
                group = new UpdateGroup
                {
                    GroupId = groupId,
                    ItemId = container.ItemId ?? "N/A",
                    ResponseId = container.ResponseId ?? "N/A",
                    Title = $"Group: {groupId}",
                };
                UpdateGroups.Insert(0, group);
            }

            // var container = new UpdateContainer(update);
            var lastGroup = UpdateGroups.First();

            if (lastGroup != group)
            {
                container.IsOutOfOrder = true;
            }

            group.Updates.Add(container);

            // if (container.ItemId is not null)
            // {
            //     
            //     
            // }
            // else
            // {
            //     var item = new UpdateGroup
            //     {
            //         GroupId = update.Kind.ToString(),
            //         ItemId = container.ItemId ?? "N/A",
            //         ResponseId = container.ResponseId ?? "N/A",
            //         Title = container.EventType ?? "N/A",
            //         Updates = { container }
            //     };
            //     UpdateGroups.Insert(0, item);
            // }

            InvokeAsync(StateHasChanged);
        }

        void SetSelectedUpdate(UpdateContainer update)
        {
            SelectedUpdate = update;
            RelatedUpdates = GetRelatedUpdates(update);
            StateHasChanged();
        }

        class UpdateGroup
        {
            public string GroupId { get; set; } = "";
            public string? ItemId { get; set; }
            public string? ResponseId { get; set; }

            public string Title { get; set; } = "";
            public List<UpdateContainer> Updates { get; } = [];
        }

        public List<UpdateContainer> GetRelatedUpdates(UpdateContainer update)
        {
            var query = from otherUpdate in Updates
                       where 
                            (update.ItemId is not null && update.ItemId == otherUpdate.ItemId)
                            || (update.ResponseId is not null && update.ResponseId == otherUpdate.ResponseId)
                            || (update.ConversationId is not null && update.ConversationId == otherUpdate.ConversationId)
                       select otherUpdate;

            var list = query.ToList();
            return list;
        }



        public class UpdateContainer(RealtimeUpdate rawUpdate, bool isOutOfOrder = false)
        {
            public string Id { get; } = Guid.NewGuid().ToString();
            public int OrderNo { get; set; } = -1;
            public string? ItemId { get; set; }
            public string? ResponseId { get; set; }
            public string? ConversationId { get; set; }

            public string? EventType { get; set; }
            public RealtimeUpdate RawUpdate { get; set; } = rawUpdate;
            public bool IsOutOfOrder { get; set; } = isOutOfOrder;

            public override string ToString()
            {
                return $"{Id} - {RawUpdate.Kind} - {EventType} - {ItemId}"
                ;
            }

        }

        private BitVariant GetTagVariant(UpdateContainer update)
        {
            var variant = RelatedUpdates.Any(r => r.Id == update.Id) 
                ? BitVariant.Fill 
                : BitVariant.Outline;
            return variant;
        }
    }
}
