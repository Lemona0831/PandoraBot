# PROJECT:PANDORA

PROJECT:PANDORA는 Discord 기반 역극 운영을 덜 번거롭게 만들기 위해 만든 운영 보조 봇입니다.  
캐릭터 관리, 판정, 전투 세션, 에너미/드롭, 운영 로그까지 한 흐름으로 이어가는 걸 목표로 하고 있습니다.

이 README는 `feature/db-combat-1.0` 기준입니다.  
지금 어디까지 구현됐고, 1.0에서 뭘 하기로 했는지 정리해 둔 문서라고 보면 됩니다.

## 먼저 보면 좋은 문서

- [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)
- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)
- [DB / 전투 세션 1.0 범위](docs/DB_COMBAT_BOARD_1_0_SCOPE.md)
- [UX 점검 문서](docs/UX_REVIEW.md)

## 한눈에 보는 구성

```text
PandoraBot/
├─ PandoraBot/       Discord Bot
├─ PandoraAdmin/     로컬 관리자 웹 대시보드
├─ PandoraShared/    봇과 웹이 공유하는 공통 모델 / DB / 서비스
├─ docs/             운영 문서
└─ PandoraBot.slnx   .NET 솔루션
```

## 운영자 문서

운영자는 개발자가 아니라는 전제로 문서를 따로 빼 두었습니다.

- [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)  
  실제 운영 흐름 기준으로, 어떤 명령을 언제 써야 하는지 적어 둔 문서입니다.
- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)  
  개장 전에 핵심 기능이 정말 도는지 빠르게 확인하는 체크리스트입니다.

## 1.0에서 포함하는 범위

### 1. Supabase / PostgreSQL 도입

1.0에는 **Supabase / PostgreSQL 도입**이 들어갑니다.

예전처럼 Google Sheets를 운영용 중심 저장소로 오래 끌고 가는 게 아니라,

- 캐릭터
- 로그
- 에너미
- 드롭
- 전투 세션

이쪽을 DB 기준으로 정리하는 쪽입니다.

Google Sheets는 완전히 없어지는 게 아니라,

- 원본 캐릭터 시트
- 초기 데이터 확인
- 마이그레이션 보조 소스

정도로 역할이 줄어듭니다.

### 2. 전투 세션 관리

1.0은 전투를 **세션 단위**로 관리합니다.

즉 여기까지는 들어갑니다.

- 전투 세션 시작
- 전투 세션 종료
- 전투 참가자 등록
- 에너미 소환
- 참가자 HP 관리
- 전투 로그 기록
- 전투 상태 조회
- 전투판 정리

다만 1.0은 턴제 엔진을 만들겠다는 뜻은 아닙니다.  
역극 운영 특성상 진행자가 흐름을 보고, 봇은 세션/참가자/HP/로그를 정리해주는 쪽에 가깝습니다.

### 3. 캐릭터 등록 기준 변경

캐릭터 저장 기준도 이름 중심에서 **원본 Google Sheet 기준**으로 바뀝니다.

`/등록`, `/갱신` 시 아래 정보를 기준으로 저장/추적합니다.

- `source_sheet_id`
- `source_sheet_url`
- `source_document_title`
- `imported_character_name`
- `display_name`
- `normalized_display_name`
- `owner_discord_id`
- stats / hp / review_status

핵심은 딱 두 가지입니다.

- 같은 시트를 다시 등록하면 새 캐릭터를 만들지 않고 기존 캐릭터를 갱신
- 시트 안 캐릭터 이름이 바뀌어도 같은 시트면 같은 캐릭터로 추적

## 지금 구현된 기능

### 캐릭터 등록 / 검수 / 선택

- `/등록`: 원본 Google Sheets 캐릭터 시트를 읽어 캐릭터를 등록합니다.
- `/갱신`: 같은 원본 시트를 다시 읽어 캐릭터 정보를 갱신합니다.
- `/검수상태`: 플레이어가 본인 캐릭터의 승인 / 대기 / 반려 상태를 확인합니다.
- `/관리검수목록`, `/관리승인`, `/관리반려`: 진행자가 검수 상태를 관리합니다.
- `/선택`: 플레이어가 승인된 캐릭터 중 현재 사용할 캐릭터를 선택합니다.

### 상태 확인 / 상태창 이미지

- `/현재`: 현재 선택 캐릭터의 HP와 능력치를 텍스트로 확인합니다.
- `/정보`: 상태창 이미지를 출력합니다.
- `/내정보`: 현재 선택 캐릭터, 보유 캐릭터, 최근 기록을 확인합니다.

### 판정 / HP 운영

- `/판정`: 선택 캐릭터 기준 `2d6 + 수정치` 판정을 굴립니다.
- `/피해`, `/회복`, `/체력설정`: 진행자가 캐릭터 HP를 조작합니다.
- `/로그`: 판정 / 관리 / 공지 / 전투 로그를 한곳에서 확인합니다.

### 공지 / 운영 로그

- `/공지`: 운영 공지를 발송하고 기록합니다.
- 운영 중 생기는 주요 기록은 PostgreSQL 로그 구조로 정리하고 있습니다.

### 에너미 / 드롭 운영

- `/에너미목록`
- `/에너미조회`
- `/에너미판정`
- `/에너미추가`
- `/에너미수정`
- `/에너미능력치`
- `/에너미비활성화`
- `/에너미활성화`
- `/드롭`
- `/드롭테스트`
- `/드롭추가`
- `/드롭목록`
- `/드롭수정`
- `/드롭삭제`
- `/드롭설정`
- `/드롭설정보기`

