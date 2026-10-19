using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AionFlex.Core.Combat;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Localization;
using Microsoft.Extensions.Logging;

namespace AionFlex.Application.Mobs;

public sealed class MobRepository : IMobRepository
{
	private sealed class MobEntry
	{
		private readonly string? _en;

		private readonly string? _ko;

		private readonly string? _zh;

		public string Role { get; }

		public int Level { get; }

		public string? Dungeon { get; }

		public uint MaxHp { get; }

		public string? Portrait { get; }

		public string? InternalName { get; }

		public int BerserkSeconds { get; init; }

		public MobEntry(string? en, string? ko, string? zh, string role, int level, string? dungeon, uint maxHp = 0u, string? portrait = null, string? internalName = null)
		{
			_en = en;
			_ko = ko;
			_zh = zh;
			Role = role;
			Level = level;
			Dungeon = dungeon;
			MaxHp = maxHp;
			Portrait = portrait;
			InternalName = internalName;
		}

		public string? GetName(Language lang)
		{
			return lang switch
			{
				Language.Korean => _ko ?? _en, 
				Language.Chinese => _zh ?? _en, 
				_ => _en, 
			};
		}
	}

	private sealed class DungeonNameEntry
	{
		private readonly string? _en;

		private readonly string? _ko;

		private readonly string? _zh;

		public DungeonNameEntry(string? en, string? ko, string? zh)
		{
			_en = en;
			_ko = ko;
			_zh = zh;
		}

		public string? GetName(Language lang)
		{
			return lang switch
			{
				Language.Korean => _ko ?? _en, 
				Language.Chinese => _zh ?? _en, 
				_ => _en, 
			};
		}
	}

	private readonly ILogger<MobRepository> _logger;

	private readonly string _mobsFilePath;

	private readonly string _npcSummonsFilePath;

	private readonly Func<Language> _languageProvider;

	private readonly ConcurrentDictionary<int, MobEntry> _mobCache = new ConcurrentDictionary<int, MobEntry>();

	private readonly ConcurrentDictionary<string, string> _mobRoleByName = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly ConcurrentDictionary<string, DungeonNameEntry> _dungeonNames = new ConcurrentDictionary<string, DungeonNameEntry>();

	private readonly ConcurrentDictionary<string, uint> _dungeonBossHpThresholds = new ConcurrentDictionary<string, uint>();

	private const uint DefaultBossHpThreshold = 500000u;

	private readonly ConcurrentDictionary<string, string> _dungeonCategories = new ConcurrentDictionary<string, string>();

	private readonly ConcurrentDictionary<string, IReadOnlyList<int>> _dungeonBossNpcIds = new ConcurrentDictionary<string, IReadOnlyList<int>>();

	private volatile bool _loadCompleted;

	private readonly ConcurrentDictionary<int, string?> _bossDungeons = new ConcurrentDictionary<int, string>();

	private static readonly HashSet<string> NonInstanceSections = new HashSet<string>(StringComparer.Ordinal) { "world", "invisible_summons", "pc_bots", "pc_summons", "untargetable_npcs" };

	private static readonly Regex MirrorHeartGradeRe = new Regex("_G_\\d", RegexOptions.Compiled);

	private static readonly Regex GradeStageRe = new Regex("_Grade\\d", RegexOptions.Compiled);

	private static readonly Regex FrozenLamentTierRe = new Regex("M_PD_FrozenLament_0(\\d)_", RegexOptions.Compiled);

	private static readonly Regex TrialRe = new Regex("_T_\\d+_|^M_PD_.*_T_\\d+$", RegexOptions.Compiled);

	private static readonly Regex AwakenTierRe = new Regex("^M_Dungeon_Awaken_\\d{3}_(\\d{2})_", RegexOptions.Compiled);

	private static readonly IReadOnlyDictionary<string, string> AwakenTierNames = new Dictionary<string, string>
	{
		["01"] = "Ascension Trial Easy",
		["02"] = "Ascension Trial Normal",
		["03"] = "Ascension Trial Hard",
		["04"] = "Ascension Trial Challenge",
		["05"] = "Ascension Trial Insane",
		["06"] = "Ascension Trial Hell",
		["07"] = "Ascension Trial Doom"
	};

