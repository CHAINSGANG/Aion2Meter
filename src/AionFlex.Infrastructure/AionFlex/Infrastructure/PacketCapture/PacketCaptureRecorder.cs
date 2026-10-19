using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AionFlex.Core.Models;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketCapture;

public sealed class PacketCaptureRecorder : IDisposable
{
	public const int Magic = 1093813072;

	public const int FormatVersion = 1;

	public const int HeaderBytes = 8;

	private readonly ILogger<PacketCaptureRecorder> _logger;

	private readonly string _captureDirectory;

	private readonly string _filePrefix;

	private readonly object _gate = new object();

	private readonly List<(long Second, long Offset)> _secondOffsets = new List<(long, long)>();

	private FileStream? _fileStream;

	private BinaryWriter? _writer;

	private long _packetCount;

	private long _totalBytes;

	private long _firstRecordMs;

	private long _lastRecordMs;

	private string? _currentFilePath;

	private volatile bool _recording;

	public bool IsRecording
	{
		get
		{
			lock (_gate)
			{
				return _writer != null;
			}
		}
	}

	public long PacketCount => Interlocked.Read(in _packetCount);

	public long TotalBytes => Interlocked.Read(in _totalBytes);

	public string? CurrentFilePath
	{
		get
		{
			lock (_gate)
			{
				return _currentFilePath;
			}
		}
	}

	public string CaptureDirectory => _captureDirectory;

	public long? FirstRecordMs
	{
		get
		{
			lock (_gate)
			{
				return (_packetCount > 0) ? new long?(_firstRecordMs) : ((long?)null);
			}
		}
	}

	public long? LastRecordMs
	{
		get
		{
			lock (_gate)
			{
				return (_packetCount > 0) ? new long?(_lastRecordMs) : ((long?)null);
			}
		}
	}

	public PacketCaptureRecorder(ILogger<PacketCaptureRecorder> logger, string filePrefix = "capture", string? captureDirectory = null)
	{
		_logger = logger;
		_filePrefix = filePrefix;
		_captureDirectory = captureDirectory ?? DefaultCaptureDirectory();
	}

	public static string DefaultCaptureDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AionFlex", "captures");
	}

	public string StartRecording(string? stamp = null)
	{
		lock (_gate)
		{
			if (_writer != null)
			{
				StopRecordingLocked();
			}
			Directory.CreateDirectory(_captureDirectory);
			if (stamp == null)
			{
				stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
			}
			string text = Path.Combine(_captureDirectory, _filePrefix + "-" + stamp + ".bin");
			if (File.Exists(text))
			{
				text = Path.Combine(_captureDirectory, $"{_filePrefix}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.bin");
			}
			_currentFilePath = text;
			_fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete, 65536);
			_writer = new BinaryWriter(_fileStream);
			_writer.Write(1093813072);
			_writer.Write(1);
			_writer.Flush();
			_recording = true;
			_packetCount = 0L;
			_totalBytes = 0L;
			_firstRecordMs = 0L;
			_lastRecordMs = 0L;
			_secondOffsets.Clear();
			_logger.LogInformation("Packet capture STARTED: {Path}", text);
			return text;
		}
	}

	public void RecordPayload(CapturedPayload payload)
	{
		if (!_recording || payload?.Data == null)
		{
			return;
		}
		lock (_gate)
		{
			if (_writer == null || _fileStream == null)
			{
				return;
			}
			try
			{
				long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				long num2 = num / 1000;
				if (_secondOffsets.Count != 0)
				{
					List<(long Second, long Offset)> secondOffsets = _secondOffsets;
					if (num2 <= secondOffsets[secondOffsets.Count - 1].Second)
					{
						goto IL_009c;
					}
				}
				_secondOffsets.Add((num2, _fileStream.Position));
				goto IL_009c;
				IL_009c:
				_writer.Write(num);
				_writer.Write(payload.SourcePort);
				_writer.Write(payload.DestinationPort);
				_writer.Write(payload.Data.Length);
				_writer.Write(payload.Data);
				if (_packetCount == 0L)
				{
					_firstRecordMs = num;
				}
				_lastRecordMs = num;
				_packetCount++;
				_totalBytes += payload.Data.Length;
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "Error recording payload");
			}
		}
	}

	public bool TryCopyRange(long fromMs, long toMs, string destPath, out long bytesCopied)
	{
		bytesCopied = 0L;
		long start;
		long end;
		string currentFilePath;
		lock (_gate)
		{
			if (_writer == null || _currentFilePath == null || _secondOffsets.Count == 0)
			{
				return false;
			}
			_writer.Flush();
			if (!TryFindRange(_secondOffsets, fromMs, toMs, _fileStream.Position, out start, out end))
			{
				return false;
			}
			currentFilePath = _currentFilePath;
		}
		bytesCopied = CaptureFileSlicer.CopyByteRange(currentFilePath, start, end, destPath);
		return bytesCopied > 0;
	}

	internal static bool TryFindRange(IReadOnlyList<(long Second, long Offset)> secondOffsets, long fromMs, long toMs, long fileEnd, out long start, out long end)
	{
		start = (end = 0L);
		if (toMs < fromMs)
		{
			return false;
		}
		long num = Math.DivRem(fromMs, 1000L, out var result) - ((result < 0) ? 1 : 0);
		long num2 = Math.DivRem(toMs, 1000L, out var result2) - ((result2 < 0) ? 1 : 0);
		int num3 = -1;
		for (int i = 0; i < secondOffsets.Count; i++)
		{
			if (secondOffsets[i].Second >= num)
			{
				num3 = i;
				break;
			}
		}
		if (num3 < 0 || secondOffsets[num3].Second > num2)
		{
			return false;
		}
		start = secondOffsets[num3].Offset;
		end = fileEnd;
		for (int j = num3 + 1; j < secondOffsets.Count; j++)
		{
			if (secondOffsets[j].Second > num2)
			{
				end = secondOffsets[j].Offset;
				break;
			}
		}
		return end > start;
	}

	public string? StopRecording()
	{
		lock (_gate)
		{
			return StopRecordingLocked();
		}
	}

	private string? StopRecordingLocked()
	{
		string currentFilePath = _currentFilePath;
		_recording = false;
		if (_writer != null)
		{
			try
			{
				_writer.Flush();
				_writer.Dispose();
			}
			catch
			{
			}
			_writer = null;
		}
		if (_fileStream != null)
		{
			try
			{
				_fileStream.Dispose();
			}
			catch
			{
			}
			_fileStream = null;
		}
		if (currentFilePath != null)
		{
			double num = (double)_totalBytes / 1024.0;
			_logger.LogInformation("Packet capture STOPPED: {Packets} packets, {Size:F1} KB -> {Path}", _packetCount, num, currentFilePath);
		}
		_currentFilePath = null;
		_secondOffsets.Clear();
		return currentFilePath;
	}

	public void Dispose()
	{
		StopRecording();
	}
}
