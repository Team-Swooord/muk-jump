# 01 — 점프·착지 먹 파티클

상태: 구현·씬 적용·에디터 회귀·실제 Play 촬영·교차검증 통과. 실기기 성능 QA는 대기.
기준 HEAD `10b177a` + 2026-09-05 워킹트리.

## 의미와 변경 경계

발판을 밀어낸 순간과 발밑 충격이 작은 모바일 화면에서도 보이게 한다.
`GameFeedbackController.EmitJump/EmitLanding`의 기존 병합·cooldown·음향·링은 유지한다.
기존 `if (strength >= 0.45f) SpawnDroplets(...)`는 새 레이어가 없을 때만 폴백으로 사용한다.
AutoJump/PlayerController/중력/속도/먹 소모/체력/카메라/앱 설정은 변경하지 않는다.

## 타임라인과 자원

| 레이어 | 시각·동작 | 수명 | 예산 |
|---|---|---|---|
| BallisticInk | 기존 4×4 먹방울 atlas, 양옆과 도약 반대 방향 먹 튐, 0.17~0.43m | 0.38~0.62초 | L/M/H 22/34/48 |
| DryBrushMotes | 같은 atlas의 작은 갈필 가루, 0.08~0.17m, 알파 0.45~0.68 | 0.50~0.85초 | 12/24/36 |
| ContactWash | 기존 InkSplash 마스크, 가로 1.7~2.6m·세로 0.45~0.72m, 알파 0.23~0.34 | 0.35~0.48초 | 3/4/6 |

시작 프레임에 세 레이어가 나오되 크기/속도/소멸 시간이 다르다. 공중 점프에는 발판 번짐을 넣지 않는다.
`InkPalette.Ink`만 사용, additive가 아닌 alpha blend. 캐릭터 발 주변으로 범위를 제한한다.
일반 점프 L/M/H 방출은 9/13/18개, 최강 착지 High 25개. 총 활성 입자 하드 상한 90개.

기존 자원:
`Assets/MukJump/VFX/InkDropJump/Textures/T_VFX_InkDropletAtlas_512.png`, `T_VFX_InkSplash_512.png`.
새 비트맵이나 외부 라이선스/패키지는 추가하지 않는다.

## 구조·구현

- `Assets/Scripts/Core/InkContactParticles.cs`: 씬당 3 ParticleSystem·2 Material을 초기화하고 재사용.
- `Assets/Resources/MukJump/Shaders/InkContactParticle.shader`: URP2D, 알파 마스크 1회 샘플, vertex color.
- `Assets/Scripts/Core/GameFeedbackController.cs`: OnEnable 생성, Update 수동 Simulate, OnDisable 정리.
- `Assets/Editor/MukJumpSceneBuilder.cs`: 기존 텍스처 2개 직렬화 연결 후 Build Main Scene 재생성.

World simulation, loop/playOnAwake/emission/shape off, Shadow/Collision/Noise/Lights/Trails off.
반복 호출에서 Instantiate/Destroy/Resources.Load/Material 생성 없음. 장식 난수는 독립 xorshift 사용.
Reduced Motion 진입 즉시 입자를 비우고 기존 링/음향만 유지. 품질 강등 시 Clear 후 예산 적용.
Pause는 시뮬레이션 정지, Resume는 최대 50ms만 처리. 게임오버·로비·비활성화는 Clear.

## 검증과 완료 기준

1. Unity `InkContactParticleTests`: tier별 개수, 200회 재사용, 수명 종료, pause/resume, reduced, low-memory,
   게임 난수열 보존, 24분신 병합/OnDisable를 검사한다.
2. 기존 `MobileFeedbackPolishTests` 7개 재실행. 프로젝트 `Logs/Editor.log` 및 사용자 Editor.log의 새 컴파일 오류 확인.
3. Build Main Scene으로 실제 연결, Play 촬영 프레임의 `ActiveContactParticleCount > 0` 증거 확보.
4. 원본 Play 프레임으로 GIF 생성. 입자가 발/획/HUD를 가리지 않는지 확인하고 강도를 조정한다.
5. Low/Reduced 시각 및 실기기 FPS/발열은 별도 게이트. 검증하지 않은 항목을 완료 처리하지 않는다.

API 확인: [Unity EmitParams](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.EmitParams.html),
[수동 Simulate](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.Simulate.html).

## 실제 결과

- 신규 9/9: `output/quality-polish/particle-stage-01/ink-contact-tests-final.xml`.
- 기존 7/7: `output/quality-polish/particle-stage-01/mobile-feedback-tests-final.xml`.
- Main 씬은 Build Main Scene으로 재생성, atlas/splash Texture2D 참조 GUID 일치.
- 최종 촬영: `output/quality-polish/captures/20260905-112206/`.
  각 140프레임·20fps·7초, 원본 540×960. 합성/보간 없이 실제 ScreenCapture.
  High peak43, Low peak21, Reduced peak0. 단일 캐릭터 제어 촬영이며 최악 부하 측정 아님.
- 실제 PlayerLoop 한 프레임 동안 수동 Advance가 없으면 위치·수명 불변,
  Advance(0.020) 뒤 정확히 20ms 수명 감소 확인.
- 첫 촬영에서 작은 입자 가독성을 확인해 크기/농도를 조정했고,
  Reduced 설정 직후 1프레임 잔류를 발견해 동기 Changed 이벤트에서 Clear하도록 수정했다.
- 읽기 전용 보조 검토 승인. iOS/Android GPU 시간·GC/오버드로·발열 검증은 완료하지 않았다.
