using Backend_RankingGeKu.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// DI
var enginePath = builder.Configuration.GetSection("Pdf")["EnginePath"] ?? "tectonic";
builder.Services.AddSingleton(new PdfCompiler(enginePath));
builder.Services.AddSingleton<CsvParser>();
builder.Services.AddSingleton<LatexBuilder>();

// CORS (für später Angular)
builder.Services.AddCors(o => o.AddPolicy("local",
    p => p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("local");
app.MapControllers();

app.Run();