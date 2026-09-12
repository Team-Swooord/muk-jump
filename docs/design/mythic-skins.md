# 용꼬리 보완 · 십이지신과 동양 신화

2026-09-13 스킨 개발 브랜치용. 기존 먹 몸체와 0~45번 ID를 유지한다. 신규 장식은 전부 외형 전용이다.
용꼬리는 짧게 말리는 v2 원화와 작은 착용 범위로 교체했다. 십이지신은 46~57번 히든,
해태·삼족오·구미호는 58~60번 극비 의상이며 발견 전에는 이름·원화를 숨긴다.

## 해금 조건

모든 조건은 현재 한 판의 실제 계측을 사용한다. 시간은 일시정지를 제외한 플레이 시간이다.
조건 충족 뒤 저장에 성공해야 알림이 나오며 디버그 해금·다른 계정의 진행을 합치지 않는다.

| 의상 | 조건 |
|---|---|
| 자 · 밤쥐 | 밤 한 판에서 아이템 3개 + 50m |
| 축 · 느긋한 소 | 그린 발판 20회 착지 + 60초 생존 |
| 인 · 작은 산군 | 밤, 아이템 없이 150m 이상으로 판 종료 |
| 묘 · 달토끼 | 달빛 관 착용 + 밤 200m |
| 진 · 꼬마 용 | 용꼬리 착용 + 300m |
| 사 · 실뱀 | 거미탈 착용 + 그린 발판 12회 착지 + 120m |
| 오 · 바람갈기 | 60초 이내에 300m 이상으로 판 종료 |
| 미 · 구름양 | 그린 발판 30회 착지 + 200m |
| 신 · 잔꾀 원숭이 | 같은 판에서 먹 3마리 동시 생존 경험 + 아이템 5개 |
| 유 · 새벽볏 | 낮 한 판에서 먹방울 3개 + 100m |
| 술 · 지킴개 | 같은 판에서 먹 2마리 동시 생존 경험 + 90초 생존 |
| 해 · 복돼지 | 먹방울 3개를 포함해 아이템 12개 |
| 해태의 작은 뿔 | 거북 투구 착용, 아이템 없이 250m 이상으로 판 종료 |
| 삼족오의 해 | 밤 한 판에서 먹방울 3개 + 333m |
| 구미호의 낮잠 | 여우탈 착용 + 먹 3마리 동시 생존 경험 + 300m |

무아이템·제한시간 조건은 종료 때만 확정한다. 나머지는 판중 알림과 기존 결과 해금 화면을 사용한다.
한·영·일 이름/요구 조건/짧은 창작 설명은 InkMythicSkins와 기존 번역 테이블에 연결한다.
저장 비트는 60번까지 사용한다. 뒤끝 growthJson은 문자열로 왕복하므로 53비트 이상의 정수도 유지한다.
63개를 넘기는 다음 확장에서는 현재 signed long 마스크를 먼저 마이그레이션해야 한다.
Game Center 등록 자료는 기존 45개 × 20점 + 신규 15개 × 5점 = 975점이다.
Apple 콘솔 등록 완료를 의미하지 않는다. [등록 상태](../store/game-center-achievements.md)를 따른다.

## 원화와 착용

이번 변경 집중 검증: 새 히든 조건·저장·알림 문구 36개, 업적 연계 12개, 용꼬리 11개,
카탈로그 1개와 실제 Main Play 착용/전체 보기/비밀 숨김 1개 — 합계 61개 통과.
3개 언어와 16종 착용 화면은 `output/qa/mythic-skins/`에 보관한다.
에디터 540×960 캡처이며 iPhone 실기기나 TestFlight 검증을 뜻하지 않는다.
현재 씬은 빌더로 재생성했다. 별도 iOS 빌드·업로드·계정 초기화·main 병합은 하지 않았다.

내장 이미지 생성 도구를 사용한 신규 원화다. 외부 캐릭터 원화·라이선스 에셋을 가져오지 않았다.
원본 먹·게임 용을 참조하고, 거친 목탄선/단순 색면/진짜 투명 배경을 사용한다.
Assets/Resources/MukJump/Skins 아래의 PNG를 씬 빌더가 Single Sprite, 최대 512px,
알파 투명, 밉맵 없음, 읽기 불가로 설정한다. 실제 먹은 교체하지 않고 장식만 앞/뒤에 붙인다.
구미호는 9개 꼬리, 삼족오는 3개 다리를 확인한다.

### 용꼬리 최종 원화

파일: `Assets/Resources/MukJump/Skins/skin_dragon_tail_v2.png`

편집 지시: 짧고 부드럽게 말린 용꼬리 한 개. 왼쪽에 작은 접점, 오른쪽 끝은 위로 말리고 작은 돌기 3개와 먹색 끝 털만 둔다. 게임의 어린아이 낙서 같은 용을 참조한다. 따뜻한 종이 회색 면과 삐뚤한 굵은 목탄선을 사용하고 몸체·얼굴·발·배경·글자는 넣지 않는다. 불투명 체크무늬를 제거하여 실제 알파 투명 PNG로 추출한다.

### rat

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_rat_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. A paired RAT EAR headband: two rounded warm ivory-gray mouse ears with tiny dusty rose centers on a very shallow charcoal crescent headband. Broad flat band sits across upper forehead, no back loop.

### ox

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_ox_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. A paired OX HORN headband: two short blunt ivory horns curve upward and outward from a small slate-gray forehead band. A single small red cloth knot at center, no straps.

### tiger

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_tiger_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. A TIGER EAR forehead wrap: two little rounded ochre ears and a soft pale ochre shallow cap band, with just three wobbly black tiger stripes above the forehead. No face or eye holes.

