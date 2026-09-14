# 스킨 원화 개편 원본 기록

2026-09-14, built-in image generation 사용. PNG 자체를 코드로 잘라 붙이거나 변형하지 않는다. 실제 적용은 기존 에셋 경로에 원본을 복사하고 씬 빌더로 임포트한다. 기존 GUID·저장 ID 유지.

## 두 번째 단계 — 실제 프레임 애니메이션

모두 원본과 같은 캐릭터를 쓰며 PNG를 변형하지 않고 생성 결과 그대로 적용한다. 후광/용꼬리는 기존 시트 GUID 유지, 구미호는 새 시트와 클립 추가. 단순 그림 재생이므로 새 파티클/재질/패키지와 반복 생성·삭제 없음. 기존 동작 줄이기 설정과 UI·월드 공통 프레임 캐시를 사용한다.

- 금빛 후광 최종: `exec-71fa3ea7-c3b8-4010-82ae-2903c75e2ecd.png` → `Assets/Art/Character/Animations/spirit_halo_sheet.png`. 첫 결과 `exec-f2254748-0170-43c1-909e-52617e859395.png`의 가짜 체크무늬를 이미지 편집으로 제거했다. 불꽃 없는 한 개의 원형 후광, 동일 지름, 잔잔한 먹 선 이동 4프레임.
- 용꼬리 최종: `exec-e6246636-af6b-4511-85b0-9eeda24e7dfc.png` → `Assets/Art/Character/Animations/dragon_tail_sheet.png`. 첫 결과 `exec-eca0910e-1a82-43d0-bbc8-c849ac3d0459.png`의 가짜 체크무늬를 제거했다. 두꺼운 뿌리, 위/수평/아래/복귀 4프레임. 빌더 슬라이스로 동일 접점 정렬.
- 구미호 최종: `exec-d751872b-7b24-4805-a15c-5b59b925857a.png` → `Assets/Art/Character/Animations/nine_tails_sheet.png`. 직사각 시트 3개는 불투명/배경 오류로 미적용(`exec-37a3ebd1-d520-4f54-a8aa-480821dd37e3.png`, `exec-8f6e38f0-bc1e-442f-ab2f-d6e9252c8610.png`, `exec-c820340e-e2b7-4d37-88ec-99a987e0386a.png`). 정사각형 재생성 `exec-de99da5a-e583-424c-be90-d4acd86606d0.png`에서 두 프레임의 부족한 꼬리를 추가한 `exec-d71dd28a-144b-4c33-bb17-236b86d77ad6.png`를 거쳐 실제 투명 배경으로 완성.

### 후광 생성 프롬프트

Edit the attached existing 2x2 four-frame sprite animation sheet for MukJump. Replace every flame-decorated blue ring with a simple elegant Eastern ink-painting HALO ONLY, behind a round black ink creature. There must be NO flames, NO floating wisps, NO creature, no letters, no background. Four equal-sized cells in a strict 2x2 grid, reading top-left top-right bottom-left bottom-right. In each cell draw one perfectly front-facing circular halo centered at exactly the same anchor and exactly the same outer diameter. Use a thin imperfect charcoal-black hand-drawn ring edge, warm pale-gold paper-colored narrow ring fill, and a second very thin concentric ink trace. A few tiny ink dashes travel a little clockwise around the SAME fixed circle across four frames: quiet restrained looping motion, no wobbling geometry, no scaling or off-axis perspective. Retain the existing crude childish hand-drawn thick ink line and paper crayon texture, not polished realistic fantasy. The circle's middle and the whole background must have real transparent alpha; never draw a checkerboard. Keep broad clear transparent margins around each cell's drawing and generous empty gutters so each equal 2x2 cell can be sliced independently without clipping. The four circles must match placement/size closely. Output ONLY the single transparent PNG four-frame sheet.

### 용꼬리 생성 프롬프트

Edit this MukJump dragon-tail four-frame sprite animation sheet. Keep its simple childish ivory-paper dragon tail with thick uneven charcoal ink contour, three little triangular dorsal spines and a three-point leaf-shaped tuft. Keep the BIG THICK ROOT, not a skinny tube. The tail projects to the RIGHT and attaches offscreen on the LEFT; no creature, no eyes, no face, no text. Make a STRICT 2x2 equally divided sprite sheet with FOUR different animation poses in reading order: 1 tail curling UP, 2 tail nearly HORIZONTAL, 3 tail curling clearly DOWN with tip LOWER than its attachment root, 4 tail easing back HORIZONTAL. This must be genuine drawn bending, not rotating the whole identical tail. The thick oval attachment root MUST stay identical at exactly the same LOCAL position and height in all four cells: local x around 13% of cell width, local y at 50% of cell height, root vertical diameter 26% of cell height. Keep the lower half of each cell available for the downward-curved pose, and upper half for upward pose. Smoothly taper only near the tip. Root, line width and overall reach must match across frames. Wide margins at least 6% of each cell, each tail fully inside its own equal quadrant, no cropping and no neighboring pixels. Genuine transparent alpha background outside shapes, no checkered background. Same crude charming child-drawn ink-and-paper art, not glossy or realistic, no extra details. Output only one transparent PNG.

