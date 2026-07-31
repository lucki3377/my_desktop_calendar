# 바탕화면 배경 달력 앱 설계 문서

> 이 문서는 여러 세션에 걸쳐 이어서 작업하기 위한 설계/진행상황 기록입니다.
> 새 세션에서 작업을 이어갈 때는 이 문서 전체를 먼저 읽고, 맨 아래 **"진행 상황 로그"** 를 확인한 뒤 작업하세요.
> 결정된 사항은 "결정 사항"으로, 아직 안 정한 것은 "미해결 질문"에 유지하고, 결정되면 문서를 갱신하세요.

## 1. 목표

윈도우 바탕화면(데스크톱 아이콘 뒤, 배경화면 위)에 항상 떠 있는 달력 위젯 프로그램.

### 요구사항 (원본)
- [x] 한국 공휴일 정보를 가져와서 휴일 표시 (주말 / 공휴일 / 임시공휴일) — Phase 3 완료
- [x] 일정 등록 / 수정 / 삭제 (로컬) — Phase 2 완료 (여러 날에 걸친 일정도 지원, 2026-07-31 추가)
- [ ] 구글 캘린더 연동 기능 제공
- [ ] 연동된(구글) 일정의 표시 여부를 설정으로 켜고 끌 수 있음
- [x] D-day 계산 기능 — Phase 4 완료

## 2. 결정 사항 (Decisions)

| 항목 | 결정 | 비고 |
|---|---|---|
| 기술 스택 | **C# / WPF (.NET, 최신 LTS)** | Win32 상호운용(WorkerW 삽입)이 쉽고 네이티브 성능/트레이 아이콘/자동시작 구현이 간단 |
| 일정 데이터 저장 | **로컬 파일 (SQLite)** | 이 PC에서만 사용. 구글 캘린더는 "조회 전용" 연동 — 로컬 일정을 구글로 올리지 않음 |
| 배경 삽입 방식 | **WorkerW 트릭** (Rainmeter/Wallpaper Engine과 동일 원리) | 아래 4.1 참조 |
| 위젯 형태 | **고정 크기/위치의 사각 위젯** (풀스크린 오버레이 아님) | 클릭스루(click-through) 로직 없이 아이콘 뒤에 박히는 사각 패널로 구현 → 구현 난이도 대폭 감소. 위치/크기는 사용자가 설정에서 드래그/조절 후 저장 |
| DB 접근 | **Microsoft.Data.Sqlite** (경량, EF Core 없이 직접 SQL) | 스키마가 단순하므로 ORM 불필요 |
| 구글 인증 토큰 저장 | **Windows DPAPI**(`ProtectedData`)로 암호화 후 로컬 저장 | 평문 저장 금지 |
| 다중 모니터 | **단일 위젯만 지원** | 가상 화면 기준 좌표에 위젯 1개. 모니터별 다중 위젯은 범위 밖 |
| 패키징 | **단순 self-contained exe** | 설치 프로그램/MSIX 없이 폴더에 두고 실행. 단, SmartScreen/백신 오탐 방지 조치 필요 → 4.9 참조 |
| 구글 OAuth Client Secret | **사용자 본인이 Google Cloud Console에서 직접 발급 후 설정에 입력** | 앱에 하드코딩하지 않음. 개인 사용 목적이므로 쿼터/보안 이슈 없음 |
| 구글 캘린더 표시 토글 | **전체 on/off만** (계정/캘린더별 세분화 없음) | 최초 버전 범위. 필요해지면 후속 버전에서 확장 |
| 위젯 클릭 동작 | **위젯 영역은 클릭 가능(일정 편집), 그 외 데스크톱 클릭은 통과 안 됨** | 완전 click-through 미구현 — 위젯이 차지하는 사각형 영역은 일반 창처럼 동작, 그 바깥은 애초에 위젯이 없으므로 아이콘 클릭에 영향 없음 |
| 가독성 확보 | **반투명 패널 배경**(반투명 검정/흰색 + 그 위에 텍스트) | 투명도는 설정에서 조절 가능하게 함 |
| 로컬↔구글 동기화 방향 | **단방향 (구글 → 로컬 조회만)** | 로컬 일정을 구글로 올리지 않음. 4.4/4.6과 일치 |

## 3. 전체 아키텍처

```
┌─────────────────────────────────────────────┐
│  DesktopCalendar.App (WPF, 진입점)            │
│  - App.xaml: 트레이 아이콘, 자동시작 등록      │
│  - MainWidgetWindow: WorkerW에 삽입되는 창    │
│  - SettingsWindow: 일반 창(태스크바에 표시)    │
└───────────────┬───────────────────────────────┘
                │
   ┌────────────┼─────────────┬───────────────┬───────────────┐
   ▼            ▼             ▼               ▼               ▼
Core.Desktop  Core.Calendar  Core.Holiday   Core.Google    Core.Storage
(WorkerW      (일정 모델,     (공공데이터    (OAuth,        (SQLite
 삽입 로직)    D-day 계산)     API 연동,      Calendar API   접근 계층,
                              캐싱)          호출, 캐싱)     설정 저장)
```

