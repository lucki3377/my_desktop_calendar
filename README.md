# 바탕화면 배경 달력

윈도우 바탕화면에 붙박이로 떠 있는 달력 위젯입니다. 배경화면 위, 데스크톱 아이콘 뒤에 자리잡아서
창을 띄우지 않아도 이번 달 일정이 늘 보입니다.

![바탕화면에 올라간 달력 위젯](docs/screenshot.png)

## 기능

- **일정** — 등록·수정·삭제, 여러 날에 걸친 일정, 색상 구분, 시작 전 알림
- **반복** — 매일 / 매주 / 매월 / 매년, 그리고 **음력 매년**(음력 생일·제사). 특정 회차만 건너뛸 수 있습니다
- **음력** — 음력으로 날짜를 골라 등록하고, 달력 각 날짜 옆에 음력을 함께 표시합니다
- **공휴일** — 대체공휴일까지 앱이 직접 계산해 API 키 없이도 표시됩니다.
  공공데이터포털 키를 넣으면 임시공휴일(선거일 등)까지 정확히 받아옵니다
- **D-day** — 위젯 위쪽에 남은 일수 표시, 매년 반복 지원
- **구글 캘린더** — 읽기 전용으로 가져와 로컬 일정과 함께 보여줍니다 (표시 여부 토글 가능)
- **꾸미기** — 배경 색상·투명도, 글자 크기, 주 시작 요일(일/월)
- **백업** — 일정·D-day를 파일로 저장/복원, iCalendar(.ics) 내보내기

## 설치

[.NET 8 데스크톱 런타임](https://dotnet.microsoft.com/download/dotnet/8.0)이 있으면 소스에서 바로 빌드할 수 있습니다.

```bash
git clone https://github.com/lucki3377/my_desktop_calendar.git
cd my_desktop_calendar
dotnet run --project src/DesktopCalendar.App
```

런타임 없이 실행되는 단일 폴더로 만들려면:

```bash
dotnet publish src/DesktopCalendar.App -c Release -r win-x64 --self-contained -o publish
```

서명된 배포본이 아니라서 처음 실행할 때 SmartScreen이 "Windows에서 PC를 보호했습니다" 경고를 띄울 수 있습니다.
[추가 정보] → [실행]으로 넘어가면 됩니다.

## 사용법

위젯을 **우클릭**하면 설정·D-day 관리·도움말이 나옵니다. 트레이 아이콘에서도 같은 메뉴를 열 수 있습니다.

- 드래그로 옮기고, 오른쪽 아래 모서리로 크기를 조절합니다. 자리를 잡았으면 [위치 잠금]
- 날짜를 클릭하면 그 날의 일정 목록이 열립니다
- 구글 캘린더 연동과 공휴일 API 키 발급 절차는 앱 안의 **[도움말]** 에 단계별로 정리해 두었습니다

## 설정과 데이터

일정·설정은 `%AppData%\DesktopCalendar\calendar.db` 한 곳에 저장됩니다.
구글 로그인 정보와 클라이언트 보안 비밀은 Windows DPAPI로 암호화해서 넣습니다.

이 파일 하나에만 있으니, PC를 초기화하면 함께 사라집니다.
설정 → 데이터 → [백업 파일로 저장]으로 가끔 다른 곳에 복사해 두세요.

## 만드는 데 쓴 것

C# / WPF (.NET 8). 배경화면과 아이콘 사이에 창을 넣는 것은 `WorkerW` 방식을 씁니다
(Rainmeter 등이 쓰는 것과 같은 원리).

| 용도 | 패키지 |
|---|---|
| 저장 | Microsoft.Data.Sqlite |
| 구글 캘린더 | Google.Apis.Calendar.v3, Google.Apis.Auth |
| 트레이 아이콘 | Hardcodet.NotifyIcon.Wpf |
| 토큰 암호화 | System.Security.Cryptography.ProtectedData |

설계와 진행 기록은 [DESIGN.md](DESIGN.md)에 있습니다.

## 라이선스

[MIT](LICENSE)
