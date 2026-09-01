namespace FindThatBook.Server.Models;

public struct GeminiResponse {

    public string Title { get; set; }

    public string Author { get; set; }

    public List<string> Keywords { get; set; }

    public long Year { get; set; }
}