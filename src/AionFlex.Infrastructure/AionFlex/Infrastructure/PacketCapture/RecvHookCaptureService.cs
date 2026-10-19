using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketCapture;

public sealed class RecvHookCaptureService : IPacketCaptureService, IDisposable
{
	private static class NM
	{
		public struct MEMORY_BASIC_INFORMATION
		{
			public long BaseAddress;

			public long AllocationBase;

			public uint AllocationProtect;

			public uint _pad1;

			public long RegionSize;

			public uint State;

			public uint Protect;

			public uint Type;

			public uint _pad2;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern nint OpenProcess(uint access, bool inherit, int pid);

		[DllImport("kernel32.dll")]
		public static extern bool CloseHandle(nint h);

		[DllImport("kernel32.dll")]
		public static extern bool ReadProcessMemory(nint h, long addr, byte[] buf, int size, out int read);

		[DllImport("kernel32.dll")]
		public static extern bool WriteProcessMemory(nint h, long addr, byte[] buf, int size, out int written);

		[DllImport("kernel32.dll")]
		public static extern long VirtualAllocEx(nint h, long addr, int size, uint type, uint protect);

		[DllImport("kernel32.dll")]
		public static extern bool VirtualFreeEx(nint h, long addr, int size, uint type);

		[DllImport("kernel32.dll")]
		public static extern bool VirtualProtectEx(nint h, long addr, int size, uint newProt, out uint oldProt);

		[DllImport("kernel32.dll")]
		public static extern int VirtualQueryEx(nint h, long addr, ref MEMORY_BASIC_INFORMATION mbi, int size);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
		public static extern nint LoadLibraryA(string name);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
		public static extern long GetProcAddress(nint module, string name);
	}

	private readonly ILogger<RecvHookCaptureService> _logger;

	private nint _gameHandle;

	private long _recvAddr;

	private long _codeAddr;

	private long _dataAddr;

	private long _hookPtrAddr;

	private byte[] _originalPtrBytes = new byte[8];

	private Thread? _pollThread;

	private volatile bool _running;

	private long _packetsReceived;

	private long _wrapRecoveredChunks;

	private long _wrapRecoveredBytes;

	private long _wrapLostChunks;

	private long _wrapCount;

	private long _queueDropped;

	private const int SHMEM_SIZE = 16777216;

	private const int RING_START = 32;

	public bool IsRunning => _running;

	public long TcpRetransmissions => 0L;

	public long TcpGaps => 0L;

	public long TcpGapBytes => 0L;

	public long QueueDropped => _queueDropped;

	public event EventHandler<CapturedPayload>? PayloadCaptured;

	public RecvHookCaptureService(ILogger<RecvHookCaptureService> logger)
	{
		_logger = logger;
	}

	public void Start()
	{
		if (_running)
		{
			return;
		}
		try
		{
			InstallHook();
			_running = true;
			_pollThread = new Thread(PollLoop)
			{
				Name = "RecvHookPoller",
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal
			};
			_pollThread.Start();
			_logger.LogInformation("Packet capture started");
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Failed to start recv hook capture");
			Cleanup();
		}
	}

	public void Stop()
	{
		if (_running)
		{
			_running = false;
			_pollThread?.Join(3000);
			if (_gameHandle != 0 && _dataAddr != 0L)
			{
				byte[] array = new byte[12];
				NM.ReadProcessMemory(_gameHandle, _dataAddr, array, 12, out var _);
				uint num = BitConverter.ToUInt32(array, 4);
				uint num2 = BitConverter.ToUInt32(array, 8);
				long num3 = num - _packetsReceived;
				_logger.LogInformation("Packet capture stopped — polled={Polled}, hookPkts={HookPkts}, hookCalls={Calls}, lost={Lost}, wraps={Wraps}, recovered={Recovered}({RecovBytes}B)", _packetsReceived, num, num2, num3, _wrapCount, _wrapRecoveredChunks, _wrapRecoveredBytes);
			}
			RemoveHook();
			Cleanup();
		}
	}

	public (long Received, long Dropped) GetCaptureStatistics()
	{
		return (Received: _packetsReceived, Dropped: _queueDropped);
	}

