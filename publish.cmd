@echo off
rem 바탕화면 달력 - 배포용 실행파일 만들기
rem 파일 하나(DesktopCalendar.exe)만 만들어집니다. 그 파일만 다른 PC로 옮겨도 실행됩니다.
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
echo   바탕화면 달력 - 배포용 실행파일 만들기
echo ================================================
echo.

rem 실행 중이면 파일이 잠겨서 덮어쓸 수 없다
call :check_running DesktopCalendar.exe
if errorlevel 1 goto :running
call :check_running DesktopCalendar.App.exe
if errorlevel 1 goto :running

echo 빌드 중입니다. 처음에는 몇 분 걸릴 수 있습니다...
echo.
"%DOTNET%" publish src\DesktopCalendar.App\DesktopCalendar.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist
if errorlevel 1 goto :failed

rem 만들어지는 이름은 프로젝트 이름을 따라가므로 보기 좋은 이름으로 바꾼다
if not exist "dist\DesktopCalendar.App.exe" goto :failed
move /y "dist\DesktopCalendar.App.exe" "dist\DesktopCalendar.exe" >nul
if errorlevel 1 goto :failed

echo.
echo ================================================
echo   완료
echo.
echo   실행파일: %CD%\dist\DesktopCalendar.exe
echo.
echo   이 파일 하나만 USB나 다른 PC로 옮겨도 실행됩니다.
echo   (.NET 설치 불필요, 약 70MB)
echo.
echo   * 일정 데이터는 실행파일이 아니라 각 PC의
echo     %%AppData%%\DesktopCalendar\calendar.db 에 저장됩니다.
echo     기존 일정을 함께 옮기려면 앱의 [백업 / 내보내기]를 쓰세요.
echo   * 서명이 없어 처음 실행 시 SmartScreen 경고가 뜰 수 있습니다.
echo     [추가 정보] - [실행] 을 누르면 됩니다.
echo ================================================
echo.

choice /c YN /n /m "dist 폴더를 열까요? (Y/N) "
if errorlevel 2 goto :end
explorer "%CD%\dist"

:end
exit /b 0

rem --- 프로세스가 떠 있으면 errorlevel 1 ---
:check_running
tasklist /fi "imagename eq %~1" 2>nul | find /i "%~1" >nul
if errorlevel 1 exit /b 0
exit /b 1

:running
echo   [!] 달력 앱이 실행 중입니다.
echo       트레이 아이콘에서 종료한 뒤 다시 실행하세요.
echo.
pause
exit /b 1

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
