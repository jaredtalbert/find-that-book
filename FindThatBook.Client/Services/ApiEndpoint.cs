using System.Runtime.InteropServices.JavaScript;

namespace FindThatBook.Client.Services;

static class ApiEndpoint {
    internal static Uri GetBaseAddress() {
        using JSObject location = JSHost.GlobalThis.GetPropertyAsJSObject("location")
                                  ?? throw new InvalidOperationException(
                                      "The browser location is unavailable.");

        Uri clientAddress = new(location.GetPropertyAsString("origin")
                                ?? throw new InvalidOperationException(
                                    "The browser origin is unavailable."));

#if DEBUG
        if (clientAddress.IsLoopback && clientAddress.Port == 5235) {
            return new UriBuilder(clientAddress) {
                Port = 5287
            }.Uri;
        }
#endif

        return clientAddress;
    }
}