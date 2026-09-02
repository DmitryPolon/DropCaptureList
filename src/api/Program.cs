using DropCaptureList.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Services.AddApplicationInsightsTelemetry();

var sql = new SqlSettings();
builder.Configuration.GetSection("Sql").Bind(sql);

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
var extra = builder.Configuration["Cors:Extra"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];
origins = origins.Concat(extra).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
if (origins.Length == 0)
{
    origins = ["http://localhost:5173"];
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
                origins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                || origin.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase)
                || origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase)
                || origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddSingleton(sql);
builder.Services.AddSingleton<AzureSql>();
builder.Services.AddSingleton<Households>();
builder.Services.AddSingleton<AppDirectory>();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapPost("/api/session", (SignInRequest body, AppDirectory directory, ILogger<Program> log) =>
{
    try
    {
        var session = directory.SignIn(body.Email, body.Household);
        return Results.Ok(new
        {
            email = session.Email,
            nickname = session.Nickname,
            household = session.Household,
            motto = session.Motto,
            logoLetter = session.LogoLetter
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Sign-in failed.");
        return Results.Problem("Could not sign in.", statusCode: 503);
    }
});

app.MapGet("/api/households", (Households households, ILogger<Program> log) =>
{
    try
    {
        return Results.Ok(households.List());
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not list households.");
        return Results.Problem("Could not load households.", statusCode: 503);
    }
});

app.MapGet("/api/households/{household}/items", (string household, AppDirectory directory, ILogger<Program> log) =>
{
    try
    {
        return Results.Ok(directory.ListItems(household));
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not load items for {Household}.", household);
        return Results.Problem("Could not load the list.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items", (
    string household,
    AddItemRequest body,
    AppDirectory directory,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        directory.AddTextItem(body.Email, household, body.Text);
        return Results.Ok(directory.ListItems(household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not add a task for {Household}.", household);
        return Results.Problem("Could not add the task.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items/{itemId:guid}/toggle", (
    string household,
    Guid itemId,
    SignInRequest body,
    AppDirectory directory,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        directory.ToggleComplete(body.Email, household, itemId);
        return Results.Ok(directory.ListItems(household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not toggle {ItemId} for {Household}.", itemId, household);
        return Results.Problem("Could not update the item.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items/{itemId:guid}/remove", (
    string household,
    Guid itemId,
    SignInRequest body,
    AppDirectory directory,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        directory.SoftDelete(body.Email, household, itemId);
        return Results.Ok(directory.ListItems(household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not remove {ItemId} for {Household}.", itemId, household);
        return Results.Problem("Could not remove the item.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/completed/clear", (
    string household,
    SignInRequest body,
    AppDirectory directory,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        directory.ClearCompleted(body.Email, household);
        return Results.Ok(directory.ListItems(household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not clear completed items for {Household}.", household);
        return Results.Problem("Could not clear completed items.", statusCode: 503);
    }
});

app.Run();

public sealed record SignInRequest(string Email, string Household);

public sealed record AddItemRequest(string Email, string Household, string Text);