### 구미호 최종 원화 지시

정사각형 투명 PNG 2×2 시트. 아홉 개의 아이보리 꼬리와 분홍 끝, 굵고 투박한 먹 선. 왼쪽/정면/오른쪽/복귀로 부드럽게 굽히되 먹 몸체는 그리지 않음. 두 왼쪽 프레임은 8개로 생성되어 각각 분홍 끝 꼬리 1개를 추가, 오른쪽 프레임 9개는 유지. 그림 밖 배경은 실제 알파 0으로 제거. 원화 자체는 Python 등으로 편집하지 않음.

## 첫 단계

출력 원본 폴더: `/Users/seungyeoning/.codex/generated_images/01a02807-53b4-7300-82fd-74057eea1a57/`

| 적용 에셋 | 생성 원본 | 목적 |
| --- | --- | --- |
| skin_cat_ears_v1.png | exec-1516d64d-4a50-411d-a881-bbd6d9096ff2.png | 머리띠 없는 두 귀 |
| skin_gat_v1.png | exec-69daac2f-a756-4c2e-8721-a687c4b5a412.png | 높이 있는 중절모 |
| skin_zodiac_tiger_v1.png | exec-2ea01d29-13b5-437f-b5ca-4a62f7e310b3.png | 호랑이 줄무늬 두건, 투명 배경 재편집 |
| skin_zodiac_pig_v1.png | exec-31d60014-d2eb-45ab-a57f-3ccc748b6de6.png | 귀 대신 코 |
| skin_zodiac_monkey_v1.png | exec-3c2ac872-9164-485a-904c-8db1062f999c.png | 귀 대신 꼬리 |
| skin_haetae_crest_v1.png | Git bfd1fa4d 동일 경로 | 요청에 따라 이전 원화 그대로 복원 |

### 고양이 귀 — 최종 프롬프트

Edit this game accessory sprite. Output only TWO SEPARATE CAT EARS on a truly transparent RGBA background. Remove the ENTIRE curved gray connecting headband and both dangling ends. Keep the same naive child-drawn rough charcoal black outlines, gray outer ears, muted pink inner triangles, simple flat paper crayon texture, no realistic shading. Exact front view, left and right ears mirrored symmetrically, comfortably separated with a wide fully transparent gap between them. Two triangular ears with slightly curved closed bases, neither headband nor support strip anywhere. No head/body/eyes/hair. Position both ear bases on exactly the same baseline, left ear centered at 24% width and right ear at 76% width. Artwork from x8% to92%, y25% to72%; plenty of transparent margin on all sides. Single still accessory, not a sprite sheet. No text, no floor/shadow, no background/checkerboard. Save the new original art for use as a Unity sprite.

### 바람 갓 — 최종 프롬프트

Redraw this accessory as a charming crude hand-drawn 1930s Korean street-fighter FEDORA hat for a tiny 2D ink blob. Yain Sidae era swagger. The existing hat is much too flat and squashed. Replace the short flat pork-pie crown with a noticeably TALL pinched fedora crown, indented top, cream hatband, gently curved broad brim. Front view symmetric overall, slight handdrawn imperfection only. Crown height about 43% of total hat width, full hat height about 55% of width. Same childlike thick rough charcoal outlines and flat dark charcoal wax-crayon fill as reference, sparse papergrain, no shiny realistic rendering. This is ONLY the hat accessory with no character/head/face/body. Centered on a true transparent RGBA background, artwork occupies 85% canvaswidth with 8%+ transparent margins all sides, no shadows, no text, no checkerboard. Important: ample tall crown, not a pancake, not a traditional conical gat.

### 산군 두건 — 생성 및 알파 보정

Replace this tiger-ear hat accessory with a TIGER STRIPED HIP-HOP DO-RAG BANDANA, no animal ears. Standalone 2D wearable sprite, no character/head/body/eyes. Must look like cloth covering the upper dome of a small round ink blob, not merely a narrow headband hovering above it. Ochre orange fabric with simple irregular black tiger stripes. Wide softly rounded dome top, shallow curved lower hem resting just above the character's eyebrows. Small cloth knot at far right temple and two short pointed orange striped cloth ends hang down on that right edge ONLY. Leave eye/face region empty transparent below lower hem. All back straps completely absent, no U-shaped support strip, no fake facial features. Symmetric main dome centered on canvas, slight asymmetric knot but not huge. Same crude child-drawn thick rough charcoal outlines, flat crayon ochre and black fill, paper flecks, not polished or 3D. Art upper cap width 70%canvas, total width including knot85%, height35%canvas, centered with plenty transparent margins. True transparent RGBA no checkerboard, no text or floor shadow.

