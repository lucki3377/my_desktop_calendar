using System.Security.Cryptography;
using System.Text;

namespace DesktopCalendar.Core.Google;

/// <summary>
/// Windows DPAPI(CurrentUser 범위)로 문자열을 암/복호화한다.
/// DESIGN.md 결정 사항: 구글 Refresh Token 등 인증 정보는 평문으로 저장하지 않는다.
/// </summary>
public static class TokenProtector
{
    public static string ProtectToBase64(string plainText)
    {
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>복호화한다. 다른 사용자/PC에서 만들어진 값이거나 형식이 깨졌으면 null.</summary>
    public static string? UnprotectFromBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
            return null;

        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(base64), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