레이어 원칙:
- `Core.*` 프로젝트들은 WPF에 의존하지 않는 순수 로직 (테스트 가능하게)
- `App` 프로젝트만 UI/Win32 특화 코드를 가짐 (단, `Core.Desktop`은 Win32 P/Invoke 자체를 담당해도 됨 — UI가 아니라 OS 연동이므로)

## 4. 핵심 기술 설계

### 4.1 배경 렌더링 (WorkerW 삽입)

원리 (Rainmeter, Wallpaper Engine과 동일):
1. `Progman`(바탕화면 관리자) 창 핸들을 `FindWindow("Progman", null)`로 획득
2. `SendMessageTimeout(progman, 0x052C, ...)` 메시지를 보내면 Windows가 배경화면 렌더링용 `WorkerW` 창을 하나 생성함 (내부 미문서화 동작이지만 오랫동안 안정적으로 동작)
3. `EnumWindows`로 `SHELLDLL_DefView`(아이콘들이 들어있는 창)를 자식으로 가진 `WorkerW`를 찾고, 그 바로 다음 형제 `WorkerW`(빈 것)를 찾음 → 이게 "배경화면과 아이콘 사이" 레이어
4. WPF 창의 `HWND`를 얻어(`WindowInteropHelper`) `SetParent(myHwnd, targetWorkerW)` 호출 → 이제 내 창은 아이콘 뒤, 배경화면 위에 위치
5. 창 스타일: `WS_EX_TOOLWINDOW`(태스크바/Alt+Tab에서 숨김), 배경 투명(`AllowsTransparency`, `Background=Transparent`), 테두리 없음

주의사항:
- 탐색기(explorer.exe)가 재시작되면 WorkerW가 새로 생성되므로, `SetParent`를 주기적으로(또는 explorer 재시작 감지 시) 재수행해야 함
- 다중 모니터: WorkerW는 전체 가상 화면 크기를 가지므로 위젯 좌표는 가상 화면 기준 절대좌표로 저장
- 이 방식은 문서화되지 않은 동작이라 향후 Windows 업데이트로 깨질 수 있음. WorkerW 삽입 실패 시 폴백(예: "항상 최하단 일반 창" 모드)은 최초 버전 범위 밖 — 필요해지면 섹션 9에 새 항목으로 추가

### 4.2 한국 공휴일 연동

공공데이터포털(data.go.kr) **"한국천문연구원_특일 정보"** API 사용:
- Base: `http://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService`
- 주요 오퍼레이션:
  - `getHoliDeInfo`: 공휴일 (법정 공휴일 + 대체공휴일 포함, 정부가 임시공휴일 지정 시 업데이트됨)
  - `getRestDeInfo`: 국경일 등 (필요시)
  - `get24DivisionsInfo`, `getSundryDayInfo`: 24절기/잡절 (선택적, 요구사항에는 없음 — 스킵 가능)
- 인증: data.go.kr에서 개인이 서비스 신청 후 발급받는 인증키(Encoding/Decoding key) 필요 → **사용자가 직접 발급받아 설정 화면에 입력**하는 구조로 설계 (앱에 키를 하드코딩하지 않음)
- 연 단위로 호출(`solYear=2026`) → 결과를 로컬 SQLite `Holiday` 테이블에 캐싱, 앱 시작 시 "캐시에 다음 연도 데이터가 없으면" 자동 호출
- **임시공휴일**: 정부가 임시공휴일을 지정하면 공공데이터포털 API 데이터도 갱신되지만 반영 시점에 지연이 있을 수 있음 → 설정에서 "공휴일 수동 추가/제외" 기능으로 사용자가 직접 보정 가능하게 함 (`Holiday.Source = Manual`)
- 주말(토/일)은 API 호출 없이 `DayOfWeek`로 로컬 계산

### 4.3 로컬 일정 CRUD

- 월간 그리드 뷰에서 날짜 클릭 → 그 날짜의 일정 목록 팝업(추가/수정/삭제)
- 필드: 제목, **시작 날짜/종료 날짜(별도 지정 가능 → 여러 날에 걸친 일정 지원)**, 시작/종료 시간, 종일 여부, 메모, 색상 태그 (2026-07-31 변경 — 처음엔 하루짜리만 지원했으나 사용자 요청으로 시작일≠종료일 다일 일정 지원 추가)
- 다일 일정은 겹치는 모든 날짜 셀에 동일하게 칩으로 표시됨 (`RenderMonth`의 날짜별 그룹핑이 StartAt~EndAt 범위를 순회)
- SQLite `Schedule` 테이블에 저장, `Source = Local`

### 4.4 구글 캘린더 연동

