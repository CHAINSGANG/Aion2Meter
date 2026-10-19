using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aion2Meter.Services;

public sealed record PetSpawnPoint(int NpcId, double X, double Y, double Z);

public sealed record PetSpawnSource(int NpcId, string Name, int Level, int SpawnCount)
{
    public string LevelText => Level > 0 ? UiLanguageService.Pick($"LV. {Level}", $"УР. {Level}") : string.Empty;
    public string SpawnText => $"{SpawnCount} {PetLocationCatalog.SpawnWord(SpawnCount)}";
}

public sealed record PetRegionLocation(string World, string Region)
{
    public string DisplayText => $"{World}  ·  {Region}";
}

public sealed record PetMapDefinition(
    string Key,
    string Title,
    int Tile,
    int Grid,
    double Scale,
    double OffsetX,
    double OffsetY,
    IReadOnlyList<string> Tables,
    IReadOnlyList<int> Zones)
{
    public bool MatchesZone(int zoneId) => zoneId > 0 && (Zones.Contains(zoneId) || Key switch
    {
        "verteron" => zoneId is 1010 or 902,
        "altgard" => zoneId is 1110 or 901,
        _ => false
    });
}

public sealed record PetMapLocation(
    string Key,
    string Title,
    PetMapDefinition? Definition,
    IReadOnlyList<PetSpawnPoint> Points,
    IReadOnlyList<PetSpawnSource> Sources)
{
    public int SpawnCount => Points.Count;
    public bool HasMap => Definition is not null && Points.Count > 0;
    public string SpawnText => $"{SpawnCount} {PetLocationCatalog.SpawnWord(SpawnCount)}";
    public string Subtitle => Sources.Count == 1
        ? $"{Sources[0].Name} · {SpawnText}"
        : $"{Sources.Count} {PetLocationCatalog.SourceWord(Sources.Count)} · {SpawnText}";
}

public sealed record PetLocationSnapshot(
    int PetId,
    IReadOnlyList<PetRegionLocation> Regions,
    IReadOnlyList<PetMapLocation> Maps)
{
    public bool HasLocations => Maps.Count > 0;
    public int TotalSpawnCount => Maps.Sum(map => map.SpawnCount);
    public string Summary => HasLocations
        ? $"{Maps.Count} {PetLocationCatalog.PlaceWord(Maps.Count)} · {TotalSpawnCount} {PetLocationCatalog.SpawnWord(TotalSpawnCount)}"
        : UiLanguageService.Pick("No spawn points for this pet were found in the game data", "Для этого питомца точки спавна в игровых таблицах не найдены");
}

internal sealed class PetLocationCatalog
{
    private static readonly Lazy<PetLocationCatalog> DefaultCatalog = new(LoadDefaultCore);

    private readonly string _root;
    private readonly Dictionary<int, int[]> _petsOfNpc;
    private readonly Dictionary<string, Dictionary<int, List<PetSpawnPoint>>> _spawns;
    private readonly IReadOnlyList<RegionGroup> _regionGroups;
    private readonly IReadOnlyList<PetMapDefinition> _mapDefinitions;
    private readonly Dictionary<string, string> _instanceNames;
    private readonly Dictionary<int, NpcInfo> _npcInfo;

    private sealed record RegionGroup(string Name, IReadOnlyList<RegionInfo> Regions);
    private sealed record RegionInfo(string Name, HashSet<int> Pets);
    private sealed record NpcInfo(string Name, int Level);

    private PetLocationCatalog(
        string root,
        Dictionary<int, int[]> petsOfNpc,
        Dictionary<string, Dictionary<int, List<PetSpawnPoint>>> spawns,
        IReadOnlyList<RegionGroup> regionGroups,
        IReadOnlyList<PetMapDefinition> mapDefinitions,
        Dictionary<string, string> instanceNames,
        Dictionary<int, NpcInfo> npcInfo)
    {
        _root = root;
        _petsOfNpc = petsOfNpc;
        _spawns = spawns;
        _regionGroups = regionGroups;
        _mapDefinitions = mapDefinitions;
        _instanceNames = instanceNames;
        _npcInfo = npcInfo;
    }

