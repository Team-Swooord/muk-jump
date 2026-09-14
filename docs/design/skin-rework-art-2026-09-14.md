# 스킨 원화 개편 원본 기록

2026-09-14, built-in image generation 사용. PNG 자체를 코드로 잘라 붙이거나 변형하지 않는다. 실제 적용은 기존 에셋 경로에 원본을 복사하고 씬 빌더로 임포트한다. 기존 GUID·저장 ID 유지.

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
