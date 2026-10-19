using System;
using System.IO;

namespace AionFlex.Infrastructure.PacketCapture;

public static class CaptureFileSlicer
{
	public static long CopyByteRange(string sourcePath, long start, long end, string destPath)
	{
		if (end <= start)
		{
			return 0L;
		}
		string text = destPath + ".tmp";
		long num = 0L;
		using (FileStream fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
		{
			using FileStream fileStream2 = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
			WriteHeader(fileStream2);
			fileStream.Seek(start, SeekOrigin.Begin);
			byte[] array = new byte[65536];
			long num2 = end - start;
			while (num2 > 0)
			{
				int num3 = fileStream.Read(array, 0, (int)Math.Min(array.Length, num2));
				if (num3 <= 0)
				{
					break;
				}
				fileStream2.Write(array, 0, num3);
				num2 -= num3;
				num += num3;
			}
		}
		if (num == 0L)
		{
			TryDelete(text);
			return 0L;
		}
		File.Move(text, destPath, overwrite: true);
		return num;
	}

	public static long SliceByTime(string inputPath, long fromMs, long toMs, string destPath)
	{
		string text = destPath + ".tmp";
		long num = 0L;
		using (FileStream fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
		{
			using BinaryReader binaryReader = new BinaryReader(fileStream);
			using FileStream output = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
			using BinaryWriter binaryWriter = new BinaryWriter(output);
			if (fileStream.Length < 8 || binaryReader.ReadInt32() != 1093813072)
			{
				throw new InvalidDataException(inputPath + " is not an A2CP capture");
			}
			binaryReader.ReadInt32();
			binaryWriter.Write(1093813072);
			binaryWriter.Write(1);
			long length = fileStream.Length;
			while (fileStream.Position + 20 <= length)
			{
				long num2 = binaryReader.ReadInt64();
				int value = binaryReader.ReadInt32();
				int value2 = binaryReader.ReadInt32();
				int num3 = binaryReader.ReadInt32();
				if (num3 < 0 || fileStream.Position + num3 > length)
				{
					break;
				}
				byte[] buffer = binaryReader.ReadBytes(num3);
				if (num2 >= fromMs && num2 <= toMs)
				{
					binaryWriter.Write(num2);
					binaryWriter.Write(value);
					binaryWriter.Write(value2);
					binaryWriter.Write(num3);
					binaryWriter.Write(buffer);
					num++;
				}
			}
		}
		if (num == 0L)
		{
			TryDelete(text);
			return 0L;
		}
		File.Move(text, destPath, overwrite: true);
		return num;
	}

	public static long CopyLogRange(string logPath, long start, long end, string destPath)
	{
		using FileStream fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536);
		end = Math.Min(end, fileStream.Length);
		start = Math.Max(0L, start);
		if (end <= start)
		{
			return 0L;
		}
		byte[] array = new byte[end - start];
		fileStream.Seek(start, SeekOrigin.Begin);
		int i;
		int num;
		for (i = 0; i < array.Length; i += num)
		{
			num = fileStream.Read(array, i, array.Length - i);
			if (num <= 0)
			{
				break;
			}
		}
		if (i == 0)
		{
			return 0L;
		}
		int num2 = 0;
		if (start > 0)
		{
			fileStream.Seek(start - 1, SeekOrigin.Begin);
			if (fileStream.ReadByte() != 10)
			{
				int num3 = Array.IndexOf(array, (byte)10, 0, i);
				if (num3 < 0)
				{
					return 0L;
				}
				num2 = num3 + 1;
			}
		}
		int num4 = Array.LastIndexOf(array, (byte)10, i - 1, i);
		if (num4 < num2)
		{
			return 0L;
		}
		int num5 = num4 + 1 - num2;
		string text = destPath + ".tmp";
		using (FileStream fileStream2 = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			fileStream2.Write(array, num2, num5);
		}
		File.Move(text, destPath, overwrite: true);
		return num5;
	}

	public static long SliceLogByTime(string logPath, DateTime logStartLocal, DateTime fromLocal, DateTime toLocal, string destPath)
	{
		string text = destPath + ".tmp";
		long num = 0L;
		using (FileStream stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			using StreamReader streamReader = new StreamReader(stream);
			using StreamWriter streamWriter = new StreamWriter(text, append: false);
			DateTime dateTime = logStartLocal.Date;
			TimeSpan? timeSpan = null;
			bool flag = false;
			string text2;
			while ((text2 = streamReader.ReadLine()) != null)
			{
				if (TryParseLineTime(text2, out var timeOfDay))
				{
					if (timeSpan.HasValue)
					{
						TimeSpan valueOrDefault = timeSpan.GetValueOrDefault();
						if (timeOfDay < valueOrDefault - TimeSpan.FromHours(12.0))
						{
							dateTime = dateTime.AddDays(1.0);
						}
					}
					timeSpan = timeOfDay;
					DateTime dateTime2 = dateTime + timeOfDay;
					if (dateTime2 > toLocal)
					{
						break;
					}
					flag = dateTime2 >= fromLocal;
				}
				if (flag)
				{
					streamWriter.WriteLine(text2);
					num += text2.Length + 1;
				}
			}
		}
		if (num == 0L)
		{
			TryDelete(text);
			return 0L;
		}
		File.Move(text, destPath, overwrite: true);
		return num;
	}

	internal static bool TryParseLineTime(string line, out TimeSpan timeOfDay)
	{
		timeOfDay = default;
		if (line.Length < 14 || line[0] != '[' || line[13] != ']' || line[3] != ':' || line[6] != ':' || line[9] != '.')
		{
			return false;
		}
		if (!int.TryParse(line.AsSpan(1, 2), out var result) || !int.TryParse(line.AsSpan(4, 2), out var result2) || !int.TryParse(line.AsSpan(7, 2), out var result3) || !int.TryParse(line.AsSpan(10, 3), out var result4))
		{
			return false;
		}
		if (result > 23 || result2 > 59 || result3 > 59)
		{
			return false;
		}
		timeOfDay = new TimeSpan(0, result, result2, result3, result4);
		return true;
	}

	public static void WriteHeader(Stream stream)
	{
		Span<byte> span = stackalloc byte[8];
		BitConverter.TryWriteBytes(span.Slice(0, 4), 1093813072);
		BitConverter.TryWriteBytes(span.Slice(4, span.Length - 4), 1);
		stream.Write(span);
	}

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}
}
