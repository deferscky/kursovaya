using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<DBManager>(serviceProvider =>
{
    var dbManager = new DBManager();
    dbManager.InitializeDatabase();
    return dbManager;
});

var app = builder.Build();

app.UseCors();

static DBManager GetDBManager(HttpRequest request) => 
    request.HttpContext.RequestServices.GetRequiredService<DBManager>();

app.MapGet("/", () => "Строковый редактор - API для обработки текста");
app.MapPost("/auth/register", async (HttpRequest httpRequest) => 
{
    var dbManager = GetDBManager(httpRequest);
    
    try
    {
        Console.WriteLine("=== Запрос на регистрацию ===");
        
        using var reader = new StreamReader(httpRequest.Body);
        var body = await reader.ReadToEndAsync();
        Console.WriteLine($"Получен запрос: {body}");
        
        if (string.IsNullOrEmpty(body))
        {
            Console.WriteLine("Пустое тело запроса");
            return Results.BadRequest("Тело запроса не может быть пустым");
        }
        
        AuthRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AuthRequest>(body, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
            return Results.BadRequest($"Неверный формат JSON: {ex.Message}");
        }
        
        if (request == null)
        {
            Console.WriteLine("Не удалось десериализовать запрос");
            return Results.BadRequest("Неверный формат запроса");
        }
        
        Console.WriteLine($"Логин: {request.Login}, Пароль: {(string.IsNullOrEmpty(request.Password) ? "пустой" : "***")}");
        
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            Console.WriteLine("Логин или пароль пустые");
            return Results.BadRequest("Логин и пароль не могут быть пустыми");
        }
        
        bool registered = dbManager.RegisterUser(request.Login, request.Password);
        
        if (registered)
        {
            Console.WriteLine($"✓ Пользователь '{request.Login}' успешно зарегистрирован");
            return Results.Ok(new { message = "Пользователь зарегистрирован" });
        }
        else
        {
            Console.WriteLine($"✗ Не удалось зарегистрировать пользователя '{request.Login}' (возможно, уже существует)");
            return Results.Conflict("Пользователь с таким логином уже существует");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Критическая ошибка при регистрации: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Results.Problem($"Внутренняя ошибка сервера: {ex.Message}");
    }
});

app.MapPost("/auth/login", async (HttpRequest httpRequest) => 
{
    var dbManager = GetDBManager(httpRequest);
    
    try
    {
        Console.WriteLine("=== Запрос на вход ===");
        
        using var reader = new StreamReader(httpRequest.Body);
        var body = await reader.ReadToEndAsync();
        Console.WriteLine($"Получен запрос: {body}");
        
        if (string.IsNullOrEmpty(body))
        {
            Console.WriteLine("Пустое тело запроса");
            return Results.BadRequest("Тело запроса не может быть пустым");
        }
        
        AuthRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AuthRequest>(body, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
            return Results.BadRequest($"Неверный формат JSON: {ex.Message}");
        }
        
        if (request == null)
        {
            Console.WriteLine("Не удалось десериализовать запрос");
            return Results.BadRequest("Неверный формат запроса");
        }
        
        Console.WriteLine($"Логин: {request.Login}");
        
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            Console.WriteLine("Логин или пароль пустые");
            return Results.BadRequest("Логин и пароль не могут быть пустыми");
        }
        
        var token = dbManager.AuthenticateUser(request.Login, request.Password);
        
        if (token != null)
        {
            Console.WriteLine($"✓ Пользователь '{request.Login}' успешно вошел в систему");
            return Results.Ok(new { token = token });
        }
        
        Console.WriteLine($"✗ Неверный логин или пароль для пользователя '{request.Login}'");
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Критическая ошибка при входе: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Results.Problem($"Внутренняя ошибка сервера: {ex.Message}");
    }
});

