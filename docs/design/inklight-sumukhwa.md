# 보유 먹빛 수묵 원화 — 2026-09-04

사용자 요청: 먹빛을 동양화 느낌으로 다시 디자인.

| Before | After | Why |
| --- | --- | --- |
| 큰 원 테두리가 둘러싼 먹빛 문양 | 갈필과 먹 농담을 가진 단일 붓방울 | 작은 재화 아이콘에서도 먹빛 자체를 읽게 함 |

- 채택 파일: `Assets/Resources/MukJump/UI/PermanentGrowth/pg_inklight_sumukhwa_v1.png`
- 생성 모드: Codex 내장 image_gen. 별도 API/CLI는 사용하지 않음.
- 채택 원본: `exec-27ce659d-83fa-4ccd-98e8-3fbb4ddcfcec.png`, 1254×1254, genuine RGBA.
- 원본을 복사했으며 픽셀 편집·외부 게임 원화 사용·기존 PNG 덮어쓰기는 하지 않음.
- 적용 범위: 성장 헤더의 보유 먹빛 아이콘만. 84×84, 위치·재화 숫자 68px, white tint, preserveAspect, 입력 비차단을 유지.
- 새 원화 임포트 전에는 기존 `pg_root_emblem`으로 폴백하며 둘 다 없으면 숫자만 표시.
- Unity 빌더의 기존 자동 설정에 포함: Single/FullRect, PPU100, alpha, max512, Bilinear, Clamp, mipmap off, CompressedHQ.
- 빈 수묵 테두리 후보는 반복 생성에서 배경이 RGB에 남아 모두 반려. 런타임에 포함하지 않음. 진행 칸의 기존 빈/찬 구분 및 64px/80px 규격은 유지.
- 원본 alpha 유무·투명 가장자리·짙은 중심 검사, 재화 원화/크기 회귀 및 모바일 임포터 계약을 테스트에 추가.
- Unity 화면 반영·실기기 확인은 아직 하지 않음. 씬 YAML과 `docs/ai-usage-log.md`는 수정하지 않음.
- 검증 결과: 원본 RGBA, 경계 alpha 최대 1/255, 몸통 RGBA=(37,35,31,253). 런타임/에디터 Roslyn 컴파일 종료 코드 0. 로그는 `output/growth-readability/compiler/inklight-Assembly-CSharp*-compile.log`. `git diff --check` 통과, 기본/프로젝트 Editor.log의 `error CS|Exception` 일치 없음. 추가 Unity 테스트는 아직 실행하지 않음.

## 채택 디자인 생성 프롬프트

```text
Use case: game UI icon, traditional Korean sumukhwa ink painting.
Redraw the ink-light resource drop for the mobile game MukJump. Use the supplied existing game icon ONLY as brush texture and monochrome style reference. Create ONE isolated full, filled ink droplet, no surrounding circle. Short slightly leaning pointed brush tip flowing into a generous rounded lower ink pool, subtly asymmetrical hand-painted silhouette. Bold deep warm charcoal (#1C1B1A) core, irregular gently feathered ink-soak edges, restrained dry-brush grain and smoky gray wash within the form. It should look like an actual calligraphy brush pressed and lifted on hanji, NOT a perfect vector teardrop, NOT a glossy liquid/rendered gem. Keep the silhouette compact and very legible down to a 64px UI icon; few broad strokes rather than fine busy lines. The single droplet occupies about 86% of square canvas height and 68% of width, visually centered with all brush tips fully inside and equal safe margins. Genuine transparent RGBA background including all exterior negative space; no paper rectangle or painted checkerboard, no shadow, no halo, no surrounding ring, no detached splatters, no text, no seal, no face, no red, no gold. Preserve natural partial transparency in feathered edges. Output square PNG.
```

## 채택 투명 배경 편집 프롬프트

```text
Edit the provided ink-drop asset. Keep its exact hand-painted brushwork, dark ink body, silhouette and placement. REMOVE the entire light gray checkerboard background. Output a PNG with genuine alpha channel: outside the ink drop all pixels must be fully transparent, NOT a picture of a transparency checkerboard, NOT white, NOT gray. Gray and white squares must not appear anywhere in the pixel RGB image. This is a production sprite to overlay on a moving game background. Preserve soft partial alpha on natural dry-brush edges and interior grain. No new elements, no paper, no shadow, no text. Keep square canvas.
```
