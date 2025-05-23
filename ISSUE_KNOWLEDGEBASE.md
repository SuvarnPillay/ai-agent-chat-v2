# AI Agent Chat Project – Issue Knowledgebase

This document catalogs common issues encountered during development and deployment of the AI Agent Chat full-stack application (React frontend + .NET backend), along with their solutions. Use this as a quick reference for future debugging and DevOps work.

---

## 1. **Environment Variables Not Injected or Read Correctly**
**Symptoms:**
- Frontend or backend does not pick up environment variables (e.g., API URLs, connection strings).

**Solutions:**
- For React, prefix variables with `REACT_APP_` and ensure they are set at build time.
- For .NET backend, use `IConfiguration` to read variables. Set them in Azure App Service under Configuration > Application settings.

---

## 2. **CORS Errors Between Frontend and Backend**
**Symptoms:**
- Browser blocks API requests with CORS errors.

**Solutions:**
- In `Program.cs`, configure CORS to allow both local and deployed frontend origins:
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("AllowReactApp",
          policy => policy
              .WithOrigins("http://localhost:3000", "https://<your-frontend-url>")
              .AllowAnyHeader()
              .AllowAnyMethod());
  });
  ...
  app.UseCors("AllowReactApp");
  ```

---

## 3. **API Endpoint Routing Issues (404 or Static File Handler)**
**Symptoms:**
- Azure App Service returns 404 or serves static files for API endpoints (e.g., `/api/chat/thread`).
- Error page shows handler as `StaticFile` and physical path points to a file, not a DLL.

**Solutions:**
- Ensure you are deploying the **publish output** (not source files) to Azure App Service.
- The publish output must include `web.config`, DLLs, and all runtime files.
- Project file (`.csproj`) must use `Microsoft.NET.Sdk.Web` and include:
  ```xml
  <AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>
  ```
- Deploy the contents of the publish folder to the root of the App Service (`site/wwwroot`).

---

## 4. **web.config Missing from Publish Output**
**Symptoms:**
- No `web.config` in the publish folder after `dotnet publish`.

**Solutions:**
- Change the SDK in `.csproj` to `Microsoft.NET.Sdk.Web`.
- Clean and republish:
  ```sh
  dotnet clean
  dotnet publish -c Release -o ./publish
  ```

---

## 5. **GitHub Actions Workflow Deploys Wrong Files**
**Symptoms:**
- Azure App Service does not run the latest code, or routing issues persist after deployment.

**Solutions:**
- Ensure the workflow publishes to a local folder (e.g., `myapp`), uploads that as an artifact, and deploys from that folder:
  ```yaml
  - name: dotnet publish
    run: dotnet publish agent_with_tool_V0 -c Release -o myapp
  - name: Upload artifact for deployment job
    uses: actions/upload-artifact@v4
    with:
      name: .net-app
      path: myapp
  ...
  - name: Download artifact from build job
    uses: actions/download-artifact@v4
    with:
      name: .net-app
  - name: Deploy to Azure Web App
    uses: azure/webapps-deploy@v3
    with:
      app-name: 'agent-chat'
      slot-name: 'Production'
      package: myapp
      publish-profile: ${{ secrets.AZUREAPPSERVICE_PUBLISHPROFILE }}
  ```

---

## 6. **Root Endpoint Not Accessible in Azure**
**Symptoms:**
- `/` returns "You do not have permission to view this directory or page." or similar.

**Solutions:**
- Ensure the backend is running as an ASP.NET Core app (see above for publish and deploy steps).
- Confirm `app.MapGet("/", ...)` is present in `Program.cs` and deployed.
- Confirm `web.config` is present in the deployed folder.

---

## 7. **Swagger UI Not Accessible**
**Symptoms:**
- `/swagger` returns 404 or is not found in Azure.

**Solutions:**
- Ensure `app.UseSwagger(); app.UseSwaggerUI();` are present in `Program.cs`.
- Confirm Swagger is enabled in all environments, not just Development.
- Confirm deployment includes all published files.

---

## 8. **General Debugging Tips**
- Use Azure App Service **Log stream** for real-time logs.
- Use **Advanced Tools > Go (Kudu)** to browse deployed files in `site/wwwroot`.
- Always restart the App Service after deployment.
- Test endpoints directly in the browser or with tools like Postman.

---

## 9. **Frontend API URL Issues**
**Symptoms:**
- Frontend cannot reach backend, or API URL is malformed.

**Solutions:**
- Ensure `REACT_APP_API_URL` is set correctly in the frontend environment.
- In code, remove trailing slashes before concatenating paths:
  ```js
  const apiUrl = (process.env.REACT_APP_API_URL || 'http://localhost:5000').replace(/\/+$/, '');
  ```

---

## 10. **Null Reference Warnings in Backend**
**Symptoms:**
- Warnings about possible null reference for connection strings or IDs.

**Solutions:**
- Ensure all required environment variables are set in Azure App Service.
- Add null checks or fallback values in code if needed.

---

## 11. **Frontend JavaScript Errors and API 500s/403s**
**Symptoms:**
- Console shows:
  - `Uncaught (in promise) TypeError: Cannot convert undefined or null to object at Object.keys`
  - `Uncaught (in promise) SyntaxError: Failed to execute 'json' on 'Response': Unexpected end of JSON input`
  - Network error: `Failed to load resource: the server responded with a status of 500 (Internal Server Error)` or 403 (Forbidden) from API endpoints.

**Solutions:**
- For 500 errors: Check backend logs for the cause and ensure the endpoint always returns valid JSON. Add error handling and logging in the backend controller.
- For 403 errors: Check CORS policy, authentication/authorization settings, and App Service access restrictions. Ensure the frontend origin is allowed and any required API keys or tokens are present.
- In the frontend, add error handling for failed fetch requests and invalid JSON responses. Always check for `null` or `undefined` before accessing object properties or using `Object.keys()`.

---

# Diagnose .NET 9 Web API 500 Errors and Missing Logs on Azure App Service

## Project Overview

- **Backend:** .NET 9 Web API (`agent_with_tool_V0`)
- **Frontend:** React (Azure Static Web App)
- **Backend Deployed To:** Azure App Service (Windows)
- **Key Files:**  
  - `Program.cs` (see code below)
  - `agent_with_tool_V0.csproj` (see below)
  - `ChatController.cs`
  - `services/PrivateAgent.cs`
  - `appsettings.json` (not used for secrets in Azure)
- **Azure App Service Configuration:** Environment variables set in portal

---

## Problem Summary

- The root endpoint `/` works and logs as expected.
- All controller endpoints (e.g., `/api/chat/thread`) return HTTP 500 errors in Azure.
- No useful logs are visible in Azure Log Stream, Application Insights, or Kudu.
- The same code works locally.
- The error persists even after adding extensive ILogger-based logging and explicit error handling.
- Console.WriteLine output does not appear in Azure logs (expected).
- Environment variables are set in Azure App Service, but their values are not visible in logs.

---

## Key Code Excerpt (`Program.cs`)

```csharp
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
```

---

## Project File (`agent_with_tool_V0.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Azure.AI.Inference" Version="1.0.0-beta.5" />
    <PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
    <PackageReference Include="Azure.AI.Projects" Version="1.0.0-beta.8" />
    <PackageReference Include="Azure.Identity" Version="1.14.0" />
    <PackageReference Include="Azure.Search.Documents" Version="11.6.0" />
    <PackageReference Include="Microsoft.AspNetCore.App" Version="2.2.8" />
    <PackageReference Include="Microsoft.AspNetCore.Cors" Version="2.3.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.5" />
    <PackageReference Include="Microsoft.Extensions.Configuration.FileExtensions" Version="9.0.5" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.5" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="8.1.1" />
  </ItemGroup>
