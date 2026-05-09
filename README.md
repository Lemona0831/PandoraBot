# PROJECT:PANDORA

PROJECT:PANDORA는 Discord 기반 역극 운영을 위해 캐릭터 관리, 판정, 전투 세션 보조, 에너미/드롭 운영을 지원하는 운영 자동화 봇입니다.

이 브랜치 `feature/db-combat-1.0` 기준으로 README는 **PANDORA 1.0 범위**를 설명합니다. 현재 범위는 `docs/DB_COMBAT_BOARD_1_0_SCOPE.md`와 동일하게 맞춰져 있습니다.

## Opening Checklist

- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)
- [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)
- [DB / 전투 세션 1.0 범위](docs/DB_COMBAT_BOARD_1_0_SCOPE.md)
- [UX 점검 문서](docs/UX_REVIEW.md)

## Preview

![Hunter status card preview](docs/status-card-preview.svg)

## 프로젝트 구성

```text
PandoraBot/
├─ PandoraBot/       Discord Bot
├─ PandoraAdmin/     로컬 관리자 웹 대시보드
├─ PandoraShared/    봇과 웹이 공유하는 공통 모델 / DB / 서비스
├─ docs/             운영 문서
└─ PandoraBot.slnx   .NET 솔루션
```

## 운영자 문서

운영자는 개발자가 아니어도 쓸 수 있어야 한다는 기준으로 문서를 분리해 두었습니다.

- [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)  
  어떤 명령을 언제 쓰는지, 어떤 값을 넣어야 하는지, 어떤 순서로 운영하면 되는지를 실제 운영 흐름 기준으로 정리한 문서입니다.
- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)  
  개장 전에 봇이 정상 동작하는지 순서대로 점검할 때 사용하는 체크리스트입니다.

## 1.0 포함 범위

### 1. Supabase / PostgreSQL 도입

PANDORA 1.0 범위에는 **Supabase / PostgreSQL DB 도입**이 포함됩니다.

이 브랜치에서는 Google Sheets를 장기 운영용 주 저장소로 유지하는 대신,
- 캐릭터
- 로그
- 에너미
- 드롭
- 전투 세션

을 PostgreSQL 중심 구조로 옮겨가는 작업을 포함합니다.

Google Sheets는 1.0에서 완전히 사라지는 대상이라기보다,
- 캐릭터 원본 시트
- 초기 데이터 확인
- 마이그레이션 보조 소스

역할로 축소됩니다.

### 2. 전투 세션화

PANDORA 1.0은 **전투를 세션 단위로 관리**합니다.

즉 1.0의 전투 범위는 단순 수동 보조를 넘어서 다음을 포함합니다.
- 전투 세션 시작
- 전투 세션 종료
- 전투 참가자 등록
- 에너미 소환
- 참가자 HP 관리
- 전투 로그 기록
- 전투 상태 조회
- 전투판 정리

다만 1.0은 턴제 전투 시스템을 목표로 하지 않습니다.
역극 운영 특성상 진행자의 판단이 중심이고, 봇은 세션/참가자/HP/로그 관리 도구 역할을 맡습니다.

### 3. 캐릭터 등록 기준 변경

캐릭터 저장 기준은 이제 “캐릭터 이름” 중심이 아니라 **원본 Google Sheet 기준**으로 바뀝니다.

`/등록`, `/갱신` 시 다음 정보를 기준으로 저장/추적합니다.
- `source_sheet_id`
- `source_sheet_url`
- `source_document_title`
- `imported_character_name`
- `display_name`
- `normalized_display_name`
- `owner_discord_id`
- stats / hp / review_status

핵심은 다음 두 가지입니다.
- 같은 시트를 다시 등록하면 새 캐릭터를 만들지 않고 기존 캐릭터를 갱신
- 시트 내부 캐릭터 이름이 바뀌어도 같은 시트면 같은 캐릭터로 추적

## 현재 구현된 기능

### 캐릭터 등록 / 검수 / 선택

- `/등록`: 원본 Google Sheets 캐릭터 시트를 읽어 캐릭터를 등록합니다.
- `/갱신`: 같은 원본 시트를 다시 읽어 캐릭터 정보를 갱신합니다.
- `/검수상태`: 플레이어가 본인 캐릭터의 승인 / 대기 / 반려 상태를 확인합니다.
- `/관리검수목록`, `/관리승인`, `/관리반려`: 진행자가 검수 상태를 관리합니다.
- `/선택`: 플레이어가 승인된 캐릭터 중 현재 사용할 캐릭터를 선택합니다.

### 상태 확인 / 상태창 이미지

- `/현재`: 현재 선택 캐릭터의 HP와 능력치를 텍스트로 확인합니다.
- `/정보`: HUNTER LICENSE 스타일 상태창 이미지를 출력합니다.
- `/내정보`: 현재 선택 캐릭터, 보유 캐릭터, 최근 기록을 확인합니다.

### 판정 / HP 운영

