using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Wallet;

public sealed class WalletState
{
	private sealed class Wallet
	{
		private readonly ulong?[] _handles = new ulong?[3];

		private readonly long[] _counts = new long[3];

		public string Character { get; set; } = "";

		public string? Region { get; set; }

		public int? ServerId { get; set; }

		public long ReadAtMs { get; set; }

		public CharacterWallet Snapshot => new CharacterWallet(Character, _counts[0], _counts[1], _counts[2], ReadAtMs, Region, ServerId);

		public (ulong? Handle, long Count) Get(WalletCurrency c)
		{
			return (Handle: _handles[(int)c], Count: _counts[(int)c]);
		}

		public void Set(WalletCurrency c, ulong handle, long count)
		{
			_handles[(int)c] = handle;
			_counts[(int)c] = count;
		}

		public void Clear(WalletCurrency c)
		{
			_handles[(int)c] = null;
			_counts[(int)c] = 0L;
		}

		public bool SameStacks(Wallet other)
		{
			for (int i = 0; i < 3; i++)
			{
				if (_handles[i] != other._handles[i] || _counts[i] != other._counts[i])
				{
					return false;
				}
			}
			return true;
		}

		public Wallet Copy()
		{
			Wallet wallet = new Wallet();
			for (int i = 0; i < 3; i++)
			{
				wallet._handles[i] = _handles[i];
				wallet._counts[i] = _counts[i];
			}
			return wallet;
		}
	}

	public const long MaxHoldMs = 30000L;

	public const long CopyWindowMs = 5000L;

	private readonly Dictionary<string, Wallet> _byCharacter = new Dictionary<string, Wallet>(StringComparer.Ordinal);

	private Wallet? _held;

	private long _heldAtMs;

	private Wallet? _lastList;

	private long _lastListAtMs;

	private string? _bound;

	public CharacterWallet? Current
	{
		get
		{
			if (_bound == null || !_byCharacter.TryGetValue(_bound, out Wallet value))
			{
				return null;
			}
			return value.Snapshot;
		}
	}

	public CharacterWallet? Of(string character)
	{
		if (!_byCharacter.TryGetValue(character, out Wallet value))
		{
			return null;
		}
		return value.Snapshot;
	}

	public bool OnList(IReadOnlyList<WalletItem> stacks, long atMs)
	{
		Wallet wallet = new Wallet();
		foreach (WalletItem stack in stacks)
		{
			wallet.Set(stack.Currency, stack.Handle, stack.Count);
		}
		if (_lastList != null && _lastList.SameStacks(wallet) && atMs - _lastListAtMs >= 0 && atMs - _lastListAtMs < 5000)
		{
			return false;
		}
		_lastList = wallet.Copy();
		_lastListAtMs = atMs;
		wallet.ReadAtMs = atMs;
		_held = wallet;
		_heldAtMs = atMs;
		return false;
	}

	public bool OnChange(IReadOnlyList<WalletItem> entries, long atMs)
	{
		if (entries.Count == 0)
		{
			return false;
		}
		if (_held != null)
		{
			Apply(_held, entries);
			return false;
		}
		if (_bound == null || !_byCharacter.TryGetValue(_bound, out Wallet value))
		{
			return false;
		}
		if (!Apply(value, entries))
		{
			return false;
		}
		value.ReadAtMs = atMs;
		return true;
	}

	public bool OnCharacterBound(string character, long atMs, string? region = null, int? serverId = null)
	{
		bool result = character != _bound;
		_bound = character;
		if (_held == null)
		{
			return result;
		}
		Wallet held = _held;
		_held = null;
		if (atMs - _heldAtMs > 30000 || atMs < _heldAtMs)
		{
			return result;
		}
		held.Character = character;
		held.Region = region;
		held.ServerId = serverId;
		_byCharacter[character] = held;
		return true;
	}

	private static bool Apply(Wallet wallet, IReadOnlyList<WalletItem> entries)
	{
		bool result = false;
		foreach (WalletItem entry in entries)
		{
			var (num, num2) = wallet.Get(entry.Currency);
			switch (entry.Op)
			{
			case InventoryItemOp.Added:
			case InventoryItemOp.Changed:
				if ((!num.HasValue || num == entry.Handle) && (num != entry.Handle || num2 != entry.Count))
				{
					wallet.Set(entry.Currency, entry.Handle, entry.Count);
					result = true;
				}
				break;
			case InventoryItemOp.Removed:
				if (num == entry.Handle)
				{
					wallet.Clear(entry.Currency);
					result = true;
				}
				break;
			}
		}
		return result;
	}
}