</Project>
```

---

## Azure App Service Configuration

- Environment variables are set as:
  - `AzureAIStudio__ConnectionString`
  - `AzureAIStudio__AgentId`
  - `AzureAIStudio__ThreadId`
- (Double underscores for nested config keys, as required by .NET.)

---

## Troubleshooting Already Done

- Replaced all `Console.WriteLine` with `ILogger` logging.
- Added explicit null/empty checks for all required environment variables.
- Added logging of environment variable values at startup and in the root endpoint.
- Confirmed that the root endpoint works and logs as expected.
- Confirmed that controller endpoints return 500 errors and do not log anything.
- Checked Azure Log Stream, Application Insights, and Kudu logs—no relevant logs appear for controller endpoint hits.
- Ensured Application Logging (Filesystem) is enabled and set to Information level.
- Confirmed environment variable names in Azure use double underscores.
- Confirmed the same code works locally with environment variables set.

---

## Observations

- The root endpoint (`/`) is mapped directly in `Program.cs` and does not depend on DI or controllers. It works and logs as expected.
- All controller endpoints (which depend on DI and the `PrivateAgent` singleton) fail with 500 errors and do not log.
- The DI registration for `PrivateAgent` logs and throws if any required environment variable is missing, but these logs do not appear when hitting controller endpoints.
- This suggests the DI container is failing to construct the controller or its dependencies, and the error is not surfacing in logs.
- No logs are written when controller endpoints are hit, even though logging is present in the DI setup.

---

## Suspected Root Causes

- Environment variables are not being read by the app in Azure, despite being set.
- The DI container fails to construct `PrivateAgent`, causing all controller endpoints to fail.
- Logging is not working for controller endpoint failures, possibly due to the failure occurring before the logger is available or due to a misconfiguration.
- There may be a global exception handler missing, so unhandled exceptions are not being logged.
- The app targets .NET 9, which may not be fully supported on Azure App Service (as of May 2025), possibly causing runtime issues.

---

## What Works

- The root endpoint `/` (does not use DI or controllers).
- Logging from the root endpoint appears in Azure logs.

## What Does Not Work

- All controller endpoints (use DI and `PrivateAgent`).
- No logs from controller endpoints or DI failures.

---

## What Is Needed

- A way to surface and log DI/container errors globally.
- A way to confirm what environment variables are actually visible to the app at runtime in Azure.
- Any additional Azure or .NET configuration that could affect environment variable visibility or logging.
- Confirmation that .NET 9 is fully supported on the current Azure App Service plan/environment.

---

**Please provide a solution or further debugging steps to surface the root cause and resolve the 500 errors and missing logs for controller endpoints in this .NET 9 Web API deployed to Azure App Service.**
