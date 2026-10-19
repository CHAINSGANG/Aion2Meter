using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Infrastructure.PacketParsing;

internal sealed class FeatureStreamReassembler
{
	private sealed class StreamRollingBuffer
	{
		private readonly int _cap;

		private byte[] _buf;

		private int _len;

		public long LastTouchTicks { get; private set; }

		public StreamRollingBuffer(int cap)
		{
			_cap = cap;
			_buf = new byte[Math.Min(8192, cap)];
			_len = 0;
			LastTouchTicks = Environment.TickCount64;
		}

		public byte[] AppendAndSnapshot(byte[] data)
		{
			LastTouchTicks = Environment.TickCount64;
			int num = data.Length;
			if (num >= _cap)
			{
				EnsureCapacity(_cap);
				Buffer.BlockCopy(data, num - _cap, _buf, 0, _cap);
				_len = _cap;
				return Snapshot();
			}
			int num2 = _len + num;
			if (num2 > _cap)
			{
				int num3 = num2 - _cap;
				int num4 = _len - num3;
				if (num4 > 0)
				{
					Buffer.BlockCopy(_buf, num3, _buf, 0, num4);
				}
				_len = Math.Max(0, num4);
			}
			EnsureCapacity(_len + num);
			Buffer.BlockCopy(data, 0, _buf, _len, num);
			_len += num;
			return Snapshot();
		}

		private void EnsureCapacity(int needed)
		{
			if (_buf.Length < needed)
			{
				byte[] array = new byte[Math.Min(_cap, Math.Max(_buf.Length * 2, needed))];
				Buffer.BlockCopy(_buf, 0, array, 0, _len);
				_buf = array;
			}
		}

		private byte[] Snapshot()
		{
			byte[] array = new byte[_len];
			Buffer.BlockCopy(_buf, 0, array, 0, _len);
			return array;
		}
	}

	public const int DefaultMaxBufferBytes = 1048576;

	public const int MaxFFFFBundleBytes = 1048576;

	public const int DefaultMaxTrackedStreams = 64;

	private readonly int _maxBufferBytes;

	private readonly int _maxTrackedStreams;

	private readonly Dictionary<string, StreamRollingBuffer> _buffers = new Dictionary<string, StreamRollingBuffer>();

	private readonly object _lock = new object();

	public int TrackedStreamCount
	{
		get
		{
			lock (_lock)
			{
				return _buffers.Count;
			}
		}
	}

	public FeatureStreamReassembler(int maxBufferBytes = 1048576, int maxTrackedStreams = 64)
	{
		_maxBufferBytes = maxBufferBytes;
		_maxTrackedStreams = maxTrackedStreams;
	}

	public CapturedPayload Accumulate(CapturedPayload payload)
	{
		byte[] data = payload.Data;
		if (data == null || data.Length == 0)
		{
			return payload;
		}
		string key = $"{payload.SourcePort}→{payload.DestinationPort}";
		byte[] data2;
		lock (_lock)
		{
			if (!_buffers.TryGetValue(key, out StreamRollingBuffer value))
			{
				PruneIfNeeded();
				value = new StreamRollingBuffer(_maxBufferBytes);
				_buffers[key] = value;
			}
			data2 = value.AppendAndSnapshot(data);
		}
		return new CapturedPayload(payload.SourcePort, payload.DestinationPort, data2, payload.DeviceName);
	}

	public void Reset()
	{
		lock (_lock)
		{
			_buffers.Clear();
		}
	}

	private void PruneIfNeeded()
	{
		if (_buffers.Count < _maxTrackedStreams)
		{
			return;
		}
		string text = null;
		long num = long.MaxValue;
		foreach (KeyValuePair<string, StreamRollingBuffer> buffer in _buffers)
		{
			if (buffer.Value.LastTouchTicks < num)
			{
				num = buffer.Value.LastTouchTicks;
				text = buffer.Key;
			}
		}
		if (text != null)
		{
			_buffers.Remove(text);
		}
	}
}
