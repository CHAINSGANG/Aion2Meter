using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace AionFlex.Infrastructure.PacketCapture;

[SupportedOSPlatform("windows")]
public sealed class NpcapCaptureService : IPacketCaptureService, IDisposable
{
	private readonly ILogger<NpcapCaptureService> _logger;

	private readonly object _lock = new object();

	private readonly List<ILiveDevice> _activeDevices = new List<ILiveDevice>();

	private CancellationTokenSource? _cts;

	private volatile bool _isRunning;

	private bool _disposed;

	private readonly ConcurrentDictionary<(IPAddress Src, ushort SrcPort, IPAddress Dst, ushort DstPort), uint> _nextExpectedSeq = new();

	private long _tcpRetransmissions;

	private long _packetsReceived;

	private long _tcpGaps;

	private long _tcpGapBytes;

	private long _queueDropped;

	private Timer? _watchdogTimer;

	private long _payloadsAtStart;

	private int _watchdogErrorsLogged;

	private DateTime _captureStartedUtc;

	private string _captureMode = "none";

	private Channel<CapturedPayload>? _payloadChannel;

	private readonly ConcurrentDictionary<(IPAddress Src, ushort SrcPort, IPAddress Dst, ushort DstPort), bool> _clientToServerPairs = new();

	public bool IsRunning => _isRunning;

	public long TcpRetransmissions => Interlocked.Read(in _tcpRetransmissions);

	public long TcpGaps => Interlocked.Read(in _tcpGaps);

	public long TcpGapBytes => Interlocked.Read(in _tcpGapBytes);

	public long QueueDropped => Interlocked.Read(in _queueDropped);

	public int ActiveDeviceCount
	{
		get
		{
			lock (_lock)
			{
				return _activeDevices.Count;
			}
		}
	}

	public string CaptureMode => _captureMode;

	public event EventHandler<CapturedPayload>? PayloadCaptured;