### 전투 세션 운영

1.0 범위의 전투 명령은 아래 기준으로 봅니다.

- `/전투시작`
- `/전투종료`
- `/전투참가`
- `/에너미소환`
- `/전투상태`
- `/전투피해`
- `/전투회복`
- `/전투퇴장`
- `/전투정리`
- `/전투보조`

실제 운영 순서와 입력 예시는 [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)에 따로 적어 두었습니다.

## 1.0에서 하지 않는 것

여기는 1.0에서 일부러 안 넣은 기능들입니다.

### 턴 / 라운드 / 이니셔티브 / 행동완료

역극 운영 철학상 1.0에서는 다음을 자동 시스템으로 만들지 않습니다.

- 턴 관리
- 라운드 관리
- 이니셔티브 관리
- 행동완료 체크
- 상태이상 지속시간 자동 감소
- 라운드 기반 상태 자동 처리

전투는 세션 단위로 관리하지만, 진행 순서를 기계적으로 굴리는 구조는 1.0 범위가 아닙니다.

### 성장 자동화

- 성장 포인트 자동 계산
- 보상 누적 기반 자동 성장
- 장비 / 능력 성장 자동 반영

이건 1.0에서 하지 않습니다.

### 웹-봇 완전 연동

웹 조작 결과가 Discord 알림과 완전히 실시간 연동되거나,  
Discord 조작 결과가 웹 UI에 바로 1:1 반영되는 수준은 1.0 범위에서 제외합니다.

### 커뮤니티 사이트 확장

- 플레이어 포털
- 커뮤니티 메인 사이트
- 인벤토리 / 보상 / 기록 조회 페이지

이쪽은 뒤로 미룹니다.

## 에너미 데이터 정책

에너미의 **출현 빈도** 데이터는 1.0 범위에서 뺍니다.

즉 아래에서도 다 빼는 쪽입니다.

- DB 스키마
- Importer
- 웹 UI
- Discord 명령 출력

## 왜 Google Sheets를 아직 쓰는가

Google Sheets를 완전히 버린 건 아닙니다.  
다만 역할이 많이 줄었습니다.

지금은 주로:

- 원본 캐릭터 시트 읽기
- 기존 데이터 확인
- 초기 이관 보조

이 정도 역할로 남아 있습니다.

즉 운영 DB 역할은 DB 쪽으로 옮기고, Sheets는 원본/이관 보조 소스로 두는 방향입니다.

## 대략적인 로드맵

### 1.0

- Supabase / PostgreSQL 도입
- source sheet 기준 캐릭터 저장
- 전투 세션 구조 도입
- 전투 참가자 / HP / 로그 관리
- 에너미 / 드롭 운영 기능 정리

### 1.5

- 관리자 웹 UX 개선
- 운영 경고와 확인 절차 강화
- 웹과 봇 사이의 결과 공유 범위 정리

### 2.0

- 캐릭터 / 선택 / 검수 흐름의 DB 전환 마무리
- 전투 운영 보조 안정화
- 관리자 웹 전투판 기능 확장

### 3.0

- 커뮤니티 사이트 / 플레이어 포털 확장
- 인벤토리 / 보상 / 기록 조회 기능 확장

## 실행 방법

### Discord Bot

```powershell
cd PandoraBot
dotnet run
```

### Admin Dashboard

```powershell
cd PandoraAdmin
dotnet run --urls http://localhost:5088
```

브라우저 주소:

```text
http://localhost:5088
```

## 설정 파일 예시

저장소에는 실제 토큰이나 private key를 넣지 않습니다.  
공개 저장소에는 예시 파일만 둡니다.

```text
PandoraBot/BotSettings.example.json
PandoraBot/Credental.example.json
PandoraBot/appsettings.example.json
```

예시 파일을 복사해서 로컬 설정 파일을 만들면 됩니다.

```powershell
copy PandoraBot\BotSettings.example.json PandoraBot\BotSettings.json
copy PandoraBot\Credental.example.json PandoraBot\Credental.json
copy PandoraBot\appsettings.example.json PandoraBot\appsettings.Development.json
```

`BotSettings.json` 예시:

```json
{
  "DiscordToken": "PUT_YOUR_DISCORD_BOT_TOKEN_HERE",
  "GuildId": 123456789012345678,
  "GoogleCredentialPath": "Credental.json"
}
```

## 보안 관련 주의

아래 파일은 Git에 올리지 않습니다.

```text
PandoraBot/BotSettings.json
PandoraBot/Credental.json
PandoraBot/appsettings.Development.json
```

민감한 정보는 주로 이런 것들입니다.

- Discord Bot Token
- Google 서비스 계정 private key
- 실제 운영 스프레드시트 접근 권한
- Supabase / PostgreSQL 연결 문자열

이런 값이 노출되면 바로 폐기하고 다시 발급하는 쪽이 안전합니다.

## 개장 전 점검

개장 전에 아래 문서를 보고 핵심 기능을 한 번씩 확인하면 됩니다.

- [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)
- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)

## 기술 스택

- C# / .NET
- Discord.Net
- Google Sheets API
- Supabase / PostgreSQL
- EF Core / Npgsql
- ImageSharp / ImageSharp.Drawing
- ASP.NET Core Minimal API

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
