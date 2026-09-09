# 수묵 VFX 보강 — 한 종류씩 제작·적용·검증

기준: 2026-09-05, HEAD `10b177a` + 현재 공유 워킹트리.
요청 범위는 실제 게임 이펙트/파티클 추가다. UI·경제·성장·점프 물리·광고·저장은 변경하지 않는다.
작성자는 root 한 명, 보조 검토는 읽기 전용. 기존 GIF 촬영은 신규 구현 성과로 세지 않는다.

| 순서 | 효과 | 현재 부족한 표현 / 보강 | 상태 |
|---|---|---|---|
| 1 | 일반 점프·착지 | 얇은 링/획 → 먹 튐 + 갈필 가루 + 발밑 번짐 | 실제 씬 적용·16개 회귀·3종 Play 촬영 통과, 실기 QA 대기 |
| 2 | 50m 상승 | 굵은 먹·가는 분사, 짧은 잔상, 선두를 따르는 붓 꼬리 | 구현·회귀·4등급 촬영·교차검토 완료, 실기 QA 대기 |
| 3 | 방어막·아이템 | 획득 응집→보유 고리→월드 파편, 기존 원화 흡수·종류별 표현 유지 | 구현·회귀·4등급 촬영·교차검토 완료, 실기 QA 대기 |
| 4 | 유효 드로잉·분신 | 유효 끝점 섬유 정착, 초기 속도를 반영한 분신 응집 | 구현·회귀·4등급 촬영·교차검토 완료, 실기 QA 대기 |
| 5 | 피격·사망·위험 | 기존 사망·피격 유지, 먹 게이지에 가리던 위험 낙관 위치 보정 | 회귀·3설정 촬영·교차검토 완료, 실기 QA 대기 |

각 단계의 구현 → EditMode 회귀 → 실제 Unity 영상 → 읽기 전용 교차검증 순서.
1단계 교차검증은 승인. 촬영 `output/quality-polish/captures/20260905-112206`에서
High/Low/Reduced 활성 입자 최고치는 43/21/0, 실제 PlayerLoop 수동 시간 검사도 통과했다.
실기기 발열/FPS·Metal/GLES/Vulkan은 에디터 영상으로 통과 처리하지 않는다.
새 패키지, Bloom/왜곡, 전체 화면 플래시, 게임 판정용 파티클 충돌은 사용하지 않는다.

## 전체 감사 근거

- `Assets/Scripts/Core/GameFeedbackController.cs`의 `EmitJump`: 0.22초 링과 0.24초 붓획뿐.
- `EmitLanding`: 0.26초 링, 강도 0.45 미만에서는 기존 방울도 없음.
- `PlayStrokeResolved`: 유효 획의 끝점 링만 있음. 획 생성 속도/판정은 유지해야 함.
- `Assets/Scripts/Items/InkDropJumpVfxInstance.cs`: 기존 기둥·spray·잔상 풀을 확장할 것. 별도 복제 시스템 금지.
- 사망은 이미 8프레임 캐릭터·먹 얼룩·Critical 파열을 갖추므로 우선순위 낮음.
- 24분신 대표 선택과 100/120ms cooldown **뒤**에서만 점프/착지 파티클 방출.

1단계 상세: [01-contact-particles.md](01-contact-particles.md).
나머지 단계 상세: [02-05-event-vfx.md](02-05-event-vfx.md).

## 2~5단계 최종 증거

- 관련 12개 클래스 EditMode 회귀 **181/181**, 실패·건너뜀 0:
  `output/quality-polish/roadmap-validation/final-regression.xml`.
- 50m 상승: `output/quality-polish/captures/20260905-120158` (High/Medium/Low/Reduced).
- 방어막: `output/quality-polish/captures/20260905-120900` (High/Medium/Low/Reduced).
- 먹선·분신: `output/quality-polish/captures/20260905-121828` (High/Medium/Low/Reduced).
  공유 추가 입자 최고치 66/48/30/0, 기존 하드 상한 90/62/37 유지.
- 피격·사망·위험: `output/quality-polish/captures/20260905-122951` (High/Low/Reduced).
- 실제 Unity Game View 540×960, 20fps, 총 15클립/1260프레임.
  기기 성능 측정이나 실서비스 광고 검증이 아니며, 실제 게임 코드를 메모리 저장소와 촬영용 사건으로 실행했다.
- `vfx_remaining_review` 읽기 전용 교차검토: 단계별 승인. 공전 위치 튐, 분신 초기 속도,
  위험 표식 선 외곽 여백·해상도별 계산 검증 보강을 반영했다.
- Unity/ProjectSettings/씬 YAML/외부 패키지/게임 물리/경제/광고/저장 규칙 변경 없음. 커밋·push 없음.
