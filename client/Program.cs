using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleStringEditorClient
{
    public class AuthResponse
    {
        public string? token { get; set; }
        public string? message { get; set; }
    }

    public class SimpleResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
    }

    public class StringResponse
    {
        public string[]? strings { get; set; }
        public string? message { get; set; }
        public bool success { get; set; }
    }

    class Program
    {
        private HttpClient? client;
        private string? currentToken = null;
        private string? currentUsername = null;

        static async Task Main(string[] args)
        {
            var program = new Program();
            await program.Run();
        }

        private async Task Run()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;

                Console.WriteLine("=== ПРОСТОЙ СТРОКОВЫЙ РЕДАКТОР ===");
                Console.WriteLine("Демонстрация клиент-серверного взаимодействия\n");

                SetupConnection();

                await HandleAuthentication();
                if (currentToken != null)
                {
                    await ShowMainMenu();
                }

                Console.WriteLine("\nНажмите любую клавишу для выхода...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nКритическая ошибка: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("\nНажмите любую клавишу для выхода...");
                Console.ReadKey();
            }
            finally
            {
                client?.Dispose();
            }
        }

        private void SetupConnection()
        {
            try
            {
                string serverAddress = "http://localhost:5200";
                
                Console.WriteLine($"Подключение к серверу: {serverAddress}");
                
                client = new HttpClient();
                client.BaseAddress = new Uri(serverAddress);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                client.Timeout = TimeSpan.FromSeconds(30);
                
                Console.WriteLine("✓ Подключение настроено");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка настройки подключения: {ex.Message}");
                throw;
            }
        }

        private async Task HandleAuthentication()
        {
            Console.WriteLine("\n=== АВТОРИЗАЦИЯ ===");
            
            while (true)
            {
                Console.WriteLine("\n1. Вход");
                Console.WriteLine("2. Регистрация");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");
                
                string choice = Console.ReadLine()?.Trim() ?? "";

                if (choice == "0")
                {
                    Console.WriteLine("Выход из программы...");
                    Environment.Exit(0);
                }

                Console.Write("Логин: ");
                string username = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Пароль: ");
                string password = Console.ReadLine()?.Trim() ?? "";

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("Логин и пароль не могут быть пустыми!");
                    continue;
                }

                if (choice == "2")
                {
                    bool registered = await RegisterUser(username, password);
                    if (registered)
                    {
                        Console.WriteLine("✓ Регистрация успешна! Теперь войдите в систему.");
                        continue; // Возвращаемся к меню авторизации
                    }
                }
                else if (choice == "1")
                {
                    bool loggedIn = await LoginUser(username, password);
                    if (loggedIn)
                    {
                        Console.WriteLine($"✓ Добро пожаловать, {username}!");
                        currentUsername = username;
                        return; // Успешный вход
                    }
                    else
                    {
                        Console.WriteLine("✗ Неверный логин или пароль");
                    }
                }
                else
                {
                    Console.WriteLine("Неверный выбор");
                }
            }
        }

        private async Task ShowMainMenu()
        {
            while (true)
            {
                Console.WriteLine("\n=== ГЛАВНОЕ МЕНЮ ===");
                Console.WriteLine("1. Сохранить строки");
                Console.WriteLine("2. Показать мои строки");
                Console.WriteLine("3. Отсортировать строки");
                Console.WriteLine("4. Найти строки");
                Console.WriteLine("5. Показать историю операций");
                Console.WriteLine("6. Изменить пароль");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");

                string choice = Console.ReadLine()?.Trim() ?? "";

                switch (choice)
                {
                    case "1":
                        await SaveStrings();
                        break;
                    case "2":
                        await ShowMyStrings();
                        break;
                    case "3":
                        await SortStrings();
                        break;
                    case "4":
                        await SearchStrings();
                        break;
                    case "5":
                        await ShowHistory();
                        break;
                    case "6":
                        await ChangePassword();
                        break;
                    case "0":
                        Console.WriteLine("Выход...");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор");
                        break;
                }
            }
        }

        private async Task<bool> RegisterUser(string username, string password)
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return false;
            }

            try
            {
                Console.WriteLine("Регистрируем пользователя...");
                
                var data = new 
                { 
                    login = username, 
                    password = password 
                };
                
                string json = JsonSerializer.Serialize(data);
                Console.WriteLine($"Отправляем JSON: {json}");
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string endpoint = "/auth/register";
                
                try
                {
                    Console.WriteLine($"Отправляем запрос на: {endpoint}");
                    HttpResponseMessage response = await client.PostAsync(endpoint, content);
                    
                    Console.WriteLine($"Статус ответа: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера: {responseText}");
                        Console.WriteLine("✓ Регистрация успешна!");
                        return true;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера: {responseText}");
                        Console.WriteLine("✗ Пользователь уже существует");
                        return false;
                    }
                    else
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера: {responseText}");
                        Console.WriteLine($"✗ Ошибка регистрации: {response.StatusCode}");
                        return false;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
                    Console.WriteLine("Проверьте, запущен ли сервер на порту 5200");
                    return false;
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine($"✗ Превышено время ожидания: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ошибка при запросе: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка при регистрации: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> LoginUser(string username, string password)
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return false;
            }

            try
            {
                Console.WriteLine("Пытаемся войти...");
                
                var data = new { login = username, password = password };
                string json = JsonSerializer.Serialize(data);
                Console.WriteLine($"Отправляем JSON: {json}");
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string endpoint = "/auth/login";
                
                try
                {
                    Console.WriteLine($"Отправляем запрос на: {endpoint}");
                    HttpResponseMessage response = await client.PostAsync(endpoint, content);
                    
                    Console.WriteLine($"Статус ответа: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера: {responseText}");
                        
                        try
                        {
                            var authResult = JsonSerializer.Deserialize<AuthResponse>(responseText, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            
                            if (authResult != null && !string.IsNullOrEmpty(authResult.token))
                            {
                                currentToken = authResult.token;
                                if (client != null)
                                {
                                    client.DefaultRequestHeaders.Authorization = 
                                        new AuthenticationHeaderValue("Bearer", currentToken);
                                }
                                Console.WriteLine("✓ Токен получен и сохранен");
                                return true;
                            }
                            else
                            {
                                Console.WriteLine("✗ Токен не получен в ответе");
                                return false;
                            }
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"✗ Ошибка парсинга ответа: {ex.Message}");
                            return false;
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера: {responseText}");
                        Console.WriteLine("✗ Неверный логин или пароль");
                        return false;
                    }
                    else
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера: {responseText}");
                        Console.WriteLine($"✗ Ошибка входа: {response.StatusCode}");
                        return false;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
                    Console.WriteLine("Проверьте, запущен ли сервер на порту 5200");
                    return false;
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine($"✗ Превышено время ожидания: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ошибка при запросе: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Критическая ошибка при входе: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private async Task SaveStrings()
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return;
            }

            Console.WriteLine("\n=== СОХРАНЕНИЕ СТРОК ===");
            Console.WriteLine("Введите строки (пустая строка - завершить ввод):");
            
            var strings = new List<string>();
            int counter = 1;
            
            while (true)
            {
                Console.Write($"{counter}. ");
                string line = Console.ReadLine()?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(line))
                    break;
                    
                strings.Add(line);
                counter++;
            }
            
            if (strings.Count == 0)
            {
                Console.WriteLine("Не введено ни одной строки");
                return;
            }
            
            Console.WriteLine($"\nСтрок для сохранения: {strings.Count}");
            
            try
            {
                var data = new { strings = strings.ToArray() };
                string json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string endpoint = "/strings/save";
                
                try
                {
                    Console.WriteLine($"Отправляем на {endpoint}...");
                    HttpResponseMessage response = await client.PostAsync(endpoint, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✓ Сохранено! Ответ: {result}");
                    }
                    else
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Ошибка сохранения: {response.StatusCode}");
                        Console.WriteLine($"Ответ сервера: {result}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ошибка при запросе: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка: {ex.Message}");
            }
        }

        private async Task ShowMyStrings()
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return;
            }

            Console.WriteLine("\n=== МОИ СТРОКИ ===");
            
            try
            {
                string endpoint = "/strings/get-all";
                
                try
                {
                    Console.WriteLine($"Запрашиваем с {endpoint}...");
                    HttpResponseMessage response = await client.GetAsync(endpoint);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Ответ сервера:\n{json}");
                        
                        try
                        {
                            var result = JsonSerializer.Deserialize<StringResponse>(json, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            if (result?.strings != null && result.strings.Length > 0)
                            {
                                Console.WriteLine($"\nНайдено {result.strings.Length} строк:");
                                for (int i = 0; i < result.strings.Length; i++)
                                {
                                    Console.WriteLine($"{i + 1}. {result.strings[i]}");
                                }
                            }
                            else if (result?.message != null)
                            {
                                Console.WriteLine($"Сообщение: {result.message}");
                            }
                            else
                            {
                                Console.WriteLine("Строк не найдено");
                            }
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
                            if (!json.Contains("<!DOCTYPE") && !json.Contains("<html"))
                            {
                                Console.WriteLine($"Ответ: {json}");
                            }
                        }
                    }
                    else
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Ошибка получения строк: {response.StatusCode}");
                        Console.WriteLine($"Ответ сервера: {json}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ошибка при запросе: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка: {ex.Message}");
            }
        }

        private async Task SortStrings()
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return;
            }

            Console.WriteLine("\n=== СОРТИРОВКА СТРОК ===");
            Console.WriteLine("1. По алфавиту (А-Я)");
            Console.WriteLine("2. Обратный порядок (Я-А)");
            Console.Write("Выберите: ");
            
            string choice = Console.ReadLine()?.Trim() ?? "";
            bool ascending = choice == "1";
            
            Console.WriteLine($"Сортировка: {(ascending ? "А-Я" : "Я-А")}");
            
            try
            {
                HttpResponseMessage response = await client.GetAsync("/strings/get-all");
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    
                    try
                    {
                        var result = JsonSerializer.Deserialize<StringResponse>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        if (result?.strings != null && result.strings.Length > 0)
                        {
                            var sorted = ascending 
                                ? result.strings.OrderBy(s => s).ToArray()
                                : result.strings.OrderByDescending(s => s).ToArray();
                            
                            Console.WriteLine("\nОтсортированные строки:");
                            foreach (var str in sorted)
                            {
                                Console.WriteLine($"- {str}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Строк для сортировки не найдено");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Не удалось обработать ответ сервера: {ex.Message}");
                    }
                }
                else
                {
                    string errorText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✗ Ошибка получения строк: {response.StatusCode}");
                    Console.WriteLine($"Ответ: {errorText}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка: {ex.Message}");
            }
        }

        private async Task SearchStrings()
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return;
            }

            Console.WriteLine("\n=== ПОИСК СТРОК ===");
            Console.Write("Введите текст для поиска: ");
            string searchText = Console.ReadLine()?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(searchText))
            {
                Console.WriteLine("Текст для поиска не может быть пустым");
                return;
            }
            
            Console.WriteLine($"Ищем: '{searchText}'");
            
            try
            {
                var emptyContent = new StringContent("{}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/strings/get-all", emptyContent);
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    
                    try
                    {
                        var result = JsonSerializer.Deserialize<StringResponse>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        if (result?.strings != null)
                        {
                            var found = result.strings
                                .Where(s => s.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            
                            if (found.Length > 0)
                            {
                                Console.WriteLine($"\nНайдено {found.Length} строк:");
                                foreach (var str in found)
                                {
                                    Console.WriteLine($"- {str}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Строки не найдены");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Строки не найдены");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Не удалось обработать ответ сервера: {ex.Message}");
                    }
                }
                else
                {
                    string errorText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✗ Ошибка получения строк: {response.StatusCode}");
                    Console.WriteLine($"Ответ: {errorText}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка: {ex.Message}");
            }
        }

        private async Task ShowHistory()
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return;
            }

            Console.WriteLine("\n=== ИСТОРИЯ ОПЕРАЦИЙ ===");
            
            try
            {
                string endpoint = "/history/get";
                
                try
                {
                    Console.WriteLine($"Запрашиваем историю с {endpoint}...");
                    HttpResponseMessage response = await client.GetAsync(endpoint);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        
                        try
                        {
                            var history = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            
                            if (history == null || history.Count == 0)
                            {
                                Console.WriteLine("История операций пуста.");
                                return;
                            }
                            
                            Console.WriteLine($"\nНайдено операций: {history.Count}\n");
                            Console.WriteLine(new string('=', 80));
                            
                            for (int i = 0; i < history.Count; i++)
                            {
                                var operation = history[i];
                                
                                var operationType = operation.ContainsKey("operation_type") 
                                    ? operation["operation_type"].GetString() ?? "неизвестно" 
                                    : "неизвестно";
                                
                                var operationTime = operation.ContainsKey("operation_time") 
                                    ? operation["operation_time"].GetString() ?? "" 
                                    : "";
                                
                                var executionTime = operation.ContainsKey("execution_time_ms") 
                                    ? operation["execution_time_ms"].GetInt64() 
                                    : 0;
                                
                                string parametersText = "";
                                if (operation.ContainsKey("parameters"))
                                {
                                    try
                                    {
                                        var paramsJson = operation["parameters"].GetString() ?? "{}";
                                        var paramsObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramsJson);
                                        
                                        if (paramsObj != null)
                                        {
                                            var paramList = new List<string>();
                                            foreach (var param in paramsObj)
                                            {
                                                string value = param.Value.ValueKind == JsonValueKind.String 
                                                    ? param.Value.GetString() ?? "" 
                                                    : param.Value.ValueKind == JsonValueKind.Number 
                                                        ? param.Value.GetInt64().ToString() 
                                                        : param.Value.ValueKind == JsonValueKind.True || param.Value.ValueKind == JsonValueKind.False
                                                            ? param.Value.GetBoolean().ToString()
                                                            : param.Value.ToString();
                                                
                                                paramList.Add($"{param.Key}: {value}");
                                            }
                                            parametersText = string.Join(", ", paramList);
                                        }
                                    }
                                    catch
                                    {
                                        parametersText = operation["parameters"].GetString() ?? "";
                                    }
                                }
                                
                                string resultText = "";
                                if (operation.ContainsKey("result"))
                                {
                                    try
                                    {
                                        var resultJson = operation["result"].GetString() ?? "{}";
                                        var resultObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultJson);
                                        
                                        if (resultObj != null)
                                        {
                                            var resultList = new List<string>();
                                            foreach (var res in resultObj)
                                            {
                                                string value = res.Value.ValueKind == JsonValueKind.String 
                                                    ? res.Value.GetString() ?? "" 
                                                    : res.Value.ValueKind == JsonValueKind.Number 
                                                        ? res.Value.GetInt64().ToString() 
                                                        : res.Value.ToString();
                                                
                                                resultList.Add($"{res.Key}: {value}");
                                            }
                                            resultText = string.Join(", ", resultList);
                                        }
                                    }
                                    catch
                                    {
                                        resultText = operation["result"].GetString() ?? "";
                                    }
                                }
                                
                                string operationName = operationType switch
                                {
                                    "save" => "СОХРАНЕНИЕ СТРОК",
                                    "sort" => "СОРТИРОВКА",
                                    "search" => "ПОИСК",
                                    "replace" => "ЗАМЕНА",
                                    "delete" => "УДАЛЕНИЕ",
                                    _ => operationType.ToUpper()
                                };
                                
                                Console.WriteLine($"\n[{i + 1}] {operationName}");
                                Console.WriteLine($"    Время: {operationTime}");
                                Console.WriteLine($"    Время выполнения: {executionTime} мс");
                                
                                if (!string.IsNullOrEmpty(parametersText))
                                {
                                    Console.WriteLine($"    Параметры: {parametersText}");
                                }
                                
                                if (!string.IsNullOrEmpty(resultText))
                                {
                                    Console.WriteLine($"    Результат: {resultText}");
                                }
                                
                                if (i < history.Count - 1)
                                {
                                    Console.WriteLine(new string('-', 80));
                                }
                            }
                            
                            Console.WriteLine($"\n{new string('=', 80)}");
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"Ошибка парсинга истории: {ex.Message}");
                            Console.WriteLine($"Сырой JSON: {json}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка обработки истории: {ex.Message}");
                            Console.WriteLine($"Сырой JSON: {json}");
                        }
                    }
                    else
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Ошибка получения истории: {response.StatusCode}");
                        Console.WriteLine($"Ответ сервера: {json}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ошибка при запросе: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка: {ex.Message}");
            }
        }

        private async Task ChangePassword()
        {
            if (client == null)
            {
                Console.WriteLine("✗ Клиент не инициализирован");
                return;
            }

            Console.WriteLine("\n=== ИЗМЕНЕНИЕ ПАРОЛЯ ===");
            
            Console.Write("Текущий пароль: ");
            string oldPassword = Console.ReadLine()?.Trim() ?? "";
            
            Console.Write("Новый пароль: ");
            string newPassword = Console.ReadLine()?.Trim() ?? "";
            
            Console.Write("Подтвердите новый пароль: ");
            string confirmPassword = Console.ReadLine()?.Trim() ?? "";
            
            if (newPassword != confirmPassword)
            {
                Console.WriteLine("✗ Пароли не совпадают");
                return;
            }
            
            if (newPassword.Length < 4)
            {
                Console.WriteLine("✗ Пароль должен быть не менее 4 символов");
                return;
            }
            
            Console.WriteLine("Изменяем пароль...");
            
            try
            {
                var data = new { 
                    oldPassword = oldPassword, 
                    newPassword = newPassword 
                };
                
                string json = JsonSerializer.Serialize(data);
                Console.WriteLine($"Отправляем JSON: {json}");
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string endpoint = "/auth/change-password";
                
                if (string.IsNullOrEmpty(currentToken))
                {
                    Console.WriteLine("✗ Токен не найден. Необходимо войти в систему.");
                    return;
                }
                
                Console.WriteLine($"Токен для запроса: {currentToken.Substring(0, Math.Min(20, currentToken.Length))}...");
                
                try
                {
                    Console.WriteLine($"Отправляем запрос на {endpoint}...");
                    HttpResponseMessage response = await client.PostAsync(endpoint, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✓ Пароль изменен! Ответ: {result}");
                        
                        Console.WriteLine("\n⚠ Для продолжения нужно войти заново");
                        Console.Write("Введите новый пароль: ");
                        string password = Console.ReadLine()?.Trim() ?? "";
                        
                        if (!string.IsNullOrEmpty(currentUsername))
                        {
                            bool success = await LoginUser(currentUsername, password);
                            if (!success)
                            {
                                Console.WriteLine("Не удалось войти с новым паролем");
                            }
                        }
                        return;
                    }
                    else
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Ошибка изменения пароля: {response.StatusCode}");
                        Console.WriteLine($"Ответ сервера: {result}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"✗ Ошибка подключения к серверу: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ошибка при запросе: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка: {ex.Message}");
            }
        }
    }
}