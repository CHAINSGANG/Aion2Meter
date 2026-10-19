using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aion2Meter.Services;

public sealed record MeterPetSnapshot(
    int PetId,
    string Name,
    string Family,
    string FamilyCode,
    int Level,
    int MaxLevel,
    string BonusText,
    string? PortraitPath)
{
    public string LevelText => MaxLevel > 0 ? $"УР. {Level} / {MaxLevel}" : $"УР. {Level}";
    public double LevelPercent => MaxLevel > 0 ? Math.Clamp((double)Level / MaxLevel * 100d, 0d, 100d) : 0d;
    public bool HasPortrait => !string.IsNullOrWhiteSpace(PortraitPath);
}

internal sealed class PetCatalog
{
    private static readonly IReadOnlyDictionary<string, string> StatNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["GroundVehicleSpeed"] = "Скорость передвижения",
        ["Accuracy"] = "Точность",
        ["DEX"] = "Ловкость",
        ["HPMax"] = "Макс. HP",
        ["Critical"] = "Крит. удар",
        ["STR"] = "Сила",
        ["SPMax"] = "Макс. энергии",
        ["CriticalResist"] = "Сопротивление криту",
        ["AGI"] = "Проворство",
        ["VehicleSprintCost"] = "Экономия ускорения",
        ["Evasion"] = "Уклонение",
        ["CON"] = "Выносливость"
    };

    private readonly Dictionary<int, PetDefinition> _pets;
    private readonly Dictionary<string, Dictionary<int, Dictionary<string, int>>> _stats;
    private readonly string _portraitDirectory;

    private sealed record PetDefinition(string Name, string Model, string Group, string Type);

    private PetCatalog(
        Dictionary<int, PetDefinition> pets,
        Dictionary<string, Dictionary<int, Dictionary<string, int>>> stats,
        string portraitDirectory)
    {
        _pets = pets;
        _stats = stats;
        _portraitDirectory = portraitDirectory;
    }

    public static PetCatalog Load(string petsPath, string statsPath)
    {
        var pets = new Dictionary<int, PetDefinition>();
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(petsPath)))
        {
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    continue;
                }

                JsonElement value = property.Value;
                string name = ReadString(value, "en") ?? $"Pet #{id}";
                string model = ReadString(value, "name") ?? string.Empty;
                string group = ReadString(value, "group") ?? string.Empty;
                string type = ReadString(value, "type") ?? string.Empty;
                pets[id] = new PetDefinition(name, model, group, type);
            }
        }

        var stats = new Dictionary<string, Dictionary<int, Dictionary<string, int>>>(StringComparer.OrdinalIgnoreCase);
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(statsPath)))
        {
            foreach (JsonProperty groupProperty in document.RootElement.EnumerateObject())
            {
                var levels = new Dictionary<int, Dictionary<string, int>>();
                foreach (JsonProperty levelProperty in groupProperty.Value.EnumerateObject())
                {
                    if (!int.TryParse(levelProperty.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
                    {
                        continue;
                    }

                    var bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonProperty statProperty in levelProperty.Value.EnumerateObject())
                    {
                        if (statProperty.Value.TryGetInt32(out int amount))
                        {
                            bonuses[statProperty.Name] = amount;
                        }
                    }
                    levels[level] = bonuses;
                }
                stats[groupProperty.Name] = levels;
            }
        }

        string dataDirectory = Path.GetDirectoryName(Path.GetFullPath(petsPath)) ?? AppContext.BaseDirectory;
        string portraitDirectory = Path.Combine(dataDirectory, "textures", "pets");
        return new PetCatalog(pets, stats, portraitDirectory);
    }

    public MeterPetSnapshot Resolve(int petId, int level)
    {
        if (!_pets.TryGetValue(petId, out PetDefinition? definition))
        {
            return new MeterPetSnapshot(
                petId,
                $"Питомец #{petId}",
                "Неизвестный тип",
                string.Empty,
                level,
                0,
                "Бонус неизвестен",
                null);
        }

        int maxLevel = _stats.TryGetValue(definition.Group, out Dictionary<int, Dictionary<string, int>>? levels)
            ? levels.Keys.DefaultIfEmpty(0).Max()
            : 0;
        return new MeterPetSnapshot(
            petId,
            definition.Name,
            LocalizeFamily(definition.Type),
            definition.Type,
            level,
            maxLevel,
            ResolveBonuses(definition.Group, level),
            ResolvePortrait(definition.Model));
    }

    private string? ResolvePortrait(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        string safeModel = Path.GetFileName(model);
        string candidate = Path.Combine(_portraitDirectory, $"UT_Vehicle_Portrait_{safeModel}.png");
        return File.Exists(candidate) ? candidate : null;
    }

    private string ResolveBonuses(string group, int level)
    {
        if (level <= 0)
        {
            return "Бонус ещё не открыт";
        }

        if (!_stats.TryGetValue(group, out Dictionary<int, Dictionary<string, int>>? levels))
        {
            return "Особый эффект";
        }

        int resolvedLevel = levels.Keys.Where(candidate => candidate <= level).DefaultIfEmpty(0).Max();
        if (resolvedLevel == 0 || !levels.TryGetValue(resolvedLevel, out Dictionary<string, int>? bonuses) || bonuses.Count == 0)
        {
            return "Особый эффект";
        }

        return string.Join(" · ", bonuses.Select(pair =>
            $"{(StatNames.TryGetValue(pair.Key, out string? name) ? name : pair.Key)} +{pair.Value}"));
    }

    private static string LocalizeFamily(string type) => type switch
    {
        "Feral" => "Фера",
        "Intellect" => "Когни",
        "Nature" => "Натура",
        "Trans" => "Вариан",
        "Special" => "Особый",
        _ => string.IsNullOrWhiteSpace(type) ? "Неизвестный тип" : type
    };

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
