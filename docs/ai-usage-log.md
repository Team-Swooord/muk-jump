# AI 활용 내역 로그

> 제출물 4번(AI 활용 기술 문서)의 원본 자료. 개발 중 AI 도구를 사용할 때마다 즉시 기록한다.

## 외부 에셋 · 오픈소스 출처

| 항목 | 출처 | 라이선스 |
|---|---|---|
| 캐릭터/배경 아트 | 팀 자체 제작 (AI 보조 드로잉 후 수작업 검수) | 자체 저작물 |
| Unity 패키지 | Unity Technologies (URP, Input System 등 공식 패키지) | Unity Companion License |

---

## AI 생성 자체 제작 에셋

| 항목 | 제작 도구 | 구분 |
|---|---|---|
| `MukJump_InkDropJump_VFX_Pack` 텍스처·효과음·연출 사양 | OpenAI Codex | 프로젝트를 위해 직접 생성한 AI 산출물이며 외부 에셋이 아님 |

---

### 2026-07-22 — 먹물방울 50m 점프 VFX·SFX 이식

- 사용 도구: OpenAI Codex CLI
- 목적: 먹물방울 획득 즉시 실행되는 50m 점프에 수묵 스플래시, 충격 링, 상승 붓획과 전용 효과음 추가
- 주요 프롬프트/지시: 기존 점프 물리와 발동 시점을 변경하지 않고 Codex로 생성한 자체 VFX·SFX 팩을 적용하며,
  전용 VFX 오디오 관리자를 만들어 중첩 효과음을 안정적으로 재생
- 결과물: `Assets/MukJump/VFX/InkDropJump/`, `Assets/Scripts/Items/InkDropJumpVfx.cs`,
  `Assets/Scripts/Core/VfxAudioManager.cs`, `Assets/Scripts/Items/ItemPickup.cs`,
  `Assets/Editor/MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: Unity Editor에서 먹물방울 연속 획득 시 연출 중첩, 효과음 음량과 모바일 성능 확인 예정

### 2026-07-22 — 로비 최고 기록·로컬 랭킹 및 아이템 연출 보강

- 사용 도구: OpenAI Codex CLI
- 목적: 로비에서 저장된 최고 고도를 확인하고 아이템 3종의 효과 상태를 시각적으로 구분
- 주요 프롬프트/지시: 기존 고도 먹 붓획 UI를 로비 최고·랭킹 표시에 재사용하고,
  황금 붓과 방어막에도 안정적인 코드 기반 연출 추가
- 결과물: `Assets/Scripts/Core/LobbyView.cs`, `Assets/Scripts/Items/ItemEffectView.cs`,
  `Assets/Editor/MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 온라인 랭킹 데이터가 없어 랭킹은 현재 기기의 로컬 기록임을 명시,
  Unity Editor에서 로고 아래 배치와 금빛 붓결·방어막 펄스 확인 예정

### 2026-07-22 — 아이템 VFX 밀도 및 자동 점프 움직임 개선

- 사용 도구: OpenAI Codex CLI
- 목적: HTML 콘셉트 프리뷰에 맞춰 아이템 연출 레이어를 늘리고 자동 점프의 정적인 수직 반복 완화
- 주요 프롬프트/지시: ParticleSystem 오류 없이 비말·잔상·금빛 부유 입자·방어막 궤도 입자를 추가하고,
  발판 기울기와 이전 수평 관성이 다음 점프에 자연스럽게 이어지도록 조정
- 결과물: `Assets/Scripts/Items/InkDropJumpVfx.cs`, `Assets/Scripts/Items/ItemEffectView.cs`,
  `Assets/Scripts/Player/AutoJump.cs`, `Assets/Scripts/Player/CharacterAnimator.cs`
- 사람의 수정/검토 내용: Unity Editor에서 아이템별 연출 밀도와 수평 이동량을 직접 확인·튜닝 예정

### 2026-07-22 — 로비 랭킹 팝업·아이템 크기·물리 감각 조정