첫 결과 exec-dcb0a8c9-b10e-4a69-afe3-2f7313901075.png는 배경 체크무늬 문제로 미적용. 다음 프롬프트로 다시 편집:

Keep the orange and black tiger striped do-rag artwork exactly intact, remove ALL the white/gray checkerboard and any paper background outside the cloth. Deliver a genuine transparent alpha PNG cutout. Every pixel outside the black outline, including below the curved hem and between the two short hanging cloth tails, must have alpha zero, not a depiction of transparency. No new checkerboard texture. Do not change the cloth or add anything. Transparent background accessory sprite only.

### 복돼지 코 — 최종 프롬프트

Change this pig-ear accessory into a single PIG SNOUT accessory only, no ears. A charming simple child-drawn horizontal pink oval pig nose, two dark charcoal oval nostrils symmetrically placed, rough thick black crayon outline, muted pink flat fill with minimal papergrain matching reference. Exactly front-facing. No mouth, no eyes, no head/body, no headband, no straps, no shading or shadow. Only the oval nose centered in a true transparent-alpha RGBA image. Art occupies approx70%width and40%height of a squarecanvas, empty transparent allaround. Must be suitable for layering on the LOWER CENTER of our black ink blob below its two eyes. Naive friendly doodle, not polished illustration. No text, no checkerboard or paper background.

### 원숭이 꼬리 — 최종 프롬프트

Replace these two monkey ears with ONE CURLY MONKEY TAIL accessory. Only the tail, not an animal, not ears, no face or body. Simple orange-brown crayon fill and thick rough dark charcoal outline, exactly the same naive children's doodle style as reference. Tail begins with a thick rounded root on the LEFT at approx x18% y65%, then sweeps right in a relaxed shallow U and curls upward into an open inward spiral near the RIGHT at x75% y45%. Uniformly tubular long flexible tail with tapered rounded tip, no excessive double coil. This is a 2D side-attachment worn on the lower right hip of a tiny black inkblob, recognizable as a monkey's curly tail. Art comfortably centered, width75%canvas height55%canvas with transparent margins; single still accessory on genuine transparent RGBA background, no shadow, no checkerboard, no text, no additional objects. Simple and charming, not 3D.
# 퇴마 타격 추가 원화

## 각시탈

- 기존 ID 18/원화 경로 `Assets/Resources/MukJump/Skins/skin_yaksha_mask_v1.png`를 유지하여 보유 기록을 보존한다.
- 기존 야차 원화를 그림체·눈 위치 참조로 생성. 체크무늬 배경의 첫 출력 `exec-dc08d57f-26aa-4d68-8dcd-a3f8e5d6af6a.png`는 미사용. 이미지 도구로 배경만 재생성한 최종 `/Users/seungyeoning/.codex/generated_images/01a02807-53b4-7300-82fd-74057eea1a57/exec-ef378e03-2fb9-424d-81fe-26051da97ff3.png`를 그대로 적용. 실제 알파와 화면 착용 검사 완료.

## 천사 날개

- 적용: `Assets/Art/Character/Animations/angel_wings_sheet.png`. 원본 `/Users/seungyeoning/.codex/generated_images/01a02807-53b4-7300-82fd-74057eea1a57/exec-ac94ffbb-f815-4080-80fd-0958f4f23648.png`.
- 기존 학 날개를 그림체 참조로 사용한 2×2 빛 리본 원화. 첫 생성 `exec-defbe635-6139-4052-8506-5cfc031c90ef.png`는 체크무늬가 들어가 미사용, 이미지 생성 도구로 투명 배경 수정. 최종 투명 픽셀 비율 79.56%, 4프레임 누락/중복/잘림 검사 통과. 픽셀은 가공 없이 복사, 정지 표시도 첫 프레임을 공유한다.

- 적용: `Assets/Art/Character/Animations/mask_strike_sheet.png`.
- 생성 원본: `/Users/seungyeoning/.codex/generated_images/01a02807-53b4-7300-82fd-74057eea1a57/exec-774c8500-9553-45fa-9d89-260defc47ad1.png`.
- 2×2 실제 4프레임 먹빛/낙관색 베기, 기본 격자 슬라이싱. AI 생성 원본 그대로 복사했으며 픽셀 가공하지 않음. 프레임 누락/중복/잘림 검사와 Play Mode 확인 완료.
