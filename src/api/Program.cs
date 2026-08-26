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
builder.Services.AddSingleton<AppDirectory>();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapPost("/api/session", (SignInRequest body, AppDirectory directory) =>
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
    catch
    {
        return Results.Problem("Could not sign in.", statusCode: 503);
    }
});

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

app.MapGet("/api/households/{household}/items", (string household, AppDirectory directory) =>
{
    try
    {
        return Results.Ok(directory.ListItems(household));
    }
    catch
    {
        return Results.Problem("Could not load the list.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items/{itemId:guid}/toggle", (
    string household,
    Guid itemId,
    SignInRequest body,
    AppDirectory directory) =>
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
    catch
    {
        return Results.Problem("Could not update the item.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items/{itemId:guid}/remove", (
    string household,
    Guid itemId,
    SignInRequest body,
    AppDirectory directory) =>
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
    catch
    {
        return Results.Problem("Could not remove the item.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/completed/clear", (
    string household,
    SignInRequest body,
    AppDirectory directory) =>
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
    catch
    {
        return Results.Problem("Could not clear completed items.", statusCode: 503);
    }
});

app.Run();

public sealed record SignInRequest(string Email, string Household);
