using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Localization;
using AionFlex.Core.Models;
using Microsoft.Extensions.Logging;

namespace AionFlex.Application.Skills;

public sealed class SkillRepository : ISkillRepository
{
	private sealed class SkillEntry
	{
		private readonly string? _en;

		private readonly string? _ko;

		private readonly string? _zh;

		public SkillEntry(string? en, string? ko = null, string? zh = null)
		{
			_en = en;
			_ko = ko;
			_zh = zh;
		}

		public string GetName(Language lang)
		{
			return lang switch
			{
				Language.Korean => _ko ?? _en ?? "?", 
				Language.Chinese => _zh ?? _en ?? "?", 
				_ => _en ?? "?", 
			};
		}
	}

	private readonly ILogger<SkillRepository> _logger;

	private readonly string _skillsFilePath;

	private readonly string _npcSkillsFilePath;

	private readonly string _theostonesFilePath;

	private readonly Func<Language> _languageProvider;

	private readonly ConcurrentDictionary<int, SkillEntry> _skillCache = new ConcurrentDictionary<int, SkillEntry>();

	private readonly ConcurrentDictionary<int, SkillMetadata> _metadataCache = new ConcurrentDictionary<int, SkillMetadata>();

	private readonly HashSet<int> _playerSkillCodes = new HashSet<int>();

	private volatile int[] _sortedSkillCodes = Array.Empty<int>();

	private readonly HashSet<int> _provenNonDamage = new HashSet<int>();

	private readonly HashSet<int> _provenHeal = new HashSet<int>();

	private readonly string _skillEffectTypesFilePath;

	private readonly HashSet<int> _summonCarriedSkills = new HashSet<int>();

	private readonly string _summonSkillsFilePath;

	private const int PlayerSkillCodeFloor = 10000000;

	public SkillRepository(ILogger<SkillRepository> logger, Func<Language>? languageProvider = null, string? skillsFilePath = null, string? npcSkillsFilePath = null, string? theostonesFilePath = null, string? skillEffectTypesFilePath = null, string? summonSkillsFilePath = null)
	{
		_logger = logger;
		_languageProvider = languageProvider ?? ((Func<Language>)(() => Language.English));
		_skillsFilePath = skillsFilePath ?? Path.Combine("data", "skills.json");
		_npcSkillsFilePath = npcSkillsFilePath ?? Path.Combine("data", "npc_skills.json");
		_theostonesFilePath = theostonesFilePath ?? Path.Combine("data", "theostones.json");
		_skillEffectTypesFilePath = skillEffectTypesFilePath ?? Path.Combine("data", "skill_effect_types.json");
		_summonSkillsFilePath = summonSkillsFilePath ?? Path.Combine("data", "summon_skills.json");
	}