	private void InstallHook()
	{
		Process process = Process.GetProcessesByName("Aion2").FirstOrDefault() ?? throw new InvalidOperationException("Aion2.exe not found");
		_gameHandle = NM.OpenProcess(2035711u, inherit: false, process.Id);
		if (_gameHandle == 0)
		{
			throw new InvalidOperationException($"Cannot open Aion2.exe (PID {process.Id}). Run as Administrator.");
		}
		nint module = NM.LoadLibraryA("WS2_32.dll");
		_recvAddr = NM.GetProcAddress(module, "recv");
		if (_recvAddr == 0L)
		{
			throw new InvalidOperationException("Cannot find recv()");
		}
		byte[] array = new byte[2];
		NM.ReadProcessMemory(_gameHandle, _recvAddr, array, 2, out var read);
		if (array[0] == byte.MaxValue && array[1] == 37)
		{
			throw new InvalidOperationException("recv() is already hooked (FF 25). Restart game first.");
		}
		_hookPtrAddr = FindRecvPointer();
		if (_hookPtrAddr == 0L)
		{
			throw new InvalidOperationException("Cannot find hookable recv() pointer in game memory");
		}
		NM.ReadProcessMemory(_gameHandle, _hookPtrAddr, _originalPtrBytes, 8, out read);
		_codeAddr = NM.VirtualAllocEx(_gameHandle, 0L, 8192, 12288u, 64u);
		_dataAddr = NM.VirtualAllocEx(_gameHandle, 0L, 16777216, 12288u, 4u);
		if (_codeAddr == 0L || _dataAddr == 0L)
		{
			throw new InvalidOperationException("VirtualAllocEx failed");
		}
		byte[] array2 = new byte[32];
		BitConverter.GetBytes(32u).CopyTo(array2, 0);
		NM.WriteProcessMemory(_gameHandle, _dataAddr, array2, 32, out read);
		byte[] array3 = BuildHook();
		NM.WriteProcessMemory(_gameHandle, _codeAddr, array3, array3.Length, out read);
		NM.VirtualProtectEx(_gameHandle, _hookPtrAddr, 8, 64u, out var _);
		byte[] bytes = BitConverter.GetBytes(_codeAddr);
		NM.WriteProcessMemory(_gameHandle, _hookPtrAddr, bytes, 8, out read);
		_logger.LogInformation("Packet capture initialized");
	}

	private long FindRecvPointer()
	{
		byte[] bytes = BitConverter.GetBytes(_recvAddr);
		NM.MEMORY_BASIC_INFORMATION mbi = default;
		long num = 0L;
		long num2 = 0L;
		string text = "";
		while (num < 140737488355327L && NM.VirtualQueryEx(_gameHandle, num, ref mbi, Marshal.SizeOf<NM.MEMORY_BASIC_INFORMATION>()) != 0 && mbi.RegionSize != 0L)
		{
			if (mbi.State == 4096 && mbi.RegionSize < 50000000)
			{
				uint oldProt = mbi.Type;
				string text2 = oldProt switch
				{
					131072u => "PRIVATE", 
					262144u => "MAPPED", 
					16777216u => "IMAGE", 
					_ => $"0x{mbi.Type:X}", 
				};
				try
				{
					byte[] array = new byte[mbi.RegionSize];
					if (NM.ReadProcessMemory(_gameHandle, mbi.BaseAddress, array, (int)mbi.RegionSize, out var _))
					{
						int num3;
						for (num3 = 0; num3 <= array.Length - 8; num3++)
						{
							num3 = Array.IndexOf(array, bytes[0], num3);
							if (num3 < 0 || num3 + 8 > array.Length)
							{
								break;
							}
							bool flag = true;
							for (int i = 1; i < 8; i++)
							{
								if (array[num3 + i] != bytes[i])
								{
									flag = false;
									break;
								}
							}
							if (flag)
							{
								long num4 = mbi.BaseAddress + num3;
								bool flag2;
								if (!(flag2 = (mbi.Protect & 4) != 0 || (mbi.Protect & 8) != 0 || (mbi.Protect & 0x40) != 0 || (mbi.Protect & 0x80) != 0))
								{
									flag2 = NM.VirtualProtectEx(_gameHandle, num4, 8, 64u, out var oldProt2);
									if (flag2)
									{
										NM.VirtualProtectEx(_gameHandle, num4, 8, oldProt2, out oldProt);
									}
								}
								if (flag2 && (num2 == 0L || (text2 == "MAPPED" && text != "MAPPED") || (text2 == "PRIVATE" && text == "IMAGE")))
								{
									num2 = num4;
									text = text2;
								}
							}
						}
					}
				}
				catch
				{
				}
			}
			long num5 = mbi.BaseAddress + mbi.RegionSize;
			if (num5 <= num)
			{
				break;
			}
			num = num5;
		}
		if (num2 == 0L)
		{
			_logger.LogError("Packet capture initialization failed — game connection not available");
		}
		return num2;
	}

