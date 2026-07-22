# AGENTS.md — 코딩 에이전트 공통 작업 규칙 (먹점프)

> 이 저장소는 **Claude Code와 Codex가 동시에** 작업한다. 아래 규칙을 지키지 않으면
> 서로의 작업을 덮어쓰거나 심사 대상인 커밋 히스토리를 망칠 수 있다.
> 프로젝트 전체 컨텍스트(기획·메커닉·로드맵)는 `CLAUDE.md`를 먼저 읽을 것.

## 0. 동시 작업 주의 (가장 중요)

- **커밋 전 반드시 `git status`로 전체 변경 목록을 확인**하고, 자신이 만들지 않은
  변경 파일은 절대 커밋·리셋·체크아웃하지 말 것. 다른 에이전트의 미커밋 작업일 수 있다.
- `git add -A` / `git add .` 금지 — 자신이 수정한 파일만 명시적으로 add.
- `git checkout` / `git reset --hard` / `git stash` 등 워킹트리를 건드리는 명령은
  실행 전 반드시 사용자에게 확인.
- 브랜치 전환은 워킹트리 전체에 영향을 주므로, 미커밋 변경이 있으면 전환하지 말 것.
- force push 금지.

## 1. 리포 운영 (커밋 히스토리가 해커톤 심사 대상)

- 브랜치: `main` 직접 커밋 금지. `feature/*` 또는 `fix/*` 브랜치 → PR → 머지.
- 커밋: Conventional Commits, 한국어 본문 (`feat(drawing): ...`, `fix(player): ...`).
  기능 단위로 잘게 나눠 커밋.
- 머지 후 로컬 main 동기화 + 머지된 브랜치 삭제까지가 한 사이클.

## 2. Unity 씬은 절대 손으로 편집하지 말 것

- `Assets/Scenes/Main.unity`는 **100% 에디터 스크립트가 생성**한다:
  `Assets/Editor/MukJumpSceneBuilder.cs`, 메뉴 `MukJump > Build Main Scene`.
- 씬에 오브젝트를 추가/수정하려면 **씬 빌더 코드를 고치고 재생성**할 것.
  (씬 YAML 직접 편집·에디터 수동 배치는 머지 충돌과 재현 불가를 만든다)
- 텍스처 임포트 설정(PPU, 스프라이트시트 슬라이싱 등)도 전부 빌더가 관리한다.
  임포터를 손으로 바꾸지 말고 빌더의 Configure* 메서드를 수정할 것.
  - 배경: 픽셀 폭과 무관하게 월드 폭 10.8유닛으로 PPU 자동 계산
  - 캐릭터 시트: 4×2 그리드 8프레임(idle·crouch·launch·rise·apex·fall·dive·land),
    프레임당 1024px, maxTextureSize 4096 필수 (2048이면 슬라이스가 어긋남)

## 3. 코드 규칙

- Unity **6000.3.10f1**, URP 2D. **Input System 전용**(activeInputHandler=1) —
  구 `Input.*` API 사용 금지. 포인터 입력은 반드시 `MukJump.Core.PointerInput` 헬퍼 사용
  (에디터의 Device Simulator가 마우스 장치를 가로채는 문제를 우회함).
- 싱글톤(`GameManager`, `ScoreManager`, `SketchToInkService`)은 `Instance`를
  **OnEnable에서 할당**한다 — Play 중 재컴파일 시 static이 초기화되기 때문. 이 패턴 유지.
- 네임스페이스: `MukJump.{Core,Player,Drawing,AI,EditorTools}`. 주석은 한국어.
- 존재가 불확실한 Unity API는 쓰기 전에 확인할 것 (예: `LineRenderer.SetWidths`는 없음 —
  정점별 두께는 widthCurve 키프레임으로).

## 4. 검증 습관

- 스크립트 수정 후 사용자에게 테스트를 요청하기 전에
  `~/Library/Logs/Unity/Editor.log`를 grep(`error CS|Exception`)해서 먼저 확인.
- Unity 에디터가 열려 있는 동안 배치모드(-batchmode) 실행 불가 (프로젝트 잠금).

## 5. 제출 요건 관련 (어기면 실격 사유)

- **API 키 없이 게임이 완전히 동작해야 한다** — AI 수묵 변환은 폴백(FallbackInkStyle)이
  기본 동작. 원격 API에 하드 의존성을 넣지 말 것.
- AI 도구로 작업할 때마다 **`docs/ai-usage-log.md`에 즉시 기록** (13절 형식은 CLAUDE.md 참고).
  이 파일이 제출물 4번(AI 활용 기술 문서)의 원본 자료다.
- 외부 에셋 사용 시 출처·라이선스를 같은 파일 상단 표에 추가.

## 6. 게임 코어 설계 요약 (자세한 것은 CLAUDE.md)

- 캐릭터는 자동 점프 (조작 불가). 발판 기울기→점프 방향, 발판 길이→점프력.
- 드로잉: 스트로크 캡처 → Chaikin 스무딩 → EdgeCollider2D. 먹은 전역 자원
  (총량·시간 회복). 캐릭터 근처 획은 무효 (물리 악용 방지).
- 상태 흐름: Lobby → (터치, 즉시 점프 연출) → Playing → (화면 하단 접촉,
  마리오식 죽음 연출) → GameOver → (터치) → 로비 생략하고 재시작.
- 아트 팔레트: INK #1C1B1A, PAPER #EAE3D2, RED(낙관) #AE1C3C — `InkPalette` 상수 사용.