app.MapPost("/auth/change-password", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    try
    {
        Console.WriteLine("=== Запрос на смену пароля ===");
        
        if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            Console.WriteLine("Отсутствует заголовок Authorization");
            return Results.Unauthorized();
        }
        
        var token = authHeader.ToString().Replace("Bearer ", "");
        Console.WriteLine($"Токен получен: {token}");
        
        var userId = dbManager.GetUserIdByToken(token);
        Console.WriteLine($"User ID из токена: {userId}");
        
        if (userId == null)
        {
            Console.WriteLine("Неверный токен или пользователь не найден");
            return Results.Unauthorized();
        }
        
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        Console.WriteLine($"Получен запрос: {body}");
        
        if (string.IsNullOrEmpty(body))
        {
            Console.WriteLine("Пустое тело запроса");
            return Results.BadRequest("Тело запроса не может быть пустым");
        }
        
        ChangePasswordRequest? data;
        try
        {
            data = JsonSerializer.Deserialize<ChangePasswordRequest>(body, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
            return Results.BadRequest($"Неверный формат JSON: {ex.Message}");
        }
        
        if (data == null)
        {
            Console.WriteLine("Не удалось десериализовать запрос");
            return Results.BadRequest("Неверный формат запроса");
        }
        
        Console.WriteLine($"OldPassword: {(string.IsNullOrEmpty(data.OldPassword) ? "пустой" : "***")}, NewPassword: {(string.IsNullOrEmpty(data.NewPassword) ? "пустой" : "***")}");
        
        if (string.IsNullOrEmpty(data.OldPassword) || string.IsNullOrEmpty(data.NewPassword))
        {
            Console.WriteLine("Старый или новый пароль пустые");
            return Results.BadRequest("Старый и новый пароль не могут быть пустыми");
        }
        
        bool success = dbManager.ChangePassword(userId.Value, data.OldPassword, data.NewPassword);
        
        if (success)
        {
            Console.WriteLine($"✓ Пароль успешно изменен для пользователя {userId.Value}");
            return Results.Ok(new { message = "Пароль успешно изменен" });
        }
        else
        {
            Console.WriteLine($"✗ Неверный старый пароль для пользователя {userId.Value}");
            return Results.BadRequest("Неверный старый пароль");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Критическая ошибка при смене пароля: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Results.Problem($"Внутренняя ошибка сервера: {ex.Message}");
    }
});

app.MapPost("/auth/delete-account", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        return Results.Unauthorized();
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    var userId = dbManager.GetUserIdByToken(token);
    
    if (userId == null)
        return Results.Unauthorized();
    
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonSerializer.Deserialize<DeleteAccountRequest>(body);
    
    if (data == null || string.IsNullOrEmpty(data.Password))
        return Results.BadRequest("Неверный формат запроса");
    
    if (dbManager.DeleteAccount(userId.Value, data.Password))
        return Results.Ok(new { message = "Аккаунт успешно удален" });
    
    return Results.BadRequest("Неверный пароль");
});

app.MapPost("/strings/get-all", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/get-all endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        Console.WriteLine("No authorization header");
        return Results.Unauthorized();
    }
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    Console.WriteLine($"Token received: {token}");
    
    var userId = dbManager.GetUserIdByToken(token);
    Console.WriteLine($"User ID from token: {userId}");
    
    if (userId == null)
    {
        Console.WriteLine("Invalid token or user not found");
        return Results.Unauthorized();
    }
    
    var strings = dbManager.GetUserStrings(userId.Value);
    return Results.Ok(new { strings = strings });
});

app.MapPost("/strings/save", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/save endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        Console.WriteLine("No authorization header");
        return Results.Unauthorized();
    }
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    Console.WriteLine($"Token received: {token}");
    
    var userId = dbManager.GetUserIdByToken(token);
    Console.WriteLine($"User ID from token: {userId}");
    
    if (userId == null)
    {
        Console.WriteLine("Invalid token or user not found");
        return Results.Unauthorized();
    }
    
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"Raw request body: {body}");
    
    try
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("strings", out var stringsProp) || stringsProp.GetArrayLength() == 0)
        {
            Console.WriteLine("Missing or empty strings property");
            return Results.BadRequest("Отсутствует или пустой массив строк");
        }
        
        var stringsList = new List<string>();
        foreach (var str in stringsProp.EnumerateArray())
        {
            stringsList.Add(str.GetString() ?? "");
        }
        
        Console.WriteLine($"Parsed {stringsList.Count} strings");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        dbManager.SaveUserStrings(userId.Value, stringsList.ToArray());
        stopwatch.Stop();
        
        Console.WriteLine($"Strings saved for user {userId}");
        
        var executionTime = stopwatch.ElapsedMilliseconds > 0 ? stopwatch.ElapsedMilliseconds : 1;
        var parameters = JsonSerializer.Serialize(new { strings_count = stringsList.Count });
        var resultJson = JsonSerializer.Serialize(new { saved_count = stringsList.Count });
        dbManager.SaveStringOperation(userId.Value, "save", parameters, resultJson, executionTime);
        
        return Results.Ok(new { message = "Строки успешно сохранены", count = stringsList.Count });
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"JSON parsing error: {ex.Message}");
        return Results.BadRequest($"Ошибка формата JSON: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unexpected error: {ex.Message}");
        return Results.Problem($"Неожиданная ошибка: {ex.Message}");
    }
});

