using Microsoft.Extensions.AI;
using OpenAI.Realtime;
using System.Diagnostics.CodeAnalysis;

namespace RealtimeSample.Console;

public static class AIExtensions
{
    [Experimental("OPENAI002")]
    public static ConversationFunctionTool ConversationTool(this AIFunction function)
    {
        var parametersJson = function.JsonSchema.ToString();

        return new ConversationFunctionTool(function.Name)
        {
            Description = function.Description,
            Parameters = BinaryData.FromString(parametersJson),
        };
    }
}
