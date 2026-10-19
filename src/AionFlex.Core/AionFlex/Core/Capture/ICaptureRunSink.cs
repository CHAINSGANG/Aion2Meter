namespace AionFlex.Core.Capture;

public interface ICaptureRunSink
{
	void WriteRun(CaptureRunIndex index, long fromMs, long toMs);
}
