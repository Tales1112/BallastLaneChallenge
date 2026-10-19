using TaskFlow.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMvcConfiguration()
    .AddSecurityConfiguration(builder.Environment)
    .AddApplicationServices()
    .AddInfrastructureServices();

var app = builder.Build();

app.UseTaskFlowPipeline();
app.MapTaskFlowEndpoints();
await app.InitializeDatabaseAsync();
await app.RunAsync();

// Exposes the entry point to the HTTP integration tests.
public partial class Program { }