	public async Task InitializeAsync()
	{
		await ReloadAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	public bool IsProvenNonDamage(int skillCode)
	{
		if (!_provenNonDamage.Contains(skillCode))
		{
			return false;
		}
		if (skillCode < 10000000)
		{
			return true;
		}
		int item = skillCode / 10000 * 10000;
		return _provenNonDamage.Contains(item);
	}

	public bool IsProvenHeal(int skillCode)
	{
		if (skillCode < 10000000)
		{
			return false;
		}
		if (!_provenHeal.Contains(skillCode))
		{
			return false;
		}
		return _provenNonDamage.Contains(skillCode / 10000 * 10000);
	}

	public string GetSkillName(int skillCode)
	{
		return GetSkillName(skillCode, _languageProvider());
	}

	public string GetSkillName(int skillCode, Language language)
	{
		if (_skillCache.TryGetValue(skillCode, out SkillEntry value))
		{
			return value.GetName(language);
		}
		int num = skillCode / 10000 * 10000;
		if (num != skillCode && _skillCache.TryGetValue(num, out value))
		{
			return value.GetName(language);
		}
		return skillCode.ToString();
	}

	public bool HasSkill(int skillCode)
	{
		if (_skillCache.ContainsKey(skillCode))
		{
			return true;
		}
		int num = skillCode / 10000 * 10000;
		if (num != skillCode)
		{
			return _skillCache.ContainsKey(num);
		}
		return false;
	}

	public IReadOnlyCollection<int> GetAllSkillCodes()
	{
		return _skillCache.Keys.ToList();
	}

	public int[] GetSortedSkillCodes()
	{
		return _sortedSkillCodes;
	}

	public async Task ReloadAsync()
	{
		_ = 4;
		try
		{
			_skillCache.Clear();
			_metadataCache.Clear();
			_playerSkillCodes.Clear();
			_provenNonDamage.Clear();
			_provenHeal.Clear();
			_summonCarriedSkills.Clear();
			JsonElement value;
			try
			{
				if (File.Exists(_summonSkillsFilePath))
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(await File.ReadAllTextAsync(_summonSkillsFilePath).ConfigureAwait(continueOnCapturedContext: false));
					value = jsonDocument.RootElement;
					if (value.TryGetProperty("skills", out var value2) && value2.ValueKind == JsonValueKind.Object)
					{
						foreach (JsonProperty item in value2.EnumerateObject())
						{
							if (int.TryParse(item.Name, out var result))
							{
								_summonCarriedSkills.Add(result);
							}
						}
					}
					_logger.LogInformation("Loaded {Count} summon-carried skill families from {Path}", _summonCarriedSkills.Count, _summonSkillsFilePath);
				}
			}
			catch (Exception exception)
			{
				_summonCarriedSkills.Clear();
				_logger.LogWarning(exception, "Failed to load summon-carried skills from {Path} — classifying nothing (fail-open)", _summonSkillsFilePath);
			}
			if (File.Exists(_skillEffectTypesFilePath))
			{
				using JsonDocument jsonDocument2 = JsonDocument.Parse(await File.ReadAllTextAsync(_skillEffectTypesFilePath).ConfigureAwait(continueOnCapturedContext: false));
				value = jsonDocument2.RootElement;
				if (value.TryGetProperty("nonDamageSkills", out var value3) && value3.ValueKind == JsonValueKind.Array)
				{
					foreach (JsonElement item2 in value3.EnumerateArray())
					{
						if (item2.TryGetInt32(out var value4))
						{
							_provenNonDamage.Add(value4);
						}
					}
				}
				value = jsonDocument2.RootElement;
				if (value.TryGetProperty("healSkills", out var value5) && value5.ValueKind == JsonValueKind.Array)
				{
					foreach (JsonElement item3 in value5.EnumerateArray())
					{
						if (item3.TryGetInt32(out var value6))
						{
							_provenHeal.Add(value6);
						}
					}
				}
				_logger.LogInformation("Loaded {Count} proven non-damage skills ({Heals} pure heals) from {Path}", _provenNonDamage.Count, _provenHeal.Count, _skillEffectTypesFilePath);
			}
			if (File.Exists(_skillsFilePath))
			{
				using JsonDocument jsonDocument3 = JsonDocument.Parse(await File.ReadAllTextAsync(_skillsFilePath).ConfigureAwait(continueOnCapturedContext: false));
				JsonElement rootElement = jsonDocument3.RootElement;
				if (rootElement.TryGetProperty("_meta", out value))
				{
					LoadStructuredFormat(rootElement);
				}
				else
				{
					LoadLegacyFormat(rootElement);
				}
				_logger.LogInformation("Loaded {Count} player skills from {Path}", _skillCache.Count, _skillsFilePath);
			}
			else
			{
				_logger.LogWarning("Skills file not found at {Path}", _skillsFilePath);
			}
			if (File.Exists(_npcSkillsFilePath))
			{
				using JsonDocument jsonDocument4 = JsonDocument.Parse(await File.ReadAllTextAsync(_npcSkillsFilePath).ConfigureAwait(continueOnCapturedContext: false));
				JsonElement rootElement2 = jsonDocument4.RootElement;
				int num = 0;
				foreach (JsonProperty item4 in rootElement2.EnumerateObject())
				{
					if (!item4.Name.StartsWith("_") && int.TryParse(item4.Name, out var result2))
					{
						_skillCache[result2] = ParseSkillEntry(item4.Value);
						num++;
					}
				}
				_logger.LogInformation("Loaded {Count} NPC skills from {Path}", num, _npcSkillsFilePath);
			}
			else
			{
				_logger.LogWarning("NPC skills file not found at {Path}", _npcSkillsFilePath);
			}
			if (File.Exists(_theostonesFilePath))
			{
				using JsonDocument jsonDocument5 = JsonDocument.Parse(await File.ReadAllTextAsync(_theostonesFilePath).ConfigureAwait(continueOnCapturedContext: false));
				JsonElement rootElement3 = jsonDocument5.RootElement;
				int num2 = 0;
				foreach (JsonProperty item5 in rootElement3.EnumerateObject())
				{
					if (!item5.Name.StartsWith("_") && int.TryParse(item5.Name, out var result3))
					{
						_skillCache[result3] = ParseSkillEntry(item5.Value);
						num2++;
					}
				}
				_logger.LogInformation("Loaded {Count} Theostone skills from {Path}", num2, _theostonesFilePath);
			}
			int[] array = _skillCache.Keys.ToArray();
			Array.Sort(array);
			_sortedSkillCodes = array;
			_logger.LogInformation("Total skills loaded: {Count} ({MetaCount} with metadata)", _skillCache.Count, _metadataCache.Count);
		}
		catch (Exception exception2)
		{
			_logger.LogError(exception2, "Error loading skills");
			throw;
		}
	}

