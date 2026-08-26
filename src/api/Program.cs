using DropCaptureList.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);

var sql = new SqlSettings();
builder.Configuration.GetSection("Sql").Bind(sql);

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton(sql);
builder.Services.AddSingleton<AzureSql>();
builder.Services.AddSingleton<Households>();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/households", (Households households) =>
{
    try
    {
        return Results.Ok(households.List());
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
    catch
    {
        return Results.Problem("Could not load households.", statusCode: 503);
    }
});

app.Run();