	public NpcapCaptureService(ILogger<NpcapCaptureService> logger)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
	}

	public void Start()
	{
		lock (_lock)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (_isRunning)
			{
				return;
			}
			_nextExpectedSeq.Clear();
			_clientToServerPairs.Clear();
			_cts = new CancellationTokenSource();
			_payloadChannel = Channel.CreateBounded<CapturedPayload>(new BoundedChannelOptions(16384)
			{
				SingleWriter = false,
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			});
			List<LibPcapLiveDevice> captureDevices = GetCaptureDevices();
			if (captureDevices.Count == 0)
			{
				_logger.LogError("No Npcap capture devices found. Is Npcap installed?");
				CleanupFailedStart();
				throw new InvalidOperationException("No Npcap capture devices found. Please install Npcap.");
			}
			foreach (LibPcapLiveDevice item in captureDevices)
			{
				StartCaptureOnDevice(item);
			}
			if (_activeDevices.Count == 0)
			{
				_logger.LogError("\ud83d\udea8 Npcap capture failed to open ANY device ({Found} found, all opens failed) — the meter cannot see game traffic. Try reinstalling Npcap.", captureDevices.Count);
				CleanupFailedStart();
				throw new InvalidOperationException("Npcap не открыл ни одного сетевого адаптера. Запустите DPS Meter от имени администратора.");
			}
			_isRunning = true;
			CancellationToken token = _cts.Token;
			ChannelReader<CapturedPayload> reader = _payloadChannel.Reader;
			_ = Task.Factory.StartNew(
				() => ConsumerLoop(reader, token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
			_payloadsAtStart = Interlocked.Read(in _packetsReceived);
			_watchdogErrorsLogged = 0;
			_captureStartedUtc = DateTime.UtcNow;
			_watchdogTimer = new Timer(WatchdogCheck, null, TimeSpan.FromSeconds(180.0), TimeSpan.FromSeconds(600.0));
			_logger.LogInformation("Npcap capture active on {Count} device(s)", _activeDevices.Count);
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
			_logger.LogInformation("Stopping Npcap capture (received={Received}, retransmissions={Retransmissions}, gaps={Gaps}, gapBytes={GapBytes}, queueDropped={QueueDropped})", Interlocked.Read(in _packetsReceived), TcpRetransmissions, TcpGaps, TcpGapBytes, Interlocked.Read(in _queueDropped));
			_watchdogTimer?.Dispose();
			_watchdogTimer = null;
			_isRunning = false;
			_cts?.Cancel();
			_payloadChannel?.Writer.TryComplete();
			foreach (ILiveDevice activeDevice in _activeDevices)
			{
				CloseDevice(activeDevice);
			}
			_activeDevices.Clear();
			_payloadChannel = null;
			_cts?.Dispose();
			_cts = null;
		}
	}

	private void CleanupFailedStart()
	{
		_payloadChannel?.Writer.TryComplete();
		_payloadChannel = null;
		_cts?.Dispose();
		_cts = null;
	}

	private void CloseDevice(ILiveDevice device)
	{
		try
		{
			device.StopCapture();
		}
		catch (Exception exception)
		{
			_logger.LogTrace(exception, "Error stopping capture device {Device}", device.Name);
		}
		try
		{
			device.Close();
		}
		catch (Exception exception)
		{
			_logger.LogTrace(exception, "Error closing capture device {Device}", device.Name);
		}
	}

	private List<LibPcapLiveDevice> GetCaptureDevices()
	{
		try
		{
			List<LibPcapLiveDevice> list = LibPcapLiveDeviceList.Instance.ToList();
			_logger.LogInformation("Found {Count} Npcap devices", list.Count);
			foreach (LibPcapLiveDevice item in list)
			{
				_logger.LogInformation("  Device: {Name} — {Desc}, Loopback={Loopback}", item.Name, item.Description ?? "(no description)", item.Loopback);
			}
			LibPcapLiveDevice libPcapLiveDevice = list.FirstOrDefault((LibPcapLiveDevice d) => d.Loopback);
			_captureMode = ((libPcapLiveDevice != null) ? "loopback+physical" : "all-physical");
			_logger.LogInformation((libPcapLiveDevice != null) ? "Capturing on loopback AND physical devices (proxy/VPN users ride loopback; direct connections ride the NIC)" : "No loopback capture device — capturing on all physical devices");
			return list;
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Failed to enumerate Npcap devices");
			_captureMode = "enumeration-failed";
			return new List<LibPcapLiveDevice>();
		}
	}

	private void WatchdogCheck(object? state)
	{
		try
		{
			long num = Interlocked.Read(in _packetsReceived) - Interlocked.Read(in _payloadsAtStart);
			if (num > 0)
			{
				_watchdogTimer?.Dispose();
				_watchdogTimer = null;
			}
			else
			{
				if (!CaptureWatchdog.ShouldFire(num, _watchdogErrorsLogged))
				{
					return;
				}
				List<string> list = new List<string>();
				lock (_lock)
				{
					if (!_isRunning)
					{
						return;
					}
					foreach (ILiveDevice activeDevice in _activeDevices)
					{
						string text = activeDevice.Description ?? activeDevice.Name;
						try
						{
							ICaptureStatistics statistics = activeDevice.Statistics;
							LibPcapLiveDevice obj = activeDevice as LibPcapLiveDevice;
							string value = ((obj != null && obj.Loopback) ? ", loopback" : "");
							list.Add($"{text}: kernelRx={statistics.ReceivedPackets}, kernelDropped={statistics.DroppedPackets}{value}");
						}
						catch
						{
							list.Add(text + ": stats unavailable");
						}
					}
				}
				_watchdogErrorsLogged++;
				int elapsedSeconds = (int)(DateTime.UtcNow - _captureStartedUtc).TotalSeconds;
				_logger.LogError("{Message}", CaptureWatchdog.ComposeBlindCaptureError(elapsedSeconds, _captureMode, list));
			}
		}
		catch
		{
		}
	}

	private void StartCaptureOnDevice(LibPcapLiveDevice device)
	{
		string deviceLabel = device.Description ?? device.Name;
		try
		{
			device.Open(DeviceModes.MaxResponsiveness, 10);
			if (device.Loopback)
			{
				device.Filter = "tcp";
			}
			else
			{
				device.Filter = "tcp and not port 443 and not port 80 and not port 53 and not port 22";
			}
			try
			{
				PropertyInfo property = device.GetType().GetProperty("KernelBufferSize");
				if (property != null)
				{
					property.SetValue(device, 67108864u);
					_logger.LogDebug("Set kernel buffer to 64MB on {Device}", deviceLabel);
				}
			}
			catch (Exception ex)
			{
				_logger.LogDebug("Could not set kernel buffer size on {Device}: {Msg}", deviceLabel, ex.Message);
			}
			try
			{
				PropertyInfo property2 = device.GetType().GetProperty("MinToCopy");
				if (property2 != null)
				{
					property2.SetValue(device, 1);
					_logger.LogDebug("Set MinToCopy=1 on {Device}", deviceLabel);
				}
			}
			catch (Exception ex2)
			{
				_logger.LogDebug("Could not set MinToCopy on {Device}: {Msg}", deviceLabel, ex2.Message);
			}
			device.OnPacketArrival += (object sender, SharpPcap.PacketCapture capture) =>
			{
				try
				{
					ProcessCapturedPacket(capture, deviceLabel);
				}
				catch (Exception exception2)
				{
					_logger.LogTrace(exception2, "Error processing packet on {Device}", deviceLabel);
				}
			};
			device.StartCapture();
			lock (_lock)
			{
				_activeDevices.Add(device);
			}
			_logger.LogInformation("Capturing on: {Device} (loopback={Loopback})", deviceLabel, device.Loopback);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Failed to start capture on {Device}", deviceLabel);
			CloseDevice(device);
		}
	}

	private void ProcessCapturedPacket(SharpPcap.PacketCapture capture, string deviceLabel)
	{
		RawCapture packet = capture.GetPacket();
		Packet parsedPacket = Packet.ParsePacket(packet.LinkLayerType, packet.Data);
		TcpPacket tcpPacket = parsedPacket.Extract<TcpPacket>();
		IPPacket ipPacket = parsedPacket.Extract<IPPacket>();
		if (tcpPacket == null || ipPacket == null)
		{
			return;
		}
		ushort sourcePort = tcpPacket.SourcePort;
		ushort destinationPort = tcpPacket.DestinationPort;
		var key = (ipPacket.SourceAddress, sourcePort, ipPacket.DestinationAddress, destinationPort);
		if (tcpPacket.Synchronize && !tcpPacket.Acknowledgment)
		{
			ForgetFlow(ipPacket.SourceAddress, sourcePort, ipPacket.DestinationAddress, destinationPort);
			_clientToServerPairs.TryAdd(key, value: true);
			_logger.LogDebug("TCP SYN detected: {Client}→{Server} (client→server)", sourcePort, destinationPort);
		}
		bool closing = tcpPacket.Finished || tcpPacket.Reset;
		if (tcpPacket.PayloadData == null || tcpPacket.PayloadData.Length == 0 || _clientToServerPairs.ContainsKey(key))
		{
			if (closing)
				ForgetFlow(ipPacket.SourceAddress, sourcePort, ipPacket.DestinationAddress, destinationPort);
			return;
		}
		Interlocked.Increment(ref _packetsReceived);
		uint sequenceNumber = tcpPacket.SequenceNumber;
		byte[] payload = tcpPacket.PayloadData;
		TcpPayloadSequenceDecision decision;
		while (true)
		{
			if (!_nextExpectedSeq.TryGetValue(key, out uint expected))
			{
				decision = TcpPayloadSequencePolicy.Evaluate(null, sequenceNumber, payload.Length);
				if (_nextExpectedSeq.TryAdd(key, decision.NextExpected))
				{
					break;
				}
				continue;
			}
			decision = TcpPayloadSequencePolicy.Evaluate(expected, sequenceNumber, payload.Length);
			if (decision.Drop)
			{
				Interlocked.Increment(ref _tcpRetransmissions);
				return;
			}
			if (_nextExpectedSeq.TryUpdate(key, decision.NextExpected, expected))
			{
				break;
			}
		}
		if (decision.GapBytes > 0)
		{
			Interlocked.Increment(ref _tcpGaps);
			Interlocked.Add(ref _tcpGapBytes, decision.GapBytes);
			_logger.LogWarning("TCP GAP: stream={Src}→{Dst}, missed {GapBytes} bytes (seq={SeqNum})", sourcePort, destinationPort, decision.GapBytes, sequenceNumber);
		}
		if (decision.SkipBytes > 0)
			payload = payload.AsSpan(decision.SkipBytes).ToArray();

		CapturedPayload item = new CapturedPayload(sourcePort, destinationPort, payload, deviceLabel);
		if (_payloadChannel is not { } channel || !channel.Writer.TryWrite(item))
		{
			Interlocked.Increment(ref _queueDropped);
		}
		if (closing)
			ForgetFlow(ipPacket.SourceAddress, sourcePort, ipPacket.DestinationAddress, destinationPort);
	}

	private void ForgetFlow(IPAddress source, ushort sourcePort, IPAddress destination, ushort destinationPort)
	{
		_nextExpectedSeq.TryRemove((source, sourcePort, destination, destinationPort), out _);
		_nextExpectedSeq.TryRemove((destination, destinationPort, source, sourcePort), out _);
		_clientToServerPairs.TryRemove((source, sourcePort, destination, destinationPort), out _);
		_clientToServerPairs.TryRemove((destination, destinationPort, source, sourcePort), out _);
	}

	private async Task ConsumerLoop(ChannelReader<CapturedPayload> reader, CancellationToken ct)
	{
		_logger.LogDebug("Npcap payload consumer started");
		try
		{
			await foreach (CapturedPayload item in reader.ReadAllAsync(ct))
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
			_logger.LogWarning("Npcap payload consumer stopped. Queue dropped {Dropped} payloads", num);
		}
	}

	public (long Received, long Dropped) GetCaptureStatistics()
	{
		long num = 0L;
		long num2 = 0L;
		lock (_lock)
		{
			foreach (ILiveDevice activeDevice in _activeDevices)
			{
				try
				{
					ICaptureStatistics statistics = activeDevice.Statistics;
					num += statistics.ReceivedPackets;
					num2 += statistics.DroppedPackets;
				}
				catch
				{
				}
			}
		}
		return (Received: num, Dropped: num2);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Stop();
			_disposed = true;
		}
	}
}
