using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DesktopCalendar.App;

/// <summary>도움말 창에서 처음 보여줄 탭.</summary>
public enum HelpTopic
{
    Google,
    Holiday,
    Widget,
}

/// <summary>
/// 외부 서비스(구글 OAuth, 공공데이터포털) 발급 절차와 위젯 사용법 안내 창.
/// 설정 대화상자들의 "도움말" 버튼과 위젯 우클릭 메뉴에서 열린다.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow(HelpTopic topic = HelpTopic.Widget)
    {
        InitializeComponent();

        TopicTabs.SelectedItem = topic switch
        {
            HelpTopic.Google => GoogleTab,
            HelpTopic.Holiday => HolidayTab,
            _ => WidgetTab,
        };
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // 기본 브라우저로 연다 (UseShellExecute 없이는 .NET Core에서 URL을 못 연다)
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
