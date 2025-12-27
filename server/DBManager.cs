using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

public class DBManager
{
    private SqliteConnection? _connection;
    private static readonly Dictionary<string, int> _activeTokens = new();
    private const string DB_PATH = "string_editor.db";

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private string GenerateToken()
    {
        return Guid.NewGuid().ToString() + DateTime.Now.Ticks.ToString();
    }

    public void InitializeDatabase()
    {
        try
        {
            _connection = new SqliteConnection($"Data Source={DB_PATH}");
            _connection.Open();
            Console.WriteLine($"Подключение к БД установлено: {DB_PATH}");

            var createUsersTable = @"
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    login TEXT UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            var createStringsTable = @"
                CREATE TABLE IF NOT EXISTS user_strings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    content TEXT NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (user_id) REFERENCES users(id)
                )";

            var createHistoryTable = @"
                CREATE TABLE IF NOT EXISTS string_operations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    operation_type TEXT NOT NULL,
                    parameters TEXT,
                    result TEXT,
                    execution_time_ms INTEGER NOT NULL,
                    operation_time DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (user_id) REFERENCES users(id)
                )";

            using var cmd1 = new SqliteCommand(createUsersTable, _connection);
            cmd1.ExecuteNonQuery();
            Console.WriteLine("Таблица users создана/проверена");

            using var cmd2 = new SqliteCommand(createStringsTable, _connection);
            cmd2.ExecuteNonQuery();
            Console.WriteLine("Таблица user_strings создана/проверена");

            using var cmd3 = new SqliteCommand(createHistoryTable, _connection);
            cmd3.ExecuteNonQuery();
            Console.WriteLine("Таблица string_operations создана/проверена");

