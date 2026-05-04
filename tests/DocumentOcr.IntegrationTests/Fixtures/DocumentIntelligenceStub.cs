using DocumentOcr.Processor.Services;

namespace DocumentOcr.IntegrationTests.Fixtures;

/// <summary>
/// In-process stub for <see cref="IDocumentIntelligenceService"/> used by
/// processor integration tests. Each invocation returns the next entry from
/// a queue of canned responses, allowing tests to script per-page extraction
/// without calling the live Azure cognitive service.
/// </summary>
public sealed class DocumentIntelligenceStub : IDocumentIntelligenceService
{
    private readonly Queue<Dictionary<string, object>> _responses = new();
    private readonly Dictionary<string, object> _default;

    public DocumentIntelligenceStub(Dictionary<string, object>? defaultResponse = null)
    {
        _default = defaultResponse ?? new Dictionary<string, object>();
    }

    public int CallCount { get; private set; }

    public DocumentIntelligenceStub Enqueue(Dictionary<string, object> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public Task<Dictionary<string, object>> AnalyzeDocumentAsync(Stream documentStream)
    {
        CallCount++;
        var next = _responses.Count > 0 ? _responses.Dequeue() : _default;
        return Task.FromResult(next);
    }
}
