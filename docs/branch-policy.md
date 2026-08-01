# 먹점프 고정 브랜치 정책

## 유지할 브랜치

저장소는 아래 세 브랜치만 사용한다.

- `main`: 제출·배포 기준. 직접 작업하거나 직접 커밋하지 않는다.
- `feature/ui`: 로비, HUD, 패널, 타이포그래피, UI 입력·전환·연출 작업.
- `feature/game`: 플레이어, 드로잉 물리, 카메라, 아이템, 장애물, 스폰·밸런스 작업.

사용자가 정책 변경을 명시하지 않는 한 기능별 임시 브랜치, 개인 이름 브랜치,
추가 worktree를 만들지 않는다.

## 교차 파일

`Assets/Editor/MukJumpSceneBuilder.cs`, `Assets/Scenes/Main.unity`, `CLAUDE.md`,
`docs/ai-usage-log.md`처럼 UI와 게임 양쪽이 공유하는 파일은 같은 시점에 두
브랜치에서 중복 수정하지 않는다. 작업의 주목적에 맞는 한 브랜치가 소유하고,
다른 브랜치는 병합된 `main`을 받은 뒤 후속 작업한다.

`Assets/Scenes/Main.unity`는 씬 빌더로만 재생성하며 직접 편집하지 않는다.

## 작업 순서

1. 작업 전 현재 브랜치와 전체 변경 파일을 확인한다.
2. UI 작업은 `feature/ui`, 게임 작업은 `feature/game`에 작은 기능 단위로 커밋한다.
3. 현재 작업 브랜치를 push하고 `main` 대상 PR을 일반 merge한다.
4. 병합 뒤 워킹트리가 깨끗한 두 작업 브랜치를 최신 `main`으로 fast-forward한다.
5. 두 고정 작업 브랜치는 삭제하지 않는다.

미커밋 변경이 있으면 브랜치 전환·fast-forward·reset·stash를 하지 않고 먼저
변경 소유권을 확인한다.