- 사용 도구: OpenAI Codex CLI
- 목적: 사용자 수정 UI를 보존하면서 로컬 랭킹 팝업을 추가하고 아이템 가시성과 캐릭터 움직임 개선
- 주요 프롬프트/지시: 랭킹 문구는 버튼에 유지하고 상세 기록은 팝업 안에 표시,
  인게임 아이템을 GameplayCanvas 아이콘과 비슷한 크기로 확대, 실제 구름 대신 미세한 시각 기울기만 적용
- 결과물: `Assets/Scripts/Core/LobbyView.cs`, `Assets/Scripts/Items/ItemSpawner.cs`,
  `Assets/Scripts/Player/{AutoJump,PlayerController,ScreenSideWalls}.cs`, `Assets/Editor/MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: `Main.unity`의 로비·게임 HUD 배치는 사용자가 직접 조정한 저장본을 유지

### 2026-07-22 — 대각선 발판 접착·발판 수명·LineSprite 적용

- 사용 도구: OpenAI Codex CLI
- 목적: 가파른 드로잉 발판의 활용도를 높이고 발판 교체 템포와 수묵 붓선 비주얼 개선
- 주요 프롬프트/지시: 그린 대각선에는 스파이더처럼 붙되 화면 양옆 벽 반동은 유지,
  발판 수명을 단축하고 Main UI의 폭 600 `LineSprite`를 실제 드로잉 선 텍스처로 사용
- 결과물: `Assets/Scripts/Player/PlayerController.cs`, `Assets/Scripts/Drawing/{StrokeCapture,PlatformCollider}.cs`,
  `Assets/Scripts/AI/FallbackInkStyle.cs`, `README.md`, `CLAUDE.md`, `docs/project-brief.md`
- 사람의 수정/검토 내용: Unity Editor에서 대각선 접착 강도, 6.5초 수명, LineSprite 늘어짐 여부 확인 예정

### 2026-07-20 — 프로젝트 기획 및 아트 시안

- 사용 도구: Claude (기획 문서화), AI 이미지 보조 (배경 산수화 시안 v1~v3)
- 목적: 게임 컨셉 확정, 캐릭터 '먹방울이' 및 세로 산수화 배경 시안 제작
- 주요 프롬프트/지시: 수묵화 스타일 세로 산수화 배경, 하단 한지 여백(플레이 공간) 유지,
  숯검댕이를 먹빛으로 재해석한 캐릭터(다리만 있음, 눈만으로 표정 표현)
- 결과물: `Assets/Art/Background/background_ink_landscape.png`,
  `Assets/Art/Character/character_muk_bangul_v3.png`, `muk_jump_hackathon_final.pptx`
- 사람의 수정/검토 내용: v1(구름형) → v2(능선+붓나무) → v3(소나무형) 시안 비교 후 최종본 선정,
  팔레트(INK/PAPER/RED 낙관) 직접 확정

### 2026-07-20 — 코어 루프 스크립트 초기 구현

- 사용 도구: Claude Code (터미널)
- 목적: 자동 점프, 터치 스트로크 → 발판 생성, 게임 루프(추락/재도전/점수) 스크립트 작성
- 주요 프롬프트/지시: CLAUDE.md의 4~7절 설계(자동 점프 주기, 발판 각도·길이 → 궤적 반영,
  스트로크 캡처 → 스무딩 → EdgeCollider, AI 변환 + 폴백 구조)를 그대로 구현하도록 지시
- 결과물: `Assets/Scripts/{Core,Player,Drawing,AI}` 하위 스크립트, 씬 빌더 에디터 스크립트
  (feature/core-loop 브랜치 커밋 이력 참고)
- 사람의 수정/검토 내용: (Unity 에디터에서 플레이 테스트 후 물리 파라미터 튜닝 예정 — 추후 기록)

### 2026-07-20 — 에디터 플레이 테스트 버그 수정

- 사용 도구: Claude Code (터미널)
- 목적: 첫 플레이 테스트에서 발견된 버그 2건 수정
- 주요 프롬프트/지시: "점프를 안 한다" / "(드로잉이) 시뮬레이터에서만 그려진다" 증상 전달 →
  원인 분석 및 수정 지시
- 결과물: PlayerController(Rigidbody2D sleep으로 접지 판정 풀리는 문제 → NeverSleep),
  PointerInput 헬퍼 신설(Device Simulator 가상 터치스크린이 Pointer.current를 차지해
  마우스 입력이 무시되는 문제 → 터치·마우스·펜 장치별 직접 확인)
- 사람의 수정/검토 내용: 에디터 Play 테스트로 증상 재현·수정 확인 (승연)

### 2026-07-20 — 먹방울이 점프 애니메이션 4프레임 제작 (폐기)

- 사용 도구: AI 이미지 보조 (SVG 벡터 재구성 → PNG 렌더), Claude Code (인게임 적용)
- 목적: 점프 모션 4프레임(웅크림→도약→정점→하강) 제작 및 물리 상태 기반 스프라이트 전환
- 주요 프롬프트/지시: 원본 캐릭터 실루엣(스파이크형 먹 블롯, 눈만으로 표정, 산(山) 모양
  다리) 유지, 스쿼시&스트레치 원칙의 4포즈, 눈 모양으로 감정 표현 (집중→놀람→편안→주시)
- 결과물: `muk_bangul_jump_0{1..4}_*.png` + SVG 원본 (Git에 커밋되지 않음)
- 사람의 수정/검토 내용: 프레임마다 눈 크기·얼굴/몸통 비율이 미묘하게 달라 스프라이트
  전환 시 캐릭터가 다른 캐릭터처럼 보이는 문제 발견 (승연) → 8프레임으로 재작업 결정,
  4프레임 산출물은 폐기

### 2026-07-21 — 먹방울이 점프 애니메이션 8프레임 재작업·적용

- 사용 도구: ChatGPT (이미지 재작업), Claude Code (인게임 적용)
- 목적: 4프레임 시도의 일관성 문제(눈·비율 흔들림)를 해결하기 위해, 마스터 몸통/눈을
  고정하고 다리 포즈만 바꾸는 방식으로 8프레임(idle·crouch·launch·rise·apex·fall·dive·land)
  재작업 요청. 프레임 간 위치·크기 어긋남 방지를 위해 4×2 스프라이트시트(4096×2048,
  프레임당 1024)로 납품받음
- 주요 프롬프트/지시: "마스터 몸통을 1개만 만들고 모든 프레임은 복사해서 다리만 변형",
  눈 크기·간격·동공 크기는 모든 프레임에서 고정, 스쿼시&스트레치는 세로/가로 ±12% 이내
- 결과물: `Assets/Art/Character/Player/muk_spritesheet.png`,
  `Assets/Scripts/Player/CharacterAnimator.cs`(수직 속도 구간으로 launch→rise→apex→
  fall→dive를 자연 전환), `Assets/Editor/MukJumpSceneBuilder.cs`(시트를 8개 서브스프라이트로
  런타임 슬라이싱)
- 사람의 수정/검토 내용: 에디터 Play 테스트에서 프레임이 잘리거나 사라지는 버그 발견·보고
  (승연) → 원인은 텍스처 임포터 기본 Max Size(2048)가 4096폭 시트를 축소해 슬라이스 좌표가
  어긋난 것으로 확인, `maxTextureSize`를 시트 실제 크기로 명시해 수정. 이후 에디터에서
  정상 동작 확인 (승연)

### 2026-07-21 — 먹 게이지 UI 아트 및 배경 개선판 제작·적용

- 사용 도구: ChatGPT (이미지 생성), Claude Code (프롬프트 설계·에셋 가공·인게임 적용)
- 목적: ① 먹 잔량 게이지를 실제 붓 획 모양(왼쪽 붓끝 가늘게 → 오른쪽 두껍게 + 붓 아이콘)
  으로 교체 ② 배경 산수화를 수채 질감·안개·매화 가지가 있는 개선판으로 교체
- 주요 프롬프트/지시: 게이지 3종(fill/track/icon) 규격·팔레트·정렬 조건을 명시한 프롬프트를
  Claude가 작성 → ChatGPT로 생성. 배경은 기존 v3 구도 유지 + 수채 질감 지시
- 결과물: `Assets/Art/UI/muk_gauge_{fill,track}.png`, `muk_brush_icon.png`,
  `Assets/Art/Background/background_ink_landscape.png` (941×1672)
- 사람의 수정/검토 내용: ChatGPT 산출물이 한 장짜리 시트에 체커보드가 불투명하게 박힌
  상태라, Claude Code가 색 키잉으로 요소 분리·투명화하고 트랙은 fill 실루엣을 재색칠해
  픽셀 정렬을 보장하도록 가공. 최종 인게임 확인 (승연)

### 2026-07-21 — 코어 안정성 점검 및 로비 UI 정리

- 사용 도구: Codex
- 목적: 전체 코어 루프의 예외 가능성을 점검하고, 사망 연출 중 카메라 이동 문제와 로고 적용 전
  로비 화면 구성을 정리
- 주요 프롬프트/지시: 저장소 전체 문제 확인 및 수정, 제작 중인 중앙 로고를 나중에 넣을 수 있도록
  로비 화면을 간결하게 개선
- 결과물: `CameraFollow.cs`, `PlayerController.cs`, `AutoJump.cs`, `ScoreManager.cs`,
  `PointerInput.cs`, `GameManager.cs`, `StrokeCapture.cs`, `PrototypeHud.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: Unity 에디터에서 9:16 로비 배치, 사망 시 카메라 고정, 재시작 흐름 확인 예정