    public static PetLocationCatalog Default => DefaultCatalog.Value;

    public string MapTileRoot => Path.Combine(_root, "maps");

    public PetLocationSnapshot Resolve(int petId)
    {
        int[] npcIds = _petsOfNpc
            .Where(pair => pair.Value.Contains(petId))
            .Select(pair => pair.Key)
            .ToArray();
        var wanted = npcIds.ToHashSet();

        var regions = _regionGroups
            .SelectMany(group => group.Regions
                .Where(region => region.Pets.Contains(petId))
                .Select(region => new PetRegionLocation(group.Name, region.Name)))
            .Distinct()
            .ToArray();

        var maps = new List<PetMapLocation>();
        var usedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PetMapDefinition definition in _mapDefinitions)
        {
            PetMapLocation? map = BuildMap(definition.Key, definition.Title, definition, definition.Tables, wanted);
            if (map is null)
            {
                continue;
            }

            maps.Add(map);
            usedTables.UnionWith(definition.Tables);
        }

        foreach ((string table, Dictionary<int, List<PetSpawnPoint>> _) in _spawns)
        {
            if (usedTables.Contains(table))
            {
                continue;
            }

            PetMapLocation? map = BuildMap(table, FriendlyMapName(table), null, new[] { table }, wanted);
            if (map is not null)
            {
                maps.Add(map);
            }
        }