app.MapPost("/strings/sort", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/sort endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        return Results.Unauthorized();
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    var userId = dbManager.GetUserIdByToken(token);
    
    if (userId == null)
        return Results.Unauthorized();
    
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        var data = JsonSerializer.Deserialize<SortRequest>(body);
        
        if (data == null || data.Strings == null || data.Strings.Length == 0)
            return Results.BadRequest("Неверный формат запроса или пустой массив строк");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = SortStrings(data.Strings, data.Ascending);
        stopwatch.Stop();
        
        var executionTime = stopwatch.ElapsedMilliseconds > 0 ? stopwatch.ElapsedMilliseconds : 1;
        
        var parameters = JsonSerializer.Serialize(new { strings_count = data.Strings.Length, ascending = data.Ascending });
        var resultJson = JsonSerializer.Serialize(new { sorted_count = result.Length });
        dbManager.SaveStringOperation(userId.Value, "sort", parameters, resultJson, executionTime);
        
        return Results.Ok(new {
            sorted_strings = result,
            executionTimeMs = executionTime
        });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest($"Ошибка JSON: {ex.Message}");
    }
});

app.MapPost("/strings/search", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/search endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        return Results.Unauthorized();
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    var userId = dbManager.GetUserIdByToken(token);
    
    if (userId == null)
        return Results.Unauthorized();
    
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        var data = JsonSerializer.Deserialize<SearchRequest>(body);
        
        if (data == null || data.Strings == null || string.IsNullOrEmpty(data.SearchText))
            return Results.BadRequest("Неверный формат запроса или пустой поисковый запрос");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = SearchInStrings(data.Strings, data.SearchText, data.CaseSensitive);
        stopwatch.Stop();
        
        var executionTime = stopwatch.ElapsedMilliseconds > 0 ? stopwatch.ElapsedMilliseconds : 1;
        
        var parameters = JsonSerializer.Serialize(new { strings_count = data.Strings.Length, search_text = data.SearchText, case_sensitive = data.CaseSensitive });
        var resultJson = JsonSerializer.Serialize(new { found_count = results.Count });
        dbManager.SaveStringOperation(userId.Value, "search", parameters, resultJson, executionTime);
        
        if (results.Count == 0)
            return Results.Ok(new { 
                message = "Совпадений не найдено",
                executionTimeMs = executionTime
            });
        
        return Results.Ok(new { 
            found_count = results.Count,
            results = results.Select(r => new { index = r.index, line = r.line }).ToArray(),
            executionTimeMs = executionTime
        });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest($"Ошибка JSON: {ex.Message}");
    }
});

app.MapPost("/strings/replace", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/replace endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        return Results.Unauthorized();
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    var userId = dbManager.GetUserIdByToken(token);
    
    if (userId == null)
        return Results.Unauthorized();
    
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        var data = JsonSerializer.Deserialize<ReplaceRequest>(body);
        
        if (data == null || data.Strings == null || string.IsNullOrEmpty(data.OldValue))
            return Results.BadRequest("Неверный формат запроса");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = ReplaceInStrings(data.Strings, data.OldValue, data.NewValue ?? "", data.CaseSensitive);
        stopwatch.Stop();
        
        var executionTime = stopwatch.ElapsedMilliseconds > 0 ? stopwatch.ElapsedMilliseconds : 1;
        
        var parameters = JsonSerializer.Serialize(new { strings_count = data.Strings.Length, old_value = data.OldValue, new_value = data.NewValue ?? "", case_sensitive = data.CaseSensitive });
        var resultJson = JsonSerializer.Serialize(new { modified_count = result.Length });
        dbManager.SaveStringOperation(userId.Value, "replace", parameters, resultJson, executionTime);
        
        return Results.Ok(new {
            modified_strings = result,
            executionTimeMs = executionTime
        });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest($"Ошибка JSON: {ex.Message}");
    }
});

app.MapPost("/strings/delete", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/delete endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        return Results.Unauthorized();
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    var userId = dbManager.GetUserIdByToken(token);
    
    if (userId == null)
        return Results.Unauthorized();
    
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        var data = JsonSerializer.Deserialize<DeleteRequest>(body);
        
        if (data == null || data.Strings == null || data.IndicesToDelete == null)
            return Results.BadRequest("Неверный формат запроса");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = DeleteLines(data.Strings, data.IndicesToDelete);
        stopwatch.Stop();
        
        var executionTime = stopwatch.ElapsedMilliseconds > 0 ? stopwatch.ElapsedMilliseconds : 1;
        var deletedCount = data.Strings.Length - result.Length;
        
        var parameters = JsonSerializer.Serialize(new { strings_count = data.Strings.Length, indices_to_delete = data.IndicesToDelete });
        var resultJson = JsonSerializer.Serialize(new { remaining_count = result.Length, deleted_count = deletedCount });
        dbManager.SaveStringOperation(userId.Value, "delete", parameters, resultJson, executionTime);
        
        return Results.Ok(new {
            remaining_strings = result,
            deleted_count = deletedCount,
            executionTimeMs = executionTime
        });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest($"Ошибка JSON: {ex.Message}");
    }
});

