using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AionFlex.Core.Capture;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketCapture;

public sealed class LiveCaptureRunWriter : ICaptureRunSink, IDisposable
{
	private const long LogMarkRetentionMs = 86400000L;

	private readonly PacketCaptureRecorder _sessionRecorder;

	private readonly Func<string?> _currentLogFile;

	private readonly ILogger _logger;

	private readonly string? _appVersion;

	private readonly object _logGate = new object();

	private readonly List<(long Ms, long Length)> _logMarks = new List<(long, long)>();

	private readonly List<Task> _pending = new List<Task>();

	private readonly object _writeGate = new object();

	private readonly Timer _sampler;

	private string? _markedLog;

	public string RunsDirectory { get; }

	public event EventHandler<CaptureRunWrittenEventArgs>? RunWritten;

	public LiveCaptureRunWriter(PacketCaptureRecorder sessionRecorder, Func<string?> currentLogFile, string runsDirectory, ILogger<LiveCaptureRunWriter> logger, string? appVersion)
	{
		_sessionRecorder = sessionRecorder;
		_currentLogFile = currentLogFile;
		RunsDirectory = runsDirectory;
		_logger = logger;
		_appVersion = appVersion;
		_sampler = new Timer((object? _) =>
		{
			SampleLogLength();
		}, null, 0, 1000);
	}

	public void WriteRun(CaptureRunIndex index, long fromMs, long toMs)
	{
		Task item = Task.Run(() =>
		{
			lock (_writeGate)
			{
				WriteRunCore(index, fromMs, toMs);
			}
		});
		lock (_pending)
		{
			_pending.RemoveAll((Task t) => t.IsCompleted);
			_pending.Add(item);
		}
	}

	public void Flush(TimeSpan timeout)
	{
		Task[] tasks;
		lock (_pending)
		{
			tasks = _pending.ToArray();
		}
		try
		{
			Task.WaitAll(tasks, timeout);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Capture run writes did not finish cleanly");
		}
	}

	private void WriteRunCore(CaptureRunIndex index, long fromMs, long toMs)
	{
		try
		{
			Directory.CreateDirectory(RunsDirectory);
			long? firstRecordMs = _sessionRecorder.FirstRecordMs;
			if (firstRecordMs.HasValue)
			{
				long valueOrDefault = firstRecordMs.GetValueOrDefault();
				if (fromMs < valueOrDefault)
				{
					fromMs = valueOrDefault;
				}
			}
			firstRecordMs = _sessionRecorder.LastRecordMs;
			if (firstRecordMs.HasValue)
			{
				long valueOrDefault2 = firstRecordMs.GetValueOrDefault();
				if (toMs > valueOrDefault2)
				{
					toMs = valueOrDefault2;
				}
			}
			string text = UniqueBaseName(RunsDirectory, index.Name);
			string destPath = Path.Combine(RunsDirectory, text + ".bin");
			if (!_sessionRecorder.TryCopyRange(fromMs, toMs, destPath, out var bytesCopied))
			{
				_logger.LogWarning("\ud83c\udfac Capture run {Name}: no packets recorded in its window — not written", index.Name);
				return;
			}
			string logFile = null;
			string text2 = _currentLogFile();
			if (text2 != null && TryGetLogRange(text2, fromMs, toMs, out var start, out var end))
			{
				string destPath2 = Path.Combine(RunsDirectory, text + ".log");
				try
				{
					if (CaptureFileSlicer.CopyLogRange(text2, start, end, destPath2) > 0)
					{
						logFile = text + ".log";
					}
				}
				catch (Exception exception)
				{
					_logger.LogWarning(exception, "\ud83c\udfac Capture run {Name}: log slice failed", text);
				}
			}
			index.Name = text;
			index.CaptureFile = text + ".bin";
			index.LogFile = logFile;
			index.SourceSessionCapture = Path.GetFileName(_sessionRecorder.CurrentFilePath);
			index.SourceSessionLog = ((text2 != null) ? Path.GetFileName(text2) : null);
			if (index.AppVersion == null)
			{
				string text3 = (index.AppVersion = _appVersion);
			}
			index.FileStartUtc = DateTimeOffset.FromUnixTimeMilliseconds(fromMs).UtcDateTime;
			index.FileEndUtc = DateTimeOffset.FromUnixTimeMilliseconds(toMs).UtcDateTime;
			string text4 = Path.Combine(RunsDirectory, text + ".json");
			CaptureRunJson.Write(text4, index);
			_logger.LogInformation("\ud83c\udfac Capture run saved: {Name} ({Mb:F1} MB, {Bosses} boss(es), {Saved} saved session(s))", text, (double)bytesCopied / 1048576.0, index.Bosses.Count, index.SavedSessions.Count);
			RunWritten?.Invoke(this, new CaptureRunWrittenEventArgs(index, text4));
		}
		catch (Exception exception2)
		{
			_logger.LogWarning(exception2, "\ud83c\udfac Capture run {Name} could not be written", index.Name);
		}
	}

	public static string UniqueBaseName(string directory, string baseName)
	{
		string text = baseName;
		int num = 2;
		while (Exists(text))
		{
			text = $"{baseName}_{num}";
			num++;
		}
		return text;
		bool Exists(string name)
		{
			if (!File.Exists(Path.Combine(directory, name + ".bin")))
			{
				return File.Exists(Path.Combine(directory, name + ".json"));
			}
			return true;
		}
	}

	private void SampleLogLength()
	{
		try
		{
			string text = _currentLogFile();
			if (text == null)
			{
				return;
			}
			long length;
			try
			{
				length = new FileInfo(text).Length;
			}
			catch
			{
				return;
			}
			long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			lock (_logGate)
			{
				if (!string.Equals(text, _markedLog, StringComparison.OrdinalIgnoreCase))
				{
					_logMarks.Clear();
					_markedLog = text;
				}
				_logMarks.Add((num, length));
				long num2 = num - 86400000;
				int i;
				for (i = 0; i < _logMarks.Count && _logMarks[i].Ms < num2; i++)
				{
				}
				if (i > 0)
				{
					_logMarks.RemoveRange(0, i);
				}
			}
		}
		catch
		{
		}
	}

	private bool TryGetLogRange(string logPath, long fromMs, long toMs, out long start, out long end)
	{
		lock (_logGate)
		{
			start = (end = 0L);
			if (!string.Equals(logPath, _markedLog, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return TryFindLogRange(_logMarks, fromMs, toMs, out start, out end);
		}
	}

	internal static bool TryFindLogRange(IReadOnlyList<(long Ms, long Length)> marks, long fromMs, long toMs, out long start, out long end)
	{
		start = (end = 0L);
		if (marks.Count == 0 || toMs < fromMs)
		{
			return false;
		}
		for (int i = 0; i < marks.Count && marks[i].Ms <= fromMs; i++)
		{
			start = marks[i].Length;
		}
		end = marks[marks.Count - 1].Length;
		for (int j = 0; j < marks.Count; j++)
		{
			if (marks[j].Ms >= toMs)
			{
				end = marks[j].Length;
				break;
			}
		}
		return end > start;
	}

	public void Dispose()
	{
		_sampler.Dispose();
	}
}
