using System.Collections.Concurrent;
using Telegram.Bot.Types;

namespace SosuBot.Services;

/// <summary>
/// Keeps the usernames that the bot has seen together with their Telegram IDs.
/// Telegram's Bot API does not expose a method for resolving an arbitrary
/// username to a user ID, while moderation methods require the numeric ID.
/// </summary>
public sealed class TelegramUserDirectory
{
    private readonly ConcurrentDictionary<string, long> _idsByUsername =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, string> _usernamesById = new();

    public void Remember(User? user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Username))
            return;

        string username = Normalize(user.Username);
        if (username.Length == 0)
            return;

        if (_usernamesById.TryGetValue(user.Id, out string? previousUsername) &&
            !string.Equals(previousUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            _idsByUsername.TryRemove(previousUsername, out _);
        }

        _usernamesById[user.Id] = username;
        _idsByUsername[username] = user.Id;
    }

    public bool TryGetUserId(string? username, out long userId)
    {
        userId = default;
        string normalized = Normalize(username);
        return normalized.Length > 0 && _idsByUsername.TryGetValue(normalized, out userId);
    }

    public bool TryGetUsername(long userId, out string username) =>
        _usernamesById.TryGetValue(userId, out username!);

    private static string Normalize(string? username) =>
        username?.Trim().TrimStart('@') ?? string.Empty;
}
