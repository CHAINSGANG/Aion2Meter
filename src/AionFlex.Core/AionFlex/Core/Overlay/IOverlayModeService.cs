using System;

namespace AionFlex.Core.Overlay;

public interface IOverlayModeService
{
	OverlayMode Mode { get; }

	bool FlipEnabled { get; }

	bool PvpTeaser { get; }

	event EventHandler<OverlayMode>? ModeChanged;

	event EventHandler<bool>? FlipEnabledChanged;

	event EventHandler<bool>? PvpTeaserChanged;

	void SetMode(OverlayMode mode);

	void Toggle();

	void SetFlipEnabled(bool enabled);

	void SetPvpTeaser(bool enabled);
}
