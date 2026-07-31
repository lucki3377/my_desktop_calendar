using System.Windows;

namespace DesktopCalendar.App;

public partial class ApiKeyDialog : Window
{
    public string? ApiKey { get; private set; }

    public ApiKeyDialog(string? currentKey)
    {
        InitializeComponent();
        ApiKeyBox.Text = currentKey ?? string.Empty;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyBox.Text))
        {
            MessageBox.Show("서비스 키를 입력하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ApiKey = ApiKeyBox.Text.Trim();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
