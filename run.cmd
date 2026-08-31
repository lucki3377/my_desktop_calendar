@echo off
rem 바탕화면 달력 - 소스에서 바로 실행 (개발용)
setlocal
chcp 65001 >nul
cd /d "%~dp0"

rem --- .NET SDK 찾기 (PATH에 없으면 기본 설치 경로) ---
set "DOTNET="
where dotnet >nul 2>&1 && set "DOTNET=dotnet"
if not defined DOTNET if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET goto :no_sdk

set "HAS_SDK="
for /f "delims=" %%i in ('"%DOTNET%" --list-sdks 2^>nul') do set "HAS_SDK=1"
if not defined HAS_SDK goto :no_sdk

rem 이미 떠 있으면 두 개가 겹쳐 보이므로 막는다
tasklist /fi "imagename eq DesktopCalendar.App.exe" 2>nul | find /i "DesktopCalendar.App.exe" >nul
if not errorlevel 1 (
    echo   [!] 달력 앱이 이미 실행 중입니다. 트레이 아이콘에서 종료한 뒤 다시 실행하세요.
    echo.
    pause
    exit /b 1
)

echo 바탕화면 달력을 실행합니다. 이 창을 닫으면 앱도 함께 종료됩니다.
echo.
"%DOTNET%" run --project src\DesktopCalendar.App\DesktopCalendar.App.csproj
if errorlevel 1 goto :failed
exit /b 0

:failed
echo.
echo   [!] 실행에 실패했습니다. 위의 오류 메시지를 확인하세요.
echo.
pause
exit /b 1

:no_sdk
echo.
echo   [!] .NET 8 SDK를 찾지 못했습니다.
echo.
echo   런타임만 설치돼 있어도 빌드는 되지 않습니다. SDK를 설치하세요:
echo     winget install Microsoft.DotNet.SDK.8
echo   또는 https://dotnet.microsoft.com/download/dotnet/8.0
echo.
pause
exit /b 1
