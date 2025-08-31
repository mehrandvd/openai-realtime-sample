using Microsoft.Extensions.AI;
using OpenAI.Realtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeSample.BlazorHybrid;

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