### 2026-07-21 — 고도별 장애물과 사망 시트 균등 슬라이스

- 사용 도구: Codex
- 목적: 원형 먹 가시 장애물을 랜덤 배치하고 100m 이후 좌우·상하 이동형으로 확장,
  잘못 자동 슬라이스된 사망 시트를 고정 그리드 애니메이션으로 연결
- 주요 프롬프트/지시: `anermy_01`을 원형 장애물로 사용, 100m 이후 이동 패턴 진화,
  `die.png`의 프레임 크기와 피벗을 동일하게 유지
- 결과물: `Assets/Scripts/Obstacles/`, `PlayerController.cs`, `CharacterAnimator.cs`,
  `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: Unity 에디터에서 장애물 크기·간격·충돌 반경과 사망 12프레임 순서 확인 예정

### 2026-07-21 — 로비 드로잉 시작 연출

- 사용 도구: Codex
- 목적: 로비의 탭 시작을 첫 발판 드로잉으로 교체하고 GUI 텍스트의 마우스 오버 상태 제거
- 주요 프롬프트/지시: “선을 그어 시작”, 그은 위치가 시작 지점이 되는 연출로 즉시 플레이 전환
- 결과물: `StrokeCapture.cs`, `GameManager.cs`, `PlayerController.cs`, `ScoreManager.cs`,
  `PrototypeHud.cs`
- 사람의 수정/검토 내용: 에디터에서 짧은 획 무효 처리, 시작 발판 착지, 임의 높이 시작 시 점수 0m 기준 확인 예정

### 2026-07-21 — 캐릭터 아트 폴더 정리 및 개별 사망 프레임 교체

- 사용 도구: Codex
- 목적: 8장의 동일 규격 사망 프레임을 순서대로 연결하고 캐릭터·사망·장애물 아트를 역할별 폴더로 정리
- 주요 프롬프트/지시: 새 개별 사망 스프라이트 사용, 미사용 기존 사망 시트와 프레임 삭제
- 결과물: `Assets/Art/Character/{Player,Death,Obstacles}/`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 기존 `die.png`, `muk_dead_a~c`는 참조 교체 후 휴지통으로 이동해 복구 가능하게 보관

### 2026-07-21 — 장애물 좌우 이동 및 고도별 속도 조정

- 사용 도구: Codex
- 목적: 모든 장애물을 좌우 이동형으로 통일하고 고도에 따라 이동 속도를 높이며 크기를 소폭 축소
- 주요 프롬프트/지시: 장애물은 항상 좌우 이동, 속도는 높이에 비례해 증가, 크기는 조금 축소
- 결과물: `Assets/Scripts/Obstacles/ObstacleSpawner.cs`
- 사람의 수정/검토 내용: 시작 구간과 300m 최고 난도의 체감 속도 및 장애물 폭 확인 예정

### 2026-07-21 — 시작선 가이드와 초기 발판 제거

- 사용 도구: Codex
- 목적: 캐릭터 아래에 첫 발판을 그리도록 유도하고 기존 고정 시작 발판을 완전히 제거
- 주요 프롬프트/지시: 캐릭터 아래 점선 가이드 표시, 위에 그으면 캐릭터가 추락해 사망,
  플레이 시작 시 사용자가 그은 선만 존재
- 결과물: `PrototypeHud.cs`, `StrokeCapture.cs`, `GameManager.cs`, `PlayerController.cs`,
  `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 로비에서 캐릭터 고정, 시작선 완성 후 물리 해제, 위쪽 오답 획의 추락 동작 확인 예정