- Google Cloud Console에서 **사용자가 직접** OAuth 2.0 클라이언트(Desktop app 유형) 생성 후 Client ID/Secret을 앱에 입력하는 구조 (앱 배포 시 자체 client secret을 임베드하지 않는 것을 기본으로 함 — 배포 방식 확정되면 재검토)
- 라이브러리: `Google.Apis.Calendar.v3`, `Google.Apis.Auth`
- 흐름: 설정 화면에서 "구글 계정 연결" → 브라우저에서 OAuth 동의 → Refresh Token을 DPAPI로 암호화해 로컬 저장
- 동기화: `Events.List` API로 지정 기간(예: 이번달 ±1개월) 이벤트를 가져와 로컬 캐시 테이블(`GoogleEventCache`)에 저장 (읽기 전용 — 로컬 일정을 구글로 쓰지 않음, 최초 버전 범위)
- 다중 캘린더: 사용자가 연동할 구글 캘린더(캘린더 목록 중 선택, 예: "가족" 캘린더만) 체크박스로 선택 가능하게 설계
- 동기화 주기: 앱 시작 시 1회 + N분마다(설정 가능, 기본 30분) 폴링. Push notification(webhook)은 로컬 앱엔 부적합하므로 폴링 방식 채택

### 4.5 연동 일정 표시 여부 설정

- 설정에 `ShowGoogleEvents: bool` 토글
- 위젯 렌더링 시: 로컬 `Schedule` + (토글 켜짐이면) `GoogleEventCache`를 병합해 날짜별로 표시
- 계정/캘린더별 개별 on/off는 최초 버전 범위 밖 (결정 사항 참조)

### 4.6 D-day 계산

- 별도 `DDay` 테이블: 제목, 대상 날짜, 매년 반복 여부(생일 등)
- 계산: `(TargetDate.Date - Today.Date).Days` → "D-7", "D-DAY", "D+3" 형태로 표시
- 반복(매년) 항목은 표시 시점에 "올해 기준 가장 가까운 발생일"로 환산해서 계산
- 위젯에 D-day 리스트를 최대 N개(설정 가능) 상단에 노출

### 4.7 UI 레이아웃 (위젯)

- 월간 캘린더 그리드(요일 헤더 + 6주 grid)
- 셀 표시 규칙: 주말(파란/빨강), 공휴일/임시공휴일(빨강 + 이름 툴팁), **일정은 날짜 셀 안에 최대 3개까지 제목(칩 형태)으로 미리 표시**, 초과분은 "+N개"로 표시(2026-07-31 변경 — 기존 계획이던 단순 점(dot) 표시 대신, 사용자 피드백으로 실제 일정 제목을 셀에서 바로 보이게 함)
- 날짜 셀 클릭 시 그 날짜의 전체 일정 목록 팝업(DayEventsWindow) — 미리보기에서 넘친 일정도 여기서 확인
- 월 표시 텍스트(예: "2026년 7월 ▾") 클릭 시 년/월을 직접 선택해 이동하는 팝업(MonthPickerWindow) — 좌우 화살표(◀▶)로 한 달씩 이동하는 것과 별개로 제공 (2026-07-31 추가, 사용자 요청)
- 상단 또는 하단에 D-day 리스트 영역
- 우클릭 컨텍스트 메뉴: 설정 열기, 위젯 위치 이동 잠금/해제, 종료
- 트레이 아이콘: 좌클릭 설정 열기, 우클릭 메뉴(종료 등)
- 위젯 기본 크기를 320x400 → **560x640으로 확대**(2026-07-31, 사용자 요청 — 일정이 한눈에 보여야 함)
- **글자 크기 조절**: 우클릭 메뉴 "글자 크기 조절..." → 슬라이더(70%~200%)로 조절하는 `FontSizeDialog`. `Widget.FontScale`로 저장, 모든 달력 텍스트(월/요일/날짜/공휴일명/일정 칩/안내문)에 배율 적용 (2026-07-31 추가, 사용자 요청 — 숫자/텍스트가 너무 작다는 피드백)

### 4.8 설정 저장

- `Settings` 테이블(or 단순 JSON 파일 `settings.json`) — SQLite에 key-value로 저장 권장(단일 저장소로 통일)
- 저장 항목: 위젯 X/Y/W/H, ShowGoogleEvents, 공공데이터 API 키, 구글 OAuth 상태, 동기화 주기, D-day 표시 개수, 테마(배경 대비용 밝기)

### 4.9 패키징 및 SmartScreen/백신 오탐 대응

단순 self-contained exe로 배포하되, Windows Defender SmartScreen이나 백신 프로그램이 "알 수 없는 게시자" 실행 파일을 위험하다고 오탐하는 것을 최대한 방지:

