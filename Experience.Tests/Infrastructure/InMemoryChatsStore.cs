namespace Experience.Tests.Infrastructure;

using System.Collections.Concurrent;

public sealed class InMemoryChatsStore
{
    public const string MockManualUrl = "https://example.com/manuals/test-manual.pdf";

    private static readonly string MockResponse =
        $"This is a test response from the mock Manuals service. Try {MockManualUrl} for the product manual.";

    private static readonly string[] StreamDeltas =
    [
        "This is a test response ",
        "from the mock Manuals service. ",
        $"Try {MockManualUrl} ",
        "for the product manual.",
    ];

    private readonly ConcurrentDictionary<string, ChatRecord> _chats = new();
    private readonly ConcurrentDictionary<string, List<MessageRecord>> _messages = new();

    public sealed record ChatRecord(string ChatId, string? Title, long CreatedAt);

    public sealed record MessageRecord(string Role, string Text);

    public IReadOnlyList<ChatRecord> GetChats() =>
        [.. _chats.Values.OrderByDescending(c => c.CreatedAt)];

    public ChatRecord? GetChat(string chatId) =>
        _chats.TryGetValue(chatId, out var chat) ? chat : null;

    public ChatRecord CreateChat()
    {
        var chatId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var chat = new ChatRecord(chatId, null, createdAt);
        _chats[chatId] = chat;
        _messages[chatId] = [];
        return chat;
    }

    public bool UpdateTitle(string chatId, string title)
    {
        if (!_chats.TryGetValue(chatId, out var existing))
        {
            return false;
        }

        _chats[chatId] = existing with { Title = title };
        return true;
    }

    public bool DeleteChat(string chatId)
    {
        _messages.TryRemove(chatId, out _);
        return _chats.TryRemove(chatId, out _);
    }

    public IReadOnlyList<MessageRecord> GetMessages(string chatId) =>
        _messages.TryGetValue(chatId, out var msgs) ? [.. msgs] : [];

    public ChatRecord? CompleteMessage(string chatId, string input)
    {
        if (!_chats.TryGetValue(chatId, out var chat))
        {
            return null;
        }

        var msgs = _messages.GetOrAdd(chatId, _ => []);
        lock (msgs)
        {
            msgs.Add(new MessageRecord("user", input));
            msgs.Add(new MessageRecord("assistant", MockResponse));
        }

        if (chat.Title is null)
        {
            var title = input.Length <= 60 ? input : (input[..60] + "…");
            _chats[chatId] = chat with { Title = title };
        }

        return _chats[chatId];
    }

    public static string GetMockResponse() => MockResponse;

    public (ChatRecord? Chat, string SseBody) CompleteStream(string chatId, string input)
    {
        var chat = CompleteMessage(chatId, input);
        var body = new System.Text.StringBuilder();
        foreach (var delta in StreamDeltas)
        {
            body.Append($"data: {{\"delta\":{{\"content\":\"{delta}\"}}}}\n\n");
        }

        body.Append("data: [DONE]\n\n");
        return (chat, body.ToString());
    }

    public void Clear()
    {
        _chats.Clear();
        _messages.Clear();
    }
}
