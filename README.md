# 먹점프 (Ink Jump)

> "선 하나가 발판이 되고, 발판 하나가 그림이 된다"

NHN NAN 2026 · Game × AI Hackathon 제출작 — Team-Swooord

동양화(수묵화) 감성의 드로잉 클라이밍 게임입니다. 캐릭터 **먹방울이**는 일정 주기마다
스스로 점프하고, 플레이어는 화면에 손가락으로 선을 그어 발판을 만들어 점프의
방향과 높이를 유도합니다. 플레이어가 대충 그린 스케치는 AI가 수묵화 붓질 질감의
발판으로 완성해, 화면 전체가 하나의 산수화가 됩니다.

## 게임 방법

| 항목 | 내용 |
|---|---|
| 목표 | 발판을 그려 먹방울이를 최대한 높이 올려 보내기 (최고 고도 = 점수) |
| 조작 | 화면 하단 여백(한지 영역)에 손가락으로 선을 긋기 → 발판 생성 |
| 점프 | 먹방울이는 일정 주기마다 자동 점프. 발판의 위치·각도·길이가 궤적을 결정 |
| 종료 | 발판을 놓쳐 화면 아래로 추락하면 게임 오버 → 재도전 |

## 실행 방법

### APK (제출 빌드)

Android 기기에서 APK 설치 후 바로 실행. 별도 계정·유료 라이선스 불필요.
AI 수묵 변환은 네트워크·API 키가 없어도 내장 먹 텍스처 폴백으로 동일하게 동작합니다.

### 에디터에서 실행 (소스 빌드)

1. Unity **6000.3.10f1** (URP) 설치
2. 이 저장소를 클론 후 Unity Hub에서 프로젝트 열기
3. 최초 1회: 메뉴 `MukJump > Build Main Scene` 실행 → `Assets/Scenes/Main.unity` 생성
4. `Main.unity` 씬을 열고 Play — Game 뷰를 세로(9:16) 비율로 설정

## 프로젝트 구조

```
Assets/
  Art/                캐릭터·배경 원본 (PNG)
  Scripts/
    Core/             GameManager, ScoreManager, CameraFollow
    Player/           AutoJump, PlayerController
    Drawing/          StrokeCapture, BezierSmoother, PlatformCollider
    AI/               SketchToInkService(수묵 변환), 폴백 잉크 스타일
  Scenes/Main.unity   메인 씬
docs/ai-usage-log.md  AI 활용 내역 (제출물 4번 원본 자료)
```

## 팀

- Team-Swooord (2인) — 역할 분담은 `docs/` 팀원 롤 기술서 참고 (제출물 5번)

## 에셋 · 라이선스

- 캐릭터·배경 아트: 팀 자체 제작 (AI 보조, 상세 내역은 `docs/ai-usage-log.md`)
- 외부 유료 에셋 없음