        return new PetLocationSnapshot(
            petId,
            regions,
            maps.OrderByDescending(map => map.Definition is not null)
                .ThenByDescending(map => map.SpawnCount)
                .ThenBy(map => map.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }

    private PetMapLocation? BuildMap(
        string key,
        string title,
        PetMapDefinition? definition,
        IReadOnlyList<string> tables,
        HashSet<int> wanted)
    {
        var points = new List<PetSpawnPoint>();
        var seen = new HashSet<(int Npc, long X, long Y, long Z)>();
        foreach (string table in tables)
        {
            if (!_spawns.TryGetValue(table, out Dictionary<int, List<PetSpawnPoint>>? tableSpawns))
            {
                continue;
            }

            foreach ((int npc, List<PetSpawnPoint> npcPoints) in tableSpawns)
            {
                if (!wanted.Contains(npc))
                {
                    continue;
                }

                foreach (PetSpawnPoint point in npcPoints)
                {
                    var identity = (npc, (long)Math.Round(point.X), (long)Math.Round(point.Y), (long)Math.Round(point.Z));
                    if (seen.Add(identity))
                    {
                        points.Add(point);
                    }
                }
            }
        }

        if (points.Count == 0)
        {
            return null;
        }

        PetSpawnSource[] sources = points
            .GroupBy(point => point.NpcId)
            .Select(group =>
            {
                NpcInfo info = _npcInfo.GetValueOrDefault(group.Key) ?? new NpcInfo(UiLanguageService.Pick($"Mob #{group.Key}", $"Моб #{group.Key}"), 0);
                return new PetSpawnSource(group.Key, info.Name, info.Level, group.Count());
            })
            .OrderByDescending(source => source.SpawnCount)
            .ThenBy(source => source.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new PetMapLocation(key, title, definition, points, sources);
    }

    private string FriendlyMapName(string table)
    {
        if (_instanceNames.TryGetValue(table, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        string leaf = table.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? table;
        int suffix = leaf.IndexOf('_');
        if (suffix > 0)
        {
            leaf = leaf[..suffix];
        }
        return leaf.Replace('_', ' ');
    }

    private static PetLocationCatalog LoadDefaultCore()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "data", "pet-locations");
        try
        {
            Dictionary<int, int[]> petsOfNpc = LoadPetMonsters(Path.Combine(root, "pets.json"));
            Dictionary<string, Dictionary<int, List<PetSpawnPoint>>> spawns = LoadSpawns(Path.Combine(root, "spawns.json"));
            IReadOnlyList<RegionGroup> regions = LoadRegions(Path.Combine(root, "regions.json"));
            Dictionary<string, string> instanceNames = LoadInstanceNames(Path.Combine(root, "instances.json"));
            IReadOnlyList<PetMapDefinition> definitions = LoadMapDefinitions(Path.Combine(root, "maps"));
            Dictionary<int, NpcInfo> npcs = LoadNpcInfo(Path.Combine(AppContext.BaseDirectory, "data", "mobs.json"));
            return new PetLocationCatalog(root, petsOfNpc, spawns, regions, definitions, instanceNames, npcs);
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return new PetLocationCatalog(root, new(), new(StringComparer.OrdinalIgnoreCase), [], [], new(StringComparer.OrdinalIgnoreCase), new());
        }
    }

    private static Dictionary<int, int[]> LoadPetMonsters(string path)
    {
        var result = new Dictionary<int, int[]>();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonProperty monster in document.RootElement.GetProperty("monsters").EnumerateObject())
        {
            int npcId = int.Parse(monster.Name, CultureInfo.InvariantCulture);
            result[npcId] = monster.Value.ValueKind == JsonValueKind.Array
                ? monster.Value.EnumerateArray().Select(item => item.GetInt32()).ToArray()
                : new[] { monster.Value.GetInt32() };
        }
        return result;
    }

    private static Dictionary<string, Dictionary<int, List<PetSpawnPoint>>> LoadSpawns(string path)
    {
        var result = new Dictionary<string, Dictionary<int, List<PetSpawnPoint>>>(StringComparer.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonProperty map in document.RootElement.GetProperty("maps").EnumerateObject())
        {
            var npcs = new Dictionary<int, List<PetSpawnPoint>>();
            foreach (JsonProperty npc in map.Value.EnumerateObject())
            {
                int npcId = int.Parse(npc.Name, CultureInfo.InvariantCulture);
                var points = new List<PetSpawnPoint>();
                foreach (JsonElement point in npc.Value.EnumerateArray())
                {
                    double x = point.GetArrayLength() > 0 ? point[0].GetDouble() : 0d;
                    double y = point.GetArrayLength() > 1 ? point[1].GetDouble() : 0d;
                    double z = point.GetArrayLength() > 2 ? point[2].GetDouble() : 0d;
                    points.Add(new PetSpawnPoint(npcId, x, y, z));
                }
                npcs[npcId] = points;
            }
            result[map.Name] = npcs;
        }
        return result;
    }

    private static IReadOnlyList<RegionGroup> LoadRegions(string path)
    {
        var result = new List<RegionGroup>();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement group in document.RootElement.GetProperty("groups").EnumerateArray())
        {
            string id = group.GetProperty("id").GetString() ?? string.Empty;
            string world = id switch
            {
                "verteron" => "Бертрон",
                "altgard" => "Альтгард",
                "abyss" => "Арэшурат",
                _ => id
            };
            var regions = new List<RegionInfo>();
            foreach (JsonElement region in group.GetProperty("regions").EnumerateArray())
            {
                JsonElement names = region.GetProperty("names");
                string name = names.TryGetProperty("ru", out JsonElement ru) && !string.IsNullOrWhiteSpace(ru.GetString())
                    ? ru.GetString()!
                    : names.TryGetProperty("en", out JsonElement en) ? en.GetString() ?? "Неизвестная область" : "Неизвестная область";
                regions.Add(new RegionInfo(name, region.GetProperty("pets").EnumerateArray().Select(item => item.GetInt32()).ToHashSet()));
            }
            result.Add(new RegionGroup(world, regions));
        }
        return result;
    }

