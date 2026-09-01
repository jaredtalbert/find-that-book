using FindThatBook.Server.Gemini;
using FindThatBook.Server.Matching;
using FindThatBook.Server.Services;
using FindThatBook.Server.Services.OpenLibrary;
using Microsoft.AspNetCore.StaticFiles;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

builder.Services.AddCors(options => options.AddPolicy("BrowserClient", policy =>
    policy
        .WithOrigins("http://localhost:5235", "http://127.0.0.1:5235")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddHttpClient<IBookCatalogClient, OpenLibraryCatalogClient>(client => {
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IGeminiTextClient, GeminiTextClient>();
builder.Services.AddTransient<IQueryInterpreter, GeminiQueryInterpreter>();
builder.Services.AddSingleton<ICandidateRanker, CandidateRanker>();
builder.Services.AddTransient<IBookSearchService, BookSearchService>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.UseCors("BrowserClient");
}

app.UseHttpsRedirection();

app.UseDefaultFiles();

FileExtensionContentTypeProvider contentTypes = new();
contentTypes.Mappings[".dat"] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions {
    ContentTypeProvider = contentTypes
});

app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();