	private byte[] BuildHook()
	{
		List<byte> list = new List<byte>();
		list.Add(86);
		list.Add(87);
		list.Add(83);
		list.AddRange(new byte[4] { 72, 131, 236, 48 });
		list.AddRange(new byte[5] { 72, 137, 84, 36, 32 });
		list.AddRange(new byte[2] { 72, 184 });
		list.AddRange(BitConverter.GetBytes(_recvAddr));
		list.AddRange(new byte[2] { 255, 208 });
		list.AddRange(new byte[2] { 137, 195 });
		list.AddRange(new byte[2] { 72, 184 });
		list.AddRange(BitConverter.GetBytes(_dataAddr));
		list.AddRange(new byte[4] { 240, 255, 64, 8 });
		list.AddRange(new byte[2] { 133, 219 });
		int count = list.Count;
		list.AddRange(new byte[2] { 126, 0 });
		list.AddRange(new byte[2] { 139, 8 });
		list.AddRange(new byte[4] { 141, 116, 25, 2 });
		list.AddRange(new byte[2] { 129, 254 });
		list.AddRange(BitConverter.GetBytes(16777216));
		list.AddRange(new byte[2] { 114, 7 });
		list.Add(185);
		list.AddRange(BitConverter.GetBytes(32));
		list.AddRange(new byte[2] { 137, 8 });
		list.AddRange(new byte[4] { 72, 141, 60, 8 });
		list.AddRange(new byte[3] { 102, 137, 31 });
		list.AddRange(new byte[4] { 72, 131, 199, 2 });
		list.AddRange(new byte[5] { 72, 139, 116, 36, 32 });
		list.AddRange(new byte[2] { 137, 217 });
		list.AddRange(new byte[2] { 243, 164 });
		list.AddRange(new byte[2] { 72, 184 });
		list.AddRange(BitConverter.GetBytes(_dataAddr));
		list.AddRange(new byte[3] { 72, 41, 199 });
		list.AddRange(new byte[2] { 137, 56 });
		list.AddRange(new byte[4] { 240, 255, 64, 4 });
		int count2 = list.Count;
		list[count + 1] = (byte)(count2 - count - 2);
		list.AddRange(new byte[2] { 137, 216 });
		list.AddRange(new byte[4] { 72, 131, 196, 48 });
		list.Add(91);
		list.Add(95);
		list.Add(94);
		list.Add(195);
		return list.ToArray();
	}

	private void RemoveHook()
	{
		if (_gameHandle == 0 || _hookPtrAddr == 0L)
		{
			return;
		}
		try
		{
			NM.WriteProcessMemory(_gameHandle, _hookPtrAddr, _originalPtrBytes, 8, out var _);
			_logger.LogDebug("Packet capture pointer restored");
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Failed to restore recv() pointer");
		}
	}