            Console.WriteLine("База данных строкового редактора инициализирована");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка инициализации БД: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _connection = null;
        }
    }

    private void EnsureConnection()
    {
        if (_connection == null)
        {
            Console.WriteLine("Попытка переподключения к БД...");
            InitializeDatabase();
        }
        
        if (_connection == null)
        {
            throw new InvalidOperationException("Не удалось установить подключение к базе данных");
        }
        
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            Console.WriteLine("Переподключение к БД...");
            _connection.Open();
        }
    }

    public bool RegisterUser(string login, string password)
    {
        try
        {
            EnsureConnection();
            
            var passwordHash = HashPassword(password);
            var query = "INSERT INTO users (login, password_hash) VALUES (@login, @passwordHash)";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@login", login);
            cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
            
            int rowsAffected = cmd.ExecuteNonQuery();
            Console.WriteLine($"RegisterUser: попытка вставки пользователя '{login}', затронуто строк: {rowsAffected}");
            
            if (rowsAffected == 1)
            {
                Console.WriteLine($"Пользователь '{login}' успешно зарегистрирован");
                return true;
            }
            
            return false;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            Console.WriteLine($"Пользователь с логином '{login}' уже существует (SQLite error 19)");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка регистрации пользователя '{login}': {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public string? AuthenticateUser(string login, string password)
    {
        try
        {
            EnsureConnection();
            
            var passwordHash = HashPassword(password);
            var query = "SELECT id FROM users WHERE login = @login AND password_hash = @passwordHash";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@login", login);
            cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
            
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var userId = reader.GetInt32(0);
                var token = GenerateToken();
                _activeTokens[token] = userId;
                Console.WriteLine($"Пользователь '{login}' (ID: {userId}) успешно аутентифицирован");
                return token;
            }
            
            Console.WriteLine($"Неверный логин или пароль для пользователя '{login}'");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка аутентификации пользователя '{login}': {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    public bool ValidateToken(string token)
    {
        return _activeTokens.ContainsKey(token);
    }

    public int? GetUserIdByToken(string token)
    {
        return _activeTokens.TryGetValue(token, out var userId) ? userId : null;
    }

    public void SaveUserStrings(int userId, string[] strings)
    {
        try
        {
            EnsureConnection();
            
            Console.WriteLine($"Saving strings for user {userId}");
            var deleteQuery = "DELETE FROM user_strings WHERE user_id = @userId";
            using var deleteCmd = new SqliteCommand(deleteQuery, _connection);
            deleteCmd.Parameters.AddWithValue("@userId", userId);
            int deleted = deleteCmd.ExecuteNonQuery();
            Console.WriteLine($"Удалено старых строк: {deleted}");
            
            int inserted = 0;
            foreach (var str in strings)
            {
                var insertQuery = "INSERT INTO user_strings (user_id, content) VALUES (@userId, @content)";
                using var insertCmd = new SqliteCommand(insertQuery, _connection);
                insertCmd.Parameters.AddWithValue("@userId", userId);
                insertCmd.Parameters.AddWithValue("@content", str);
                insertCmd.ExecuteNonQuery();
                inserted++;
            }
            
            Console.WriteLine($"Saved {inserted} strings for user {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving strings for user {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    public List<string> GetUserStrings(int userId)
    {
        var strings = new List<string>();
        
        try
        {
            EnsureConnection();
            
            var query = "SELECT content FROM user_strings WHERE user_id = @userId ORDER BY created_at DESC";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                strings.Add(reader.GetString(0));
            }
            
            Console.WriteLine($"Retrieved {strings.Count} strings for user {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting strings for user {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        return strings;
    }
    public bool DeleteAllUserStrings(int userId)
    {
        try
        {
            EnsureConnection();
            
            Console.WriteLine($"Deleting all strings for user {userId}");
            
            var query = "DELETE FROM user_strings WHERE user_id = @userId";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            
            int rowsAffected = cmd.ExecuteNonQuery();
            
            Console.WriteLine($"Strings deleted: {rowsAffected}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting all strings for user {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public void SaveStringOperation(int userId, string operationType, string parameters, string result, long executionTimeMs)
    {
        try
        {
            EnsureConnection();
            
            Console.WriteLine($"Saving operation '{operationType}' for user {userId}");
            
            var query = @"
                INSERT INTO string_operations 
                (user_id, operation_type, parameters, result, execution_time_ms) 
                VALUES (@userId, @operationType, @parameters, @result, @executionTime)";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@operationType", operationType);
            cmd.Parameters.AddWithValue("@parameters", parameters ?? "");
            cmd.Parameters.AddWithValue("@result", result ?? "");
            cmd.Parameters.AddWithValue("@executionTime", executionTimeMs);
            
            cmd.ExecuteNonQuery();
            Console.WriteLine($"Operation saved for user {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving operation for user {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public List<Dictionary<string, object>> GetUserStringHistory(int userId)
    {
        var history = new List<Dictionary<string, object>>();
        
        try
        {
            EnsureConnection();
            
            var query = @"
                SELECT id, operation_type, parameters, result, execution_time_ms, 
                       datetime(operation_time, 'localtime') as operation_time 
                FROM string_operations 
                WHERE user_id = @userId 
                ORDER BY operation_time DESC 
                LIMIT 50";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var record = new Dictionary<string, object>
                {
                    ["id"] = reader.GetInt32(0),
                    ["operation_type"] = reader.GetString(1),
                    ["parameters"] = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ["result"] = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ["execution_time_ms"] = reader.GetInt64(4),
                    ["operation_time"] = reader.GetString(5)
                };
                history.Add(record);
            }
            
            Console.WriteLine($"Retrieved {history.Count} operations for user {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения истории операций для пользователя {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        return history;
    }

    public bool DeleteAllStringHistory(int userId)
    {
        try
        {
            EnsureConnection();
            
            Console.WriteLine($"Deleting all string history for user {userId}");
            
            var query = "DELETE FROM string_operations WHERE user_id = @userId";
            
            using var cmd = new SqliteCommand(query, _connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            
            int rowsAffected = cmd.ExecuteNonQuery();
            
            Console.WriteLine($"History records deleted: {rowsAffected}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting all string history for user {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public bool ChangePassword(int userId, string oldPassword, string newPassword)
    {
        try
        {
            EnsureConnection();
            
            var oldPasswordHash = HashPassword(oldPassword);
            var newPasswordHash = HashPassword(newPassword);
            var checkQuery = "SELECT id FROM users WHERE id = @userId AND password_hash = @oldPasswordHash";
            using var checkCmd = new SqliteCommand(checkQuery, _connection);
            checkCmd.Parameters.AddWithValue("@userId", userId);
            checkCmd.Parameters.AddWithValue("@oldPasswordHash", oldPasswordHash);
            
            using var reader = checkCmd.ExecuteReader();
            if (!reader.Read())
            {
                Console.WriteLine($"Неверный старый пароль для пользователя {userId}");
                return false; 
            }
            
            var updateQuery = "UPDATE users SET password_hash = @newPasswordHash WHERE id = @userId";
            using var updateCmd = new SqliteCommand(updateQuery, _connection);
            updateCmd.Parameters.AddWithValue("@userId", userId);
            updateCmd.Parameters.AddWithValue("@newPasswordHash", newPasswordHash);
            
            var result = updateCmd.ExecuteNonQuery() == 1;
            
            if (result)
            {
                Console.WriteLine($"Пароль изменен для пользователя {userId}");
                var tokensToRemove = _activeTokens
                    .Where(kv => kv.Value == userId)
                    .Select(kv => kv.Key)
                    .ToList();
                
                foreach (var token in tokensToRemove)
                {
                    _activeTokens.Remove(token);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка изменения пароля для пользователя {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
    public bool DeleteAccount(int userId, string password)
    {
        try
        {
            EnsureConnection();
            
            var passwordHash = HashPassword(password);
            var checkQuery = "SELECT id FROM users WHERE id = @userId AND password_hash = @passwordHash";
            using var checkCmd = new SqliteCommand(checkQuery, _connection);
            checkCmd.Parameters.AddWithValue("@userId", userId);
            checkCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
            
            using var reader = checkCmd.ExecuteReader();
            if (!reader.Read())
            {
                Console.WriteLine($"Неверный пароль для удаления аккаунта пользователя {userId}");
                return false; 
            }
            using var transaction = _connection.BeginTransaction();
            
            try
            {
                var deleteStringsQuery = "DELETE FROM user_strings WHERE user_id = @userId";
                using var deleteStringsCmd = new SqliteCommand(deleteStringsQuery, _connection, transaction);
                deleteStringsCmd.Parameters.AddWithValue("@userId", userId);
                int stringsDeleted = deleteStringsCmd.ExecuteNonQuery();
                Console.WriteLine($"Удалено строк пользователя: {stringsDeleted}");

                var deleteHistoryQuery = "DELETE FROM string_operations WHERE user_id = @userId";
                using var deleteHistoryCmd = new SqliteCommand(deleteHistoryQuery, _connection, transaction);
                deleteHistoryCmd.Parameters.AddWithValue("@userId", userId);
                int historyDeleted = deleteHistoryCmd.ExecuteNonQuery();
                Console.WriteLine($"Удалено записей истории: {historyDeleted}");

                var deleteUserQuery = "DELETE FROM users WHERE id = @userId";
                using var deleteUserCmd = new SqliteCommand(deleteUserQuery, _connection, transaction);
                deleteUserCmd.Parameters.AddWithValue("@userId", userId);
                var result = deleteUserCmd.ExecuteNonQuery() == 1;
                
                var tokensToRemove = _activeTokens
                    .Where(kv => kv.Value == userId)
                    .Select(kv => kv.Key)
                    .ToList();
                
                foreach (var token in tokensToRemove)
                {
                    _activeTokens.Remove(token);
                }
                
                transaction.Commit();
                
                if (result)
                {
                    Console.WriteLine($"Аккаунт пользователя {userId} удален");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Ошибка при удалении аккаунта пользователя {userId}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки пароля для удаления аккаунта пользователя {userId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public List<Dictionary<string, object>> GetAllUsers()
    {
        var users = new List<Dictionary<string, object>>();
        
        try
        {
            EnsureConnection();
            
            var query = "SELECT id, login, created_at FROM users ORDER BY created_at DESC";
            using var cmd = new SqliteCommand(query, _connection);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var user = new Dictionary<string, object>
                {
                    ["id"] = reader.GetInt32(0),
                    ["login"] = reader.GetString(1),
                    ["created_at"] = reader.GetDateTime(2)
                };
                users.Add(user);
            }
            
            Console.WriteLine($"Получено пользователей: {users.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения пользователей: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        return users;
    }
}