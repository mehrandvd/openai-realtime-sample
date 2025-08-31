# OpenAI Realtime .NET Samples

This repository contains two C# samples (Console + .NET MAUI Blazor Hybrid) that demonstrate a common pain point for .NET developers using the OpenAI Realtime API with Azure OpenAI: integrating `Microsoft.Extensions.AI` `AIFunction`s as Realtime conversation tools.

## The Problem
Most C# examples of the Realtime API:
- Hard‑code `ConversationFunctionTool` JSON parameter schemas manually.
- Do not leverage the strongly typed `AIFunction` / `AIFunctionFactory` model binding & description metadata.
- Leave developers duplicating function shape, descriptions, and enums in fragile JSON.

When moving from regular chat completions (where `AIFunction` works seamlessly) to the Realtime streaming model, developers often ask:
"How do I reuse my existing `AIFunction` definitions as Realtime tools without rewriting JSON?"

## The Approach Shown Here
Both samples show how to:
1. Define strongly typed functions using `AIFunctionFactory.Create(...)` with `[Description]` attributes and enums.
2. Convert those `AIFunction` instances into `ConversationFunctionTool` objects expected by the Realtime API.
3. Capture tool invocation events (function call IDs + JSON argument payload) and map them back to the original `AIFunction` for execution.
4. Serialize the tool result back to the Realtime session using `RealtimeItem.CreateFunctionCallOutput`.

A small shared pattern enables this:
```csharp
public static ConversationFunctionTool ConversationTool(this AIFunction function) =>
    new(function.Name) { Description = function.Description, Parameters = BinaryData.FromString(function.JsonSchema.ToString()) };
```
(The full version lives in each project as `AIExtensions.cs`).

Then:
```csharp
var tools = GetTools();                // AIFunction[]
var conversationTools = tools.Select(t => t.ConversationTool());
sessionOptions.Tools.AddRange(conversationTools);
```
And when a tool call finishes streaming:
```csharp
var tool = tools.First(t => t.Name == functionName);
var result = await tool.InvokeAsync(parsedArgs);
await session.AddItemAsync(RealtimeItem.CreateFunctionCallOutput(callId, result?.ToString() ?? ""));
```

## Projects
### 1. RealtimeSample.Console
A minimal, linear sample to grasp the core concept:
- Shows the end‑to‑end flow with a single weather function.
- Easiest place to copy the pattern into your own app.

### 2. RealtimeSample.BlazorHybrid (.NET MAUI)
A richer, event‑centric view:
- Visual grouping of `RealtimeUpdate` objects.
- Demonstrates how Realtime emits: session start, input speech events, delta/content streaming, tool invocation, function completion, and response finalization.
- Helps you understand ordering and why you must wait for `OutputStreamingFinishedUpdate` before invoking a tool.

## Key Takeaways
- You can reuse your existing `AIFunction` definitions—no manual JSON duplication required.
- The JsonSchema already produced by `AIFunction` is directly consumable by `ConversationFunctionTool`.
- Always respond to a tool call with a `FunctionCallOutput` item referencing the original `function_call_id`.
- After supplying tool outputs, trigger a new model turn (`StartResponseAsync`) so the model can incorporate results.

## Extending
Add more functions by returning them from `GetTools()` in each project. Enums and descriptions automatically flow into the tool schema.

## Requirements
- .NET 9
- Azure OpenAI Realtime deployment (environment variables used: `AzureOpenAI:gpt4o-realtime:Endpoint`, `Key`, `Deployment`).

## Summary
These samples bridge the gap between the higher-level `Microsoft.Extensions.AI` function abstraction and the lower-level OpenAI Realtime tool protocol, giving you both a simple and an introspective learning path.

Feel free to adapt the extension method into a shared library for broader reuse.