	private static readonly HashSet<int> TrainingDummyNpcIds = new HashSet<int> { 2300229, 2310229, 2320229, 2300919, 2310919, 2320919 };

	public MobRepository(ILogger<MobRepository> logger, Func<Language>? languageProvider = null, string? mobsFilePath = null, string? npcSummonsFilePath = null)
	{
		_logger = logger;
		_languageProvider = languageProvider ?? ((Func<Language>)(() => Language.English));
		_mobsFilePath = mobsFilePath ?? Path.Combine("data", "mobs.json");
		_npcSummonsFilePath = npcSummonsFilePath ?? Path.Combine("data", "npc_summons.json");
	}

	public string? GetMobName(int npcId)
	{
		if (_mobCache.TryGetValue(npcId, out MobEntry value))
		{
			Language lang = _languageProvider();
			return value.GetName(lang);
		}
		return null;
	}

	public string? GetMobRole(int npcId)
	{
		if (!_mobCache.TryGetValue(npcId, out MobEntry value))
		{
			return null;
		}
		return value.Role;
	}

	public string? GetMobRoleByName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		return _mobRoleByName.TryGetValue(name.Trim(), out string? role) ? role : null;
	}

	public bool IsBoss(int npcId)
	{
		if (_mobCache.TryGetValue(npcId, out MobEntry value))
		{
			return value.Role == "boss";
		}
		return false;
	}

	public bool IsMiniboss(int npcId)
	{
		if (_mobCache.TryGetValue(npcId, out MobEntry value))
		{
			return value.Role == "miniboss";
		}
		return false;
	}

	public uint GetMaxHp(int code)
	{
		if (!_mobCache.TryGetValue(code, out MobEntry value))
		{
			return 0u;
		}
		return value.MaxHp;
	}

	public IReadOnlyList<int> GetDungeonBossNpcIds(string dungeonKey)
	{
		if (!_loadCompleted)
		{
			return ScanDungeonBossNpcIds(dungeonKey);
		}
		return _dungeonBossNpcIds.GetOrAdd(dungeonKey, (string key, MobRepository self) => self.ScanDungeonBossNpcIds(key), this);
	}

	private IReadOnlyList<int> ScanDungeonBossNpcIds(string dungeonKey)
	{
		return (from kvp in _mobCache.Where((KeyValuePair<int, MobEntry> kvp) =>
			{
				bool flag = kvp.Value.Dungeon == dungeonKey;
				if (flag)
				{
					string role = kvp.Value.Role;
					bool flag2 = ((role == "boss" || role == "miniboss") ? true : false);
					flag = flag2;
				}
				return flag;
			})
			select kvp.Key).ToList();
	}

	public bool HasMob(int npcId)
	{
		return _mobCache.ContainsKey(npcId);
	}

	public string? GetMobDungeon(int npcId)
	{
		if (!_mobCache.TryGetValue(npcId, out MobEntry value))
		{
			return null;
		}
		return value.Dungeon;
	}

	public string? GetBossInstanceDungeon(int npcId)
	{
		if (!_bossDungeons.TryGetValue(npcId, out string value))
		{
			return null;
		}
		return value;
	}

	public bool IsInstanceDungeon(string? dungeonKey)
	{
		if (dungeonKey != null)
		{
			return !NonInstanceSections.Contains(dungeonKey);
		}
		return false;
	}

	public string? GetMobDifficulty(int npcId)
	{
		if (!_mobCache.TryGetValue(npcId, out MobEntry value) || value.InternalName == null)
		{
			return null;
		}
		string internalName = value.InternalName;
		Match match = AwakenTierRe.Match(internalName);
		if (match.Success && AwakenTierNames.TryGetValue(match.Groups[1].Value, out string value2))
		{
			return value2;
		}
		Match match2 = FrozenLamentTierRe.Match(internalName);
		if (match2.Success)
		{
			return match2.Groups[1].Value switch
			{
				"1" => "Hard", 
				"2" => "Normal", 
				"3" => "Easy", 
				_ => "Normal", 
			};
		}
		if (internalName.Contains("_Easy", StringComparison.OrdinalIgnoreCase))
		{
			return "Exploration";
		}
		if (internalName.Contains("_hard", StringComparison.OrdinalIgnoreCase))
		{
			return "Hard";
		}
		if (GradeStageRe.IsMatch(internalName))
		{
			return "Grade";
		}
		if (MirrorHeartGradeRe.IsMatch(internalName))
		{
			return "Grade";
		}
		if (TrialRe.IsMatch(internalName))
		{
			return "Ordeal";
		}
		return "Normal";
	}

	public string? GetMobTierToken(int npcId)
	{
		if (!_mobCache.TryGetValue(npcId, out MobEntry value) || value.InternalName == null)
		{
			return null;
		}
		return NpcTierToken.Of(value.InternalName);
	}

	public int? GetBerserkSeconds(int npcId)
	{
		if (!_mobCache.TryGetValue(npcId, out MobEntry value) || value.BerserkSeconds <= 0)
		{
			return null;
		}
		return value.BerserkSeconds;
	}

	public string? GetDungeonDisplayName(string dungeonKey)
	{
		if (_dungeonNames.TryGetValue(dungeonKey, out DungeonNameEntry value))
		{
			Language lang = _languageProvider();
			return value.GetName(lang) ?? dungeonKey;
		}
		return dungeonKey;
	}

	public uint GetBossHpThreshold(string? dungeonKey)
	{
		if (dungeonKey != null && _dungeonBossHpThresholds.TryGetValue(dungeonKey, out var value))
		{
			return value;
		}
		return 500000u;
	}

	public string? GetDungeonCategory(string? dungeonKey)
	{
		if (dungeonKey == null)
		{
			return null;
		}
		if (!_dungeonCategories.TryGetValue(dungeonKey, out string value))
		{
			return null;
		}
		return value;
	}

	public string? GetPortraitPath(int npcId)
	{
		if (!_mobCache.TryGetValue(npcId, out MobEntry value) || value.Portrait == null)
		{
			return null;
		}
		string fullPath = Path.GetFullPath(Path.Combine(Path.Combine(Path.GetDirectoryName(_mobsFilePath) ?? "data", "textures", "portraits"), value.Portrait));
		if (!File.Exists(fullPath))
		{
			return null;
		}
		return fullPath;
	}

	public async Task ReloadAsync()
	{
		_loadCompleted = false;
		_mobCache.Clear();
		_mobRoleByName.Clear();
		_dungeonNames.Clear();
		_dungeonBossHpThresholds.Clear();
		_dungeonCategories.Clear();
		_dungeonBossNpcIds.Clear();
		_bossDungeons.Clear();
		await LoadFileAsync(_mobsFilePath, required: true).ConfigureAwait(continueOnCapturedContext: false);
		await LoadFileAsync(_npcSummonsFilePath, required: false).ConfigureAwait(continueOnCapturedContext: false);
		_loadCompleted = true;
		_logger.LogInformation("Loaded {Count} total mob/NPC names ({MobsFile} + {SummonsFile})", _mobCache.Count, Path.GetFileName(_mobsFilePath), Path.GetFileName(_npcSummonsFilePath));
	}

	private async Task LoadFileAsync(string path, bool required)
	{
		try
		{
			if (!File.Exists(path))
			{
				if (required)
				{
					_logger.LogWarning("Mob data file not found at {Path}. Skipping.", path);
				}
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(continueOnCapturedContext: false));
			JsonElement rootElement = jsonDocument.RootElement;
			int count = _mobCache.Count;
			if (rootElement.TryGetProperty("_meta", out var _))
			{
				LoadStructuredFormat(rootElement);
			}
			else
			{
				LoadLegacyFormat(rootElement);
			}
			_logger.LogInformation("Loaded {Count} entries from {Path}", _mobCache.Count - count, path);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Error loading mob data from {Path}", path);
			if (required)
			{
				throw;
			}
		}
	}

	private void LoadStructuredFormat(JsonElement root)
	{
		if (root.TryGetProperty("dungeons", out var value) && value.ValueKind == JsonValueKind.Object)
		{
			foreach (JsonProperty item in value.EnumerateObject())
			{
				if (item.Value.TryGetProperty("name", out var value2))
				{
					string en = (value2.TryGetProperty("en", out var value3) ? value3.GetString() : null);
					string ko = (value2.TryGetProperty("ko", out var value4) ? value4.GetString() : null);
					string zh = (value2.TryGetProperty("zh", out var value5) ? value5.GetString() : null);
					_dungeonNames[item.Name] = new DungeonNameEntry(en, ko, zh);
				}
				if (item.Value.TryGetProperty("bossHpThreshold", out var value6))
				{
					_dungeonBossHpThresholds[item.Name] = (uint)value6.GetInt64();
				}
				if (item.Value.TryGetProperty("category", out var value7))
				{
					string value8 = value7.GetString();
					if (!string.IsNullOrEmpty(value8))
					{
						_dungeonCategories[item.Name] = value8;
					}
				}
				LoadNpcSection(item.Value, item.Name);
				IndexBossDungeon(item.Value, item.Name);
			}
		}
		foreach (JsonProperty item2 in root.EnumerateObject())
		{
			if (!(item2.Name == "_meta") && !(item2.Name == "dungeons"))
			{
				JsonElement value9 = item2.Value;
				if (value9.ValueKind == JsonValueKind.Object && item2.Value.TryGetProperty("npcs", out value9))
				{
					LoadNpcSection(item2.Value, item2.Name);
				}
			}
		}
	}

	private void LoadNpcSection(JsonElement section, string sectionName)
	{
		if (!section.TryGetProperty("npcs", out var value))
		{
			return;
		}
		foreach (JsonProperty item in value.EnumerateObject())
		{
			if (int.TryParse(item.Name, out var result))
			{
				string en = (item.Value.TryGetProperty("en", out var value2) ? value2.GetString() : null);
				string ko = (item.Value.TryGetProperty("ko", out var value3) ? value3.GetString() : null);
				string zh = (item.Value.TryGetProperty("zh", out var value4) ? value4.GetString() : null);
				string text = (item.Value.TryGetProperty("role", out var value5) ? value5.GetString() : "normal");
				int level = (item.Value.TryGetProperty("level", out var value6) ? value6.GetInt32() : 0);
				uint maxHp = (uint)(item.Value.TryGetProperty("maxHp", out var value7) ? value7.GetInt64() : 0u);
				string portrait = (item.Value.TryGetProperty("portrait", out var value8) ? value8.GetString() : null);
				string internalName = (item.Value.TryGetProperty("internalName", out var value9) ? value9.GetString() : null);
				int berserkSeconds = (int)((item.Value.TryGetProperty("berserkTime", out var value10) && value10.TryGetInt64(out var value11) && value11 > 0 && value11 <= int.MaxValue) ? value11 : 0);
				string role = text ?? "normal";
				_mobCache[result] = new MobEntry(en, ko, zh, role, level, sectionName, maxHp, portrait, internalName)
				{
					BerserkSeconds = berserkSeconds
				};
				IndexRoleName(en, role);
				IndexRoleName(ko, role);
				IndexRoleName(zh, role);
			}
		}
	}

	private void IndexRoleName(string? name, string role)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			_mobRoleByName[name.Trim()] = role;
		}
	}

	private void IndexBossDungeon(JsonElement dungeon, string dungeonKey)
	{
		if (!dungeon.TryGetProperty("npcs", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return;
		}
		foreach (JsonProperty item in value.EnumerateObject())
		{
			if (!int.TryParse(item.Name, out var result) || item.Value.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			string text = (item.Value.TryGetProperty("role", out var value2) ? value2.GetString() : null);
			if ((text == "boss" || text == "miniboss") ? true : false)
			{
				_bossDungeons.AddOrUpdate(result, dungeonKey, (int _, string? existing) => (!(existing == dungeonKey)) ? null : existing);
			}
		}
	}

	private void LoadLegacyFormat(JsonElement root)
	{
		foreach (JsonProperty item in root.EnumerateObject())
		{
			if (int.TryParse(item.Name, out var result))
			{
				string en = item.Value.GetString();
				_mobCache[result] = new MobEntry(en, null, null, "unknown", 0, null);
			}
		}
	}

	public int GetMobCount()
	{
		return _mobCache.Count;
	}

	public bool IsTrainingDummy(int npcId)
	{
		return TrainingDummyNpcIds.Contains(npcId);
	}
}