- `/판정`: 선택 캐릭터 기준 `2d6 + 수정치` 판정을 굴립니다.
- `/피해`, `/회복`, `/관리체력`: 진행자가 캐릭터 HP를 조작합니다.
- `/로그`: 판정 / 관리 / 공지 / 전투 로그를 한곳에서 확인합니다.

### 공지 / 운영 로그

- `/공지`: 운영 공지를 발송하고 기록합니다.
- 관리자 조작과 전투 관련 결과는 PostgreSQL 로그 구조로 통합되고 있습니다.

### 에너미 / 드롭 운영

- `/에너미목록`
- `/에너미조회`
- `/에너미판정`
- `/에너미추가`
- `/에너미수정`
- `/에너미능력치`
- `/드롭`
- `/드롭테스트`
- `/드롭추가`

에너미와 드롭 운영 기능은 PostgreSQL 저장소 기준으로 정리 중입니다.

### 전투 세션 운영

1.0 범위의 전투 명령은 다음을 기준으로 합니다.
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

실제 운영 순서와 입력 예시는 [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)에 따로 정리되어 있습니다.

## 1.0에서 제외되는 기능

다음은 1.0 범위에서 제외됩니다.

### 턴 / 라운드 / 이니셔티브 / 행동완료

역극 운영 철학상 1.0에서는 다음을 자동 시스템으로 만들지 않습니다.
- 턴 관리
- 라운드 관리
- 이니셔티브 관리
- 행동완료 체크
- 상태이상 지속시간 자동 감소
- 라운드 기반 상태 자동 처리

즉 전투는 세션 단위로 관리하지만, 진행 순서를 기계적으로 통제하는 구조는 1.0에 포함하지 않습니다.

### 성장 자동화

- 성장 포인트 자동 계산
- 보상 누적 기반 자동 성장
- 장비 / 능력 성장 자동 반영

은 1.0 범위에 포함하지 않습니다.

### 웹-봇 완전 연동

웹에서의 조작이 Discord 알림과 완전히 실시간 동기화되거나,
Discord 조작이 웹 전용 상태 패널에 완전 반영되는 수준의 양방향 통합은 1.0에서 제외합니다.

### 커뮤니티 사이트 확장

- 플레이어 포털
- 커뮤니티 메인 사이트
- 인벤토리 / 지급 / 보상 페이지
- 공개형 포털 기능

은 이후 단계로 미룹니다.

## 에너미 데이터 정책

에너미의 **출현 빈도** 데이터는 의미 없는 운영 데이터로 보고 1.0 범위에서 제거합니다.

따라서 다음 범위에서 제외합니다.
- DB 스키마
- Importer
- 웹 UI
- Discord 명령 출력

## Google Sheets를 여전히 사용하는 이유

현재 브랜치에서도 Google Sheets는 완전히 버리는 대상이 아니라,
다음 역할을 위해 유지됩니다.

- 원본 캐릭터 시트 읽기
- 기존 운영 데이터 마이그레이션
- 초기 검증 및 대조

즉 “운영 DB” 역할은 줄이고, “원본/이관 보조 소스” 역할로 정리하는 방향입니다.

## 로드맵

### 1.0

- Supabase / PostgreSQL 도입
- 캐릭터 저장 기준의 source sheet 전환
- 전투 세션 구조 도입
- 전투 참가자 / HP / 로그 관리
- 에너미 / 드롭 운영 기능 정리

### 1.5

- 관리자 웹 UX 개선
- 운영 확인 절차와 경고 표시 강화
- 웹과 봇 사이의 결과 공유 범위 정리

### 2.0

- 캐릭터 / 선택 / 검수 흐름의 DB 전환 완료
- 전투 운영 보조의 안정화
- 관리자 웹 전투판 기능 확장

### 3.0

- 커뮤니티 사이트 / 플레이어 포털 확장
- 인벤토리 / 보상 / 기록 조회 기능 확장
- 장기 운영 구조 정리

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

브라우저 접속:

```text
http://localhost:5088
```

## 설정 파일 예시

저장소에는 실제 토큰이나 private key를 포함하지 않습니다. 공개 저장소에는 예시 파일만 포함합니다.

```text
PandoraBot/BotSettings.example.json
PandoraBot/Credental.example.json
PandoraBot/appsettings.example.json
```

예시 파일을 복사해 로컬 설정 파일을 만듭니다.

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

## 보안 주의

다음 파일은 Git에 포함하지 않습니다.

```text
PandoraBot/BotSettings.json
PandoraBot/Credental.json
PandoraBot/appsettings.Development.json
```

주의 대상 정보:
- Discord Bot Token
- Google 서비스 계정 private key
- 실제 운영 스프레드시트 접근 권한
- Supabase / PostgreSQL 연결 문자열

민감 정보가 노출되면 즉시 폐기하고 다시 발급해야 합니다.

## 스모크 테스트

개장 전에는 아래 문서를 따라 핵심 기능을 순서대로 점검합니다.

- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)
- [운영자 명령 안내서](docs/OPERATOR_GUIDE.md)

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
