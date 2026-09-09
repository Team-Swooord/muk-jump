# 맵별 느린 배경 연출

## 적용 사양

- 원본 산수화·달·나무·UI는 고정한다. 기존 한지색 구름에 신규 원화 6종을 추가했다.
- 배경 Sprite 참조와 테마를 연결하므로 누락 맵·정렬 변경에도 잘못된 효과로 치환하지 않는다. 알 수 없는 맵/누락 에셋은 기본 구름으로 폴백한다.
- 공통 카메라 자식 AmbientCloudView 하나, 고정 SpriteRenderer 4개를 재사용한다. Low/Medium/High 2/3/4개, 재질 복제·런타임 로드·매 프레임 생성/삭제 없음.
- 맵 전환은 효과가 0.5초 동안 옅어진 뒤 alpha=0에서 원화를 교체하고 0.5초 동안 나타난다. 빠른 요청은 최신 맵만 반영한다. 이동 시계는 초기화하지 않는다.
- 기존 배경은 1초 교차 전환한다. 효과의 프레임 delta는 0.05초 제한으로, 20fps 미만/복귀 장시간 프레임에서는 원화 전환이 배경보다 조금 늦게 끝날 수 있다.
- 초당 화면 폭 이동률 0.001215~0.004968 (10초 약 1.2~5.0%). 최초 속도의 1.8배로 가로 이동만 뚜렷하게 한다. 작은 상하 흔들림·회전·명도 변화는 기존 85~145초 주기를 유지하며 급한 떨림/번쩍임은 없다.
- 일시정지·앱 비활성·움직임 줄이기는 이동/흔들림 시간을 멈춘다. 맵 교체 자체는 완료해 배경과 맞춘다.
- sortingOrder=-8, 원본 배경 앞/플레이 오브젝트 뒤. 회전 후 높이도 화면의 16% 이하로 제한하며 하단 여백을 비운다. 화면 밖에서만 순환한다.
- 씬/스프라이트 importer는 MukJumpSceneBuilder.Build로 생성한다. 원화 픽셀 편집이나 씬 YAML 수동 편집 없음.

| 맵 | 배경 장식 | 움직임 |
|---|---|---|
| 고요한 산길 | 기존 한지색 구름 | 오른쪽으로 느리게 흐름 |
| 바람 능선 | 가는 은청색 바람결 | 조금 빠른 흐름, 미세한 굽이 |
| 먹비 계곡 | 차가운 비안개 | 반대 방향으로 흐름, 약한 숨쉬기 |
| 검은 절벽 | 따뜻한 연무·먼지 | 가장 느린 흐름, 완만한 상하 이동 |
| 먹빛 성문 | 먹청색·금빛 별가루 | 미세한 회전과 명도 변화 |
| 월련 성해 | 은보라 연꽃 안개 | 반대 방향 흐름, 아주 느린 흔들림 |
| 천하수 | 푸른 물결·금빛 실선 | 완만한 물결 흐름 |

## 원화와 생성 기록

2026-09-04, **Codex 내장 image generation 모드**. 신규 생성 6회, 1536×1024 RGBA 투명 PNG. 기존 타 게임 픽셀은 사용하지 않았다. 기본 구름은 [이전 제작 기록](ambient-clouds.md)을 사용한다.

저장 폴더: `Assets/Resources/MukJump/Background/`.

- 1: `ambient_01_wind_ribbons_v1.png`
- 2: `ambient_02_rain_veil_v1.png`
- 3: `ambient_03_cliff_haze_v1.png`
- 4: `ambient_04_gate_stardust_v1.png`
- 5: `ambient_05_lotus_mist_v1.png`
- 6: `ambient_06_river_current_v1.png`

각 생성에 사용한 전체 프롬프트는 아래 공통 본문에 해당 테마 문단을 붙인 것이다.

