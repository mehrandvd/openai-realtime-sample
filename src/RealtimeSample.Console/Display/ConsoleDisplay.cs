using Spectre.Console;
using System.Text;

namespace RealtimeSample.Console.Display;

public static class ConsoleDisplay
{
    private static readonly object _lock = new();
    private static readonly StringBuilder _currentAssistantResponse = new();
    private static bool _isStreaming = false;

    public static void ShowWelcome()
    {
        lock (_lock)
        {
            var panel = new Panel("[bold blue]OpenAI Realtime API Sample[/]")
            {
                Header = new PanelHeader("[yellow]🎤 Real-time Voice Conversation[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Blue)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }

    public static void ShowSessionStarted(string sessionId)
    {
        lock (_lock)
        {
            var panel = new Panel($"[green]Session ID:[/] [white]{sessionId}[/]")
            {
                Header = new PanelHeader("[green]✅ Session Started[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }

    public static void ShowVoiceActivityStarted(TimeSpan startTime)
    {
        lock (_lock)
        {
            AnsiConsole.MarkupLine($"[dim]🎤 Voice activity started at {startTime}[/]");
        }
    }

    public static void ShowVoiceActivityEnded(TimeSpan endTime)
    {
        lock (_lock)
        {
            AnsiConsole.MarkupLine($"[dim]🔇 Voice activity ended at {endTime}[/]");
        }
    }

    public static void StartAssistantResponse()
    {
        lock (_lock)
        {
            if (!_isStreaming)
            {
                _isStreaming = true;
                _currentAssistantResponse.Clear();
                AnsiConsole.Markup("[bold cyan]🤖 Assistant:[/] ");
            }
        }
    }

    public static void AppendToAssistantResponse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        lock (_lock)
        {
            _currentAssistantResponse.Append(text);
            AnsiConsole.Markup($"[cyan]{text.EscapeMarkup()}[/]");
        }
    }

    public static void FinishAssistantResponse()
    {
        lock (_lock)
        {
            if (_isStreaming)
            {
                AnsiConsole.WriteLine();
                _isStreaming = false;
            }
        }
    }

    public static void ShowUserTranscript(string transcript)
    {
        lock (_lock)
        {
            var panel = new Panel($"[white]{transcript.EscapeMarkup()}[/]")
            {
                Header = new PanelHeader("[blue]👤 User[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Blue)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }

    public static void ShowFunctionCall(string functionName, string arguments)
    {
        lock (_lock)
        {
            var panel = new Panel($"[yellow]Function:[/] [white]{functionName.EscapeMarkup()}[/]\n[yellow]Arguments:[/] [dim]{arguments.EscapeMarkup()}[/]")
            {
                Header = new PanelHeader("[yellow]🔧 Tool Call[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };

            AnsiConsole.Write(panel);
        }
    }

    public static void ShowFunctionResult(string result)
    {
        lock (_lock)
        {
            var panel = new Panel($"[green]{result.EscapeMarkup()}[/]")
            {
                Header = new PanelHeader("[green]📋 Tool Result[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }

    public static void ShowTurnFinished(string status)
    {
        lock (_lock)
        {
            AnsiConsole.MarkupLine($"[dim]✅ Turn finished with status: {status.EscapeMarkup()}[/]");
        }
    }

    public static void ShowError(string errorMessage)
    {
        lock (_lock)
        {
            var panel = new Panel($"[red]{errorMessage.EscapeMarkup()}[/]")
            {
                Header = new PanelHeader("[red]❌ Error[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }

    public static void ShowDebugInfo(string message)
    {
        lock (_lock)
        {
            AnsiConsole.MarkupLine($"[dim]--- {message.EscapeMarkup()}[/]");
        }
    }

    public static void ShowStreamingFinished(string itemId)
    {
        lock (_lock)
        {
            AnsiConsole.MarkupLine($"[dim]📤 Item streaming finished: {itemId.EscapeMarkup()}[/]");
        }
    }

    public static void ShowPendingToolResponses()
    {
        lock (_lock)
        {
            AnsiConsole.MarkupLine("[dim]⏳ Waiting for tool responses...[/]");
        }
    }
}