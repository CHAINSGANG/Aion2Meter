using System;
using System.Collections.Generic;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.Daevanion;

public sealed class DaevanionPacketParser : IDaevanionPacketParser, IFeaturePacketParser
{
	private readonly ILogger<DaevanionPacketParser> _logger;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private readonly HashSet<long> _emittedDaevanionHashes = new HashSet<long>();

	private readonly Queue<long> _emittedDaevanionOrder = new Queue<long>();

	private const int MaxDaevanionDedupHistory = 16;

	public event EventHandler<DaevanionSnapshot>? DaevanionDiscovered;

	public DaevanionPacketParser(ILogger<DaevanionPacketParser> logger, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger;
		_protocolHealth = protocolHealth;
	}

	public void ProcessPayload(CapturedPayload payload)
	{
		try
		{
			byte[] data = payload.Data;
			if (data != null && data.Length >= 8)
			{
				ScanForDaevanionPackets(data);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "DaevanionPacketParser: error processing payload");
		}
	}

	private void ScanForDaevanionPackets(byte[] data)
	{
		IReadOnlyList<DaevanionSnapshot> readOnlyList;
		IReadOnlyList<int> anchoredButFailedOffsets;
		try
		{
			readOnlyList = DaevanionParser.ScanAll(data, out anchoredButFailedOffsets);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "DaevanionParser threw on a {Bytes}-byte payload — skipping", data.Length);
			return;
		}
		for (int i = 0; i < anchoredButFailedOffsets.Count; i++)
		{
			_protocolHealth?.RecordDecodeMiss("daevanion", data.AsSpan(anchoredButFailedOffsets[i]));
		}
		if (readOnlyList.Count == 0)
		{
			return;
		}
		_protocolHealth?.RecordDecodeOk("daevanion");
		for (int j = 0; j < readOnlyList.Count; j++)
		{
			DaevanionSnapshot daevanionSnapshot = readOnlyList[j];
			long item = ComputeDaevanionSnapshotHash(daevanionSnapshot);
			if (!_emittedDaevanionHashes.Contains(item))
			{
				_emittedDaevanionHashes.Add(item);
				_emittedDaevanionOrder.Enqueue(item);
				while (_emittedDaevanionOrder.Count > 16)
				{
					_emittedDaevanionHashes.Remove(_emittedDaevanionOrder.Dequeue());
				}
				int num = 0;
				for (int k = 0; k < daevanionSnapshot.Boards.Count; k++)
				{
					num += daevanionSnapshot.Boards[k].NodeIds.Count;
				}
				_logger.LogInformation("\ud83d\udcdc Daevanion packet decoded (DaevanionPacketParser): {Boards} boards, {Total} nodes", daevanionSnapshot.Boards.Count, num);
				DaevanionDiscovered?.Invoke(this, daevanionSnapshot);
			}
		}
	}

	private static long ComputeDaevanionSnapshotHash(DaevanionSnapshot snap)
	{
		long num = -3750763034362895579L;
		for (int i = 0; i < snap.Boards.Count; i++)
		{
			DaevanionBoardEntry daevanionBoardEntry = snap.Boards[i];
			num = (num ^ daevanionBoardEntry.BoardId) * 1099511628211L;
			IReadOnlyList<int> nodeIds = daevanionBoardEntry.NodeIds;
			for (int j = 0; j < nodeIds.Count; j++)
			{
				num = (num ^ nodeIds[j]) * 1099511628211L;
			}
		}
		return num;
	}
}