### 2026-07-21 — 시작 가이드 붓 아이콘 연출

- 사용 도구: Codex
- 목적: 로비 시작 점선 위에 반투명 붓 아이콘을 왕복시켜 드로잉 위치를 직관적으로 안내
- 주요 프롬프트/지시: 붓 아이콘에 투명도를 적용하고 가이드라인 위쪽을 따라 이동
- 결과물: `PrototypeHud.cs`
- 사람의 수정/검토 내용: 9:16 화면에서 아이콘 크기·높이·왕복 속도 확인 예정

### 2026-07-21 — 로비 UI Canvas 전환

- 사용 도구: Codex
- 목적: 런타임 OnGUI 로비를 하이어라키에서 직접 편집 가능한 Canvas 오브젝트로 전환
- 주요 프롬프트/지시: 빨간 점선과 부제 제거, `선을 그어 시작`만 유지, Play 전에도 로비 요소 확인·수정 가능
- 결과물: `LobbyView.cs`, `PrototypeHud.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: `LobbyCanvas/{Logo,StartPrompt,BrushGuide}`의 9:16 배치와 한글 폰트 확인 예정

### 2026-07-21 — 먹점프 로고 적용

- 사용 도구: Codex
- 목적: 새 수묵 캘리그래피 로고를 로비의 텍스트 제목 대신 적용
- 주요 프롬프트/지시: 추가된 먹점프 로고로 기존 제목 대체
- 결과물: `Assets/Art/UI/muk_logo.png`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 1536×1024 원본 비율에 맞춰 로고 UI 영역을 3:2로 설정

### 2026-07-21 — 시작 안내 붓획 버튼 적용

- 사용 도구: Codex
- 목적: 새 먹 붓획 UI를 `선을 그어 시작` 안내의 배경으로 적용하고 로고 크기를 확대
- 주요 프롬프트/지시: 버튼을 얇은 가로형으로 사용, 문구는 한 줄 흰색, 로고 크기 증가
- 결과물: `Assets/Art/UI/muk_start_button.png`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 하이어라키를 `StartPrompt/Label`로 분리해 배경과 텍스트를 독립 편집 가능하게 구성

### 2026-07-21 — 붓획 고도 HUD 및 로비 재시작 흐름

- 사용 도구: Codex
- 목적: 로비 시작 안내 UI를 제거하고 붓획 이미지를 플레이 중 고도 표시 배경으로 재사용,
  게임 오버 후 반드시 로비에서 시작선을 다시 그리도록 흐름 수정
- 주요 프롬프트/지시: 시작 안내 텍스트·UI 제거, 버튼 UI를 `고도 0` 위치에 배치, 사망 후 메인 화면 복귀
- 결과물: `GameplayHudView.cs`, `GameManager.cs`, `PrototypeHud.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: `GameplayCanvas/HeightDisplay/HeightText`를 하이어라키에서 편집 가능하게 구성

