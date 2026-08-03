# 먹점프 `main` 단일 브랜치 정책

## 유지할 브랜치와 프로젝트

2026-08-03 사용자 결정에 따라 저장소는 `main` 하나만 사용한다. UI·플레이·물리·
콘텐츠·문서는 모두 `/Users/seungyeoning/Desktop/UnityProject/muk-jump`에서 작업한다.
사용자가 다시 요청하기 전에는 기능 브랜치, 별도 clone, 추가 worktree를 만들지 않는다.

## 동시 작업

같은 워킹트리에 여러 작성자가 동시에 수정하지 않는다. 주 에이전트 한 명만 파일을
수정하고, 보조 에이전트는 읽기 전용 조사·리뷰를 수행한다. 작업 전후 `git status`로
사용자 또는 다른 도구의 미커밋 변경을 확인하며 소유권이 불명확한 파일은 덮어쓰지 않는다.

`Assets/Scenes/Main.unity`는 `Assets/Editor/MukJumpSceneBuilder.cs`로만 재생성하고
직접 편집하지 않는다.

## 작업 순서

1. `main`과 전체 변경 파일을 확인한다.
2. 요청한 기능을 작은 단위로 구현하고 빠른 컴파일·로그 검증을 수행한다.
3. 이번 작업에서 수정한 파일만 명시적으로 stage한다. `git add .`과 `git add -A`는 금지한다.
4. Conventional Commit 형식의 한국어 커밋을 만든다.
5. 원격 `main`으로 push해 GitHub와 Unity 작업 폴더를 같은 상태로 유지한다.

긴 Unity Test Runner·반복 밸런스 플레이는 사용자가 명시적으로 요청할 때만 실행한다.