- **코드 서명**: 가능하면 코드 서명 인증서(개인/조직 EV 또는 OV)로 exe에 서명. 서명이 없으면 SmartScreen이 "Windows에서 PC를 보호했습니다" 경고를 띄울 가능성이 높음 (평판이 쌓이기 전까지는 서명해도 초기엔 경고가 나올 수 있음)
- **트리밍/난독화 지양**: `PublishTrimmed`, 코드 난독화, UPX 등 실행파일 패커는 백신 휴리스틱 탐지에 걸릴 확률을 높이므로 사용하지 않음. 일반적인 `dotnet publish -c Release --self-contained` 결과물을 그대로 배포
- **불필요한 권한 최소화**: 관리자 권한 요구 없이 일반 사용자 권한으로 실행되게 유지 (매니페스트에 `asInvoker`), Win32 후킹은 WorkerW `SetParent`/`FindWindow`/`EnumWindows` 등 표준 API만 사용 (프로세스 인젝션, 후킹 API 등 악성코드가 흔히 쓰는 패턴 회피)
- **자동시작 등록 방식**: 레지스트리 `Run` 키에 직접 쓰기보다, 가능하면 `Microsoft.Win32.TaskScheduler`나 시작프로그램 폴더 바로가기처럼 사용자가 눈으로 확인 가능한 방식을 우선 검토 (은닉성 낮은 방식일수록 오탐 확률 감소)
- **평판 축적**: 서명을 하더라도 신규 인증서는 SmartScreen 평판이 없어 처음엔 경고가 뜰 수 있음 — 다운로드/실행 수가 쌓이면 자연히 완화됨. 급하면 Microsoft에 오탐 신고(false positive submission)로 조기 해제 요청 가능
- 위 조치는 "완전히 차단 안 됨"을 보장하지 않음 — 최종 확인은 실제 빌드 후 VirusTotal 등으로 점검

## 5. 데이터 모델

```
Schedule (로컬 일정)
- Id: GUID (PK)
- Title: string
- Description: string?
- StartAt: datetime
- EndAt: datetime
- IsAllDay: bool
- Color: string?
- CreatedAt / UpdatedAt: datetime

Holiday (공휴일 캐시)
- Date: date (PK)
- Name: string
- Kind: enum(PublicHoliday, SubstituteHoliday, TemporaryHoliday, Manual)
- Source: enum(Api, Manual)

GoogleAccount
- Id: GUID (PK)
- Email: string
- EncryptedRefreshToken: blob
- ConnectedCalendarIds: string (JSON 배열)
- LastSyncedAt: datetime?

GoogleEventCache (읽기 전용 캐시)
- Id: string (구글 이벤트 ID, PK)
- CalendarId: string
- Title: string
- StartAt / EndAt: datetime
- IsAllDay: bool
- FetchedAt: datetime

DDay
- Id: GUID (PK)
- Title: string
- TargetDate: date
- IsRecurringYearly: bool

Settings (key-value)
- Key: string (PK)
- Value: string
```

## 6. 프로젝트 폴더 구조 (예정)

```
캘린더/
├─ DESIGN.md                     (이 문서)
├─ DesktopCalendar.sln
├─ src/
│  ├─ DesktopCalendar.App/       (WPF 진입점, MainWidgetWindow, SettingsWindow, TrayIcon)
│  ├─ DesktopCalendar.Core.Desktop/   (WorkerW P/Invoke 로직)
│  ├─ DesktopCalendar.Core.Calendar/  (Schedule, DDay 모델 및 로직)
│  ├─ DesktopCalendar.Core.Holiday/   (공공데이터 API 클라이언트, 캐싱)
│  ├─ DesktopCalendar.Core.Google/    (OAuth, Calendar API 클라이언트)
│  └─ DesktopCalendar.Core.Storage/   (SQLite 접근 계층, Settings)
└─ tests/
   └─ DesktopCalendar.Core.Tests/    (D-day 계산, 공휴일 파싱 등 순수 로직 단위테스트)
```

## 7. 외부 의존성

| 용도 | 패키지/서비스 | 사용자 준비물 |
|---|---|---|
| 공휴일 | 공공데이터포털 "한국천문연구원 특일정보" | data.go.kr 가입 + API 키 발급 (사용자가 설정에 입력) |
| 구글 캘린더 | `Google.Apis.Calendar.v3`, `Google.Apis.Auth` NuGet | Google Cloud Console OAuth Client ID/Secret 발급 |
| DB | `Microsoft.Data.Sqlite` NuGet | 없음 |
| 트레이 아이콘 | `Hardcodet.NotifyIcon.Wpf` (또는 동급) | 없음 |
| 토큰 암호화 | `System.Security.Cryptography.ProtectedData` | 없음 |

## 8. 개발 로드맵 (세션 간 이어서 진행)

각 Phase는 독립적으로 커밋 가능한 단위로 설계. 완료된 항목은 `[x]`로 체크하고, 다음 세션은 미완료 항목부터 진행.

### Phase 0 — 프로젝트 셋업
- [x] .sln + 프로젝트 구조 생성 (`src/` 하위 5개 프로젝트 + `tests/`)
- [x] git 저장소 초기화 (`git init`), `.gitignore` (bin/obj, *.user, appsettings.local 등)
- [x] NuGet 패키지 설치 (Sqlite, NotifyIcon, Google API, DPAPI 등)

