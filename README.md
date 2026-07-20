# 먹점프 (Ink · Jump)

> NHN NAN 2026 · Game × AI Hackathon 제출작
> 동양화 붓질로 그리는 발판 + 자동으로 오르는 캐릭터 + AI가 완성하는 수묵화

"선 하나가 발판이 되고, 발판 하나가 그림이 된다"

## 게임 소개

캐릭터 **먹방울이**는 일정 주기마다 스스로 점프합니다. 플레이어는 점프 타이밍에 맞춰
화면에 손가락으로 선을 그어 발판을 만들고, 발판의 위치·각도·길이로 캐릭터의 다음 궤적을
유도합니다. 플레이어가 대충 그린 스케치는 AI가 실시간으로 수묵화 붓질 질감으로 완성해,
그림 실력과 무관하게 화면 전체가 하나의 산수화처럼 통일된 경험을 만듭니다.

- 장르: 캐주얼 아케이드 클라이밍 (드로잉 플랫포머)
- 플랫폼: 모바일 (Android APK)
- 엔진: Unity 2D
- 플레이 타임: 1회 1~3분, 반복 도전형 (하이스코어 경쟁)

## 조작 방법

1. 화면을 손가락으로 눌러 선을 그으면 발판이 생깁니다.
2. 캐릭터는 자동으로 점프하며, 그려둔 발판에 착지하면 상승을 이어갑니다.
3. 발판을 놓치거나 잘못 그리면 캐릭터가 추락합니다 — 다시 도전하세요.

## 실행 방법

- APK 파일을 다운로드해 Android 기기에 설치 후 실행 (별도 로그인/라이선스 불필요)
- 또는 Unity 2022.x LTS 이상으로 `Assets/Scenes/Main.unity`를 열어 에디터에서 실행

## 프로젝트 구조

```
Assets/
  Art/
    Character/     캐릭터(먹방울이) 아트
    Background/     배경(수묵 산수화) 아트
  Scripts/
    Core/           GameManager, ScoreManager — 게임 상태·점수
    Player/         AutoJump, PlayerController — 자동 점프·캐릭터 상태
    Drawing/        StrokeCapture, BezierSmoother, PlatformCollider — 발판 드로잉 파이프라인
    AI/             SketchToInkService, FallbackInkShader — AI 수묵 변환 + 폴백
  Scenes/
    Main.unity
docs/
  ai-usage-log.md   AI 도구·프롬프트 활용 내역 (개발 중 계속 기록)
```

## 팀

- 승연 — (역할 기재 필요)
- 팀원 2 — (역할 기재 필요)

## 라이선스 / 에셋 출처

외부 에셋·오픈소스 사용 시 출처와 라이선스를 여기와 AI 활용 기술 문서(PDF)에 함께 명시합니다.
