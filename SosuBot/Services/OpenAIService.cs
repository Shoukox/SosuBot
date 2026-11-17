using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using SosuBot.Configuration;
using SosuBot.Services.Ai;

#pragma warning disable OPENAI001

namespace SosuBot.Services;

/// <summary>One earlier message of the conversation as the model sees it.</summary>
public sealed record AiTurn(bool FromAssistant, string Text);

/// <param name="Context">Facts about the chat and the speaker (time, names, osu! accounts).</param>
/// <param name="History">Earlier chat messages, oldest first.</param>
/// <param name="Message">The message to answer, already formatted with its author.</param>
/// <param name="SpeakerId">Telegram id of the speaker; sent to OpenAI only as a hash.</param>
public sealed record AiReplyRequest(string Context, IReadOnlyList<AiTurn> History, string Message, long SpeakerId);

/// <param name="Text">
/// Everything the model wrote; parts meant as separate messages are split by <see cref="OpenAiService.MessageSeparator" />.
/// </param>
/// <param name="Stopped">The user stopped the generation; <paramref name="Text" /> is what was written by then.</param>
public sealed record AiReply(string Text, bool Stopped);

/// <summary>
/// Talks to the OpenAI Responses API as the bot's persona: streams the reply text and runs the osu! tools the
/// model asks for until it has answered.
/// </summary>
public sealed class OpenAiService
{
    /// <summary>The model separates messages it wants to send one after another with this marker.</summary>
    public const string MessageSeparator = "|||";

    private const int MaxToolRounds = 6;

    private readonly ResponsesClient _client;
    private readonly OpenAiConfiguration _configuration;
    private readonly AiToolbox _toolbox;
    private readonly ILogger<OpenAiService> _logger;
    private readonly Lazy<string> _persona;

    public OpenAiService(IOptions<OpenAiConfiguration> configuration, AiToolbox toolbox, ILogger<OpenAiService> logger)
    {
        _configuration = configuration.Value;
        _toolbox = toolbox;
        _logger = logger;
        string token = Environment.GetEnvironmentVariable("OPEN_AI_TOKEN") ?? _configuration.Token;
        _client = new ResponsesClient(token);
        _persona = new Lazy<string>(() =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "developer-prompt.txt")).Trim());
    }

    /// <param name="onText">Receives the whole text written so far each time it grows.</param>
    /// <param name="cancellationToken">Cancelling it stops the generation and returns what was written.</param>
    public async Task<AiReply> GenerateAsync(AiReplyRequest request, AiToolContext toolContext,
        Func<string, Task>? onText, CancellationToken cancellationToken)
    {
        List<ResponseItem> items =
        [
            ResponseItem.CreateDeveloperMessageItem(_persona.Value),
            ResponseItem.CreateDeveloperMessageItem(request.Context)
        ];
        items.AddRange(request.History.Select(turn => turn.FromAssistant
            ? ResponseItem.CreateAssistantMessageItem(turn.Text, [])
            : (ResponseItem)ResponseItem.CreateUserMessageItem(turn.Text)));
        items.Add(ResponseItem.CreateUserMessageItem(request.Message));

        var text = new StringBuilder();
        try
        {
            for (var round = 0; round < MaxToolRounds; round++)
            {
                // Text written before a tool call reads like "one sec, checking": it becomes its own message.
                int roundStart = text.Length;
                if (roundStart > 0)
                    text.Append('\n').Append(MessageSeparator).Append('\n');
                int textStart = text.Length;

                ResponseResult response = await StreamRoundAsync(CreateOptions(items, request.SpeakerId), text,
                    onText, cancellationToken);
                items.AddRange(response.OutputItems);

                // A round without text must not leave a dangling separator.
                if (text.Length == textStart)
                    text.Length = roundStart;

                FunctionCallResponseItem[] calls = response.OutputItems.OfType<FunctionCallResponseItem>().ToArray();
                if (calls.Length == 0)
                    break;

                foreach (FunctionCallResponseItem call in calls)
                {
                    _logger.LogInformation("AI calls {Tool} {Arguments}", call.FunctionName, call.FunctionArguments);
                    string output = await _toolbox.ExecuteAsync(call.FunctionName, call.FunctionArguments,
                        toolContext, cancellationToken);
                    items.Add(new FunctionCallOutputResponseItem(call.CallId, output));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new AiReply(text.ToString().Trim(), Stopped: true);
        }

        return new AiReply(text.ToString().Trim(), Stopped: false);
    }

    private CreateResponseOptions CreateOptions(IEnumerable<ResponseItem> items, long speakerId)
    {
        var options = new CreateResponseOptions
        {
            Model = _configuration.Model,
            MaxOutputTokenCount = _configuration.MaxOutputTokens,
            StreamingEnabled = true,
            ParallelToolCallsEnabled = true,
            // The same instructions start every request, so OpenAI can reuse the cached prompt prefix.
            PromptCacheKey = "sosubot-ai-persona",
            SafetyIdentifier = HashUserId(speakerId)
        };
        foreach (ResponseTool tool in _toolbox.Tools)
            options.Tools.Add(tool);
        foreach (ResponseItem item in items)
            options.InputItems.Add(item);

        if (_configuration.Temperature is { } temperature)
            options.Temperature = temperature;
        if (!string.IsNullOrWhiteSpace(_configuration.Reasoning))
        {
            options.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(_configuration.Reasoning.Trim())
            };
        }

        return options;
    }

    private async Task<ResponseResult> StreamRoundAsync(CreateResponseOptions options, StringBuilder text,
        Func<string, Task>? onText, CancellationToken cancellationToken)
    {
        ResponseResult? result = null;
        await foreach (StreamingResponseUpdate update in
                       (await _client.CreateResponseStreamingAsync(options, cancellationToken))
                       .WithCancellation(cancellationToken))
        {
            switch (update)
            {
                case StreamingResponseOutputTextDeltaUpdate delta:
                    text.Append(delta.Delta);
                    if (onText is not null)
                        await onText(text.ToString());
                    break;
                case StreamingResponseCompletedUpdate completed:
                    result = completed.Response;
                    break;
                case StreamingResponseIncompleteUpdate incomplete:
                    // Out of output tokens: keep what was written.
                    _logger.LogWarning("AI reply was cut short: {Reason}",
                        incomplete.Response?.IncompleteStatusDetails?.Reason);
                    result = incomplete.Response;
                    break;
                case StreamingResponseFailedUpdate failed:
                    throw new InvalidOperationException($"OpenAI failed: {failed.Response?.Error?.Message}");
                case StreamingResponseErrorUpdate error:
                    throw new InvalidOperationException($"OpenAI error {error.Code}: {error.Message}");
            }
        }

        return result ?? throw new InvalidOperationException("OpenAI ended the stream without a response.");
    }

    private static string HashUserId(long userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"sosubot:{userId}")))[..32].ToLowerInvariant();
}
