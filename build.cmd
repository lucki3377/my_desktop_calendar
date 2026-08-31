@echo off
rem 바탕화면 달력 - 빌드 + 테스트 (개발용)
rem 더블클릭하거나 명령 프롬프트에서 build.cmd 로 실행하세요.
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

echo ================================================
echo   바탕화면 달력 - 빌드 + 테스트
echo ================================================
echo.

echo [1/2] 빌드 중...
"%DOTNET%" build DesktopCalendar.sln
if errorlevel 1 goto :failed

echo.
echo [2/2] 테스트 중...
"%DOTNET%" test DesktopCalendar.sln --no-build
if errorlevel 1 goto :failed

echo.
echo ================================================
echo   완료. 앱을 실행해 보려면:
echo     run.cmd
echo   배포용 실행파일을 만들려면:
echo     publish.cmd
echo ================================================
echo.
pause
exit /b 0

:failed
echo.
echo   [!] 실패했습니다. 위의 오류 메시지를 확인하세요.
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
