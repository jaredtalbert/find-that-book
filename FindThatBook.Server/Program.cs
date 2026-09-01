using FindThatBook.Server.Gemini;
using FindThatBook.Server.Matching;
using FindThatBook.Server.Services;
using FindThatBook.Server.Services.OpenLibrary;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

builder.Services.AddHttpClient<IBookCatalogClient, OpenLibraryCatalogClient>(client => {
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<IGeminiTextClient, GeminiTextClient>();
builder.Services.AddTransient<IQueryInterpreter, GeminiQueryInterpreter>();
builder.Services.AddSingleton<ICandidateRanker, CandidateRanker>();
builder.Services.AddTransient<IBookSearchService, BookSearchService>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Run();