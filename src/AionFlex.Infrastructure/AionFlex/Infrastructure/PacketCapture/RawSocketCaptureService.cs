using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketCapture;

[SupportedOSPlatform("windows")]
public sealed class RawSocketCaptureService : IPacketCaptureService, IDisposable
{
	private readonly ILogger<RawSocketCaptureService> _logger;

	private readonly object _lock = new object();

	private readonly List<Socket> _sockets = new List<Socket>();

	private CancellationTokenSource? _cts;

	private bool _isRunning;

	private bool _disposed;

	private readonly ConcurrentDictionary<(ushort SrcPort, ushort DstPort), uint> _nextExpectedSeq = new ConcurrentDictionary<(ushort, ushort), uint>();

	private long _tcpRetransmissions;

	private long _packetsReceived;

	private long _tcpGaps;

	private long _tcpGapBytes;

	private long _queueDropped;

	private readonly Channel<CapturedPayload> _payloadChannel = Channel.CreateBounded<CapturedPayload>(new BoundedChannelOptions(8192)
	{
		FullMode = BoundedChannelFullMode.DropOldest,
		SingleWriter = false,
		SingleReader = true
	});

	public bool IsRunning => _isRunning;

	public long TcpRetransmissions => Interlocked.Read(in _tcpRetransmissions);

	public long TcpGaps => Interlocked.Read(in _tcpGaps);

	public long TcpGapBytes => Interlocked.Read(in _tcpGapBytes);

	public event EventHandler<CapturedPayload>? PayloadCaptured;