### 2026-07-21 — UI 수동 배치 보존

- 사용 도구: Codex
- 목적: 씬 빌더 재실행으로 사용자가 조정한 로고·고도 HUD 크기와 위치가 초기화되는 문제 방지
- 주요 프롬프트/지시: 메인 로비에서는 고도 HUD 숨김, Inspector에서 맞춘 UI 배치는 그대로 유지
- 결과물: `GameplayHudView.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 빌드 전 RectTransform을 하이어라키 경로별로 캡처하고 재생성 후 복원하도록 변경

### 2026-07-21 — 사망 모션 화면 크기 통일

- 사용 도구: Codex
- 목적: 사망 프레임의 큰 투명 여백 때문에 캐릭터가 작아 보이는 문제 수정
- 주요 프롬프트/지시: 모든 캐릭터 모션의 화면상 크기를 동일하게 유지
- 결과물: `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 사망 프레임 8장 모두 PPU 720을 동일 적용해 일반 프레임 대비 약 1.25배 확대

### 2026-07-21 — 마리오식 죽음 연출·로비 화면·붓 아이콘 교체

- 사용 도구: ChatGPT (죽음 포즈 시트·붓 클로즈업 이미지 생성), Claude Code (가공·구현)
- 목적: ① 화면 하단 접촉 시 마리오식 죽음(멈칫→폴짝→가속 낙하 + X눈 3포즈 1회 재생)
  ② 씬 전환 없는 로비 화면(터치 시 캐릭터가 즉시 점프하며 시작) ③ 붓 아이콘 고품질 교체
