# PROJECT:PANDORA

Discord 기반 역극 커뮤니티 운영을 위해 캐릭터 관리, 판정, HP 조작, 에너미 조회, 드롭 굴림을 보조하는 운영 자동화 봇입니다.

현재 README는 PANDORA 0.9 / 1.0 개장 안정판 기준의 기능 범위를 설명합니다. 구현된 기능과 아직 제공하지 않는 기능을 분리해 적었습니다.

## Opening Checklist

- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)

## Preview

![Hunter status card preview](docs/status-card-preview.svg)

## 프로젝트 구성

```text
PandoraBot/
├─ PandoraBot/       Discord Bot
├─ PandoraAdmin/     로컬 관리자 웹 대시보드
├─ PandoraShared/    봇과 웹이 함께 쓰는 에너미/드롭 공통 모델 및 서비스
├─ docs/             운영 문서
└─ PandoraBot.slnx   .NET 솔루션
```

## 현재 구현된 기능

### 캐릭터 등록 / 검수 / 선택

- `/등록`: Google Sheets 캐릭터 시트 URL을 읽어 중앙 저장소에 캐릭터를 등록합니다.
- 신규 등록 캐릭터는 검수 상태로 관리됩니다.
- `/검수상태`: 플레이어가 본인 캐릭터의 승인, 대기, 반려 상태를 확인합니다.
- `/관리검수목록`, `/관리승인`, `/관리반려`: 진행자가 캐릭터 검수 흐름을 관리합니다.
- `/선택`: 플레이어가 승인된 캐릭터 중 현재 사용할 캐릭터를 선택합니다.

### 상태 확인 / 상태창 이미지

- `/현재`: 현재 선택 캐릭터의 HP와 능력치를 텍스트로 확인합니다.
- `/정보`: 선택 캐릭터의 HUNTER LICENSE 상태창 이미지를 출력합니다.
- `/내정보`: 현재 선택 캐릭터, 보유 캐릭터, 최근 판정 기록을 한 번에 확인합니다.

### 플레이어 판정

- `/판정`: 선택 캐릭터 기준으로 `2d6 + 능력치 수정치` 판정을 굴립니다.
- 판정 결과는 `판정 로그` 시트에 기록됩니다.
- 결과 구간은 다음 기준을 사용합니다.

```text
10+   성공
7-9   부분 성공
6-    실패
```

능력치 수정치 기준:

```text
18       +3
16-17    +2
13-15    +1
9-12      0
6-8      -1
4-5      -2
3 이하   -3
```

### HP 피해 / 회복

- `/피해`: 진행자가 캐릭터 HP를 감소시킵니다.
- `/회복`: 진행자가 캐릭터 HP를 회복시킵니다.
- `/관리체력`: 진행자가 현재 HP를 직접 설정합니다.
- HP 조작은 `관리 로그` 시트에 기록됩니다.

### 관리자 명령

- `/관리목록`: 등록 캐릭터 목록을 조회합니다.
- `/관리정보`: 특정 캐릭터 상세 정보를 조회합니다.
- `/관리선택해제`: 특정 캐릭터 소유자의 선택 상태를 해제합니다.
- `/관리삭제`: 등록 캐릭터를 삭제합니다.
- `/관리판정로그`: 최근 판정 로그를 조회합니다.

### 공지 / 로그

- `/공지`: 공지 Embed를 발송하고 `공지 로그` 시트에 기록합니다.
- 역할 멘션, 유저 멘션, 전체 멘션 옵션을 지원합니다.
- 운영 기록은 Google Sheets의 `판정 로그`, `관리 로그`, `공지 로그`에 분리해 기록합니다.

### 에너미 목록 / 조회

- `/에너미목록`: 등록된 에너미의 ID, 이름, HP, 출현구분을 조회합니다.
- `/에너미조회`: 에너미ID 또는 이름 일부로 에너미 상세 정보를 조회합니다.
- 여러 후보가 있으면 후보 목록을 안내합니다.

### 에너미 판정

- `/에너미판정`: 에너미 능력치 기준으로 `2d6 + 능력치 수정치` 판정을 굴립니다.
- 플레이어 판정과 같은 수정치 공식을 사용합니다.
- 결과는 Embed로 출력하고 `관리 로그`에 기록합니다.

### 드롭 굴림

