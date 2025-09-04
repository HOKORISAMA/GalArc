using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GalArc.Models.Database.Commons;

internal static class DatabaseManager
{
    public static readonly string DefaultPath = Path.Combine(Environment.CurrentDirectory, "Database");

    public static T LoadScheme<T>(JsonTypeInfo<T> typeInfo) where T : class
    {
        string filePath = Path.Combine(DefaultPath, $"{typeof(T).Name[..^6]}.json");
        return File.Exists(filePath) ? JsonSerializer.Deserialize(File.ReadAllText(filePath), typeInfo) ?? default : default;
    }
}
