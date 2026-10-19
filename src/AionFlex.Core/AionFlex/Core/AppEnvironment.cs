namespace AionFlex.Core;

public static class AppEnvironment
{
	public const string DataFolderName = "AionFlex";

	public const string SingleInstanceMutexName = "AionFlex_SingleInstance_Mutex";

	public const bool IsDevChannel = false;

	public const string SingleInstanceClosingEventName = "AionFlex_SingleInstance_Mutex_Closing";

	public const string SingleInstanceShowEventName = "AionFlex_SingleInstance_Mutex_Show";
}
