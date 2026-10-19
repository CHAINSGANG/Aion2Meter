using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Diagnostics;

public sealed class ProtocolHealthMonitor : IDisposable
{
	private readonly ILogger<ProtocolHealthMonitor> _logger;

	private readonly ConcurrentDictionary<ushort, long> _window = new ConcurrentDictionary<ushort, long>();

	private readonly ConcurrentDictionary<string, long> _featureWindow = new ConcurrentDictionary<string, long>();

	private readonly Timer _timer;

	private readonly HashSet<ushort> _firedError = new HashSet<ushort>();

	private readonly HashSet<string> _firedFeatureError = new HashSet<string>();

	private readonly object _evalLock = new object();

	private readonly ConcurrentDictionary<string, long> _decodeOkWindow = new ConcurrentDictionary<string, long>();

	private readonly ConcurrentDictionary<string, long> _decodeMissWindow = new ConcurrentDictionary<string, long>();

	private readonly List<DecodeMissSample> _decodeMissSamples = new List<DecodeMissSample>();

	private readonly object _sampleLock = new object();

	private const int MaxSamplesPerWindow = 5;

	private const int MaxSampleBytes = 64;

	private const int MaxSeenSignatures = 2000;

	private readonly HashSet<string> _seenSignatures = new HashSet<string>();

	private readonly Queue<string> _seenOrder = new Queue<string>();

	private readonly string? _seenFilePath;

	private readonly object _seenLock = new object();

	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(120.0);

	private static readonly (ushort[] Watch, ushort Trigger, int MinTrigger, int ConsecutiveWindows, string Feature)[] Rules = new (ushort[], ushort, int, int, string)[3]
	{
		(new ushort[1] { 141 }, 1080, 50, 2, "entity/boss HP bar + wipe/death detection"),
		(new ushort[2] { 18742, 18998 }, 1080, 50, 3, "character stat deltas → snapshot/profile uploads"),
		(new ushort[3] { 16438, 16694, 16950 }, 1080, 200, 3, "NPC/boss identification + instance detection")
	};

	private readonly Dictionary<ushort, int> _consecutiveSilent = new Dictionary<ushort, int>();

	private static readonly (string Watch, string Trigger, int MinTrigger, string Feature)[] FeatureRules = new (string, string, int, string)[1] { ("summon_owner", "summon_spawn_observed", 10, "summon → owner attribution (player-summon spawns seen but zero owners extracted)") };

	public event EventHandler<ProtocolHealthSnapshot>? SnapshotReady;

	public ProtocolHealthMonitor(ILogger<ProtocolHealthMonitor> logger, string? seenSignatureFilePath = null)
	{
		_logger = logger;
		if (seenSignatureFilePath == null)
		{
			try
			{
				string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				_seenFilePath = Path.Combine(folderPath, "AionFlex", "decode-miss-seen.txt");
			}
			catch
			{
				_seenFilePath = null;
			}
		}
		else
		{
			_seenFilePath = (string.IsNullOrEmpty(seenSignatureFilePath) ? null : seenSignatureFilePath);
		}
		LoadSeenSignatures();
		_timer = new Timer((object? _) =>
		{
			Evaluate();
		}, null, Interval, Interval);
	}

	public void Record(ushort opcode)
	{
		_window.AddOrUpdate(opcode, 1L, (ushort _, long v) => v + 1);
	}

	public void RecordFeature(string feature)
	{
		_featureWindow.AddOrUpdate(feature, 1L, (string _, long v) => v + 1);
	}

	public void RecordDecodeOk(string feature)
	{
		_decodeOkWindow.AddOrUpdate(feature, 1L, (string _, long v) => v + 1);
	}

