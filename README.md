# PROJECT:PANDORA

PROJECT:PANDORA는 Discord 기반 역극 운영을 위한 운영 보조 봇입니다.  
플레이어의 원본 캐릭터 시트를 불러오고, 검수와 판정을 처리하고, 에너미와 드롭을 관리하고, 전투 세션까지 정리하는 걸 목표로 하고 있습니다.

지금 이 README는 **`feature/db-combat-1.0` 브랜치 기준**입니다.  
즉, “현재 어디까지 구현됐는지”, “1.0에서 어디까지를 범위로 보는지”, “운영자는 뭘 보면 되는지”를 한 번에 정리한 문서라고 보면 됩니다.

## 먼저 보면 되는 문서

처음 보는 사람이라면 아래 순서로 보면 편합니다.

1. [운영자 안내서](docs/OPERATOR_GUIDE.md)  
   실제 운영자가 어떤 명령을 언제 쓰는지 정리한 문서입니다.
2. [개장 전 스모크 테스트](docs/SMOKE_TEST.md)  
   개장 전에 핵심 기능이 정말 도는지 확인하는 체크리스트입니다.
3. [DB / 전투 세션 1.0 범위](docs/DB_COMBAT_BOARD_1_0_SCOPE.md)  
   1.0에서 무엇을 하고, 무엇을 하지 않는지 고정해 둔 문서입니다.
4. [UX 점검 메모](docs/UX_REVIEW.md)  
   실제 운영에서 어디가 막히는지 적어 둔 문서입니다.

## 이 프로젝트가 지금 하는 일

지금 PANDORA는 아래 흐름을 실제로 다루고 있습니다.

- 플레이어 원본 시트 기반 캐릭터 등록 / 갱신
- 캐릭터 검수 / 승인 / 반려
- 캐릭터 선택, 상태 조회, 상태창 이미지 출력
- 플레이어 판정과 운영 로그 기록
- 에너미 등록 / 수정 / 비활성화 / 재활성화
- 드롭 아이템 / 드롭 설정 관리
- 전투 세션 시작 / 종료
- 전투 참가자 추가 / 제거 / 상태 확인 / HP 조작
- 전투 로그와 운영 로그 조회

한 줄로 말하면,  
**운영자가 채팅방, 시트, 메모를 계속 오가며 손으로 하던 일을 줄여주는 봇**에 가깝습니다.

## 지금 기준 구조

```text
PandoraBot/
├─ PandoraBot/       Discord Bot
├─ PandoraAdmin/     로컬 관리자 웹 대시보드
├─ PandoraShared/    공통 모델 / DB / importer / 마이그레이션
├─ docs/             운영 문서
└─ PandoraBot.slnx   .NET 솔루션
```

## PANDORA 1.0에서 포함하는 범위

### 1. Supabase / PostgreSQL 도입

1.0에는 **Supabase / PostgreSQL 도입**이 들어갑니다.

예전처럼 Google Sheets를 운영용 중심 저장소로 오래 끌고 가는 게 아니라,

- 캐릭터 운영 데이터
- 운영 로그
- 에너미
- 드롭
- 전투 세션 / 참가자 / 전투 로그

이쪽을 DB 기준으로 정리하는 방향입니다.

### 2. 전투 세션 관리

1.0은 전투를 **세션 단위**로 관리합니다.

즉 여기까지는 들어갑니다.

- 전투 세션 시작
- 전투 세션 종료
- 참가자 추가 / 제거
- 에너미 소환
- 세션 기준 HP 관리
- 전투 로그 기록
- 전투 상태 확인
- 전투판 정리

다만 이건 턴제 엔진을 만들겠다는 뜻은 아닙니다.  
진행자가 흐름을 보고, 봇은 세션/참가자/HP/로그를 정리해주는 쪽에 가깝습니다.

### 3. 캐릭터 저장 기준 변경

캐릭터는 이제 이름만 믿고 저장하지 않습니다.  
원본 Google Sheet 기준으로 추적합니다.

중심 필드는 아래와 같습니다.

- `source_sheet_id`
- `source_sheet_url`
- `source_document_title`
- `imported_character_name`
- `display_name`
- `normalized_display_name`

그래서:

