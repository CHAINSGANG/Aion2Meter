using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AionFlex.Core.Search;

public static class SearchRanker
{
	public const int TitleWordStart = 100;

	public const int TitleInside = 60;

	public const int KeywordStart = 50;

	public const int KeywordInside = 30;

	public const int SubtitleInside = 15;

	public static IReadOnlyList<SearchHit> Rank(IEnumerable<SearchEntry> entries, string? query, int max = 20)
	{
		IReadOnlyList<string> readOnlyList = Words(query);
		if (readOnlyList.Count == 0 || max <= 0)
		{
			return Array.Empty<SearchHit>();
		}
		List<SearchHit> list = new List<SearchHit>();
		foreach (SearchEntry entry in entries)
		{
			int num = Score(entry, readOnlyList);
			if (num > 0)
			{
				list.Add(new SearchHit(entry, num));
			}
		}
		List<SearchHit> source = list.OrderByDescending((SearchHit h) => h.Score).ThenBy((SearchHit h) => h.Entry.Title, StringComparer.OrdinalIgnoreCase).Take(max)
			.ToList();
		Dictionary<SearchGroup, int> groupBest = (from h in source
			group h by h.Entry.Group).ToDictionary((IGrouping<SearchGroup, SearchHit> g) => g.Key, (IGrouping<SearchGroup, SearchHit> g) => g.Max((SearchHit h) => h.Score));
		return (from h in source
			orderby groupBest[h.Entry.Group] descending, h.Entry.Group, h.Score descending
			select h).ThenBy((SearchHit h) => h.Entry.Title, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static IReadOnlyList<SearchHit> Rank(IEnumerable<SearchEntry> entries, IEnumerable<SearchEntry> extra, int maxExtra, string? query, int max = 20)
	{
		IEnumerable<SearchEntry> second = from h in Rank(extra, query, maxExtra)
			select h.Entry;
		return Rank(entries.Concat(second), query, max);
	}

	public static int Score(SearchEntry entry, IReadOnlyList<string> words)
	{
		string text = Normalize(entry.Title);
		string[] source = Split(text);
		List<string> source2 = entry.Keywords.Select(Normalize).ToList();
		string text2 = Normalize(entry.Subtitle);
		int num = 0;
		foreach (string word in words)
		{
			int num2;
			if (source.Any((string t) => t.StartsWith(word, StringComparison.Ordinal)))
			{
				num2 = 100;
			}
			else if (text.Contains(word, StringComparison.Ordinal))
			{
				num2 = 60;
			}
			else if (source2.Any((string k) => Split(k).Any((string t) => t.StartsWith(word, StringComparison.Ordinal))))
			{
				num2 = 50;
			}
			else if (source2.Any((string k) => k.Contains(word, StringComparison.Ordinal)))
			{
				num2 = 30;
			}
			else
			{
				if (!text2.Contains(word, StringComparison.Ordinal))
				{
					return 0;
				}
				num2 = 15;
			}
			num += num2;
		}
		return num;
	}

	public static IReadOnlyList<string> Words(string? query)
	{
		if (!string.IsNullOrWhiteSpace(query))
		{
			return Split(Normalize(query));
		}
		return Array.Empty<string>();
	}

	public static string Normalize(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		string text2 = text.Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder(text2.Length);
		bool flag = true;
		string text3 = text2;
		foreach (char c in text3)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(char.ToLowerInvariant(c));
					flag = false;
				}
				else if (!flag)
				{
					stringBuilder.Append(' ');
					flag = true;
				}
			}
		}
		return stringBuilder.ToString().Trim();
	}

	private static string[] Split(string normalized)
	{
		return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
	}
}
