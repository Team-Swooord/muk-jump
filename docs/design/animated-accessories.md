# 움직이는 뒷장식

먹 몸체·발·물리·기존 의상 ID는 유지한다. 새 ID 40–42는 장식 전용이며 능력이나 수치 보너스를 더하지 않는다.

| 장식 | 해금 | 움직임 |
| --- | --- | --- |
| 도깨비불 나들이 | 먹방울 아이템 20회 | 7fps, 불꽃 혀·불씨가 달라지는 4장 |
| 길 잃은 등불 | 정상 플레이 7판 | 4fps, 등과 술의 방향이 바뀌는 4장 |
| 달구름 둥지 | 밤에 200m | 4fps, 구름과 달의 위치가 다른 4장 |
| 기존 도깨비 불 | 기존 조건 유지 | 7fps, 고리 주위 불꽃의 4장 |
| 학·먹나비·접힌 차원 날개 | 기존 조건 유지 | 6fps, 펼침·접힘을 그린 각 4장 |
| 질풍 두건 | 기존 조건 유지 | 6fps, 이마를 유지하고 끈 방향이 바뀌는 4장 |
| 용꼬리 | 기존 조건 유지 | 6fps, 꼬리 끝이 움직이는 4장 |

로비·꾸미기 큰 미리보기·해금 화면·플레이어/분신에 적용한다. 작은 목록과 리더보드 아이콘, 미해금 실루엣은 정지 상태다. 히든은 기존처럼 `?`만 표시한다. 단단한 모자·탈·신발에는 불필요한 흔들림을 넣지 않는다.

## 렌더링과 검증 범위

- UI와 월드는 `InkSpriteAnimation`의 같은 4장 원화 참조를 재생한다. 메시·스케일·셰이더 변형으로 움직임을 만들지 않는다.
- 각 2×2 PNG는 빌더가 실제 원본 크기로 슬라이싱한다. FullRect, 중앙 피벗, 1024px 이하 압축, 밉맵 없음. 홀수 크기는 중앙 여백 1px을 제외한다.
- 동작 줄이기는 정지 원화다. 앱 비활성·일시정지·로비 옵션 뒤에서는 시간이 멈춘다.
- 월드는 기존 SpriteRenderer 한 개를 사용한다. 프레임 참조만 캐시하며 별도 애니메이션 메시·재질을 만들지 않는다. 새 파티클 시스템·광원·Bloom·패키지는 추가하지 않는다.
- 로비 잡기 또한 `muk_held`의 4장 원화(6fps)를 쓴다. 몸통을 찌그러뜨리지 않으며, 신발 2종은 각 포즈의 발끝 앵커를 따라간다. 놓기·앱 비활성·화면 닫기에는 즉시 원래 모습으로 돌아온다.
- 실제 iPhone GPU/배터리 측정이나 TestFlight 업로드는 이 작업에 포함하지 않는다.

집중 검증: 원화 4장 참조·프레임 순서·반복·난수 격리, UI 꼭짓점 불변, 신발 앵커·드래그 경계,
9종 실제 꾸미기/로비/플레이 프레임 재생, 일시정지·동작 줄이기·잠긴 의상 정지, 잡기/놓기 입력을 확인한다.
새 원화와 프롬프트는 `sprite-animation-art.md`, 화면 증거는 `output/qa/sprite-animation-2026-09-13/`에 둔다.

## 원화와 생성 프롬프트

기본 내장 imagegen으로 각 1회 생성했다. 실제 알파를 유지한 PNG를 아래 프로젝트 경로로 복사했으며, 기존 원화를 덮어쓰지 않았다. 기존 씬 빌더가 Single/FullRect/512px/압축/밉맵 없음으로 가져온다. 이름·해금 조건·뒷이야기는 기존 한국어·영어·일본어 번역 체계에 등록한다.

### Assets/Resources/MukJump/Skins/skin_blue_wisps_v1.png

Use case: stylized-concept. Asset type: transparent 2D Korean ink-game back accessory sprite, one square PNG. Subject: a loose halo made of five separate small turquoise-blue dokkaebi spirit flames arranged around a very large empty round center. Three flames over top and two at sides, no bottom flames. Each flame is a comically simple crooked teardrop with one little curled tip, no face. Thick imperfect dark charcoal crayon outlines, dusty blue flat hand-scribbled fills, naive kindergarten drawing, charmingly clumsy, deliberately low detail. Flat front view, no perspective. The flames must occupy only the outer rim, central 65% diameter completely empty transparent, generous margin all edges. Accessory ONLY, no character, no eyes, no body, no text, no logos, no glossy shading, no bloom, no sparkles, no magic circle, no gold, no checkerboard pixels. Genuinely transparent alpha background including center. This will sit BEHIND an existing simple black ink-blot character.

### Assets/Resources/MukJump/Skins/skin_paper_lanterns_v1.png

Use case: stylized-concept. Asset type: one square transparent PNG sprite for a Korean ink-doodle game's BACK HALO accessory. Subject: three tiny crooked paper lanterns floating on a wide horseshoe arrangement, one at top center, one on far left and one on far right. Little cream rice-paper oval lanterns with muted cinnabar-red caps, very short dangling tassels, charcoal scribble outlines. The imaginary round character is NOT drawn. The CENTER 65 percent circle is EMPTY actual transparency, lanterns stay on outer rim only, generous margin around all edges. Flat straight-on 2D, comically naive kindergarten crayon doodle, irregular black line weight, very simple flat fills, no realistic lighting, no dimensional rendering. No face, no eyes, no text or symbols, no character or mannequin, no supporting wire/ring, no gold, no checkerboard pixels, no backdrop. Genuinely transparent alpha background inside and around isolated lanterns. Cheerful modestly drawn folk accessory matching a simple black ink-blot character.

### Assets/Resources/MukJump/Skins/skin_moon_clouds_v1.png

Use case: stylized-concept. Asset type: one square transparent 2D sprite, BACK HALO accessory for a simple Korean ink-blot game character. Subject: a crooked crescent of three pale dusty lavender cloud wisps and one small off-white crescent moon at the very top, arranged on the outer rim of a large EMPTY invisible circle. Side clouds at left and right and one small cloud diagonally above right, loosely balanced, sparse, large individual shapes, not a continuous ring. The middle 65 percent diameter circle and bottom half center must be EMPTY actual alpha transparency. Do NOT draw the character. Drawing style: a young child with a thick black charcoal crayon, wobbly simple outlines, flat scribbled muted fills, low detail, endearingly clumsy folk doodle, front view with no perspective. Generous transparent outer margin. No faces, eyes, body, mannequin, text, logos, stars, gold, glossy gradients, realistic shading or bloom. Genuine transparent background, NOT a checkerboard drawing. Accessory alone, intended behind an existing black ink-blot body, the original body will remain unchanged.
