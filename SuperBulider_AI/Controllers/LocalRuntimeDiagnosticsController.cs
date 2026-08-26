            var parsed = ParseHostPort(server, dbType);
            return new SafeConnectionInfo(dbType, parsed.Host, parsed.Port, database, true, null, parsed.OriginalServer);
        }
        catch (Exception ex)
        {
            return new SafeConnectionInfo(dbType, null, null, null, false, $"连接字符串解析失败: {ex.Message}", null);
        }
    }

    private static string? GetValue(DbConnectionStringBuilder builder, string key)
    {
        foreach (string itemKey in builder.Keys)
        {
            if (string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase))
                return builder[itemKey]?.ToString();
        }
        return null;
    }

    private static (string? Host, int? Port, string? OriginalServer) ParseHostPort(string? server, string? dbType)
    {
        if (string.IsNullOrWhiteSpace(server)) return (null, null, null);
        var original = server;