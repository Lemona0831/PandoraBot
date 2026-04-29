# GitHub 업로드 가이드

이 프로젝트를 GitHub에 올리기 전에 민감 정보가 포함되지 않았는지 확인해야 합니다.

## 1. Git 설치 확인

```powershell
git --version
```

현재 PC에서 `git` 명령이 인식되지 않는다면 Git for Windows를 설치하거나 GitHub Desktop을 사용합니다.

## 2. 절대 올리면 안 되는 파일

다음 파일은 `.gitignore`에 등록되어 있으며 GitHub에 올리면 안 됩니다.

```text
PandoraBot/BotSettings.json
PandoraBot/Credental.json
```

`bin/`, `obj/`, `.vs/`, `*.log`도 업로드 대상에서 제외됩니다.

## 3. 최초 커밋

Git이 설치되어 있다면 프로젝트 루트에서 실행합니다.

```powershell
cd C:\Users\bit\Documents\Codex\2026-04-29\files-mentioned-by-the-user-pandorabot\PandoraBot
git init
git status
```

`git status`에 `BotSettings.json` 또는 `Credental.json`이 보이면 중단하고 `.gitignore`를 먼저 확인합니다.

문제가 없다면:

```powershell
git add .
git commit -m "Initial Project PANDORA bot and admin dashboard"
```

## 4. GitHub 원격 저장소 연결

GitHub에서 빈 저장소를 만든 뒤, 아래 명령의 URL을 본인 저장소 주소로 바꿉니다.

```powershell
git branch -M main
git remote add origin https://github.com/YOUR_NAME/YOUR_REPOSITORY.git
git push -u origin main
```

## 5. 토큰 재발급 권장

Discord Bot Token이 코드나 대화, 화면 캡처, 문서에 노출된 적이 있다면 Discord Developer Portal에서 토큰을 재발급하는 것이 안전합니다.
