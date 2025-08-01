using System;
using System.Globalization;
using System.Net.Http;
using Microsoft.AspNetCore.Components;
using AntDesign;
using ImageForensic.Api;
using ImageForensics.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IForensicsAnalyzer, ForensicsAnalyzer>();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntDesign();
builder.Services.AddHttpClient();

var app = builder.Build();

var culture = new CultureInfo("it-IT");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
LocaleProvider.DefaultLanguage = "it-IT";

app.UseStaticFiles();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapAnalyzerEndpoints();

app.Run();

public partial class Program { }