### rabbit

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_rabbit_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. A RABBIT EAR headband: two long but floppy warm white rabbit ears, one gently bent, faint faded rose inner ears. Joined at bottom by a shallow soft charcoal forehead crescent.

### dragon

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_dragon_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE tiny EAST ASIAN DRAGON HORN crown. Two short stubby branching ivory antlers and two floppy parchment fins, joined by a low muted pale jade forehead band. Just a single black wavy scale mark on the band. Not a western horned demon.

### snake

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_snake_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE friendly coiled pale jade SNAKE crown, a short loose S-shaped snake curled atop a shallow charcoal forehead band. A tiny blunt head looking sideways with just one dot and a short tongue; not a full creature, just a comical little snake as a hat. No human or ink character.

### horse

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_horse_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE HORSE MANE cap: two short pointed warm gray ears joined by a shallow charcoal forehead cap with a short tousled ink mane sweeping to the right. One small muted ivory forelock. Not a horse face.

### goat

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_goat_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE curled GOAT HORN cap: two small inward-curling ivory horns at the sides of a low cloudlike wool forehead band, three clumsy fluffy lobes, just one spiral stroke on each horn. Childlike doodle, not a realistic ram skull.

### monkey

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_monkey_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE MONKEY EAR headband: two low round cinnamon-brown ears with pale parchment inner curls, joined by a broad low dark brown band with a small ivory tuft at center. No golden jewelry, no face or eyes.

### rooster

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_rooster_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE ROOSTER COMB crown: three rounded uneven vermilion comb lobes on a short charcoal forehead band, and two tiny ivory feather tufts near the sides. Comical and modest; no bird head, no beak or eyes.

### dog

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_dog_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE FLOPPY DOG EAR cap: a low warm brown forehead cap, with two short soft floppy dog ears hugging the left and right sides. Broad opening beneath the cap, no face, no nose, no eyes. One tiny parchment patch on the band.

### pig

파일: `Assets/Resources/MukJump/Skins/skin_zodiac_pig_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE PIG EAR headband: two small floppy triangular dusty rose pig ears with a very low soft rose forehead band. A little spiral tuft at center resembles a curly pig tail; not a snout or face, no nose, no eyes.

### haetae_crest

파일: `Assets/Resources/MukJump/Skins/skin_haetae_crest_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Fits the forehead naturally, no complete closed loop or back straps. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE small HAETAE HORN forehead crown. Korean guardian creature inspired, not a full character: one short rounded ivory central horn, two tiny floppy ears, a low muted blue-gray curly mane shaped like three cloud curls hugging the forehead. Only two wobbly ink curl marks. No face, eyes or animal body.

### three_legged_crow

파일: `Assets/Resources/MukJump/Skins/skin_three_legged_crow_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Goes BEHIND the body, with only the outer accessory visible. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE rear halo costume sprite inspired by SAMJOKO, the three-legged sun crow. Small muted rust-red round sun disk in the UPPER CENTER, with a naive charcoal crow silhouette inside showing two short wings, small beak and EXACTLY THREE short distinct legs. The disk occupies only 35% canvas width. Two short charcoal feather ribbons extend gently to left and right below the sun, framing a LARGE EMPTY transparent center where an unchanged round game character will stand. Simple childish doodle, not intricate feathers, not golden religious art. No character underneath, no text.

### nine_tails

파일: `Assets/Resources/MukJump/Skins/skin_nine_tails_v1.png`

최종 제작 프롬프트:

Use case: stylized-concept. ONE production 2D game costume ACCESSORY ONLY, for a small round black ink creature whose body stays original. Front-on flat 2D. Naive child-drawn Korean ink/crayon doodle, comically humble, wobbly bold charcoal outline #1C1B1A, flat warm parchment #EAE3D2, one muted color accent only. Broad simple shapes, no fine engraving or realism, no polished vector look, no gradients, no lighting, no fabric texture. Goes BEHIND the body, with only the outer accessory visible. Composition: single isolated accessory centered in a square canvas, about 78% canvas width, all ends inside with margins. Nothing underneath it. REAL ALPHA TRANSPARENT BACKGROUND, not painted checkerboard; no shadows, floor, text, watermark, head, creature, eyes, body or second accessory. ONE rear costume accessory: a fan of EXACTLY NINE short plump fox tails spreading from a single small root at lower center. All nine tips individually countable: four on left, one in top center, four on right, arranged evenly like a scalloped hand fan. Very simple parchment-white tails with faint dusty rose tips and wobbly thick charcoal outlines, one simple curved ink stroke per tail, no fur detail. Root is low, wide shallow fan silhouette surrounds a large central area that will be COVERED by an unchanged round black ink creature. No fox body, no fox face, no eyes, no legs. Broad baby-drawn shapes.

개수 수정:

Precise edit. This nine-tailed fox accessory mistakenly has only SEVEN tails. Correct to EXACTLY NINE separate visible tail tips by adding TWO extra tails, one on each side between the outermost horizontal tail and its diagonal neighbor. Five tails fan on left including center? NO: FOUR distinct tips on LEFT, ONE top CENTER, FOUR distinct tips on RIGHT = NINE total. Keep every existing shape and add one in each lower side gap. Simple naive child-drawn ink/crayon, off-white fill, dusty rose tips. No fox body or head. Same compact fan accessory alone. Genuine alpha transparent background, no checkerboard painted in. Count nine tips clearly. All tips fit inside square canvas.

알파 추출:

Background-extraction only. Preserve this EXACT NINE-TAILED accessory without redrawing or changing the nine contours. Remove ALL BLACK BACKGROUND outside and between the tails. Output a transparent PNG with REAL ALPHA=0 around the object, no painted black backdrop and no painted checkerboard. Keep black contour lines on the tails themselves. Exactly NINE white tails with pink tips, no more no fewer. Do not change the drawing or add anything.
