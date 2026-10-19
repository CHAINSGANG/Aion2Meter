namespace AionFlex.Core.Overlay;

public sealed record NewRecordToastLayout(NewRecordToastMode Mode, bool ShowBoss, bool ShowDelta, bool LongTitle, bool ShowUnit)
{
	private sealed record Widths(double Boss, double Delta, double LongTitle, double Unit);

	public bool IsTwoLine => Mode == NewRecordToastMode.TwoLine;

	public bool IsOneLine => Mode != NewRecordToastMode.TwoLine;

	public bool IsMini => Mode == NewRecordToastMode.Mini;

	public const double TwoLineMinHeight = 34.0;

	public const double OneLineMinHeight = 22.0;

	public const string ShortTitle = "NEW BEST";

	private static readonly Widths TwoLineWidths = new Widths(150.0, 236.0, 196.0, 160.0);

	private static readonly Widths OneLineWidths = new Widths(290.0, 236.0, 200.0, 150.0);

	private static readonly Widths MiniWidths = new Widths(260.0, 210.0, 180.0, 140.0);

	public static NewRecordToastLayout For(double width, double height)
	{
		if (!double.IsFinite(width) || width < 0.0)
		{
			width = 0.0;
		}
		if (!double.IsFinite(height) || height < 0.0)
		{
			height = 0.0;
		}
		NewRecordToastMode newRecordToastMode = ((!(height >= 34.0)) ? ((height >= 22.0) ? NewRecordToastMode.OneLine : NewRecordToastMode.Mini) : NewRecordToastMode.TwoLine);
		Widths widths = newRecordToastMode switch
		{
			NewRecordToastMode.TwoLine => TwoLineWidths, 
			NewRecordToastMode.OneLine => OneLineWidths, 
			_ => MiniWidths, 
		};
		return new NewRecordToastLayout(newRecordToastMode, width >= widths.Boss, width >= widths.Delta, width >= widths.LongTitle, width >= widths.Unit);
	}
}
