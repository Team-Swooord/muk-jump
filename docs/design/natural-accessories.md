# 띠 없는 동물 장식

2026-09-13. `codex/ink-skins`의 외형 수정. 동물 특징마다 머리띠를 붙이지 않는다.
용·소·양은 뿔 한 쌍, 해태는 뿔 하나, 귀 의상은 귀 한 쌍만 남긴다.
말은 갈기, 닭은 볏, 뱀은 작게 감긴 뱀 자체다. 줄무늬 두건과 원래 모자·머리띠인 의상은 유지한다.

## 적용 기준

- 원먹 몸통·눈·팔다리·판정은 교체하지 않는다. 의상 ID·해금·능력·서버 저장 구조도 유지한다.
- 뿔·귀의 뿌리는 몸체 뒤에 묻혀 자연스럽게 붙는다. 연결용 띠·고리·목걸이·받침대를 추가하지 않는다.
- 이마 위 한가운데 장식은 이름에 맞는 작은 크기만 남긴다. 얼굴까지 내려오는 띠로 착용을 대신하지 않는다.
- 게임·로비·꾸미기·해금·리더보드가 `InkSkinCatalog`의 동일 원화·크기·위치·앞뒤 순서를 사용한다.
- 기존 PNG를 교체하고 GUID/Resources 경로를 유지한다. 불필요한 구버전 원화를 Resources에 추가하지 않는다.
- 씬 빌더의 Single Sprite, FullRect, 최대512px, 알파, 밉맵 없음, 읽기 불가 설정을 유지한다.

## 착용선과 애니메이션 경계

- 줄무늬 두건·용뿔·닭볏·주사위·찻잔·고양이 귀·학의 볏은 머리 중앙에 맞춰 폭과 높이를 조정한다.
  고양이 귀의 연결부와 닭볏 밑면은 몸체 뒤로 숨긴다. 용꼬리는 뿌리를 옆구리 안으로 넣고,
  바람부채는 등에 가까이 붙이며 달구름의 달은 머리 뒤에 잘려 보이지 않도록 높인다.
- 등불·용꼬리·질풍 두건 시트는 그림 간격이 정확한 2×2 격자가 아니므로
  `InkSpriteAnimationBuilder.SourceRect`의 원본 해상도 좌표로 자른다. 네 프레임의 크기와 비율은
  일정하며, PNG를 재가공하거나 애니메이션을 코드 변형으로 대체하지 않는다.
- 움직이는 장식의 목록·리더보드·동작 줄이기 그림은 같은 애니메이션의 첫 프레임이다.
  별도 정지 원화의 다른 크기·여백 때문에 선택 순간 장착 위치가 달라지지 않는다.
- `AccessoryFrameFit`은 장식 애니메이션 9종의 모든 불투명 픽셀이 정확히 한 프레임에만
  포함되는지, 가장자리 3px 여백, 실제 임포터 반영, 공통 정지 프레임 및 신고된 12종의
  실제 꾸미기 착용/네 프레임을 확인한다. 촬영과 결과는 `output/qa/accessory-fit/`에 둔다.

## 최종 원화

경로 기준: `Assets/Resources/MukJump/Skins/`.

| ID | 파일 | 남기는 형태 |
|---|---|---|
| 46 | skin_zodiac_rat_v1.png | 동그란 쥐 귀 두 개 |
| 47 | skin_zodiac_ox_v1.png | 짧은 소뿔 두 개 |
| 49 | skin_zodiac_rabbit_v1.png | 하나 접힌 토끼 귀 두 개 |
| 50 | skin_zodiac_dragon_v1.png | 가지 친 작은 용 뿔 두 개 |
| 51 | skin_zodiac_snake_v1.png | 받침대 없이 감긴 실뱀 |
| 52 | skin_zodiac_horse_v1.png | 바람에 쓸린 갈기와 작은 밝은 앞머리 |
| 53 | skin_zodiac_goat_v1.png | 돌돌 말린 양 뿔 두 개 |
| 54 | skin_zodiac_monkey_v1.png | 둥근 원숭이 귀 두 개 |
| 55 | skin_zodiac_rooster_v1.png | 붉은 볏 하나 |
| 56 | skin_zodiac_dog_v1.png | 늘어진 강아지 귀 두 개 |
| 57 | skin_zodiac_pig_v1.png | 접힌 분홍 돼지 귀 두 개 |
| 58 | skin_haetae_crest_v1.png | 작은 해태 뿔 하나 |

