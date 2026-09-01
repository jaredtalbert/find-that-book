using FindThatBook.Server.Matching;

namespace FindThatBook.Server.Gemini;

public interface IQueryInterpreter {
    Task<QueryIntent> InterpretAsync(string query, CancellationToken cancellationToken = default);
}