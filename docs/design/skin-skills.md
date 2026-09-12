# 연동 보상과 전용 스킨 비기

## 적용
- 기존 의상 ID 0–42 유지. 인연 매듭(BondKnot) 43, 바람부채(WindFan) 44.
- 인연 매듭: 현재 Apple 제공자 인증과 같은 계정의 서버 프로필 적용/저장 완료를 모두 확인한 뒤 기존 성장 JSON의 해금 비트에 저장한다. 다른 계정/미인증/기록 선택 대기에는 지급하지 않는다. 단순 연동으로 판 기록·0m 순위를 생성하지 않는다.
- 장착된 인연 매듭: 새 판에서 안내·붓 전환이 끝나 실제 게임이 시작될 때 먹방울 효과 50m를 한 번 받는다. 수집 1회로 기록하며 광고 부활/분신 생성/일시정지 복귀에는 재지급하지 않는다.
- 바람부채: 먹방울 아이템 수집 5회로 해금. 장착 시에만 오른쪽 아래 한지 버튼. 클릭하면 살아 있는 본체만 25m 보호 상승하고 재사용까지 실제 플레이 20초를 기다린다. 특수 상승 중 재발동, 본체 부활, 분신 발동은 없다. 스킬 사용은 아이템 수집으로 세지 않는다.
- 게임 종료·전환·튜토리얼·일시정지·앱 비활성에서는 사용하지 못한다. 대기시간은 일시정지/광고 부활에 초기화되지 않는다.
- 기존 점프 VFX 풀을 재사용한다. 새 반복 파티클·새 SDK·서버 테이블은 없다. 원래 먹 몸체와 충돌 크기는 유지한다.
- ko/en/ja의 이름·설명·해금 조건·버튼·짧은 이야기 포함.
- 서버/계정 삭제, 네이티브 Archive, TestFlight 업로드는 이 기능 작업에서 실행하지 않았다.

## 생성 에셋
내장 image_gen 모드로 제작. 원본 생성 결과는 Codex generated_images에 보존하고 프로젝트로 복사했다. 참조는 기존 skin_bell_ribbon_v1의 굵은 어린이 낙서 그림체만 사용했다. 실제 투명 alpha PNG, 씬 빌더의 공통 Single/FullRect/512 임포트를 사용한다.

- Assets/Resources/MukJump/Skins/skin_bond_knot_v1.png
- Assets/Resources/MukJump/Skins/skin_wind_fan_v1.png

### 인연 매듭 최종 프롬프트
Use case: stylized-concept. Asset type: transparent 2D game wearable accessory sprite for a naive Korean ink-drop creature. Create a NEW tiny red traditional friendship knot with two short cream paper feather streamers: 'Bond Knot', a reward for linking an account, evokes a fortunate first leap. Reference image only supplies childish coarse crayon/ink style, NOT its bell or bow shape. Thick wobbly black ink outlines, very simple uneven coloring, muted vermilion #AE1C3C and warm paper #EAE3D2. One compact sideways knot near the upper side of a round head, dangling two SHORT feather-shaped strips, no writing. Front view, centered asset, occupies 80% of square canvas; truly transparent background with actual alpha, generous transparent margin. Accessory ONLY, no creature/body/eyes/head, no Apple logo, no icon tile, no backdrop, no shadow, no realistic cloth/material detail, no metallic shine, no text/watermark. Match a child's endearing ink doodle rather than high-detail fantasy art.

### 바람부채 최종 프롬프트
Use case: stylized-concept. Asset type: transparent 2D game wearable accessory sprite. Create a NEW oversized little Korean paper folding fan worn diagonally on the back of a round ink-drop creature, for a wind-leap skill. Reference image only supplies childish coarse crayon/ink style, not the bell or ribbon subject. Accessory ONLY: one open five-fold fan with broad warm-paper panels, very thick wobbly black ink outline, 3 simple muted blue-grey curved wind marks and a tiny muted red fastening knot at the base. Front orthographic view, fan tilted 20 degrees, centered, spans 80% of square canvas. Endearing wonky doodle by a young child, naive flat shape, uneven dry ink edges, black #1C1B1A, paper #EAE3D2, bluegrey #758A8A, small red #AE1C3C. Genuinely transparent background actual alpha, generous transparent margin. No creature/body/head/eyes/arms, no text, no icons or UI, no scenery, no gradient backdrop, no shadows, no realistic silk or ornate fantasy details, no metallic highlights or watermark. It will render behind an unchanged black round creature, fan upper left showing outside body.

## 검증
- 기능/계정 격리 테스트 11개 통과(실패·건너뜀 0). 미인증·오프라인·다른 계정·프로필 교체·저장 실패, 보상 중복, 0m 순위 생성 방지, 새 판/튜토리얼/부활, 본체·분신 구분, 대기시간과 아이템 업적 분리를 검사했다.
- 실제 Play Mode 표시·입력 테스트 1개 통과. 두 의상의 한·영·일 설명/조건/진척도와 착용 경계, 스킬 버튼의 화면 안전영역, 터치 소비, 연타 방지, 일시정지 숨김을 확인했다. 생성된 화면에서 머리/등 장착, 설명과 버튼 배치를 직접 검수했다.
- iOS 비개발 조건 managed 코드 36개 어셈블리 컴파일 성공. 재생성된 Main의 누락 스크립트 0, 씬 소스 일치, Player 안전 검사 문제 0. 네이티브 Archive/실기기 검증은 아니다.
- 모의 계정과 메모리 저장소로 검사했다. 실제 아이폰 Apple 인증 팝업 및 뒤끝 서버 저장 왕복은 미검증이며 서버 계정은 수정·삭제하지 않았다.
- 씬 재생성 과정에서 기존 장애물 텍스처의 임포트 일관성 경고가 발생했다. 이번 두 스킨의 컴파일/표시 검사와는 별개이며 프로젝트 전체 무경고를 의미하지 않는다.
- 로컬 증거: `output/qa/skin-skills/rules-tests.json`, `presentation-tests.json`, `scene-audit.json`, 두 의상×3개 언어 PNG와 `skill-ready.png`/`skill-cooldown.png`. 화면은 에디터 캡처이며 iPhone 촬영본이 아니다.
