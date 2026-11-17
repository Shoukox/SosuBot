using System.Collections.Concurrent;
using Telegram.Bot.Types;

namespace SosuBot.Services;

/// <summary>
/// Keeps the usernames that the bot has seen together with their Telegram IDs.
/// Telegram's Bot API does not expose a method for resolving an arbitrary
/// username to a user ID, while moderation methods require the numeric ID.
/// Every update feeds the directory, so it keeps only the most recently seen users.
/// </summary>
public sealed class TelegramUserDirectory
{
    /// <summary>Above this many users the least recently seen tenth is forgotten.</summary>
    public const int MaxUsers = 100_000;

    private readonly ConcurrentDictionary<string, long> _idsByUsername =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, (string Username, long LastSeen)> _usersById = new();
    private int _trimming;

    public int Count => _usersById.Count;

    public void Remember(User? user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Username))
            return;

        string username = Normalize(user.Username);
        if (username.Length == 0)
            return;

        if (_usersById.TryGetValue(user.Id, out var previous) &&
            !string.Equals(previous.Username, username, StringComparison.OrdinalIgnoreCase))
        {
            // Only drop the old name if nobody else has taken it since.
            _idsByUsername.TryRemove(new KeyValuePair<string, long>(previous.Username, user.Id));
        }

        _usersById[user.Id] = (username, Environment.TickCount64);
        _idsByUsername[username] = user.Id;

        if (_usersById.Count > MaxUsers)
            ForgetLeastRecentlySeen();
    }

    public bool TryGetUserId(string? username, out long userId)
    {
        userId = default;
        string normalized = Normalize(username);
        return normalized.Length > 0 && _idsByUsername.TryGetValue(normalized, out userId);
    }

    public bool TryGetUsername(long userId, out string username)
    {
        bool found = _usersById.TryGetValue(userId, out var entry);
        username = entry.Username;
        return found;
    }

    private void ForgetLeastRecentlySeen()
    {
        if (Interlocked.Exchange(ref _trimming, 1) == 1)
            return;

        try
        {
            int excess = _usersById.Count - MaxUsers / 10 * 9;
            if (excess <= 0)
                return;

            foreach (KeyValuePair<long, (string Username, long LastSeen)> user in _usersById
                         .OrderBy(pair => pair.Value.LastSeen)
                         .Take(excess)
                         .ToList())
            {
                // Skip users seen again while trimming: their entry no longer matches.
                if (_usersById.TryRemove(user))
                    _idsByUsername.TryRemove(new KeyValuePair<string, long>(user.Value.Username, user.Key));
            }
        }
        finally
        {
            Volatile.Write(ref _trimming, 0);
        }
    }

    private static string Normalize(string? username) =>
        username?.Trim().TrimStart('@') ?? string.Empty;
}
