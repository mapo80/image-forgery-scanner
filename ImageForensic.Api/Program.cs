using ImageForensic.Api;
using ImageForensics.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IForensicsAnalyzer, ForensicsAnalyzer>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapAnalyzerEndpoints();

app.Run();

public partial class Program { }