### Phase 1 — 배경 렌더링 코어 ✅ 완료 (2026-07-31)
- [x] `Core.Desktop`: WorkerW 탐색/삽입 Win32 로직 구현 (`DesktopBackgroundHost`, `NativeMethods`)
- [x] `App`: 투명 배경의 빈 사각 창(`WidgetWindow`)을 WorkerW에 삽입 → 데스크톱 아이콘 뒤에 뜨는지 육안 확인 (스크린샷으로 검증 완료 — 아이콘이 위젯 패널 위에 그려짐)
- [x] explorer.exe 재시작 감지 후 재삽입 로직 (`DispatcherTimer` 5초 주기로 `EnsureAttached` 호출)
- [x] 위젯 위치/크기 드래그 이동 + 잠금 토글 + Settings 저장 (`SettingsStore` key-value, Widget.Left/Top/Width/Height/Locked)

### Phase 2 — 로컬 일정 + 달력 UI ✅ 완료 (2026-07-31)
- [x] SQLite 스키마 생성/마이그레이션 (`ScheduleRepository.EnsureSchema`)
- [x] 월간 캘린더 그리드 렌더링 (오늘 날짜 강조, 요일 헤더, 주말 색상) — 사용자 피드백으로 위젯 크기 560x640으로 확대
- [x] 일정 등록/수정/삭제 UI (`DayEventsWindow` + `ScheduleEditorWindow`) — 날짜 셀에는 dot 대신 **일정 제목을 최대 3개 칩으로 미리보기**, 초과분은 "+N개"
- [x] 년/월 직접 이동 팝업(`MonthPickerWindow`, 사용자 요청으로 추가) — 헤더의 "yyyy년 M월 ▾" 텍스트 클릭으로 열림

### Phase 3 — 공휴일 연동 ✅ 완료 (2026-07-31)
- [x] 공공데이터포털 API 클라이언트 (`HolidayApiClient.GetHolidaysAsync`, `getHoliDeInfo` 호출 + JSON 파싱, `System.Text.Json`)
- [x] 연도별 캐싱(`HolidayRepository` + `HolidayCachedYear` 테이블) + 달 표시 시 미캐시 연도 자동 비동기 조회(`EnsureHolidaysForYearAsync`)
- [x] 주말(파란/빨강) + 공휴일 빨간색 렌더링, **공휴일 이름을 날짜 셀 안에 직접 텍스트로 표시**(2026-07-31 변경 — 처음엔 툴팁만 계획했으나 사용자 요청으로 셀에 인라인 표시로 변경, 일정 칩과 같은 위치 논리)
- [x] 공휴일 수동 추가/제외 UI — `DayEventsWindow`에 "공휴일로 추가"/"공휴일 해제" 토글 버튼 + `SimpleInputDialog`(이름 입력)
- [x] API 키 입력 UI (`ApiKeyDialog`, 위젯 우클릭 메뉴 "공휴일 API 키 설정..."에서 진입) — 키는 사용자가 data.go.kr에서 직접 발급받아 입력, 앱에 하드코딩 안 함

### Phase 4 — D-day ✅ 완료 (2026-07-31)
- [x] DDay CRUD(`DDayRepository`) + 계산 로직(`DDayCalculator`, 매년 반복 시 "다가오는 가장 가까운 발생일"로 환산, 2/29 생일은 평년에 2/28로 보정) + 단위테스트 7개 전부 통과
- [x] 위젯 상단에 D-day 리스트 영역 렌더링(`DDayPanel`, 남은 일수 적은 순 최대 5개 칩), 클릭하면 `DDayListWindow`(추가/수정/삭제) 오픈. 우클릭 메뉴 "D-day 관리..."로도 접근 가능

### Phase 5 — 구글 캘린더 연동
- [ ] OAuth 클라이언트 ID/Secret 설정 UI
- [ ] OAuth 로그인 플로우 + Refresh Token DPAPI 암호화 저장
- [ ] 캘린더 목록 조회 + 연동할 캘린더 선택 UI
- [ ] 이벤트 폴링 동기화 (주기 설정 가능) + 로컬 캐시 저장
- [ ] `ShowGoogleEvents` 토글 및 위젯 렌더링에 병합 반영

### Phase 6 — 마무리
- [ ] 트레이 아이콘 + 컨텍스트 메뉴(설정/종료)
- [ ] Windows 시작 시 자동 실행 등록/해제
- [ ] 설정 창 UI 정리 (전체 항목 한 곳에서 관리)
- [ ] self-contained exe 빌드 (`dotnet publish -c Release --self-contained`), 4.9 조치 적용 후 VirusTotal 점검

## 9. 미해결 질문

