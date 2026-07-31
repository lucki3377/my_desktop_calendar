namespace DesktopCalendar.Core.Storage;

public static class AppPaths
{
    private const string AppFolderName = "DesktopCalendar";

    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabaseFilePath => Path.Combine(DataDirectory, "calendar.db");
}