    private static Dictionary<string, string> LoadInstanceNames(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonProperty map in document.RootElement.GetProperty("maps").EnumerateObject())
        {
            string? name = map.Value.TryGetProperty("ru", out JsonElement ru) ? ru.GetString() : null;
            name ??= map.Value.TryGetProperty("en", out JsonElement en) ? en.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                result[map.Name] = name;
            }
        }
        return result;
    }

    private static IReadOnlyList<PetMapDefinition> LoadMapDefinitions(string mapsRoot)
    {
        var result = new List<PetMapDefinition>();
        Read(Path.Combine(mapsRoot, "maps.json"), true);
        Read(Path.Combine(mapsRoot, "instances.json"), false);
        return result;

        void Read(string path, bool world)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (JsonProperty map in document.RootElement.GetProperty("maps").EnumerateObject())
            {
                JsonElement value = map.Value;
                string table = value.GetProperty("table").GetString() ?? string.Empty;
                string[] tables = value.TryGetProperty("tables", out JsonElement tableList)
                    ? tableList.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray()
                    : new[] { table };
                string title = world ? map.Name switch
                {
                    "verteron" => "Бертрон",
                    "altgard" => "Альтгард",
                    "abyss" => "Арэшурат",
                    _ => value.GetProperty("title").GetString() ?? map.Name
                } : value.GetProperty("title").GetString() ?? map.Name;
                result.Add(new PetMapDefinition(
                    map.Name,
                    title,
                    value.GetProperty("tile").GetInt32(),
                    value.GetProperty("grid").GetInt32(),
                    value.GetProperty("scale").GetDouble(),
                    value.GetProperty("offsetX").GetDouble(),
                    value.GetProperty("offsetY").GetDouble(),
                    tables,
                    value.TryGetProperty("zones", out JsonElement zones)
                        ? zones.EnumerateArray().Select(item => item.GetInt32()).ToArray()
                        : []));
            }
        }
    }

    private static Dictionary<int, NpcInfo> LoadNpcInfo(string path)
    {
        var result = new Dictionary<int, NpcInfo>();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Visit(document.RootElement);
        return result;

        void Visit(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) &&
                    property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.TryGetProperty("en", out JsonElement nameElement))
                {
                    string name = nameElement.GetString() ?? $"Моб #{id}";
                    int level = property.Value.TryGetProperty("level", out JsonElement levelElement) && levelElement.TryGetInt32(out int parsed)
                        ? parsed
                        : 0;
                    result.TryAdd(id, new NpcInfo(name, level));
                }
                else
                {
                    Visit(property.Value);
                }
            }
        }
    }

    public static string SpawnWord(int count)
    {
        if (!UiLanguageService.IsRussian)
        {
            return count == 1 ? "point" : "points";
        }
        int lastTwo = count % 100;
        int last = count % 10;
        return lastTwo is >= 11 and <= 14 ? "точек" : last switch
        {
            1 => "точка",
            2 or 3 or 4 => "точки",
            _ => "точек"
        };
    }

    public static string PlaceWord(int count)
    {
        if (!UiLanguageService.IsRussian)
        {
            return count == 1 ? "location" : "locations";
        }
        int lastTwo = count % 100;
        int last = count % 10;
        return lastTwo is >= 11 and <= 14 ? "локаций" : last switch
        {
            1 => "локация",
            2 or 3 or 4 => "локации",
            _ => "локаций"
        };
    }

    public static string SourceWord(int count)
    {
        if (!UiLanguageService.IsRussian)
        {
            return count == 1 ? "source mob" : "source mobs";
        }
        int lastTwo = count % 100;
        int last = count % 10;
        return lastTwo is >= 11 and <= 14 ? "мобов-источников" : last switch
        {
            1 => "моб-источник",
            2 or 3 or 4 => "моба-источника",
            _ => "мобов-источников"
        };
    }
}