	public RawSocketCaptureService(ILogger<RawSocketCaptureService> logger)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
	}

	public void Start()
	{
		lock (_lock)
		{
			if (_isRunning)
			{
				_logger.LogWarning("Raw socket capture is already running");
				return;
			}
			_cts = new CancellationTokenSource();
			DisableReceiveSideCoalescing();
			if (TryStartOnAddress(IPAddress.Loopback))
			{
				_logger.LogInformation("Raw socket capture started on loopback (127.0.0.1)");
			}
			foreach (IPAddress physicalAddress in GetPhysicalAddresses())
			{
				if (TryStartOnAddress(physicalAddress))
				{
					_logger.LogInformation("Raw socket capture started on {Address}", physicalAddress);
				}
			}
			if (_sockets.Count == 0)
			{
				_logger.LogError("Failed to start raw socket capture on any interface");
				throw new InvalidOperationException("Cannot start raw socket capture. Ensure you are running as Administrator.");
			}
			_isRunning = true;
			Task.Factory.StartNew(() => ConsumerLoop(_cts.Token), _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			_logger.LogInformation("Raw socket capture active on {Count} interface(s)", _sockets.Count);
		}
	}

	public void Stop()
	{
		lock (_lock)
		{
			if (!_isRunning)
			{
				return;
			}
			_logger.LogInformation("Stopping raw socket capture (received={Received}, retransmissions={Retransmissions}, gaps={Gaps}, gapBytes={GapBytes}, queueDropped={QueueDropped})", Interlocked.Read(in _packetsReceived), TcpRetransmissions, TcpGaps, TcpGapBytes, Interlocked.Read(in _queueDropped));
			_cts?.Cancel();
			foreach (Socket socket in _sockets)
			{
				try
				{
					socket.Close();
					socket.Dispose();
				}
				catch (Exception exception)
				{
					_logger.LogTrace(exception, "Error closing raw socket");
				}
			}
			_sockets.Clear();
			_isRunning = false;
		}
	}

	private bool TryStartOnAddress(IPAddress address)
	{
		try
		{
			Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
			socket.Bind(new IPEndPoint(address, 0));
			socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), null);
			socket.ReceiveBufferSize = 268435456;
			lock (_lock)
			{
				_sockets.Add(socket);
			}
			CancellationToken ct = _cts.Token;
			string label = address.ToString();
			Thread thread = new Thread(() =>
			{
				ReceiveLoopSync(socket, label, ct);
			});
			thread.Name = "RawSocket-" + label;
			thread.IsBackground = true;
			thread.Priority = ThreadPriority.AboveNormal;
			thread.Start();
			return true;
		}
		catch (SocketException ex)
		{
			_logger.LogDebug("Cannot bind raw socket to {Address}: {Error} (code {Code})", address, ex.Message, ex.SocketErrorCode);
			return false;
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Cannot start raw socket on {Address}", address);
			return false;
		}
	}

	private void ReceiveLoopSync(Socket socket, string label, CancellationToken ct)
	{
		byte[] array = new byte[65535];
		socket.ReceiveTimeout = 500;
		_logger.LogDebug("Receive loop (sync) started on {Label}", label);
		try
		{
			while (!ct.IsCancellationRequested)
			{
				int num;
				try
				{
					num = socket.Receive(array, 0, array.Length, SocketFlags.None);
				}
				catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
				{
					continue;
				}
				catch (SocketException ex2) when (ex2.SocketErrorCode == SocketError.OperationAborted || ex2.SocketErrorCode == SocketError.Interrupted)
				{
					break;
				}
				if (num > 0)
				{
					ProcessIpPacket(array, num, label);
				}
			}
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception exception)
		{
			if (!ct.IsCancellationRequested)
			{
				_logger.LogError(exception, "Error in receive loop on {Label}", label);
			}
		}
		_logger.LogDebug("Receive loop ended on {Label}", label);
	}

	private async Task ConsumerLoop(CancellationToken ct)
	{
		_logger.LogDebug("Payload consumer started");
		try
		{
			await foreach (CapturedPayload item in _payloadChannel.Reader.ReadAllAsync(ct))
			{
				try
				{
					PayloadCaptured?.Invoke(this, item);
				}
				catch (Exception exception)
				{
					_logger.LogError(exception, "Error processing payload");
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception2)
		{
			if (!ct.IsCancellationRequested)
			{
				_logger.LogError(exception2, "Error in consumer loop");
			}
		}
		long num = Interlocked.Read(in _queueDropped);
		if (num > 0)
		{
			_logger.LogWarning("Payload consumer stopped. Queue dropped {Dropped} payloads", num);
		}
		else
		{
			_logger.LogDebug("Payload consumer stopped");
		}
	}

	private void ProcessIpPacket(byte[] data, int length, string label)
	{
		if (length < 20)
		{
			return;
		}
		int num = (data[0] & 0xF) * 4;
		int num2 = (data[2] << 8) | data[3];
		if (data[9] == 6 && num >= 20 && num <= length)
		{
			int num3 = Math.Min(num2 - num, length - num);
			if (num3 >= 20)
			{
				ProcessTcpSegment(data, num, num3, label);
			}
		}
	}

	private void ProcessTcpSegment(byte[] data, int offset, int length, string label)
	{
		ushort num = (ushort)((data[offset] << 8) | data[offset + 1]);
		ushort num2 = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
		uint num3 = (uint)((data[offset + 4] << 24) | (data[offset + 5] << 16) | (data[offset + 6] << 8) | data[offset + 7]);
		int num4 = ((data[offset + 12] >> 4) & 0xF) * 4;
		int num5 = offset + num4;
		int num6 = length - num4;
		if (num6 <= 0 || num == 443 || num2 == 443)
		{
			return;
		}
		if (num6 >= 2)
		{
			byte b = data[num5];
			byte b2 = data[num5 + 1];
			if ((b == 22 || b == 23) && b2 == 3)
			{
				return;
			}
		}
		Interlocked.Increment(ref _packetsReceived);
		(ushort, ushort) key = (num, num2);
		uint num7 = num3 + (uint)num6;
		while (true)
		{
			if (!_nextExpectedSeq.TryGetValue(key, out var value))
			{
				if (_nextExpectedSeq.TryAdd(key, num7))
				{
					break;
				}
				continue;
			}
			int num8 = (int)(num3 - value);
			if (num8 < 0)
			{
				if ((int)(num7 - value) <= 0)
				{
					Interlocked.Increment(ref _tcpRetransmissions);
					return;
				}
			}
			else if (num8 > 0)
			{
				Interlocked.Increment(ref _tcpGaps);
				Interlocked.Add(ref _tcpGapBytes, num8);
				_logger.LogWarning("⚠\ufe0f TCP GAP: stream={Src}→{Dst}, missed {GapBytes} bytes (seq={SeqNum}, expected={Expected})", num, num2, num8, num3, value);
			}
			if (_nextExpectedSeq.TryUpdate(key, num7, value))
			{
				break;
			}
		}
		byte[] array = new byte[num6];
		Buffer.BlockCopy(data, num5, array, 0, num6);
		if (!_payloadChannel.Writer.TryWrite(new CapturedPayload(num, num2, array, label)))
		{
			Interlocked.Increment(ref _queueDropped);
		}
	}

	private List<IPAddress> GetPhysicalAddresses()
	{
		List<IPAddress> list = new List<IPAddress>();
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface networkInterface in allNetworkInterfaces)
			{
				if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
				{
					continue;
				}
				foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
				{
					if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						list.Add(unicastAddress.Address);
					}
				}
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Error enumerating network interfaces");
		}
		return list;
	}

	private void DisableReceiveSideCoalescing()
	{
		try
		{
			string fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = "-NoProfile -Command \"Get-NetAdapterRsc | Where-Object { $_.IPv4Enabled -or $_.IPv6Enabled } | ForEach-Object { Disable-NetAdapterRsc -Name $_.Name -IPv4 -IPv6 -ErrorAction SilentlyContinue; $_.Name }\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (process == null)
			{
				return;
			}
			string text = process.StandardOutput.ReadToEnd();
			process.WaitForExit(5000);
			if (!string.IsNullOrWhiteSpace(text))
			{
				string[] array = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
				_logger.LogInformation("RSC disabled on {Count} adapter(s): {Adapters}", array.Length, string.Join(", ", array.Select((string a) => a.Trim())));
			}
			else
			{
				_logger.LogDebug("RSC already disabled on all adapters (or no adapters support it)");
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Could not disable RSC (non-critical)");
		}
	}

	public (long Received, long Dropped) GetCaptureStatistics()
	{
		return (Received: Interlocked.Read(in _packetsReceived), Dropped: 0L);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Stop();
			_cts?.Dispose();
			_disposed = true;
		}
	}
}