	private void PollLoop()
	{
		byte[] array = new byte[16];
		int readPos = 32;
		byte[] array2 = new byte[2];
		long num = Environment.TickCount64;
		byte[] array3 = new byte[131072];
		int mergeLen = 0;
		while (_running)
		{
			try
			{
				NM.ReadProcessMemory(_gameHandle, _dataAddr, array, 16, out var read);
				if (read == 0)
				{
					_logger.LogWarning("Cannot read ring buffer — game may have exited");
					_running = false;
					break;
				}
				int num2 = (int)BitConverter.ToUInt32(array, 0);
				uint num3 = BitConverter.ToUInt32(array, 4);
				uint num4 = BitConverter.ToUInt32(array, 8);
				ReadChunks(ref readPos, num2, array3, ref mergeLen, array2);
				if (num2 < readPos && num2 >= 32)
				{
					_wrapCount++;
					int num5 = 0;
					int num6 = 0;
					while (readPos < 16777212)
					{
						NM.ReadProcessMemory(_gameHandle, _dataAddr + readPos, array2, 2, out var read2);
						ushort num7 = BitConverter.ToUInt16(array2, 0);
						if (num7 == 0 || num7 > 60000 || readPos + 2 + num7 > 16777216)
						{
							break;
						}
						EmitMergeIfFull(array3, ref mergeLen, num7);
						byte[] array4 = new byte[num7];
						NM.ReadProcessMemory(_gameHandle, _dataAddr + readPos + 2, array4, num7, out read2);
						Buffer.BlockCopy(array4, 0, array3, mergeLen, num7);
						Interlocked.Increment(ref _packetsReceived);
						mergeLen += num7;
						readPos += 2 + num7;
						num5++;
						num6 += num7;
					}
					if (num5 > 0)
					{
						_wrapRecoveredChunks += num5;
						_wrapRecoveredBytes += num6;
					}
					readPos = 32;
					ReadChunks(ref readPos, num2, array3, ref mergeLen, array2);
				}
				if (mergeLen > 0)
				{
					byte[] array5 = new byte[mergeLen];
					Buffer.BlockCopy(array3, 0, array5, 0, mergeLen);
					PayloadCaptured?.Invoke(this, new CapturedPayload(0, 0, array5, "recv-hook"));
					mergeLen = 0;
				}
				long tickCount = Environment.TickCount64;
				if (tickCount - num > 10000)
				{
					long num8 = num3 - _packetsReceived;
					if (num8 > 0)
					{
						_wrapLostChunks = num8;
						_logger.LogWarning("⚠\ufe0f CAPTURE LOSS: hook={HookPkts}, polled={Polled}, lost={Lost}, wraps={Wraps}, recovered={Recovered}({RecovBytes}B), hookCalls={Calls}", num3, _packetsReceived, num8, _wrapCount, _wrapRecoveredChunks, _wrapRecoveredBytes, num4);
					}
					else
					{
						_logger.LogDebug("\ud83d\udce1 Capture OK: hook={HookPkts}, polled={Polled}, wraps={Wraps}, recovered={Recovered}({RecovBytes}B)", num3, _packetsReceived, _wrapCount, _wrapRecoveredChunks, _wrapRecoveredBytes);
					}
					num = tickCount;
				}
				Thread.Sleep(5);
			}
			catch (Exception exception)
			{
				_logger.LogTrace(exception, "Poll error");
				Thread.Sleep(50);
			}
		}
	}

	private void ReadChunks(ref int readPos, int writePos, byte[] mergeBuf, ref int mergeLen, byte[] lenBuf)
	{
		while (readPos < writePos && readPos < 16777212)
		{
			NM.ReadProcessMemory(_gameHandle, _dataAddr + readPos, lenBuf, 2, out var read);
			ushort num = BitConverter.ToUInt16(lenBuf, 0);
			if (num == 0 || num > 60000)
			{
				_logger.LogWarning("Bad packet length {Len} at pos {Pos}, resetting", num, readPos);
				readPos = 32;
				mergeLen = 0;
				break;
			}
			EmitMergeIfFull(mergeBuf, ref mergeLen, num);
			byte[] array = new byte[num];
			NM.ReadProcessMemory(_gameHandle, _dataAddr + readPos + 2, array, num, out read);
			Buffer.BlockCopy(array, 0, mergeBuf, mergeLen, num);
			Interlocked.Increment(ref _packetsReceived);
			mergeLen += num;
			readPos += 2 + num;
		}
	}

	private void EmitMergeIfFull(byte[] mergeBuf, ref int mergeLen, int nextChunkLen)
	{
		if (mergeLen + nextChunkLen > mergeBuf.Length && mergeLen > 0)
		{
			byte[] array = new byte[mergeLen];
			Buffer.BlockCopy(mergeBuf, 0, array, 0, mergeLen);
			PayloadCaptured?.Invoke(this, new CapturedPayload(0, 0, array, "recv-hook"));
			mergeLen = 0;
		}
	}

	private void Cleanup()
	{
		if (_gameHandle != 0)
		{
			if (_codeAddr != 0L)
			{
				NM.VirtualFreeEx(_gameHandle, _codeAddr, 0, 32768u);
			}
			if (_dataAddr != 0L)
			{
				NM.VirtualFreeEx(_gameHandle, _dataAddr, 0, 32768u);
			}
			NM.CloseHandle(_gameHandle);
			_gameHandle = 0;
		}
	}

	public void Dispose()
	{
		Stop();
	}
}
