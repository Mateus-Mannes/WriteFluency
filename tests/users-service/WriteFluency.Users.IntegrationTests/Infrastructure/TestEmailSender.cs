using System.Collections.Concurrent;
using WriteFluency.Users.WebApi.Email;

namespace WriteFluency.Users.IntegrationTests.Infrastructure;

public sealed class TestEmailSender : IAppEmailSender
{
    private readonly ConcurrentQueue<TestEmailMessage> _messages = new();

    public Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new TestEmailMessage(toEmail, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }

    public IReadOnlyList<TestEmailMessage> Messages => _messages.ToArray();

    public TestEmailMessage? FindLastBySubjectContains(string subjectPart)
    {
        return _messages.ToArray().LastOrDefault(m =>
            m.Subject.Contains(subjectPart, StringComparison.OrdinalIgnoreCase));
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}

public sealed record TestEmailMessage(string ToEmail, string Subject, string HtmlBody, string TextBody);