## 이미지 제작 지시

내장 imagegen 사용. 뿔·쥐·토끼는 기존 원화를 편집하고, 투명 배경 보존에 실패한 나머지는
같은 그림체 지시로 새로 제작한다. 실제 알파가 없는 체크무늬 결과와 윤곽선이 사라진 결과는 적용하지 않는다.

공통 편집 프롬프트:

> Precise-object-edit of the supplied child-drawn Korean ink game accessory. Keep ONLY the named natural parts. Remove the complete connecting headband, mounting cuffs, knots, pedestals and unrelated ornaments. Preserve thick wobbly charcoal crayon outlines, simple muted flat fills and small asymmetries. No character, body, hat, crown, connector, shadow or text. Production accessory alone on a genuinely alpha-transparent PNG, with empty margins and transparent space between paired shapes.

편집별 추가 지시:

- 용: two branching ivory horns only; remove jade band, mounting cuffs, emblem and both side fins.
- 소: two short ivory ox horns only; remove slate headband, red knot and all ties.
- 양: two curled ivory horns only; remove every wool/cloud lobe and the connecting band.
- 쥐: two round ivory-gray ears with dusty pink centers; remove only the connecting arc.
- 토끼: two ivory rabbit ears with rose centers, one bent; remove the arched band but keep thick black contours around BOTH ears.

공통 신규 제작 프롬프트:

> Create one isolated production game accessory sprite. Naive child's Korean ink/crayon doodle, funny and humble, very thick wobbly charcoal-black outlines, 5-8 bold simple strokes, broad flat matte colors, tiny asymmetry. No realistic textures, no gradients, no light or shading, no shiny fantasy details. Accessory ONLY, NO headband, NO hat, NO crown, NO connecting strap, no extra ornaments, no character, no body or text. Centered in square canvas, generous empty margins. ISOLATED ON A TRANSPARENT BACKGROUND, transparent PNG alpha.

각 신규 원화의 주제:

- 원숭이: two separate cinnamon-brown round ears, ivory inner curls; left/right at the same height, empty center.
- 돼지: two floppy dusty-rose ears, rounded triangular tips, one darker pink crease each; no curly tuft.
- 강아지: two separate long floppy cinnamon-brown ears, rounded teardrops with one crooked fold line; slant outward.
- 뱀: one little pale jade snake curled in a loose S-shaped knot, tiny rounded head with ink eye and short tongue; no jewel or pedestal.
- 닭: one little red rooster comb, three uneven rounded upright lobes, middle longest, organic curved base; no feathers.
- 해태: one short ivory horn, blunt slightly curved tip, squat 3:4 silhouette, two crooked charcoal curves; no ears or cloud mane.
- 말: one windswept charcoal mane tuft, three/four broad swept-right points, one ivory forelock; no ears or band/cap beneath hair.

## 집중 검증

`NaturalAccessoryArt`는 위 12종의 실제 알파·검은 윤곽·독립된 형태 개수·중앙 띠 없음·
먹과의 접점·모바일 임포트 설정을 검사한다. `NaturalAccessoryPresentation`은 실제 Main
Play의 꾸미기에서 원먹 유지·앞뒤 순서·잘림·히든 숨김을 확인하고 착용 화면을 촬영한다.
UI 촬영: `output/qa/mythic-skins/natural-*.png`. 에디터 검증이며 실기기/업로드 검증이 아니다.
최종 결과: 원화 12개·착용 화면 1개 검사 통과. 12종 촬영 화면에서 부착 위치와 눈 가림이 없는지
확인했다. 결과 파일은 `output/qa/natural-accessories-2026-09-13/`에 보관한다.
