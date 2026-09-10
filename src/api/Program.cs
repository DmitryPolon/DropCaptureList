using DropCaptureList.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSignalR();

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

var dataDirectory = DataPaths.Resolve(builder.Configuration);
builder.Services.AddSingleton(new FileDirectory(dataDirectory));
builder.Services.AddSingleton<StoreFront>();
builder.Services.AddSingleton<ListNotifier>();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { ok = true, mode = "File" }));

app.MapGet("/api/storage-mode", () => Results.Ok(new
{
    mode = "File",
    signalR = true
}));

app.MapPost("/api/session", (SignInRequest body, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        var session = store.SignIn(body.Email, body.Household);
        return Results.Ok(new
        {
            email = session.Email,
            nickname = session.Nickname,
            household = session.Household,
            motto = session.Motto,
            logoLetter = session.LogoLetter,
            userId = session.UserId,
            isAppAdmin = store.IsAppAdmin(session.Email)
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

app.MapGet("/api/households", (StoreFront store, ILogger<Program> log) =>
{
    try
    {
        return Results.Ok(store.ListHouseholds());
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

app.MapGet("/api/households/{household}/items", (string household, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        return Results.Ok(store.ListItems(household));
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not load items for {Household}.", household);
        return Results.Problem("Could not load the list.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items", async (
    string household,
    AddItemRequest body,
    StoreFront store,
    ListNotifier notifier,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        store.AddTextItem(body.Email, household, body.Text);
        await notifier.ListChanged(household);
        return Results.Ok(store.ListItems(household));
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

app.MapPost("/api/households/{household}/items/bulk", async (
    string household,
    BulkItemsRequest body,
    StoreFront store,
    ListNotifier notifier,
    ILogger<Program> log) =>
{
    try
    {
        store.UpsertItems(body.Email, household, body.Items);
        await notifier.ListChanged(household);
        return Results.Ok(store.ListItems(household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not save items for {Household}.", household);
        return Results.Problem("Could not save the list.", statusCode: 503);
    }
});

app.MapPost("/api/households/{household}/items/{itemId:guid}/toggle", async (
    string household,
    Guid itemId,
    SignInRequest body,
    StoreFront store,
    ListNotifier notifier,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        store.ToggleComplete(body.Email, household, itemId);
        await notifier.ListChanged(household);
        return Results.Ok(store.ListItems(household));
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

app.MapPost("/api/households/{household}/items/{itemId:guid}/remove", async (
    string household,
    Guid itemId,
    SignInRequest body,
    StoreFront store,
    ListNotifier notifier,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        store.RemoveItem(body.Email, household, itemId);
        await notifier.ListChanged(household);
        return Results.Ok(store.ListItems(household));
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

app.MapPost("/api/households/{household}/completed/clear", async (
    string household,
    SignInRequest body,
    StoreFront store,
    ListNotifier notifier,
    ILogger<Program> log) =>
{
    try
    {
        if (!string.Equals(body.Household, household, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Household does not match.", statusCode: 400);
        }

        store.ClearCompleted(body.Email, household);
        await notifier.ListChanged(household);
        return Results.Ok(store.ListItems(household));
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

app.MapPost("/api/households/{household}/clear", async (
    string household,
    SignInRequest body,
    StoreFront store,
    ListNotifier notifier,
    ILogger<Program> log) =>
{
    try
    {
        store.ClearAll(body.Email, household);
        await notifier.ListChanged(household);
        return Results.Ok(store.ListItems(household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not clear {Household}.", household);
        return Results.Problem("Could not clear the list.", statusCode: 503);
    }
});

app.MapGet("/api/admin/users", (string? email, StoreFront store) =>
{
    try
    {
        return Results.Ok(store.ListUsers(email ?? ""));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

app.MapGet("/api/admin/directory", (string email, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        return Results.Ok(store.ListDirectory(email));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not list household directory.");
        return Results.Problem("Could not load households.", statusCode: 503);
    }
});

app.MapGet("/api/admin/households/{userId:guid}", (Guid userId, StoreFront store) =>
    Results.Ok(store.HouseholdsForUser(userId)));

app.MapGet("/api/members", (string email, string household, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        return Results.Ok(store.ListMembers(email, household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not list members.");
        return Results.Problem("Could not load members.", statusCode: 503);
    }
});

app.MapPost("/api/members", (MemberRequest body, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        store.AddMember(body.ActorEmail, body.Household, body.Email, body.Nickname);
        return Results.Ok(store.ListMembers(body.ActorEmail, body.Household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not add member.");
        return Results.Problem("Could not add the member.", statusCode: 503);
    }
});

app.MapPost("/api/members/remove", (MemberRemoveRequest body, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        store.RemoveMember(body.ActorEmail, body.Household, body.UserId);
        return Results.Ok(store.ListMembers(body.ActorEmail, body.Household));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not remove member.");
        return Results.Problem("Could not remove the member.", statusCode: 503);
    }
});

app.MapPost("/api/admin/households", (AdminHouseholdRequest body, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        store.CreateHousehold(body.ActorEmail, body.Name, body.Motto, body.MemberEmail, body.MemberNickname);
        return Results.Ok();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not create household.");
        return Results.Problem("Could not create the household.", statusCode: 503);
    }
});

app.MapPost("/api/admin/households/delete", (AdminDeleteHouseholdRequest body, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        store.DeleteHousehold(body.ActorEmail, body.Name);
        return Results.Ok();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not delete household.");
        return Results.Problem("Could not delete the household.", statusCode: 503);
    }
});

app.MapPost("/api/admin/motto", (AdminMottoRequest body, StoreFront store, ILogger<Program> log) =>
{
    try
    {
        store.SetMotto(body.ActorEmail, body.Household, body.Motto);
        return Results.Ok();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Could not save motto.");
        return Results.Problem("Could not save the motto.", statusCode: 503);
    }
});

app.MapHub<ListHub>("/hubs/list");
app.Run();

public sealed record SignInRequest(string Email, string Household);

public sealed record AddItemRequest(string Email, string Household, string Text);

public sealed record BulkItemsRequest(string Email, string Household, List<FileItem> Items);

public sealed record MemberRequest(string ActorEmail, string Household, string Email, string Nickname);

public sealed record MemberRemoveRequest(string ActorEmail, string Household, Guid UserId);

public sealed record AdminHouseholdRequest(string ActorEmail, string Name, string? Motto, string MemberEmail, string MemberNickname);

public sealed record AdminDeleteHouseholdRequest(string ActorEmail, string Name);

public sealed record AdminMottoRequest(string ActorEmail, string Household, string Motto);