- 주요 프롬프트/지시: 죽음 연출을 "마리오처럼"으로 지시, 프레임 반복이 어색해 1회 재생으로
  수정 지시. 시트의 회색 글로우는 Claude Code가 알파·명도 필터로 제거 후 포즈별 추출
- 결과물: `Assets/Art/Character/muk_dead_{a,b,c}.png`(원본 `die.png`),
  `PlayerController`(죽음 시퀀스), `CharacterAnimator`(죽음 프레임), `GameManager`(로비 상태),
  `PrototypeHud`(타이틀 UI·낙관 도장)
- 사람의 수정/검토 내용: 죽음 프레임 반복 재생의 어색함 발견 → 1회 재생으로 변경 요청,
  붓 아이콘 크기·게이지 간격 조정 지시, 에디터 Play 테스트 (승연)

### 2026-07-21 — 임시 아이템 3종 및 먹 방어막 추락 복귀

- 사용 도구: Codex
- 목적: 먹물방울·황금 붓·먹 방어막 아이템을 임시 비주얼로 구현하고, 먹 방어막이 장애물뿐 아니라 추락도 1회 막도록 확장
- 주요 프롬프트/지시: 먹물방울은 50m 점프, 황금 붓은 일정 시간 먹 무소모, 먹은 피해 또는 추락 1회 방어 후 추락 시 크게 재도약
- 결과물: `ItemPickup.cs`, `ItemSpawner.cs`, `PlayerController.cs`, `StrokeCapture.cs`, `Obstacle.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 정식 스프라이트 제작 전까지 기존 원형 이미지를 색상별 플레이스홀더로 사용하며, 황금 붓 지속시간은 8초로 설정

### 2026-07-21 — 아이템 효과 테스트 버튼

- 사용 도구: Codex
- 목적: 플레이 중 아이템 3종의 효과를 즉시 확인할 수 있는 개발용 UI 제공
- 주요 프롬프트/지시: 화면 왼쪽에 아이템 아이콘 버튼을 배치하고 누르면 즉시 효과 실행
- 결과물: `GameplayHudView.cs`, `ItemPickup.cs`, `StrokeCapture.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 버튼은 50m·무한·방어 문구와 임시 색상 아이콘으로 구분하며 버튼 터치가 발판 드로잉으로 전달되지 않도록 처리

