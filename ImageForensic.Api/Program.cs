using System;
using System.Globalization;
using ImageForensic.Api;
using ImageForensics.Core;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IForensicsAnalyzer, ForensicsAnalyzer>();
builder.Services.AddRazorPages();

var app = builder.Build();

var culture = new CultureInfo("it-IT");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

app.UseStaticFiles();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI();

app.MapAnalyzerEndpoints();
app.MapRazorPages();

app.Run();

Log.CloseAndFlush();

public partial class Program { }