	private void LoadStructuredFormat(JsonElement root)
	{
		if (root.TryGetProperty("classes", out var value))
		{
			foreach (JsonProperty item in value.EnumerateObject())
			{
				if (!item.Value.TryGetProperty("skills", out var value2))
				{
					continue;
				}
				foreach (JsonProperty item2 in value2.EnumerateObject())
				{
					if (int.TryParse(item2.Name, out var result))
					{
						_skillCache[result] = ParseSkillEntry(item2.Value);
						_playerSkillCodes.Add(result);
						SkillMetadata skillMetadata = ParseSkillMetadata(item2.Value);
						if (skillMetadata != null)
						{
							_metadataCache[result] = skillMetadata;
						}
					}
				}
			}
		}
		if (!root.TryGetProperty("spirits", out var value3))
		{
			return;
		}
		foreach (JsonProperty item3 in value3.EnumerateObject())
		{
			if (int.TryParse(item3.Name, out var result2))
			{
				_skillCache[result2] = ParseSkillEntry(item3.Value);
				_playerSkillCodes.Add(result2);
			}
		}
	}

	private void LoadLegacyFormat(JsonElement root)
	{
		foreach (JsonProperty item in root.EnumerateObject())
		{
			if (int.TryParse(item.Name, out var result))
			{
				_skillCache[result] = new SkillEntry(item.Value.GetString() ?? item.Name);
			}
		}
	}

	private static SkillEntry ParseSkillEntry(JsonElement element)
	{
		string? en = (element.TryGetProperty("en", out var value) ? value.GetString() : null);
		string ko = (element.TryGetProperty("ko", out var value2) ? value2.GetString() : null);
		string zh = (element.TryGetProperty("zh", out var value3) ? value3.GetString() : null);
		return new SkillEntry(en, ko, zh);
	}

	private static SkillMetadata? ParseSkillMetadata(JsonElement element)
	{
		if (!element.TryGetProperty("type", out var value))
		{
			return null;
		}
		List<string> list = new List<string>();
		if (element.TryGetProperty("cat", out var value2) && value2.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in value2.EnumerateArray())
			{
				string text = item.GetString();
				if (text != null)
				{
					list.Add(text);
				}
			}
		}
		JsonElement value3;
		JsonElement value4;
		JsonElement value5;
		JsonElement value6;
		int value7;
		return new SkillMetadata
		{
			Type = (value.GetString() ?? "Active"),
			Categories = list,
			DamageType = (element.TryGetProperty("dmg", out value3) ? (value3.GetString() ?? "None") : "None"),
			Disposition = (element.TryGetProperty("disp", out value4) ? (value4.GetString() ?? "Negative") : "Negative"),
			IsStigma = (element.TryGetProperty("stigma", out value5) && value5.ValueKind == JsonValueKind.True),
			ChainPrevCode = ((element.TryGetProperty("chainPrev", out value6) && value6.ValueKind == JsonValueKind.Number && value6.TryGetInt32(out value7)) ? value7 : 0)
		};
	}

	public SkillMetadata? GetSkillMetadata(int skillCode)
	{
		if (_metadataCache.TryGetValue(skillCode, out SkillMetadata value))
		{
			return value;
		}
		int num = skillCode / 10000 * 10000;
		if (num != skillCode && _metadataCache.TryGetValue(num, out value))
		{
			return value;
		}
		return null;
	}

	public bool IsPlayerSkill(int skillCode)
	{
		if (_playerSkillCodes.Contains(skillCode))
		{
			return true;
		}
		int num = skillCode / 10000 * 10000;
		if (num != skillCode)
		{
			return _playerSkillCodes.Contains(num);
		}
		return false;
	}

	public bool IsGhostDamageSkill(int skillCode)
	{
		return GetSkillMetadata(skillCode)?.CanProduceGhostDamage ?? false;
	}

	public bool IsSummonCarriedSkill(int skillCode)
	{
		if (!_summonCarriedSkills.Contains(skillCode))
		{
			return _summonCarriedSkills.Contains(skillCode / 10000 * 10000);
		}
		return true;
	}

	public SkillRole GetSkillRole(int skillCode)
	{
		return GetSkillMetadata(skillCode)?.Role ?? SkillRole.Dps;
	}

	public bool IsNonDamageSkill(int skillCode)
	{
		SkillMetadata skillMetadata = GetSkillMetadata(skillCode);
		if (skillMetadata == null)
		{
			return false;
		}
		if (skillMetadata.IsAttack || skillMetadata.IsDebuff)
		{
			return false;
		}
		if (!skillMetadata.IsBuff && !skillMetadata.IsHeal)
		{
			return skillMetadata.IsSpawn;
		}
		return true;
	}

	public bool IsHealSkill(int skillCode)
	{
		SkillMetadata skillMetadata = GetSkillMetadata(skillCode);
		if (skillMetadata == null)
		{
			return false;
		}
		if (skillMetadata.IsHeal)
		{
			return !skillMetadata.IsAttack;
		}
		return false;
	}

	public int GetSkillCount()
	{
		return _skillCache.Count;
	}
}
