using System;
using System.Globalization;
using ImageForensic.Api;
using ImageForensics.Core;

var builder = WebApplication.CreateBuilder(args);

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

public partial class Program { }