	public void RecordDecodeMiss(string feature, ReadOnlySpan<byte> sample)
	{
		_decodeMissWindow.AddOrUpdate(feature, 1L, (string _, long v) => v + 1);
		try
		{
			ReadOnlySpan<byte> bytes = sample[..Math.Min(sample.Length, 64)];
			string text = ComputeSignature(feature, sample);
			lock (_seenLock)
			{
				if (!_seenSignatures.Add(text))
				{
					return;
				}
				_seenOrder.Enqueue(text);
				while (_seenOrder.Count > 2000)
				{
					_seenSignatures.Remove(_seenOrder.Dequeue());
				}
			}
			lock (_sampleLock)
			{
				if (_decodeMissSamples.Count >= 5)
				{
					return;
				}
				_decodeMissSamples.Add(new DecodeMissSample(feature, text, ToHex(bytes)));
			}
			AppendSeenSignature(text);
		}
		catch
		{
		}
	}

	internal static string ComputeSignature(string feature, ReadOnlySpan<byte> sample)
	{
		ulong num = 14695981039346656037uL;
		byte[] bytes = Encoding.UTF8.GetBytes(feature);
		foreach (byte b in bytes)
		{
			num = (num ^ b) * 1099511628211L;
		}
		byte b2 = (byte)Math.Min(sample.Length, 255);
		num = (num ^ b2) * 1099511628211L;
		int num2 = Math.Min(8, sample.Length);
		for (int j = 0; j < num2; j++)
		{
			num = (num ^ sample[j]) * 1099511628211L;
		}
		return num.ToString("x16");
	}

	private static string ToHex(ReadOnlySpan<byte> bytes)
	{
		StringBuilder stringBuilder = new StringBuilder(bytes.Length * 2);
		ReadOnlySpan<byte> readOnlySpan = bytes;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			byte b = readOnlySpan[i];
			stringBuilder.Append(b.ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	private void LoadSeenSignatures()
	{
		if (_seenFilePath == null)
		{
			return;
		}
		try
		{
			if (!File.Exists(_seenFilePath))
			{
				return;
			}
			string[] array = File.ReadAllLines(_seenFilePath);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length != 0 && _seenSignatures.Add(text))
				{
					_seenOrder.Enqueue(text);
					while (_seenOrder.Count > 2000)
					{
						_seenSignatures.Remove(_seenOrder.Dequeue());
					}
				}
			}
		}
		catch
		{
		}
	}