```text
Use case: stylized-concept. Asset type: production transparent PNG sprite atlas for MukJump, a warm beige Korean ink-wash mountain mobile game. Generate an ORIGINAL atmospheric background decoration atlas, 1536 x 1024 landscape canvas, with EXACTLY THREE distinct long horizontal effects, ONE centered in each equal-height horizontal row. Each whole shape fits inside its row with at least 32px genuinely transparent padding on all sides. Low wide silhouette approximately 5:1. Keep the canvas GENUINELY TRANSPARENT with actual alpha; no opaque paper, no backdrop, no checkerboard baked into pixels. Delicate hand-painted Korean sumukhwa, watercolor feathered edges, fine hanji pigment grain INSIDE silhouettes only. Muted desaturated palette, understated distant background layer, elegant and minimal, no thick cartoon outlines, no photoreal 3D clouds. No landscape/mountains/buildings/characters, no text/labels/grid/borders/watermark.
```

### ambient_01_wind_ribbons_v1.png

```text
Theme: WIND RIDGE. Three pale ivory and very pale sage-blue wind ribbons / elongated cirrus wisps. Each is a few long flowing silk-like S curves with tapered brush ends and lots of transparent space between filaments. Thin, airy, directional; not puffy clouds and not ornamental scrolling cumulus. Distinct silhouettes across the three rows. Palette ivory #F5F1E6, pale blue-gray #B8C6C6, a little beige.
```

### ambient_02_rain_veil_v1.png

```text
Theme: INK RAIN VALLEY. Three distinct flat banks of cool rain mist, softly melting blue-gray watercolor veils, with a handful of very fine descending ink threads contained entirely inside each row. Low-contrast translucent slate #B8C4CA and ivory, long frayed mist edges. No large raindrop balls and no bright highlights; this is distant wet valley haze, not a foreground weather warning.
```

### ambient_03_cliff_haze_v1.png

```text
Theme: BLACK CLIFF. Three distinct low bands of warm smoke / valley dust haze in wispy curling layers, softly rising charcoal-brown pigment with a few sparse sand-colored motes tightly inside each silhouette. Gray umber #99958A, muted warm ivory #E2D6BA. Smooth dispersed smoke edges, mysterious but light enough over beige. Not black opaque smoke, no flames, no red particles, no puffy white clouds.
```

### ambient_04_gate_stardust_v1.png

```text
Theme: INK GALAXY GATE. Three distinct very long sweeping crescent fragments of ink-blue nebula haze with thin muted champagne-gold dust trails, tiny sparse gold specks close to each wisp. Arc fragments stretched horizontally, NOT closed rings. Desaturated ink-blue #87959E fading into ivory #EEE9DA, small soft gold #D5BC82 accents. Magical East Asian ink painting, not neon space CGI and not ordinary cumulus.
```

### ambient_05_lotus_mist_v1.png

```text
Theme: CELESTIAL LOTUS. Three distinct long translucent mist banks with two or three very faint elongated lotus-petal silhouettes dissolving into each bank. Silvery lilac #C9C7D8, pale moon ivory #F5F1E6, subtle champagne flecks. Peaceful soft gauze, delicate upward-curved petal shapes blended into the mist. NOT full flowers, not flower icons, not bright pink, not opaque objects. Sparse and ethereal.
```

### ambient_06_river_current_v1.png

```text
Theme: HEAVENLY INK RIVER. Three distinct long meandering celestial-water ribbons, a few fine flowing parallel streamlines with soft blue-gray ink diffusion and sparse ivory/gold glints. Elongated horizontal S curves, tapered translucent ends. Muted blue-gray #9DAFB6, ivory #F5F1E6, restrained old gold #D0BB89. Fluid layered brush currents, not waves splashing, not ordinary clouds, not a full landscape.
```

## 원화 QA

- 6개 최종 원본의 실제 alpha 확인. 흑절벽/성문은 원화의 투명 여백 위치에 맞춰 builder에서 각각 Y(top 기준) 360/710, 350/665로 슬라이스한다. 나머지는 세 등분.
- 흑절벽/성문 여백 재배치 편집 2회는 실제 alpha 없는 결과로 반려했다. 게임에는 미포함이며 `output/ambient-clouds/drafts/*_v2_rejected.png`에 보관했다. 최종은 실제 alpha가 있는 v1 원본이다.
- 반려 편집의 프롬프트(각 v1 원본을 참조, 내장 편집 모드):

