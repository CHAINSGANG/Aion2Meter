using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AionFlex.Core.Capture;

namespace AionFlex.Infrastructure.PacketCapture;

public static class CaptureRunJson
{
	public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static void Write(string path, CaptureRunIndex index)
	{
		string text = path + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(index, Options));
		File.Move(text, path, overwrite: true);
	}

	public static CaptureRunIndex? TryRead(string path)
	{
		try
		{
			return JsonSerializer.Deserialize<CaptureRunIndex>(File.ReadAllText(path), Options);
		}
		catch
		{
			return null;
		}
	}
}
