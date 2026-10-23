using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Aion2Meter.Services;

public sealed class SkillSpecialtyCatalog
{
    private readonly IReadOnlyDictionary<int, IReadOnlyList<SkillSpecialtyText>> _skills;
    private readonly string _iconDirectory;

    public SkillSpecialtyCatalog()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "data", "skill_specialties.json");
        _skills = Load(path);
        _iconDirectory = Path.Combine(AppContext.BaseDirectory, "data", "textures", "specialties");
    }

    public string? GetOptionIconPath(int skillCode, int option)
    {
        string? classCode = (skillCode / 1_000_000) switch
        {
            11 => "GL",
            12 => "TE",
            13 => "AS",
            14 => "RA",
            15 => "SO",
            16 => "EL",
            17 => "CL",
            18 => "CH",
            19 => "GT",
            _ => null
        };
        if (classCode is null || option is < 1 or > 5)
            return null;

        int iconNumber = option % 5 + 1;
        string path = Path.Combine(_iconDirectory,
            $"Icon_Item_Usable_Stigma_{classCode}_A_c_{iconNumber:000}.png");
        return File.Exists(path) ? path : null;
    }

    public string GetDescription(int skillCode, int option)
    {
        if (option is >= 1 and <= 5 &&
            _skills.TryGetValue(skillCode, out IReadOnlyList<SkillSpecialtyText>? texts) &&
            option <= texts.Count)
        {
            SkillSpecialtyText text = texts[option - 1];
            if (!string.IsNullOrWhiteSpace(text.En) && !string.IsNullOrWhiteSpace(text.Ru))
                return UiLanguageService.Pick(text.En, text.Ru);
        }

        return UiLanguageService.Pick(
            $"Option {option} · description unavailable",
            $"Вариант {option} · описание недоступно");
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<SkillSpecialtyText>> Load(string path)
    {
        var result = new Dictionary<int, IReadOnlyList<SkillSpecialtyText>>();
        if (!File.Exists(path))
            return result;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("skills", out JsonElement skills))
                return result;

            foreach (JsonProperty skill in skills.EnumerateObject())
            {
                if (!int.TryParse(skill.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) ||
                    skill.Value.ValueKind != JsonValueKind.Array)
                    continue;

                var texts = new List<SkillSpecialtyText>();
                foreach (JsonElement option in skill.Value.EnumerateArray())
                {
                    string en = option.TryGetProperty("en", out JsonElement english) ? english.GetString() ?? "" : "";
                    string ru = option.TryGetProperty("ru", out JsonElement russian) ? russian.GetString() ?? "" : "";
                    texts.Add(new SkillSpecialtyText(en, ru));
                }
                result[code] = texts;
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        return result;
    }

    private sealed record SkillSpecialtyText(string En, string Ru);
}