```text
Edit this sprite atlas ONLY to repair the THREE equal-height row layout. Preserve the existing ink-wash artwork, colors, motifs, real transparent alpha and 1536x1024 canvas. Fit each entire effect including every glowing edge and speck strictly within these safe rectangles: top row x=48..1488,y=40..300; middle row x=48..1488,y=382..642; bottom row x=48..1488,y=724..984. The complete rows y=301..381 and y=643..723 must be PURE TRANSPARENT alpha 0, and top/bottom/side margins also pure alpha 0. Compress/reposition the artwork inside its own row as needed. No elements can cross those row boundaries, no hard cropped wisps, no added background, no checkerboard, no text or grid. It will be sliced exactly at y=341 and y=682, so transparent gutters are essential.
```

## 검증

### 이동 가시성 조정 — 2026-09-08

- 7개 맵의 가로 이동 속도를 공통 1.8배로 조정. 원화·방향·투명도·흔들림·품질별 개수는 유지했다.
- 관련 회귀 검사 **52/52 통과**(Play 모드 정지/복귀 및 맵 전환, 밤 전환 포함).
  최초 실행 1건은 Unity 오디오 초기화/도메인 리로드 오류로 중단됐고 같은 코드의 재실행에서 통과했다.
- `output/ambient-clouds/clearer-motion/verified-results.xml`과 7개 맵의 실제 시간 1배속 GIF를 보관했다.
  영상은 UI를 제외한 Unity URP 렌더이며 실기기 성능 검사나 새 배포 빌드는 아니다.

- AmbientCloudThemeTests: 7개 테마 매핑, 누락/재정렬/폴백, alpha/슬라이스 여백, 100회 전환, 역방향 화면 밖 순환, 4화면비에서 높이/배치, 투명 상태 원화 교체.
- AmbientCloudRuntimeTests: 실제 Play 상태의 맵 전환 coroutine + 최신 요청 + 움직임 줄이기 + 즉시 복귀/재활성화.
- 기존 AmbientCloudViewTests의 속도·품질·재사용·카메라 이동·배경 반전 계약도 유지한다.
- `MukJump > 검증 > 맵별 움직이는 배경 프리뷰 저장`: 실제 빌더/URP 배경을 7맵 × 4화면비로 저장한다. UI 제외 Editor 렌더이며 실기기/Device Simulator 캡처는 아니다.
- 산출물: `output/map-atmospheres/`. 실기기 GPU·발열·배터리 및 새 TestFlight 빌드는 별도 확인이 필요하다.

### 확인 결과 — 2026-09-04

- Unity 6000.5.9f1 씬 빌더 재생성 성공: `output/map-atmospheres/scene-build.log`.
- 전체 EditMode 스위트 **1,078/1,078 통과**, 실패/건너뜀 0. 실제 Play 진입 테스트 포함.
  결과: `output/map-atmospheres/editmode-final-results.xml`.
- 실시간 전환 테스트 초기 실패는 EnterPlayMode 도메인 재로드가 테스트의 캡처 클로저를 유실한 원인이었다.
  정적 테스트 헬퍼로 수정 후 개별 2/2 및 전체 스위트 재검증 통과. 제품 코드 예외와 구분한다.
- 실제 URP 렌더: 7개 맵 각각 1080×1920, 1179×2556, 1440×3200, 1920×1080 총 28장,
  시간별 420프레임. 원화별 투명 alpha·슬라이스 여백·품질별 고정 렌더러·높이 제한 검증 통과.
- `scene-build.log`, `preview.log`, 검사 시점 Unity `Editor.log`에서 `error CS|Exception` 일치 0건.
  테스트 로그의 의도적 예외 주입과는 별개다.
- 독립 읽기 전용 코드 검토에서 신규 P0~P2 결함 없음. 권고된 실제 MapBackground coroutine 회귀 테스트도 추가·통과했다.
- 영상: `output/map-atmospheres/seven-maps-slow-motion.mp4` (1080×1080, 15초, 4fps, 실제 속도 1배속).
  Unity 실제 배경 렌더 7개를 비교용으로 배치한 영상이며 UI/실기기 촬영은 아니다.
- `seven-maps-overview.png`와 인코딩 후 디코딩한 네 시점으로 배치/방향/지속 시간을 확인했다.