### 2026-07-21 — 아이템 활성 시각 효과 및 먹물방울 보호

- 사용 도구: Codex
- 목적: 아이템 효과의 활성 상태를 즉시 알아볼 수 있게 하고 먹물방울 상승 중 장애물 사망 방지
- 주요 프롬프트/지시: 50m 점프 중 장애물 무적, 황금 붓은 하단 붓 금색화와 파티클, 먹 방어막은 캐릭터 주변 먹 원 효과
- 결과물: `PlayerController.cs`, `ItemEffectView.cs`, `ItemPickup.cs`, `PrototypeHud.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 먹물방울 무적은 상승 구간까지만 유지하고, 방어막 소모 시 원형 효과가 즉시 사라지도록 구성

### 2026-07-21 — 먹물방울 연속 도약 및 상승 파티클

- 사용 도구: Codex
- 목적: 먹물방울을 연속 획득할 때마다 현재 위치에서 다시 50m 상승시키고 상승 상태를 시각화
- 주요 프롬프트/지시: 먹물방울 효과 도중 다시 먹어도 추가 50m 상승, 올라가는 이펙트 추가
- 결과물: `PlayerController.cs`, `ItemEffectView.cs`
- 사람의 수정/검토 내용: 재획득마다 상승 속도를 초기화하고 캐릭터 아래 먹빛 파티클을 즉시 추가 방출하도록 구성

### 2026-07-21 — 아이템 파티클 제거

- 사용 도구: Codex
- 목적: 먹물방울 실행 시 Particle System 곡선 모드 오류 제거
- 주요 프롬프트/지시: 파티클 관련 구현 전부 제거
- 결과물: `ItemEffectView.cs`, `PlayerController.cs`, `PrototypeHud.cs`
- 사람의 수정/검토 내용: 연속 50m 도약·상승 무적·금색 붓·먹 방어막 원은 유지하고 Particle System과 파티클형 GUI 연출만 제거

### 2026-07-21 — 낙묵석 장애물 구현

- 사용 도구: OpenAI Codex CLI
- 목적: 예고 후 낙하하며 플레이어를 공격하고 드로잉 발판을 파괴하는 장애물 구현
- 주요 지시: 기존 `PlayerController.TakeHit()` 사망 흐름과 `PlatformCollider` 등록 해제 흐름 재사용
- 결과물: `Assets/Scripts/Obstacles/FallingInkRock.cs`, `Assets/Scripts/Obstacles/FallingInkRockSpawner.cs`, `Assets/Scripts/Drawing/PlatformCollider.cs`, `Assets/Editor/MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: Unity Editor 수동 플레이 테스트 예정

### 2026-07-21 — 먹물방울 정식 아이템 이미지 연결

- 사용 도구: OpenAI Codex CLI
- 목적: 새 먹물방울 이미지를 월드 아이템과 효과 테스트 버튼에 적용
- 주요 지시: 해당 이미지는 먹물방울 아이템 이미지임을 확인
- 결과물: `Assets/Scripts/Items/ItemSpawner.cs`, `Assets/Editor/MukJumpSceneBuilder.cs`, `Assets/Art/UI/ink_drop.png`
- 사람의 수정/검토 내용: 황금 붓과 먹 방어막은 정식 이미지가 추가될 때까지 기존 임시 표시 유지

### 2026-07-21 — 아이템 이미지 이름 정리 및 3종 연결

- 사용 도구: OpenAI Codex CLI
- 목적: 임시 번호 파일을 실제 아이템 이름으로 변경하고 세 아이템 비주얼에 연결
- 주요 지시: 1·2·3번 이미지 이름을 아이템 이름에 맞게 변경
- 결과물: `ink_drop.png`, `golden_brush.png`, `ink_shield.png`, `ItemSpawner.cs`, `MukJumpSceneBuilder.cs`
- 사람의 수정/검토 내용: 붓 형태인 기존 1번은 황금 붓, 나머지 임시 먹방울은 기존 순서대로 먹물방울과 먹 방어막에 배정
