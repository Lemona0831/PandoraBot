# PANDORA Admin Dashboard

진행자가 코드를 직접 수정하지 않고 캐릭터 저장소를 확인하고 관리하기 위한 로컬 웹 대시보드입니다.

## 실행 방법

```powershell
cd C:\Users\bit\Documents\Codex\2026-04-29\files-mentioned-by-the-user-pandorabot\PandoraBot\PandoraAdmin
dotnet run --urls http://localhost:5088
```

브라우저에서 다음 주소로 접속합니다.

```text
http://localhost:5088
```

## 제공 기능

• 등록 캐릭터 수, 등록 유저 수, 선택 캐릭터 수, 중복 의심 행 요약  
• 캐릭터명 또는 Discord User ID 검색  
• 캐릭터별 HP, Physical, Mental 능력치 확인  
• HP 직접 설정, 피해 적용, 회복 적용  
• 검수 상태 승인, 대기, 반려 변경  
• 선택 상태 확인  
• 반려 시 선택 상태 자동 해제  
• 캐릭터 행 삭제  
• 동일 User ID + 캐릭터 이름 기준 중복 데이터 정리
• 웹 조작 내용을 `관리 로그` 시트에 기록

## 주의 사항

삭제 버튼은 Google Sheets의 해당 행 A:L 값을 비웁니다.
중복 데이터 정리는 같은 User ID와 캐릭터 이름을 가진 행 중 하나만 남기고 나머지를 비웁니다.
현재 대시보드는 Discord 봇과 분리되어 Google Sheets만 직접 수정합니다. Discord 알림과 봇 연동은 최종 단계에서 별도 구조로 연결합니다.
