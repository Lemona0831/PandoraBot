# Supabase Setup

이 문서는 PANDORA 1.0의 Supabase PostgreSQL 연결 준비 단계만 다룬다.  
이 단계에서는 기존 Google Sheets 기능을 제거하지 않고 유지한다.

## 목표

- `ConnectionStrings:PandoraDb`로 PostgreSQL 연결 문자열을 읽는다.
- 연결 문자열이 없으면 봇은 기존 Google Sheets 모드로 계속 동작한다.
- 실제 비밀값은 Git에 커밋하지 않는다.

## 현재 상태

- 공통 `PandoraDbContext`가 추가되어 있다.
- 봇 시작 시 `ConnectionStrings:PandoraDb`를 읽는다.
- 연결 문자열이 없으면 DB 기능을 강제하지 않고 Google Sheets 기반 기능만 유지한다.

## 준비 파일

예시 파일:

- [PandoraBot/appsettings.example.json](C:/Users/ardwi/Downloads/판도라/PandoraBot/PandoraBot/appsettings.example.json)

로컬 개발용 파일:

- `PandoraBot/appsettings.Development.json`

`appsettings.Development.json`은 `.gitignore`에 포함되어 있어 Git에 올라가지 않아야 한다.

## 로컬 설정 방법

1. `PandoraBot/appsettings.example.json`을 참고해 `PandoraBot/appsettings.Development.json`을 만든다.
2. `ConnectionStrings:PandoraDb`에 Supabase PostgreSQL 연결 문자열을 넣는다.
3. 실제 비밀번호, 실제 연결 문자열, 실제 프로젝트 정보는 커밋하지 않는다.

예시:

```json
{
  "ConnectionStrings": {
    "PandoraDb": "Host=db.your-project-ref.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_REAL_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

## 환경 변수 대안

`appsettings.Development.json` 대신 환경 변수도 사용할 수 있다.

예시 키:

```text
ConnectionStrings__PandoraDb
```

## 동작 방식

- 연결 문자열이 없으면:
  - DB 연결을 강제하지 않는다.
  - 기존 Google Sheets 기반 기능이 계속 동작한다.
- 연결 문자열이 있으면:
  - `PandoraDbContext` 스캐폴드가 준비된다.
  - 이후 티켓에서 EF Core 엔티티, 마이그레이션, Repository 전환을 이어서 붙일 수 있다.

## 주의

- `BotSettings.json`
- `Credental.json`
- `appsettings.Development.json`
- 실제 Supabase 비밀번호
- 실제 Discord 토큰

위 파일이나 값은 Git에 커밋하면 안 된다.
