# 먹점프 외부 에셋 고지

## Unity Pipeline

- com.unity.pipeline 0.6.0-exp.1 — Copyright © 2026 Unity Technologies.
- 공식 Unity Package Manager 레지스트리 의존성. 패키지 원본 소스는 이 저장소에 재배포하지 않는다.
- Unity Package Distribution License: https://unity.com/legal/licenses/unity-package-distribution-license
- 먹점프는 Player용 원격 제어를 비활성화한다. 포함되는 바이너리에는 패키지에 동봉된 고지와 라이선스 조건이 적용된다.

## Kaisei Decol 일본어 서체

- Copyright 2020 The Kaisei Project Authors (https://github.com/Font-Kai/Kaisei).
- 원본: https://github.com/google/fonts/tree/main/ofl/kaiseidecol
- `KaiseiDecol-Regular.ttf`를 수정 없이 일본어 UI와 닉네임 표시용으로 사용한다.
- SIL Open Font License 1.1. 배포 원문: `Assets/StreamingAssets/ThirdParty/KaiseiDecol-OFL.txt`.

## Twemoji 지역 아이콘

- Copyright Twitter, Inc. and other contributors. Twemoji v14.0.2.
- 원본: https://github.com/twitter/twemoji/tree/v14.0.2/assets/72x72
- 국가·지역 깃발 258개와 지구 아이콘(1f310)을 수정 없이 사용한다.
- 라이선스: CC BY 4.0 https://creativecommons.org/licenses/by/4.0/
- 원문 및 배포 고지: `Assets/StreamingAssets/ThirdParty/Twemoji/`.
- 아이콘은 기기 지역 설정을 나타내며 실제 국적·현재 위치를 인증하지 않는다.

## DOTween

- DOTween 1.3.030 — Copyright (c) 2014–2026 Daniele Giardini / Demigiant.
- 공식 배포 원본: `Assets/Plugins/Demigiant/DOTween/` (core 및 런타임 Modules).
- 라이선스: https://dotween.demigiant.com/license.php — 상용 이용 가능. 원본 readme 및 저작권 고지를 보존한다.
- 이 저장소에는 공식 배포 파일을 수정 없이 포함한다. DOTween Pro는 사용하지 않는다.

이 문서는 먹점프 1.0의 저장소와 스토어 빌드에 실제로 포함되는 외부 미디어의 출처와
재배포 근거를 기록한다. SDK와 Unity 패키지는 각 패키지에 동봉된 라이선스 및 서비스
약관을 따른다.

## Firebase Unity SDK

- Google LLC, Firebase App(Core) / Analytics **13.16.0**
- 공식 배포 원본: [Google Unity 아카이브](https://developers.google.com/unity/archive#firebase)
- 고정 패키지: `Packages/Firebase/com.google.firebase.app-13.16.0.tgz`, `com.google.firebase.analytics-13.16.0.tgz`
- 두 배포 패키지의 `LICENSE.md`(Apache License 2.0)와 포함된 네이티브 라이브러리 고지를 원본 그대로 보존한다.
- Firebase/Google Analytics 서비스 이용에는 별도 [약관·개인정보 조건](https://firebase.google.com/support/privacy)이 적용된다.

## Nanum Brush Script

- 파일: `Assets/Resources/MukJump/Fonts/NanumBrushScript-Regular.ttf`
- 저작권: Copyright © 2010 NHN Corporation. Font designed by Sandoll Communications Inc.
- 라이선스: SIL Open Font License 1.1
- 원본: https://github.com/google/fonts/tree/main/ofl/nanumbrushscript
- 고정 원본 커밋: `6a003b5eb672dc8bf5bff5937cf5863f8b175445`
- SHA-256: `27ceaf578c96f594cdf07fe0181b251790acbb746a164e45c1f6473f89911a31`
- 라이선스 원문: `Assets/ThirdParty/NanumBrushScript/OFL.txt`
- 배포물 동봉본: `Assets/StreamingAssets/ThirdParty/NanumBrushScript-OFL.txt`

## Brush sound

- 파일: `Assets/Resources/MukJump/Audio/SFX/SFX_Brush_Community.mp3`
- 원본: Freesound `brush.wav`, Reitanna, sound 332666
- 라이선스: Creative Commons Zero 1.0 (CC0)
- 출처: https://freesound.org/people/Reitanna/sounds/332666/

## Inkdrop Ascent background music

- 파일: `Assets/Resources/MukJump/Audio/InkdropAscent.wav`
- 제작: 팀 Suno Pro 계정에서 유료 구독 중 생성
- 사용 범위: 게임 내 반복 배경음악
- 권리 기록: 생성 당시 유료 구독 상업 이용권 확인 기록을
  `docs/ai-usage-log.md`와 제출 문서에 보관

## 프로젝트 자체 제작물

캐릭터, 배경, 장애물, 성장 UI, 절차 생성 공용 버튼 마스크와 사망·게임오버 WAV는
프로젝트를 위해 제작·가공한 자산이다. 재배포 제한이 있던 Healthset OTF, Pixabay MP3
두 개와 상업 라이선스 증빙이 없던 Pngtree 파생 버튼은 현재 빌드 경로에 포함하지 않는다.

### 설정 수묵 아이콘

설정 아이콘 9종(`Assets/Resources/MukJump/UI/Common/settings_icon_*_v1.png`)은
2026-09-06 Codex 내장 이미지 생성 도구로 새로 제작했다. 사용자 제공 게임 화면과
Supercell 공식 안내는 배치·계정 동선 참고로만 사용하고 타 게임 원화는 포함하지 않았다.
투명 RGBA 원본·전체 프롬프트·Unity 적용 조건은 `docs/design/settings-ink-icons.md`에 보존한다.

### 결과창 한지 두루마리 롤

- 파일: `Assets/Resources/MukJump/UI/Common/scroll_roll_hanji_v2.png`
- 제작: 2026-09-05 Codex 내장 이미지 생성 도구로 먹점프용 신규 제작. 타 게임 원화는 사용하지 않았다.
- 투명 RGBA 원본을 보존하고 Unity 씬 빌더의 알파 영역 슬라이싱만 적용한다.
- 기존 자체 제작 `pg_hanji_background.png`는 변경 없이 종이 면에 재사용한다.
- 전체 생성 프롬프트·모션 사양: `docs/design/hanji-scroll-result.md`.

### 보유 먹빛 수묵 아이콘

- 파일: `Assets/Resources/MukJump/UI/PermanentGrowth/pg_inklight_sumukhwa_v1.png`
- 제작: 2026-09-04 Codex 내장 이미지 생성 도구로 먹점프용 신규 제작. 프로젝트의 `pg_root_emblem`을 붓결 참고로 사용했다.
- 규격: 1254×1254 투명 RGBA 원본. 체크무늬가 구워진 후보는 제외했으며 원본 픽셀을 그대로 포함한다.
- 적용: 상단 보유 먹빛 아이콘. 기존 진행 칸·외부 게임 원화는 변경하거나 포함하지 않는다.
- 전체 생성/투명 편집 프롬프트와 검증 범위: `docs/design/inklight-sumukhwa.md`

### 느린 배경 구름 원화

- 파일: `Assets/Resources/MukJump/Background/ambient_cloud_atlas_v1.png`
- 제작: 2026-09-04 Codex 내장 이미지 생성 도구로 먹점프용 신규 제작
- 규격: 1536×1024, 투명 RGBA, 가로 구름 3종. 생성 원본을 그대로 사용하고 Unity 빌더에서 슬라이스한다.
- 사용자가 제공한 도원경 영상은 움직임 방향·속도 참고만 사용했다. 영상 프레임이나 타 게임의 원화는 게임 에셋에 포함하지 않는다.
- 생성 모드·전체 프롬프트와 적용 사양: `docs/design/ambient-clouds.md`

### 맵별 움직이는 배경 원화

- 파일: `Assets/Resources/MukJump/Background/ambient_01_wind_ribbons_v1.png`, `ambient_02_rain_veil_v1.png`, `ambient_03_cliff_haze_v1.png`, `ambient_04_gate_stardust_v1.png`, `ambient_05_lotus_mist_v1.png`, `ambient_06_river_current_v1.png`
- 제작: 2026-09-04 Codex 내장 이미지 생성 도구로 먹점프용 신규 제작, 1536×1024 투명 RGBA.
- 원본 픽셀 그대로 사용하며 Unity 빌더에서 각각 3개 Sprite로 슬라이스한다. 타 게임 원화/영상 픽셀은 포함하지 않는다.
- 생성 모드·전체 프롬프트·선택/반려 내역: `docs/design/map-atmospheres.md`