- [x] **Windows Smart App Control 차단 문제** — 2026-07-31 **해결됨**: 이 개발 PC에서 Smart App Control이 새로 빌드한 서명 안 된 DLL 로드를 계속 차단해 앱이 크래시했음(`FileLoadException` 0x800711C7). `Unblock-File`(MOTW 제거)은 효과 없었음(애초에 Zone.Identifier 스트림이 없는 로컬 빌드라 지울 게 없었음 — SAC의 평판 기반 정책 차단이지 MOTW 문제가 아니었음). 사용자가 Windows 보안 > 앱 및 브라우저 컨트롤 > Smart App Control에서 직접 끔 → 이후 정상 실행 확인. **개발 중에는 SAC를 꺼두는 것이 일반적인 관행**(SAC는 앱 단위 예외를 지원하지 않아 개발 워크플로우와 근본적으로 안 맞음 — MS Q&A 커뮤니티에서도 동일 안내). 배포 단계에서는 4.9절(코드 서명 등)로 별도 대응 예정. 참고: 2026년 4월 누적 업데이트(KB5083769)부터 SAC를 재설치 없이 다시 켤 수 있음(이전엔 비가역적이었음).

## 10. 진행 상황 로그

> 새 세션에서 작업을 진행한 뒤, 아래에 날짜 + 요약을 append 하세요.