- `/드롭`: 전투 종료 후 실전 보상용 드롭 결과를 굴립니다.
- 에너미 드롭 설정의 발생률, 드롭 횟수, 아이템별 확률, 최소/최대 수량을 적용합니다.
- 여러 마리 수량을 입력하면 결과를 합산해 출력합니다.
- `/드롭테스트`: 실제 보상 지급 없이 드롭 확률을 반복 시뮬레이션합니다.

### 수동 전투 보조

- `/전투보조`: 전투 세션 시스템 없이 진행자가 어떤 명령을 어떤 순서로 사용하면 되는지 안내합니다.
- 현재 1.0 범위에서는 봇이 판정, HP, 드롭을 보조하고 전투 흐름 자체는 진행자가 수동으로 관리합니다.

## 현재 제외된 기능

다음 기능은 0.9 / 1.0 개장 안정판 범위에 포함하지 않았습니다.

- `/전투시작`, `/전투종료`
- 턴 / 라운드 자동 관리
- 전투 세션 참가자, 몬스터 상태, 전장 상태 자동 추적
- 웹사이트 조작 결과와 Discord 봇 알림의 완전 자동 연동
- 플레이어 인벤토리 지급 자동화
- Google Sheets에서 DB로의 완전 이전

이 항목들은 미구현 상태이며, 현재 README에서는 구현된 것처럼 설명하지 않습니다.

## Google Sheets를 초기 저장소로 사용한 이유

PANDORA는 개장 전 운영 검증과 빠른 수정이 중요한 프로젝트입니다. 초기 저장소로 Google Sheets를 사용한 이유는 다음과 같습니다.

- 진행자가 데이터를 직접 확인하고 수정하기 쉽습니다.
- 캐릭터, 선택 상태, 판정 로그, 관리 로그, 공지 로그를 빠르게 나눠 볼 수 있습니다.
- 별도 DB 서버 없이도 테스트와 운영 검증을 시작할 수 있습니다.
- 문제가 생겼을 때 Discord 로그와 시트 데이터를 비교해 원인을 찾기 쉽습니다.

다만 Google Sheets는 장기적으로 커뮤니티 포털, 인벤토리, 전투 세션, 대량 로그를 모두 담당하기에는 한계가 있습니다. 그래서 DB 이전은 후속 로드맵으로 분리합니다.

## 로드맵

### 1.5

- 관리자 웹 대시보드 안정화
- 웹에서 에너미/드롭 관리 UX 개선
- 웹 조작 결과를 봇 알림과 연결할 범위 설계
- 운영자 실수를 줄이는 확인 절차와 경고 표시 강화

### 2.0

- 전투 세션 기능 설계 및 구현 검토
- 전투 시작/종료, 참가자, 에너미 상태 관리
- 피해, 회복, 장갑치, 진행 상태 기록의 구조화
- 플레이어 포털 기초 기능 검토

### 3.0

- Google Sheets 중심 구조에서 DB 중심 구조로 이전
- 커뮤니티 웹사이트 / 플레이어 포털 확장
- 인벤토리, 보상 지급, 장기 로그 조회 기능 확장
- 24시간 운영 환경 정비

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

## 설정 파일 예시

저장소에는 실제 토큰이나 private key를 포함하지 않습니다. 공개 저장소에는 예시 파일만 포함합니다.

```text
PandoraBot/BotSettings.example.json
PandoraBot/Credental.example.json
```

실행할 때는 예시 파일을 복사해 실제 설정 파일을 만들고, 로컬에서만 값을 채웁니다.

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

## 보안 주의

다음 파일은 Git에 포함하지 않습니다.

```text
PandoraBot/BotSettings.json
PandoraBot/Credental.json
```

주의할 정보:

- Discord Bot Token
- Google 서비스 계정 private key
- 실제 운영 스프레드시트 접근 권한
- 운영 서버 환경 변수

토큰이나 private key가 외부에 노출되었다면 즉시 폐기하고 새로 발급해야 합니다.

## 스모크 테스트

개장 전에는 다음 문서를 따라 핵심 기능을 순서대로 점검합니다.

- [개장 전 스모크 테스트](docs/SMOKE_TEST.md)

## 기술 스택

- C# / .NET
- Discord.Net
- Google Sheets API
- ImageSharp / ImageSharp.Drawing
- ASP.NET Core Minimal API

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
