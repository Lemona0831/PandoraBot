# PROJECT:PANDORA

Discord 기반 TRPG 운영 자동화 봇 및 관리자 대시보드입니다.  
캐릭터 등록, 선택, 조회, 상태창 이미지 출력, 기본 판정, 도움말, 관리자용 캐릭터 관리를 통해 어반 판타지 커뮤니티 운영 흐름을 자동화하는 것을 목표로 합니다.

## Preview

![Hunter status card preview](docs/status-card-preview.svg)

## 프로젝트 구성

```text
PandoraBot/
├─ PandoraBot/       Discord 봇
├─ PandoraAdmin/     관리자용 로컬 웹 대시보드
└─ PandoraBot.slnx   솔루션 파일
```

## 주요 기능

### Discord Bot

- `/등록`: Google Sheets 캐릭터 원본 시트에서 캐릭터 정보를 저장소에 등록
- `/선택`: 본인 소유 캐릭터 중 현재 사용할 캐릭터를 선택 또는 변경
- `/현재`: 현재 선택된 캐릭터의 주요 상태 확인
- `/정보`: 선택 캐릭터의 HUNTER LICENSE 상태창 이미지 출력
- `/판정`: 선택 캐릭터로 `2d6 + 능력치 수정치` 판정 후 `판정 로그` 시트에 기록
- `/내정보`: 현재 선택 캐릭터, 보유 캐릭터, 최근 판정 확인
- `/검수상태`: 내 캐릭터들의 승인/대기/반려 상태 확인
- `/도움말`: 플레이어용 사용 가이드와 판정/관리자 도움말 확인
- `/해제`: 현재 선택된 캐릭터의 선택 상태 해제
- `/헤제`: `/해제` 오타 대비 별칭
- `/삭제`: 본인 소유 캐릭터 삭제, `확인:true` 필요
- `/목록`: 본인 등록 캐릭터 목록 확인

### Admin Commands

관리자 명령어는 캐릭터명 기준으로 동작합니다. 같은 캐릭터명을 여러 유저가 공유하지 않는다는 운영 전제를 사용합니다.

- `/관리목록`: 저장소에 등록된 캐릭터 목록 요약 조회
- `/관리정보`: 캐릭터 이름으로 특정 캐릭터 조회
- `/관리체력`: 특정 캐릭터의 현재 HP 조정
- `/피해`: 특정 캐릭터에게 피해 적용 후 `관리 로그` 시트에 기록
- `/회복`: 특정 캐릭터에게 회복 적용 후 `관리 로그` 시트에 기록
- `/관리검수목록`: 검수 상태별 캐릭터 목록 조회
- `/관리승인`: 신규 또는 대기 캐릭터 승인
- `/관리반려`: 캐릭터 반려 및 선택 상태 해제
- `/관리판정로그`: 최근 판정 로그 조회
- `/공지`: 공지 템플릿 발송, 역할/유저/전체 멘션 호출, `공지 로그` 시트 기록
- `/관리선택해제`: 특정 유저의 선택 캐릭터 상태 해제
- `/관리삭제`: 특정 유저의 캐릭터 삭제, `확인:true` 필요

### 판정 규칙

`/판정`은 Apocalypse Engine / Dungeon World 계열 공식을 기준으로 합니다.

```text
2d6 + 능력치 수정치

10+  성공
7-9  부분 성공
6-   실패
```

능력치 수정치는 다음 표를 사용합니다.

```text
18      +3
16-17   +2
13-15   +1
9-12     0
6-8     -1
4-5     -2
3 이하  -3
```

### Admin Dashboard

- 등록 캐릭터 수, 등록 유저 수, 선택 캐릭터 수, 중복 의심 수 요약
- 캐릭터명 또는 Discord User ID 검색
- HP, Physical, Mental 능력치 확인
- 선택 상태 변경, 해제, 삭제
- 동일 User ID + 캐릭터 이름 기준 중복 데이터 정리

### Google Sheets

봇은 운영 기록 정리를 위해 다음 시트를 사용합니다.

- `캐릭터 저장소`: 캐릭터 기본 데이터와 선택 상태, 검수 상태 관리
- `선택 상태`: Discord 유저별 현재 선택 캐릭터 관리
- `판정 로그`: `/판정` 결과 자동 기록
- `관리 로그`: `/피해`, `/회복`, 검수 상태 변경 등 관리자 조작 기록
- `공지 로그`: `/공지` 발송 기록

관리자 명령은 캐릭터 이름이 중복될 경우 조작을 중단하고 중복 정리를 요구합니다. 선택 상태는 더 이상 `selected` 텍스트에 의존하지 않고 `선택 상태` 시트에서 `유저ID -> 캐릭터명` 구조로 관리합니다.
`/피해`와 `/회복`은 처리 후 대상 플레이어를 멘션하는 공개 상태 변경 알림을 전송합니다.

## 기술 스택

- C# / .NET
- Discord.Net
- Google Sheets API
- ImageSharp / ImageSharp.Drawing
- ASP.NET Core Minimal API

## 설정 파일

저장소에는 실제 토큰과 Google 서비스 계정 키를 올리지 않습니다.

공개 저장소에는 아래 예시 파일만 포함합니다.

```text
PandoraBot/BotSettings.example.json
PandoraBot/Credental.example.json
```

실행할 때는 예시 파일을 복사해 실제 파일을 만든 뒤 값을 채웁니다.

```powershell
copy PandoraBot\BotSettings.example.json PandoraBot\BotSettings.json
copy PandoraBot\Credental.example.json PandoraBot\Credental.json
```

`BotSettings.json` 예시:

```json
{
  "DiscordToken": "PUT_YOUR_DISCORD_BOT_TOKEN_HERE",
  "GuildId": 123456789012345678,
  "GoogleCredentialPath": "Credental.json"
}
```

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

브라우저에서 접속:

```text
http://localhost:5088
```

## 보안 주의

다음 파일은 절대 GitHub에 올리지 않습니다.

```text
PandoraBot/BotSettings.json
PandoraBot/Credental.json
```

Discord Bot Token이 외부에 노출된 적이 있다면 Discord Developer Portal에서 즉시 토큰을 재발급해야 합니다.

## 개발 현황

현재 캐릭터 관리, 상태창 이미지, 플레이어 도움말, 판정 로그, 피해/회복 처리, 공지 템플릿, 캐릭터 검수/승인, 관리자용 기본 운영 명령까지 구현되어 있습니다. 다음 단계는 전투 상태 관리와 몬스터 데이터 관리입니다.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
