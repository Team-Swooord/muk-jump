# 느린 배경 구름

> 기본 산길 구름의 최초 제작 기록이다. 현재 7개 맵별 효과·원화·최신 검증은 [맵별 느린 배경 연출](map-atmospheres.md)을 따른다.

## 적용

- 산과 달·나무·UI는 움직이지 않는다. 따뜻한 한지색 구름 실루엣만 오른쪽으로 이동한다.
- 공통 `Main Camera/Background/AmbientClouds` 하나로 로비·성장·플레이에 적용한다.
- 네 레이어의 초당 화면 폭 이동률은 0.00306 / 0.00432 / 0.00360 / 0.00270(최초 속도의 1.8배).
  화면 폭만큼 이동하는 데 약 4~6분이 걸린다. 한 바퀴 주기는 구름 자체 폭까지 포함해 더 길다.
- 3종 atlas 하나, 고정 SpriteRenderer 네 개. Low/Medium/High에서 2/3/4개 표시한다.
  매 프레임 생성·삭제·리소스 로드·재질 복제·난수·물리·UI raycast를 사용하지 않는다.
- sortingOrder=-8로 기존 배경(-10/-9) 앞, 캐릭터·발판 뒤에 둔다. 하단 플레이 여백은 비운다.
- 화면 밖으로 실루엣 전체가 나간 뒤 반대편으로 순환한다. 가로형에서는 화면 높이의 16%로 크기를 제한한다.
- 앱 비활성·일시정지·움직임 줄이기에서 시간을 멈춘다. 복귀 delta를 최대 0.05초로 제한한다.
- 배경 고도 전환·무한 구간 반전·50m 상승·카메라 재구도와 구름의 누적 시간을 분리한다.
- 씬·슬라이스·압축·필터는 `MukJumpSceneBuilder.Build`로 생성한다. 씬 YAML은 직접 수정하지 않는다.

## 원화

- 에셋: `Assets/Resources/MukJump/Background/ambient_cloud_atlas_v1.png`
- 2026-09-04, **내장 image generation 모드**. 새 이미지 생성이며 참조 원화 편집/복제가 아니다.
- PNG 1536×1024 RGBA, 실제 투명 alpha. 도원경 영상은 동작 참고이며 픽셀은 포함하지 않는다.
- 전체 프롬프트:

```text
Use case: stylized-concept. Asset type: a single production-ready transparent PNG cloud sprite atlas for MukJump, a Korean ink-wash mountain mobile game. Primary request: exactly THREE different long horizontal auspicious East Asian floating cloud banks, one per equal horizontal row, on a GENUINELY TRANSPARENT alpha background. 1536 x 1024 landscape canvas. Each row is independent, generous transparent padding between rows and at all four edges, nothing touches/crosses the row boundaries. First cloud long thin wisps with a few softly curling tips; second a fuller cloud bank with sparse traditional scrolling curl motifs; third an asymmetric medium bank with tapered mist trails. Shape should remain low and wide, approximately 5:1 cloud width/height. Style: delicate hand-painted Korean sumukhwa watercolor, diffuse ink wash and fine hanji grain INSIDE the cloud only, pale warm ivory cloud bodies (#F5F1E6), muted warm gray-brown light contours (#A9A090), understated shadows, edges softly feathered. Calm, elegant, original artwork that fits warm beige mountain paintings. NOT bold black cartoon outlines, not realistic 3D cumulus, not pixel art. No landscape, no sky, no sun, no particles, no square paper background, no checkerboard baked into the image, no text, no labels, no watermark, no logo. Original designs; do not reproduce any existing game's cloud art. This atlas will be sliced into 3 equal-height rows by Unity, so keep each entire silhouette centered within its own row with at least 24 transparent pixels all round.
```

## 검증 도구와 범위

- `AmbientCloudViewTests`: 속도, 화면 밖 wrap, 장시간 누적, 2/3/4개 품질, 텍스처/재질 재사용,
  네 화면비, 카메라 이동/줌, 재활성화, 빌더 소유자, 고도 배경 교체·반전.
- `AmbientCloudRuntimeTests`: 실제 Play 상태의 LateUpdate와 움직임 줄이기/플랫폼 비활성/복귀.
- `MukJump > 검증 > 느린 구름 프리뷰 저장`: 저장하지 않는 preview scene에 실제 빌더 결과를 만든다.
  게임 UI를 제외한 배경을 실제 URP로 렌더한다. 540×960 / 4fps / 15초 프레임은 실제 속도(1배속).
- `output/ambient-clouds/rendered`: 시간별 PNG, 1080×1920·1179×2556·1440×3200·1920×1080 이미지,
  일곱 고도 배경과 구름의 조합. 이는 Editor 렌더이며 Device Simulator/실기기 캡처가 아니다.
- Unity의 [RenderPipeline.SubmitRenderRequest](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.RenderPipeline.SubmitRenderRequest.html)와
  [Camera.scene](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-scene.html)을 사용한다.
- VFX 기준에 따라 실기기 GPU 시간·오버드로우·발열·배터리는 별도 확인이 필요하다.
  이번 원화/코드는 기존 TestFlight 아카이브에는 포함되지 않는다.

## 확인 결과 — 2026-09-04

- Unity 6000.5.9f1 전체 EditMode 스위트(실제 Play 진입 통합 테스트 포함): **1,062/1,062 통과**, 실패·건너뜀 0.
  결과: `output/ambient-clouds/editmode-verified-results.xml`.
- 씬 빌더 재생성 성공. `scene-build-final.log`, `preview-final.log`, 현재 Unity `Editor.log`에서
  `error CS|Exception` 일치 0건. 테스트 로그의 의도적 예외 주입은 해당 테스트의 기대 로그와 별개다.
- 네 화면비와 일곱 고도 조합의 실제 URP 렌더 확인. 독립 읽기 전용 검토에서도
  산수화 고정·미세한 구름 이동·하단 여백 유지 확인, 신규 P0~P2 지적 없음.
- 실제 속도 영상: `output/ambient-clouds/slow-cloud-preview.mp4` (540×960, 15초, 1배속).
  이는 게임에 연결한 코드/에셋의 배경 전용 Editor 렌더이며 실기기 실행 녹화는 아니다.
