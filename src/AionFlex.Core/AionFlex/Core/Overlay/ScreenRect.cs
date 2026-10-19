namespace AionFlex.Core.Overlay;

public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
	public int Width => Right - Left;

	public int Height => Bottom - Top;

	public bool OverlapsWithPositiveArea(ScreenRect other)
	{
		if (Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0 && Left < other.Right && other.Left < Right && Top < other.Bottom)
		{
			return other.Top < Bottom;
		}
		return false;
	}
}
