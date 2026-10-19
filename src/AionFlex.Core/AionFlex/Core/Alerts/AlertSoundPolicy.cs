using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AionFlex.Core.Alerts;

public static class AlertSoundPolicy
{
	public const int MinVolume = 0;

	public const int MaxVolume = 100;

	public const int DefaultVolume = 80;

	public const string CustomPrefix = "custom:";

	public const long MaxCustomBytes = 5242880L;

	public static IReadOnlyList<AlertSoundKind> Kinds { get; } = new _003C_003Ez__ReadOnlyArray<AlertSoundKind>(new AlertSoundKind[8]
	{
		AlertSoundKind.Rift,
		AlertSoundKind.Siege,
		AlertSoundKind.AbyssBoss,
		AlertSoundKind.FieldBoss,
		AlertSoundKind.BossEnrage,
		AlertSoundKind.ShugoFesta,
		AlertSoundKind.Invasion,
		AlertSoundKind.Mechanics
	});

	public static IReadOnlyList<string> CuratedSounds { get; } = BuildCurated();

	public static IReadOnlyList<string> CustomExtensions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { ".wav", ".mp3" });

	private static IReadOnlyList<string> BuildCurated()
	{
		List<string> list = new List<string>();
		for (int i = 1; i <= 10; i++)
		{
			list.Add($"Alarm{i:D2}");
		}
		for (int j = 1; j <= 10; j++)
		{
			list.Add($"Ring{j:D2}");
		}
		list.Add("Windows Notify System Generic");
		list.Add("Windows Exclamation");
		list.Add("Windows Ding");
		return list;
	}

	public static string Label(AlertSoundKind kind)
	{
		return kind switch
		{
			AlertSoundKind.Rift => "Rift", 
			AlertSoundKind.Siege => "Artifact Siege", 
			AlertSoundKind.AbyssBoss => "Abyss bosses", 
			AlertSoundKind.FieldBoss => "Field bosses", 
			AlertSoundKind.BossEnrage => "Boss enrage", 
			AlertSoundKind.ShugoFesta => "Shugo Festival", 
			AlertSoundKind.Invasion => "Dimensional Invasion", 
			AlertSoundKind.Mechanics => "Mechanics (stagger, pillars)", 
			_ => kind.ToString(), 
		};
	}

	public static string Key(AlertSoundKind kind)
	{
		return kind switch
		{
			AlertSoundKind.Rift => "rift", 
			AlertSoundKind.Siege => "siege", 
			AlertSoundKind.AbyssBoss => "abyssBoss", 
			AlertSoundKind.FieldBoss => "fieldBoss", 
			AlertSoundKind.BossEnrage => "bossEnrage", 
			AlertSoundKind.ShugoFesta => "shugoFesta", 
			AlertSoundKind.Invasion => "invasion", 
			AlertSoundKind.Mechanics => "mechanics", 
			_ => kind.ToString(), 
		};
	}

	public static string DefaultSound(AlertSoundKind kind)
	{
		return kind switch
		{
			AlertSoundKind.BossEnrage => "Alarm02", 
			AlertSoundKind.FieldBoss => "Windows Notify System Generic", 
			_ => "Alarm01", 
		};
	}

	public static int ClampVolume(int volume)
	{
		return Math.Clamp(volume, 0, 100);
	}

	public static AlertSoundSetting Resolve(AlertSoundKind kind, AlertSoundSetting? saved)
	{
		if ((object)saved != null)
		{
			return saved with
			{
				SoundName = (string.IsNullOrWhiteSpace(saved.SoundName) ? DefaultSound(kind) : saved.SoundName),
				Volume = ClampVolume(saved.Volume)
			};
		}
		return new AlertSoundSetting(Enabled: false, DefaultSound(kind), 80);
	}

	public static IReadOnlyList<string> AvailableSounds(IEnumerable<string> mediaFiles)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string mediaFile in mediaFiles)
		{
			if (string.Equals(Path.GetExtension(mediaFile), ".wav", StringComparison.OrdinalIgnoreCase))
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(mediaFile);
				if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
				{
					hashSet.Add(fileNameWithoutExtension);
				}
			}
		}
		List<string> list = CuratedSounds.Where(hashSet.Contains).ToList();
		list.AddRange(hashSet.Where((string n) => !CuratedSounds.Contains(n, StringComparer.OrdinalIgnoreCase)).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase));
		return list;
	}

	public static bool IsCustom(string? soundName)
	{
		return soundName?.StartsWith("custom:", StringComparison.Ordinal) ?? false;
	}

	public static string Label(string soundName)
	{
		if (!IsCustom(soundName))
		{
			return soundName;
		}
		int length = "custom:".Length;
		return Path.GetFileNameWithoutExtension(soundName.Substring(length, soundName.Length - length)) + " (your sound)";
	}

	public static string? CustomFileFor(string customFolder, string? soundName)
	{
		if (!IsCustom(soundName) || string.IsNullOrWhiteSpace(customFolder))
		{
			return null;
		}
		int length = "custom:".Length;
		string text = soundName.Substring(length, soundName.Length - length);
		if (text.Length == 0 || text.IndexOfAny(new char[3]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar,
			':'
		}) >= 0)
		{
			return null;
		}
		if (text.Contains("..", StringComparison.Ordinal) || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			return null;
		}
		if (!CustomExtensions.Contains(Path.GetExtension(text), StringComparer.OrdinalIgnoreCase))
		{
			return null;
		}
		return Path.Combine(customFolder, text);
	}

	public static string? ImportProblem(string path, long length)
	{
		if (!CustomExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
		{
			return "Only WAV and MP3 files can be used.";
		}
		if (length <= 0)
		{
			return "The file is empty.";
		}
		if (length > 5242880)
		{
			return "The file is larger than 5 MB.";
		}
		return null;
	}

	public static string ImportFileName(string sourcePath, IEnumerable<string> existingFileNames)
	{
		string text = Path.GetExtension(sourcePath).ToLowerInvariant();
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);
		char[] invalid = Path.GetInvalidFileNameChars();
		fileNameWithoutExtension = new string(fileNameWithoutExtension.Select((char c) => (!Enumerable.Contains(invalid, c) && c != ':') ? c : '_').ToArray()).Trim().Replace("..", "_").Trim('.', ' ');
		if (fileNameWithoutExtension.Length == 0)
		{
			fileNameWithoutExtension = "sound";
		}
		if (fileNameWithoutExtension.Length > 60)
		{
			fileNameWithoutExtension = fileNameWithoutExtension.Substring(0, 60);
		}
		HashSet<string> hashSet = new HashSet<string>(existingFileNames, StringComparer.OrdinalIgnoreCase);
		string text2 = fileNameWithoutExtension + text;
		int num = 2;
		while (hashSet.Contains(text2))
		{
			text2 = $"{fileNameWithoutExtension} ({num}){text}";
			num++;
		}
		return text2;
	}

	public static string? FileFor(string mediaFolder, string? soundName)
	{
		if (string.IsNullOrWhiteSpace(soundName))
		{
			return null;
		}
		if (soundName.IndexOfAny(new char[3]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar,
			':'
		}) >= 0)
		{
			return null;
		}
		if (soundName.Contains("..", StringComparison.Ordinal))
		{
			return null;
		}
		return Path.Combine(mediaFolder, soundName + ".wav");
	}
}
