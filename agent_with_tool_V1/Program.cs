using Azure.Identity;
using Azure.AI.Projects; //prerelease
using OpenAI.Chat;
using Azure.AI.OpenAI;
using Azure.AI.Inference;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// builder.Logging.AddApplicationInsights();

// Allow CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy
            .WithOrigins("http://localhost:3000",
                "https://calm-pond-02dfab80f.6.azurestaticapps.net"
              )
            
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Register PrivateAgent as a singleton
builder.Services.AddSingleton<agent_with_tool_V0.services.PrivateAgent>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogInformation("Creating PrivateAgent...");
    try
    {
        var connectionString = config["AzureAIStudio:ConnectionString"];
        var agentId = config["AzureAIStudio:AgentId"];
        var threadId = config["AzureAIStudio:ThreadId"];

        logger.LogInformation("Connection String: {ConnectionString}", connectionString);
        logger.LogInformation("Agent ID: {AgentId}", agentId);
        logger.LogInformation("Thread ID: {ThreadId}", threadId);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new Exception("AzureAIStudio:ConnectionString is missing or empty!");
        if (string.IsNullOrWhiteSpace(agentId))
            throw new Exception("AzureAIStudio:AgentId is missing or empty!");
        if (string.IsNullOrWhiteSpace(threadId))
            throw new Exception("AzureAIStudio:ThreadId is missing or empty!");

        return new agent_with_tool_V0.services.PrivateAgent(connectionString, agentId, threadId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create PrivateAgent");
        throw;
    }
});

var app = builder.Build();

// Add global exception handler middleware before any other middleware
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        if (errorFeature?.Error != null)
        {
            logger.LogError(errorFeature.Error, "Unhandled exception caught in middleware");
        }
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("An internal error occurred.");
    });
});

// Enable Swagger UI in all environments for debugging
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowReactApp");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", (ILogger<Program> logger, IConfiguration config) => {
    logger.LogInformation("Root endpoint hit: AI Agent API is running!");
    logger.LogInformation("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
    logger.LogInformation("AzureAIStudio:ConnectionString: {ConnectionString}", config["AzureAIStudio:ConnectionString"]);
    logger.LogInformation("AzureAIStudio:AgentId: {AgentId}", config["AzureAIStudio:AgentId"]);
    logger.LogInformation("AzureAIStudio:ThreadId: {ThreadId}", config["AzureAIStudio:ThreadId"]);
    return "AI Agent API is running!";
});

app.Run();