	private void AppendSeenSignature(string signature)
	{
		if (_seenFilePath == null)
		{
			return;
		}
		try
		{
			string directoryName = Path.GetDirectoryName(_seenFilePath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.AppendAllText(_seenFilePath, signature + Environment.NewLine);
		}
		catch
		{
		}
	}

	private void Evaluate()
	{
		KeyValuePair<ushort, long>[] array = _window.ToArray();
		_window.Clear();
		KeyValuePair<string, long>[] source = _featureWindow.ToArray();
		_featureWindow.Clear();
		KeyValuePair<string, long>[] source2 = _decodeOkWindow.ToArray();
		_decodeOkWindow.Clear();
		KeyValuePair<string, long>[] source3 = _decodeMissWindow.ToArray();
		_decodeMissWindow.Clear();
		List<DecodeMissSample> decodeMissSamples;
		lock (_sampleLock)
		{
			decodeMissSamples = new List<DecodeMissSample>(_decodeMissSamples);
			_decodeMissSamples.Clear();
		}
		if (array.Length == 0)
		{
			return;
		}
		List<(string, string)> list = new List<(string, string)>();
		lock (_evalLock)
		{
			Dictionary<ushort, long> counts = array.ToDictionary((KeyValuePair<ushort, long> kv) => kv.Key, (KeyValuePair<ushort, long> kv) => kv.Value);
			Dictionary<string, long> featureCounts = source.ToDictionary((KeyValuePair<string, long> kv) => kv.Key, (KeyValuePair<string, long> kv) => kv.Value);
			string text = string.Join(" ", from kv in array
				orderby kv.Key
				select $"{Hex(kv.Key)}={kv.Value}");
			_logger.LogInformation("\ud83e\ude7a PROTOCOL CENSUS [{Sec}s]: {Census}", (int)Interval.TotalSeconds, text);
			(ushort[], ushort, int, int, string)[] rules = Rules;
			for (int num = 0; num < rules.Length; num++)
			{
				(ushort[], ushort, int, int, string) tuple = rules[num];
				ushort[] item = tuple.Item1;
				ushort item2 = tuple.Item2;
				int item3 = tuple.Item3;
				int item4 = tuple.Item4;
				string item5 = tuple.Item5;
				ushort num2 = item[0];
				string text2 = string.Join(" / ", item.Select(Hex));
				bool flag = item.All((ushort w) => Get(w) == 0);
				if ((Get(item2) >= item3) & flag)
				{
					int num3 = _consecutiveSilent.GetValueOrDefault(num2) + 1;
					_consecutiveSilent[num2] = num3;
					if (num3 >= item4)
					{
						list.Add((string.Concat(item.Select((ushort w) => Hex(w).Replace(" ", ""))), item5));
						if (_firedError.Add(num2))
						{
							_logger.LogError("⚠\ufe0f PROTOCOL BREAK SUSPECTED: opcode family {Watch} ALL SILENT for {Windows} consecutive windows while {Trigger} flowing ({TriggerN} pkts/{Sec}s) — feature DOWN: {Feature}. Likely a game-patch opcode shift to a value outside the family; capture a fresh fight and follow the opcode-shift runbook in CLAUDE.md.", text2, num3, Hex(item2), Get(item2), (int)Interval.TotalSeconds, item5);
						}
					}
				}
				else if (item.Any((ushort w) => Get(w) > 0))
				{
					_consecutiveSilent[num2] = 0;
					_firedError.Remove(num2);
				}
			}
			(string, string, int, string)[] featureRules = FeatureRules;
			for (int num = 0; num < featureRules.Length; num++)
			{
				var (text3, k, num4, text4) = featureRules[num];
				if (GetF(k) >= num4 && GetF(text3) == 0L)
				{
					list.Add((text3, text4));
					if (_firedFeatureError.Add(text3))
					{
						_logger.LogError("⚠\ufe0f SUMMON ATTRIBUTION BREAK SUSPECTED: {TriggerN} player-summon spawns seen in {Sec}s but ZERO owners extracted — feature DOWN: {Feature}. The summoner owner block (8×FF + 07 02 06) likely changed; capture a summon fight and follow the opcode-shift runbook in CLAUDE.md.", GetF(k), (int)Interval.TotalSeconds, text4);
					}
				}
				else if (GetF(text3) > 0)
				{
					_firedFeatureError.Remove(text3);
				}
			}
			long Get(ushort key)
			{
				if (!counts.TryGetValue(key, out var value))
				{
					return 0L;
				}
				return value;
			}
			long GetF(string key)
			{
				if (!featureCounts.TryGetValue(key, out var value))
				{
					return 0L;
				}
				return value;
			}
		}
		EventHandler<ProtocolHealthSnapshot> eventHandler = SnapshotReady;
		if (eventHandler == null)
		{
			return;
		}
		try
		{
			Dictionary<string, long> opcodes = array.ToDictionary((KeyValuePair<ushort, long> kv) => $"{kv.Key:X4}", (KeyValuePair<ushort, long> kv) => kv.Value);
			Dictionary<string, long> features = source.ToDictionary((KeyValuePair<string, long> kv) => kv.Key, (KeyValuePair<string, long> kv) => kv.Value);
			Dictionary<string, long> decodeOk = source2.ToDictionary((KeyValuePair<string, long> kv) => kv.Key, (KeyValuePair<string, long> kv) => kv.Value);
			Dictionary<string, long> decodeMiss = source3.ToDictionary((KeyValuePair<string, long> kv) => kv.Key, (KeyValuePair<string, long> kv) => kv.Value);
			eventHandler(this, new ProtocolHealthSnapshot(opcodes, features, list, (int)Interval.TotalSeconds, decodeOk, decodeMiss, decodeMissSamples));
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "ProtocolHealthMonitor: SnapshotReady subscriber threw");
		}
	}

	private static string Hex(ushort op)
	{
		return $"{op >> 8:X2} {op & 0xFF:X2}";
	}

	public void Dispose()
	{
		_timer.Dispose();
	}
}
