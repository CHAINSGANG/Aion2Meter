using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using AionFlex.Core.Interfaces;

namespace Aion2Meter.Services;

public sealed class SkillIconService : ISkillIconService
{
    private readonly string _iconDirectory;
    private readonly IReadOnlyDictionary<int, string> _icons;

    public SkillIconService()
    {
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        _iconDirectory = Path.Combine(dataDirectory, "textures", "skills");
        _icons = LoadMappings(Path.Combine(dataDirectory, "skill_icons.json"));
    }

    public string? GetIconPath(int skillCode)
    {
        if (TryResolve(skillCode, out string? path))
        {
            return path;
        }

        int baseSkillCode = skillCode / 10_000 * 10_000;
        return baseSkillCode != skillCode && TryResolve(baseSkillCode, out path)
            ? path
            : null;
    }

    private bool TryResolve(int skillCode, out string? path)
    {
        path = null;
        if (!_icons.TryGetValue(skillCode, out string? fileName))
        {
            return false;
        }

        string safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return false;
        }

        string candidate = Path.Combine(_iconDirectory, safeFileName);
        if (!File.Exists(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static IReadOnlyDictionary<int, string> LoadMappings(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<int, string>();
        }

        try
        {
            Dictionary<string, string>? serialized = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(path));
            var result = new Dictionary<int, string>();
            if (serialized is null)
            {
                return result;
            }

            foreach ((string key, string value) in serialized)
            {
                if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillCode))
                {
                    result[skillCode] = value;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<int, string>();
        }
        catch (IOException)
        {
            return new Dictionary<int, string>();
        }
    }
}
