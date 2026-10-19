namespace AionFlex.Core.Overlay;

public interface IZOrderReader
{
	nint WindowAbove(nint hwnd);

	bool Exists(nint hwnd);

	WindowAboveUs Describe(nint hwnd);
}
