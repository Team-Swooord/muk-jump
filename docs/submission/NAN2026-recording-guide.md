# NAN 2026 먹점프 플레이 영상 촬영 가이드

## 목적

30~60초 제출 영상에서 게임의 핵심을 한 번에 보여 준다. 촬영 모드는 에디터에서만
실행되고 실제 최고기록, 먹빛, 성장 선택, 튜토리얼 완료 상태를 변경하지 않는다.

## Unity Recorder 준비

현재 프로젝트에 Recorder가 보이지 않으면 Unity Package Manager의 **Add package by
name**에서 아래 패키지를 설치한다.

```text
com.unity.recorder@5.1.7
```

Recorder Window에서 Movie Clip을 추가하고 다음처럼 설정한다.

- Source: Game View
- Output: 1080 × 1920 세로
- Format: MP4 / H.264 / High
- Frame rate: Constant 30 fps, Cap FPS 켜기
- Audio: 켜기
- Recording mode: Time Interval, 0~50초
- Output folder: 프로젝트 밖 `Recordings/`

녹화 직전 Game View를 1080×1920으로 바꾸고 Game View 탭을 마지막으로 한 번 누른다.

## 촬영 방법

1. Unity 상단 메뉴에서 `MukJump > Recording > NAN 2026 촬영 시나리오`를 연다.
2. `다음 Play 촬영 예약`을 누른다.
3. Recorder Window에서 `START RECORDING`을 누른다.
4. 약 50초 뒤 Recorder가 자동 종료할 때까지 입력하지 않는다.

빠른 확인만 할 때는 `예약하고 바로 미리보기`를 누른다. Play 중 촬영 창의 `다음 장면`
버튼으로 현재 컷을 건너뛸 수 있다. 이 버튼은 Game View 밖의 EditorWindow에 있으므로
영상에는 찍히지 않는다.

## 자동 시나리오

1. 로비에서 먹점프 제목과 팀 콘셉트
2. 실제 로비 전환을 거친 영구 성장 먹나무
3. 게임 시작과 자동 점프
4. 실제 먹 비용·스무딩·붓소리를 사용하는 두 번의 자동 먹선
5. 먹분신 획득과 최고 생존자 중심 카메라
6. 황금붓과 먹물방울 아이템
7. 좌우 해태 경고와 풍맥·풍향 변화
8. 모든 생존자의 실제 사망 처리와 게임오버 두루마리
9. `최연소밴드 · 김승연 · 최성빈` 엔드카드

성장 화면은 로비 전용이라는 실제 게임 규칙을 유지하기 위해 플레이보다 먼저 보여
준다. 결과 화면도 임의 UI가 아니라 실제 `Kill → GameOver → 두루마리` 흐름을 사용한다.

## 촬영 안전장치

- 촬영 Play가 시작되기 전에 Score, 영구 성장, 옵션을 메모리 저장소로 교체한다.
- 최초 튜토리얼 완료 상태도 메모리에서만 제공한다.
- 촬영 중 낮게 떨어지는 캐릭터는 안전 높이로 복귀시키고 초반 장애물 보호를 준다.
- 촬영 종료 또는 Play 종료 시 기본 저장소를 복원한다.
- 촬영용 컨트롤러와 자동 획 API는 `UNITY_EDITOR`에서만 컴파일된다.
