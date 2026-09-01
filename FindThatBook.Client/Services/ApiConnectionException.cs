using System.Net;

namespace FindThatBook.Client.Services;

public sealed class ApiConnectionException(string message, HttpStatusCode statusCode)
    : Exception(message) {
    public HttpStatusCode StatusCode { get; } = statusCode;
}