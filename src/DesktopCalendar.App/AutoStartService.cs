using System.IO;

namespace DesktopCalendar.App;

/// <summary>
/// Windows 시작 시 자동 실행 등록/해제 (DESIGN.md 4.9).
///
/// 레지스트리 Run 키 대신 <b>시작프로그램 폴더에 바로가기</b>를 만든다. 사용자가 폴더를 열어
/// 눈으로 확인하고 직접 지울 수 있고, 작업 관리자의 "시작 프로그램" 탭에도 그대로 보인다.
/// 은닉성이 낮을수록 백신 휴리스틱에 걸릴 확률도 낮다.
/// </summary>
public static class AutoStartService
{
    private const string ShortcutName = "바탕화면 달력.lnk";

    private static string StartupFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static string ShortcutPath => Path.Combine(StartupFolder, ShortcutName);

    /// <summary>실행 파일 경로. 단일 파일로 배포해도 올바른 경로가 나오도록 프로세스 경로를 쓴다.</summary>
    private static string? ExecutablePath => Environment.ProcessPath;

    public static bool IsEnabled => File.Exists(ShortcutPath);

    /// <summary>바로가기를 만들거나 지운다. 실패하면 이유를 담은 예외를 던진다.</summary>
    public static void SetEnabled(bool enabled)
    {
        if (enabled)
            CreateShortcut();
        else if (File.Exists(ShortcutPath))
            File.Delete(ShortcutPath);
    }

    /// <summary>사용자가 직접 확인할 수 있도록 시작프로그램 폴더 경로를 알려준다.</summary>
    public static string GetStartupFolderPath() => StartupFolder;

    private static void CreateShortcut()
    {
        var target = ExecutablePath
            ?? throw new InvalidOperationException("실행 파일 경로를 알 수 없어 자동 실행을 등록하지 못했습니다.");

        // WScript.Shell(COM)으로 바로가기를 만든다. .NET에 내장된 IDispatch 바인딩을 쓰므로
        // 추가 패키지가 필요 없다.
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows 스크립트 호스트를 찾을 수 없습니다.");

        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("바로가기를 만들지 못했습니다.");

            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = Path.GetDirectoryName(target) ?? string.Empty;
            shortcut.Description = "바탕화면 배경 달력";
            shortcut.Save();
        }
        finally
        {
            if (shell is not null)
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}
