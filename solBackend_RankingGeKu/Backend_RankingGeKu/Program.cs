using Backend_RankingGeKu.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var enginePath = PdfCompiler.ResolveEnginePath(builder.Configuration.GetSection("Pdf")["EnginePath"]);
builder.Services.AddSingleton(new PdfCompiler(enginePath));
builder.Services.AddSingleton<CsvParser>();
builder.Services.AddSingleton<LatexBuilder>();

builder.Services.AddCors(o => o.AddPolicy("local", p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .AllowAnyMethod()
));

var app = builder.Build();

app.UseRouting(); // wichtig für CORS/Endpoints

var useHttpsRedirect = builder.Configuration.GetValue<bool>("UseHttpsRedirection", false);
if (useHttpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseCors("local");
app.MapControllers();

app.Run();
