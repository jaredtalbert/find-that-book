using FindThatBook.Server.Services;
using FindThatBook.Server.Services.OpenLibrary;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true);


builder.Services.AddHttpClient(OpenLibraryApiConnectionService.ServiceKey, client => {
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Each provider gets its own key and client configuration, even when sharing the interface.
builder.Services.AddKeyedTransient<IApiConnectionService>(OpenLibraryApiConnectionService.ServiceKey,
    (services, _) => new OpenLibraryApiConnectionService(
        services.GetRequiredService<IHttpClientFactory>().CreateClient(OpenLibraryApiConnectionService.ServiceKey)));

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Run();