app.MapPost("/strings/delete-all", async (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    Console.WriteLine("/strings/delete-all endpoint called");
    
    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        Console.WriteLine("No authorization header");
        return Results.Unauthorized();
    }
    
    var token = authHeader.ToString().Replace("Bearer ", "");
    Console.WriteLine($"Token received: {token}");
    
    var userId = dbManager.GetUserIdByToken(token);
    Console.WriteLine($"User ID from token: {userId}");
    
    if (userId == null)
    {
        Console.WriteLine("Invalid token or user not found");
        return Results.Unauthorized();
    }
    
    try
    {
        bool success = dbManager.DeleteAllUserStrings(userId.Value);
        
        if (success)
        {
            Console.WriteLine($"All strings deleted for user {userId}");
            return Results.Ok(new { message = "Все строки успешно удалены" });
        }
        else
        {
            Console.WriteLine($"Failed to delete strings for user {userId}");
            return Results.Problem("Не удалось удалить строки");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting strings: {ex.Message}");
        return Results.Problem($"Ошибка при удалении строк: {ex.Message}");
    }
});

app.MapPost("/history/get", (HttpRequest request) => 
{
    var dbManager = GetDBManager(request);
    
    try
    {
        Console.WriteLine("=== Запрос на получение истории операций ===");
        
        if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            Console.WriteLine("Отсутствует заголовок Authorization");
            return Results.Unauthorized();
        }
        
        var token = authHeader.ToString().Replace("Bearer ", "");
        Console.WriteLine($"Токен получен: {token}");
        
        var userId = dbManager.GetUserIdByToken(token);
        Console.WriteLine($"User ID из токена: {userId}");
        
        if (userId == null)
        {
            Console.WriteLine("Неверный токен или пользователь не найден");
            return Results.Unauthorized();
        }
        
        var history = dbManager.GetUserStringHistory(userId.Value);
        Console.WriteLine($"Возвращено {history.Count} записей истории для пользователя {userId.Value}");
        
        return Results.Ok(history);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Критическая ошибка при получении истории: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Results.Problem($"Внутренняя ошибка сервера: {ex.Message}");
    }
});

app.MapGet("/system/info", () => 
{
    return new 
    {
        Name = "Строковый редактор",
        Description = "Клиент-серверное приложение для обработки текста",
        Features = new List<string>
        {
            "Сортировка строк (по возрастанию/убыванию)",
            "Поиск по строкам (с учетом/без учета регистра)",
            "Замена текста в строках",
            "Удаление строк по индексам",
            "Авторизация пользователей",
            "Сохранение истории операций"
        }
    };
});

app.Run();

string[] SortStrings(string[] strings, bool ascending = true)
{
    return ascending ? strings.OrderBy(s => s).ToArray() 
                     : strings.OrderByDescending(s => s).ToArray();
}

List<(int index, string line)> SearchInStrings(string[] strings, string searchText, bool caseSensitive = false)
{
    var results = new List<(int, string)>();
    
    for (int i = 0; i < strings.Length; i++)
    {
        if (caseSensitive)
        {
            if (strings[i].Contains(searchText))
                results.Add((i, strings[i]));
        }
        else
        {
            if (strings[i].IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                results.Add((i, strings[i]));
        }
    }
    
    return results;
}

string[] ReplaceInStrings(string[] strings, string oldValue, string newValue, bool caseSensitive = false)
{
    return strings.Select(s => 
        caseSensitive ? s.Replace(oldValue, newValue) 
                      : ReplaceIgnoreCase(s, oldValue, newValue))
        .ToArray();
}

string ReplaceIgnoreCase(string text, string oldValue, string newValue)
{
    int index = text.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
    if (index >= 0)
    {
        return text.Remove(index, oldValue.Length).Insert(index, newValue);
    }
    return text;
}

string[] DeleteLines(string[] strings, int[] indicesToDelete)
{
    return strings.Where((_, index) => !indicesToDelete.Contains(index)).ToArray();
}
public record AuthRequest(string Login, string Password);
public record SortRequest(string[] Strings, bool Ascending = true);
public record SearchRequest(string[] Strings, string SearchText, bool CaseSensitive = false);
public record ReplaceRequest(string[] Strings, string OldValue, string? NewValue, bool CaseSensitive = false);
public record DeleteRequest(string[] Strings, int[] IndicesToDelete);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record DeleteAccountRequest(string Password);