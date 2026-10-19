namespace AionFlex.Core.Overlay;

public readonly record struct RowEdit(RowEditKind Kind, int From, int To)
{
	public static RowEdit RemoveAt(int index)
	{
		return new RowEdit(RowEditKind.RemoveAt, index, -1);
	}

	public static RowEdit Insert(int index)
	{
		return new RowEdit(RowEditKind.Insert, -1, index);
	}

	public static RowEdit Move(int from, int to)
	{
		return new RowEdit(RowEditKind.Move, from, to);
	}
}
