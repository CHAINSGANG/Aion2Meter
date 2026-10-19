using System;

namespace AionFlex.Core.Overlay;

public sealed class OverlayModeService : IOverlayModeService
{
	private readonly object _gate = new object();

	private OverlayMode _mode;

	private bool _flipEnabled;

	private bool _pvpTeaser;

	public OverlayMode Mode
	{
		get
		{
			lock (_gate)
			{
				return _mode;
			}
		}
	}

	public bool FlipEnabled
	{
		get
		{
			lock (_gate)
			{
				return _flipEnabled;
			}
		}
	}

	public bool PvpTeaser
	{
		get
		{
			lock (_gate)
			{
				return _pvpTeaser;
			}
		}
	}

	public event EventHandler<OverlayMode>? ModeChanged;

	public event EventHandler<bool>? FlipEnabledChanged;

	public event EventHandler<bool>? PvpTeaserChanged;

	public void SetMode(OverlayMode mode)
	{
		lock (_gate)
		{
			if ((mode == OverlayMode.Pvp && !_flipEnabled) || _mode == mode)
			{
				return;
			}
			_mode = mode;
		}
		ModeChanged?.Invoke(this, mode);
	}

	public void Toggle()
	{
		OverlayMode overlayMode;
		lock (_gate)
		{
			if (!_flipEnabled)
			{
				return;
			}
			overlayMode = ((_mode == OverlayMode.Dps) ? OverlayMode.Pvp : OverlayMode.Dps);
			if (_mode == overlayMode)
			{
				return;
			}
			_mode = overlayMode;
		}
		ModeChanged?.Invoke(this, overlayMode);
	}

	public void SetFlipEnabled(bool enabled)
	{
		bool flag = false;
		bool flag2;
		lock (_gate)
		{
			flag2 = _flipEnabled != enabled;
			_flipEnabled = enabled;
			if (!enabled && _mode != OverlayMode.Dps)
			{
				_mode = OverlayMode.Dps;
				flag = true;
			}
		}
		if (flag2)
		{
			FlipEnabledChanged?.Invoke(this, enabled);
		}
		if (flag)
		{
			ModeChanged?.Invoke(this, OverlayMode.Dps);
		}
	}

	public void SetPvpTeaser(bool enabled)
	{
		bool flag;
		lock (_gate)
		{
			flag = _pvpTeaser != enabled;
			_pvpTeaser = enabled;
		}
		if (flag)
		{
			PvpTeaserChanged?.Invoke(this, enabled);
		}
	}
}
