namespace DesktopCalendar.Core.Desktop;

/// <summary>
/// 바탕화면 아이콘 뒤, 배경화면 위 레이어(WorkerW)에 창을 붙여넣는다.
/// Rainmeter/Wallpaper Engine이 쓰는 것과 동일한 비공식 기법(Progman + 0x052C 메시지)에 기반한다.
/// </summary>
public sealed class DesktopBackgroundHost
{
    private IntPtr _attachedWorkerW = IntPtr.Zero;

    /// <summary>
    /// 지정한 창(hwnd)을 데스크톱 배경 레이어에 붙인다. 성공하면 true.
    /// </summary>
    public bool Attach(IntPtr hwnd)
    {
        var workerW = FindWorkerW();
        if (workerW == IntPtr.Zero)
            return false;

        NativeMethods.SetParent(hwnd, workerW);
        _attachedWorkerW = workerW;
        return true;
    }

    /// <summary>
    /// 현재 붙어 있는 WorkerW가 여전히 유효한지 확인하고, explorer.exe 재시작 등으로
    /// 무효화됐다면 다시 찾아서 재부착한다. 매 호출마다 재부착 여부(true=재부착 발생)를 반환한다.
    /// 주기적으로(예: 타이머) 호출하는 용도.
    /// </summary>
    public bool EnsureAttached(IntPtr hwnd)
    {
        if (_attachedWorkerW != IntPtr.Zero && NativeMethods.IsWindow(_attachedWorkerW))
            return false;

        return Attach(hwnd);
    }

    /// <summary>
    /// 창을 태스크바/Alt+Tab에서 숨긴다 (WS_EX_TOOLWINDOW 부여, WS_EX_APPWINDOW 제거).
    /// WPF의 ShowInTaskbar=False만으로는 Alt+Tab에서 완전히 숨겨지지 않으므로 별도로 필요하다.
    /// </summary>
    public static void HideFromTaskbarAndAltTab(IntPtr hwnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle = (exStyle | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Progman에 WorkerW 생성을 트리거한 뒤, 아이콘 레이어(SHELLDLL_DefView)의
    /// 형제 창인 빈 WorkerW를 찾아 반환한다. 못 찾으면 IntPtr.Zero.
    /// </summary>
    public static IntPtr FindWorkerW()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return IntPtr.Zero;

        // Progman에게 WorkerW를 생성하라는 메시지를 보낸다 (문서화되지 않은 동작이지만 안정적으로 동작함).
        NativeMethods.SendMessageTimeout(
            progman,
            NativeMethods.WM_SPAWN_WORKER,
            IntPtr.Zero,
            IntPtr.Zero,
            fuFlags: 0, // SMTO_NORMAL
            uTimeout: 1000,
            out _);

        var workerW = IntPtr.Zero;

        NativeMethods.EnumWindows((topHandle, _) =>
        {
            var defView = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                // 아이콘을 담고 있는 SHELLDLL_DefView를 자식으로 가진 최상위 창(WorkerW 또는 Progman) 바로
                // "다음" 형제 WorkerW를 찾는다. 이게 아이콘 뒤에 있는 빈 배경 레이어다.
                workerW = NativeMethods.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
            }

            return true; // 계속 열거
        }, IntPtr.Zero);

        return workerW;
    }
}