- 같은 시트를 다시 등록하면 새 캐릭터를 만들지 않고 갱신
- 시트 안 이름이 바뀌어도 같은 시트면 같은 캐릭터로 처리

가 됩니다.

## 지금 구현된 명령 범위

### 플레이어 기본 명령

- `/등록`
- `/갱신`
- `/검수상태`
- `/선택`
- `/현재`
- `/정보`
- `/판정`
- `/내정보`
- `/목록`
- `/해제`
- `/삭제`
- `/도움말`

### 캐릭터 운영 명령

- `/관리목록`
- `/정보조회`
- `/체력설정`
- `/피해`
- `/회복`
- `/관리선택해제`
- `/관리삭제`
- `/관리검수목록`
- `/관리승인`
- `/관리반려`
- `/공지`
- `/로그`

### 에너미 운영 명령

- `/에너미목록`
- `/에너미조회`
- `/에너미판정`
- `/에너미추가`
- `/에너미수정`
- `/에너미능력치`
- `/에너미비활성화`
- `/에너미활성화`

### 드롭 운영 명령

- `/드롭`
- `/드롭테스트`
- `/드롭추가`
- `/드롭목록`
- `/드롭수정`
- `/드롭삭제`
- `/드롭설정`
- `/드롭설정보기`

### 전투 세션 명령

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

실제 운영 순서와 입력 예시는 [운영자 안내서](docs/OPERATOR_GUIDE.md)에 따로 적어 두었습니다.

## 1.0에서 하지 않는 것

여기는 1.0에서 일부러 안 넣은 기능들입니다.

### 턴 / 라운드 / 이니셔티브 / 행동완료

아래는 1.0 범위 밖입니다.

- 턴 관리
- 라운드 관리
- 이니셔티브
- 행동완료 체크
- 상태이상 지속시간 자동 감소
- 라운드 기반 자동 처리

즉, **전투 세션은 있지만 턴제 엔진은 아닙니다.**

### 성장 자동화

- 성장 포인트 자동 계산
- 보상 누적 기반 자동 성장
- 장비 / 능력 성장 자동 반영

이건 아직 안 합니다.

### 웹-봇 완전 연동

웹에서 누른 결과가 Discord에 완전히 실시간 동기화되고,  
Discord 조작 결과가 웹 UI에 즉시 1:1 반영되는 수준은 아직 아닙니다.

### 커뮤니티 사이트 확장

- 플레이어 포털
- 공개형 커뮤니티 사이트
- 인벤토리 / 보상 / 기록 조회 사이트

이건 뒤 단계입니다.

## Google Sheets와 DB의 역할

여기가 가장 헷갈리기 쉬운 부분이라 분명하게 적어 둡니다.

### Google Sheets가 하는 일

- 원본 캐릭터 시트 읽기
- `/등록`, `/갱신`
- 초기 데이터 확인
- 이관 보조

### DB가 하는 일

- 캐릭터 조회 / 운영
- 검수 / 로그
- 에너미 / 드롭
- 전투 세션 / 참가자 / 상태

즉,  
**운영 중에는 DB를 본다고 생각하면 거의 맞고, Sheets는 원본/이관 보조 소스라고 보면 됩니다.**

## 에너미 데이터 정책

에너미의 `출현 빈도` 데이터는 1.0 범위에서 뺐습니다.

즉 아래에서도 다 빼는 쪽입니다.

- DB 스키마
- importer
- 웹 UI
- Discord 명령 출력

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

브라우저 접속 주소:

```text
http://localhost:5088
```

## 설정 파일

저장소에는 실제 토큰이나 private key를 넣지 않습니다.  
공개 저장소에는 예시 파일만 둡니다.

```text
PandoraBot/BotSettings.example.json
PandoraBot/Credental.example.json
PandoraBot/appsettings.example.json
```

복사 예시:

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

민감 정보 예시:

- Discord Bot Token
- Google 서비스 계정 private key
- 실제 운영 스프레드시트 접근 권한
- Supabase / PostgreSQL 연결 문자열

이런 값이 노출되면 바로 폐기하고 다시 발급하는 쪽이 안전합니다.

## 개장 전 점검

개장 전에 아래 두 문서를 같이 보면 됩니다.

- [운영자 안내서](docs/OPERATOR_GUIDE.md)
- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)

## 로드맵

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