- **2026-07-31**: 최초 설계 문서 작성. 기술 스택(WPF), 저장 방식(로컬 SQLite) 결정. 아직 코드 없음 — Phase 0부터 시작 필요.
- **2026-07-31**: 미해결 질문 7개 전부 결정 완료 (다중 모니터=단일 위젯, 패키징=단순 exe+SmartScreen 대응, 구글 Client Secret=사용자 직접 발급, 표시 토글=전체 on/off, 클릭 동작=위젯만 클릭 가능, 가독성=반투명 패널, 동기화=단방향). 4.9 "패키징 및 SmartScreen/백신 오탐 대응" 섹션 신규 추가. 아직 코드 없음 — 다음 세션은 Phase 0부터 시작.
- **2026-07-31**: Phase 0 완료. .NET 8 SDK를 winget으로 설치(이 PC에 없었음). `DesktopCalendar.sln` + 7개 프로젝트(App, Core.Desktop/Calendar/Holiday/Google/Storage, Core.Tests) 생성 및 참조 연결(App→모든 Core, Core.Calendar/Holiday/Google→Core.Storage, Tests→Core.Calendar/Holiday). NuGet 패키지 설치: Microsoft.Data.Sqlite(Core.Storage), Google.Apis.Calendar.v3 + Google.Apis.Auth + System.Security.Cryptography.ProtectedData(Core.Google), Hardcodet.NotifyIcon.Wpf.NetCore(App). `dotnet build` 전체 성공(경고/오류 0개). `git init` + `.gitignore` 추가, 아직 커밋은 안 함(사용자가 명시적으로 요청 안 함). 참고: 이 PC의 PowerShell 툴 세션은 PATH가 갱신 안 돼서 dotnet 호출 시 `C:\Program Files\dotnet\dotnet.exe` 전체 경로를 써야 함. 다음 세션은 Phase 1(WorkerW 배경 렌더링 코어)부터 시작.
- **2026-07-31**: Phase 1 완료.
  - `Core.Desktop`에 `NativeMethods`(P/Invoke: FindWindow/FindWindowEx/SendMessageTimeout/EnumWindows/SetParent/GetWindowLong/SetWindowLong)와 `DesktopBackgroundHost`(WorkerW 탐색 `FindWorkerW()`, 부착 `Attach()`, 재부착 `EnsureAttached()`, Alt+Tab 숨김 `HideFromTaskbarAndAltTab()`) 구현.
  - `Core.Storage`에 `AppPaths`(%AppData%\DesktopCalendar\calendar.db 경로)와 `SettingsStore`(SQLite 기반 key-value, Get/SetString·Double·Bool) 구현 — Phase 2에서 재사용/확장 예정.
  - `App`에 `WidgetWindow`(기존 MainWindow 대체) 구현: 투명/무테두리 창, 반투명 패널(#B3202020) + 오늘 날짜 표시, 드래그 이동, 우하단 Thumb으로 크기 조절, 우클릭 메뉴(위치 잠금 토글/종료), `SettingsStore`로 위치·크기·잠금 상태 저장/복원, `DispatcherTimer`(5초)로 `EnsureAttached` 호출해 explorer 재시작 대응.
  - **실제 검증**: 앱을 빌드·실행 후 Win32 `Shell.Application.MinimizeAll()`로 바탕화면을 노출시키고 스크린샷 캡처 → 위젯 패널이 렌더링되고, "Docker Desktop" 등 데스크톱 아이콘이 위젯 패널 **위에** 그려지는 것을 확인(= WorkerW 레이어 삽입 성공, 아이콘 뒤 배치 확인됨). `%AppData%\DesktopCalendar\calendar.db` 생성도 확인.
  - **미검증 항목**: 드래그 이동/크기조절/잠금 토글은 코드 리뷰 수준으로만 확인, 실제 마우스 인터랙션(사람이 직접 드래그)으로는 테스트 못 함 — 다음에 앱 실행 중일 때 사용자가 직접 드래그/우클릭 메뉴를 눌러보고 이상 있으면 알려줄 것.
  - `dotnet build` 전체 경고/오류 0개. 아직 git commit은 안 함.
  - 다음 세션은 Phase 2(로컬 일정 CRUD + 달력 그리드 UI)부터 시작.
- **2026-07-31**: Phase 2 코드 작성 완료, 그러나 **실행 검증 중 Windows Smart App Control 차단 발견 (섹션 9 참조, 미해결)**.
  - `Core.Calendar`에 `Schedule` 모델과 `ScheduleRepository`(SQLite CRUD: GetByMonth/GetByDate/Add/Update/Delete) 구현.
  - `App`의 `WidgetWindow`를 월간 캘린더 그리드로 교체: 월 이동 헤더(◀/▶), 요일 헤더(주말 색상 구분), 7x6 날짜 그리드(오늘 강조, 일정 있는 날 점 표시), 날짜 클릭 시 `DayEventsWindow` 오픈.
  - `DayEventsWindow`(그 날짜 일정 목록 + 추가/수정/삭제/닫기) + `ScheduleEditorWindow`(제목/종일 체크박스/날짜picker/시작·종료 시간/메모 입력 폼) 구현.
  - `dotnet build`는 성공(경고/오류 0개)했으나, 실제 실행 시 새로 추가된 `DesktopCalendar.Core.Calendar.dll` 로드가 Windows Smart App Control에 의해 차단되어 앱이 시작 즉시 크래시(`System.IO.FileLoadException`, WER 이벤트 로그로 확인). Phase 1에서 이미 실행되던 `Core.Desktop.dll`은 통과했었는데, 새로 생성된 DLL은 막힌 것으로 보아 파일 단위 평판 검사로 추정됨.
  - 따라서 월간 그리드/CRUD UI는 **코드 리뷰 + 빌드 성공까지만 확인, 실제 화면 렌더링·클릭 동작은 아직 검증 못함**. 다음 세션은 섹션 9의 SAC 문제를 사용자와 먼저 정리한 뒤, 실행 검증부터 이어서 진행.
- **2026-07-31**: SAC 문제 해결(사용자가 Smart App Control 끔) 후 실행 검증 완료, 추가로 사용자 피드백 2건 반영.
  - 사용자 피드백 1: "달력이 너무 작다. 일정을 2~3개까지 셀에 바로 보여주고, 넘치면 클릭 시 팝업으로." → 위젯 기본 크기 320x400 → 560x640으로 확대, 날짜 셀에 dot 대신 일정 제목 칩(최대 3개, 시간+제목, 초과분 "+N개") 렌더링하도록 `BuildDayCell`/`RenderMonth` 재작성.
  - 사용자 피드백 2: "월 이동을 좌우 화살표뿐 아니라 년/월 선택으로도 하고 싶다" → 헤더의 "yyyy년 M월 ▾" 텍스트를 클릭하면 `MonthPickerWindow`(년/월 콤보박스 + 오늘로/이동/취소)가 뜨도록 추가.
  - **실행 검증**: PowerShell + UI Automation(System.Windows.Automation)으로 날짜 셀 좌표를 정확히 찾아 실제 마우스 클릭 시뮬레이션 → DayEventsWindow 오픈 확인 → "추가" 버튼 Invoke → ScheduleEditorWindow에서 제목 입력(ValuePattern) 후 "저장" Invoke → 그리드에 칩으로 정상 반영됨을 스크린샷으로 확인. 사용자도 실제 데스크톱에서 위젯을 직접 드래그 이동/크기 조절하고 "test"/"test22" 일정을 손으로 추가해본 것으로 보임(스크린샷에 31일 셀에서 확인, 의도한 테스트 데이터 아님 — 필요시 앱에서 직접 삭제하면 됨) → 드래그 이동, 크기 조절, 일정 등록이 실사용 환경에서도 정상 동작함을 간접 확인.
  - Phase 0~2 전체 완료. 다음 세션은 Phase 3(공휴일 연동)부터 시작.
- **2026-07-31**: Phase 3(공휴일 연동) 완료 + 사용자 피드백 반영.
  - `Core.Holiday`에 `Holiday` 모델, `HolidayApiClient`(공공데이터포털 `getHoliDeInfo` 호출, System.Text.Json 파싱, item이 배열/단일객체 둘 다 처리), `HolidayRepository`(SQLite 캐싱 + `HolidayCachedYear`로 연도별 캐시 여부 추적, 수동 추가/제외) 구현.
  - `App`에 `ApiKeyDialog`(우클릭 메뉴 "공휴일 API 키 설정..."), `SimpleInputDialog`(범용 텍스트 입력 — VB `InputBox` 대신 직접 구현해 불필요한 어셈블리 의존성 피함) 추가. `WidgetWindow.RenderMonth`에서 표시 연도가 미캐시면 `EnsureHolidaysForYearAsync`로 비동기 조회 후 재렌더링.
  - `DayEventsWindow`에 공휴일 상태 표시 + "공휴일로 추가"/"공휴일 해제" 토글 버튼 추가.
  - 사용자 피드백: "공휴일도 토,일 빼고 무슨 공휴일인지 표시해줘" → 툴팁만으로는 부족하다고 판단, 날짜 셀 안에 공휴일 이름을 작은 빨간 텍스트로 항상 표시하도록 `BuildDayCell` 수정 (주말 자체는 이미 색으로 구분되므로 별도 라벨 없음, 공휴일이 겹치면 이름 표시).
  - **실행 검증**: API 키 없이 실행해도 크래시 없음 확인. `DayEventsWindow`에서 "공휴일로 추가" → `SimpleInputDialog`에 이름 입력 → 저장까지 UI Automation으로 왕복 테스트, 그리드 셀에 빨간 날짜 숫자 + 공휴일 이름 텍스트가 정상적으로 보이는 것을 스크린샷으로 확인(7/20을 "임시공휴일"로 수동 지정한 예시).
  - **미검증 항목**: 실제 data.go.kr API 키로 `HolidayApiClient.GetHolidaysAsync`가 정상적으로 진짜 공휴일 데이터를 받아오는지는 **테스트 못함**(유효한 서비스 키가 없음) — 사용자가 API 키를 발급받아 "공휴일 API 키 설정"에 입력한 뒤 정상 조회되는지 직접 확인 필요. 이때 오류가 나면 `HolidayApiClient`의 JSON 파싱/쿼리 파라미터를 점검할 것.
  - `dotnet build` 전체 경고/오류 0개. 다음 세션은 Phase 4(D-day)부터 시작.
- **2026-07-31**: 사용자 피드백으로 글자 크기 조절 기능 추가. `FontSizeDialog`(슬라이더 70%~200%) + 위젯 우클릭 메뉴 "글자 크기 조절..." 추가, `Widget.FontScale` 설정으로 저장, 월/요일/날짜/공휴일명/일정칩/안내문 전체에 배율 적용(`ApplyFontScale()`). UI Automation으로 우클릭→메뉴 항목 Invoke→슬라이더 RangeValuePattern.SetValue(1.6)→적용까지 왕복 테스트, 스크린샷으로 전체 텍스트가 커진 것 확인. `dotnet build` 경고/오류 0개.
- **2026-07-31**: Phase 4(D-day) 완료 + 다일(multi-day) 일정 지원 추가.
  - `Core.Calendar`에 `DDay` 모델, `DDayRepository`(SQLite CRUD), `DDayCalculator`(순수 로직: 일반/매년 반복 D-day 계산, 2/29 생일 평년 보정) 구현. `DDayCalculatorTests` 7개 케이스 작성, `dotnet test` 전체 통과.
  - `App`에 `DDayEditorWindow`(제목/날짜/매년반복 체크박스), `DDayListWindow`(목록+추가/수정/삭제) 구현. `WidgetWindow` 상단에 `DDayPanel`(WrapPanel, 남은 일수 적은 순 최대 5개 칩) 추가, 클릭 또는 우클릭 메뉴 "D-day 관리..."로 `DDayListWindow` 오픈.
  - 사용자 피드백: "며칠부터 며칠까지 기간만큼 일정으로 표시되도록 수정해줘" → `ScheduleEditorWindow`를 시작 날짜/종료 날짜를 별도로 지정할 수 있게 재구성(기존엔 날짜 하나 + 같은 날 시작·종료 시간만 가능했음). 시작 날짜 변경 시 종료 날짜가 그보다 이전이면 자동으로 맞춰주되, 사용자가 종료 날짜를 뒤로 미루면 다일 일정이 됨. `DayEventsWindow`의 일정 목록 표시도 다일 일정이면 "7/6~7/9 [종일] 제목"처럼 날짜 범위를 접두로 보여주도록 수정.
  - **실행 검증**: UI Automation으로 (1) 우클릭→D-day 관리→추가→제목/날짜/매년반복 입력→저장 후 위젯 상단에 "D-10 생일" 칩 렌더링 확인, (2) 7/6 날짜 클릭→추가→종일 체크→종료 날짜를 7/9로 설정→저장 후 7/6·7/7·7/8·7/9 네 칸 모두에 "여름 휴가" 칩이 표시되는 것을 스크린샷으로 확인.
  - `dotnet build` 전체 경고/오류 0개. Phase 0~4 전체 완료. 다음 세션은 Phase 5(구글 캘린더 연동)부터 시작.
- **2026-07-31**: 첫 git 커밋 생성 + GitHub 원격 저장소(`https://github.com/lucki3377/my_desktop_calendar.git`)에 push 완료(`origin/master`). 이전까지 로컬에만 있던 변경사항을 하나의 커밋("Add desktop background calendar app (Phase 0-4)")으로 묶음. 앞으로도 의미 있는 단위로 커밋 권장.
