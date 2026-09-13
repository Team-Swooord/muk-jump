# 손그림 스프라이트 애니메이션 원화

2026-09-13 요청: 코드로 그림을 휘지 않고, 직접 그린 프레임으로 잡힌 먹과 움직이는 장식을 표현한다.

내장 imagegen으로 제작했다. 출력 PNG를 가공하지 않고 복사했으며 Unity Sprite Editor 데이터 제공자로 2×2 슬라이싱한다. 체크무늬가 실제 픽셀에 포함된 중간 출력은 채택하지 않았다. 아래 최종 10장 모두 PNG 알파 채널을 확인했다.

| 원화 (`Assets/Art/Character/Animations/`) | 생성 출력 파일 | 프레임 |
| --- | --- | --- |
| muk_held_sheet.png | exec-590dc684-5025-493e-8f53-d2eee1333618.png | 4 |
| spirit_halo_sheet.png | exec-3936f312-7701-4d8b-a1e5-382154785d8c.png | 4 |
| paper_lanterns_sheet.png | exec-6a4859be-a79f-4286-ba09-d43d767cbb09.png | 4 |
| moon_clouds_sheet.png | exec-a1bf68ac-17d8-48bc-a4aa-a25e9f7c4953.png | 4 |
| origami_wings_sheet.png | exec-85e330be-9b69-4a64-90e1-330ab1c5292f.png | 4 |
| crane_wings_sheet.png | exec-f26bb534-03d0-49cb-86f5-0182ef5955b9.png | 4 |
| storm_scarf_sheet.png | exec-1e1761ca-bcf0-4dcc-8e16-df44aa3a9bf5.png | 4 |
| blue_wisps_sheet.png | exec-1b30d31b-4ab5-4f5a-89de-d0624a411c14.png | 4 |
| butterfly_wings_sheet.png | exec-0b095fb4-884b-4554-bbd6-d049a56ae296.png | 4 |
| dragon_tail_sheet.png | exec-0d249e8d-d577-4835-a3a8-b5d00cccd3a5.png | 4 |

원본 출력 위치: `/Users/seungyeoning/.codex/generated_images/01a02807-53b4-7300-82fd-74057eea1a57/`.

- 원먹 참조: `Assets/Art/Character/Player/muk_spritesheet.png`.
- 장식 참조: `Assets/Resources/MukJump/Skins/skin_<이름>_v1.png` (용꼬리만 v2).
- 프레임 등록: `Assets/Resources/MukJump/SpriteAnimations/`의 10개 `InkSpriteAnimation` 데이터. 장식 36장+잡힌 먹 4장.
- 드래그 시 먹 크기/외곽을 휘지 않는다. 그린 다리 포즈를 교체하고, 신발 위치는 해당 포즈 발끝에 등록한다.
- 단단한 모자·탈·신발은 의도적으로 자체 흔들림을 넣지 않는다. 기존 의상 ID·해금·능력·기록 동기화는 변경하지 않는다.

## 잡힌 먹 생성 프롬프트

Use case: identity-preserve. Asset type: production transparent 2D game sprite sheet. Input image is the EXACT character identity and drawing style reference, not a backdrop. Create a new 2 by 2 grid of FOUR hand-drawn animation frames showing THIS SAME black ink-blot creature being held in midair and gently wriggling its TWO STICK LEGS. Keep its broad round irregular soft-pointed black/very-dark-charcoal ink silhouette, two large ivory oval eyes with small round black pupils. No arms, no hands, NO mouth, NO eyebrows, NO highlights, no new face or accessories. Body top stays at the same place, width and center stay locked in every cell, so existing hats can fit unchanged. Pupils look slightly upward to convey surprised 'you caught me'. The motion is in the redrawn legs: frame1 left leg bent outward, right leg dangling; frame2 both legs dangling with knees slightly bent; frame3 right leg bent outward, left leg dangling; frame4 both legs bend out a little to return to frame1. The legs must emerge from the lower left and lower right body sides like the reference, ONLY TWO legs made of angular charcoal strokes with tiny hooked feet, NEVER arms or extra limbs. Keep all poses within the original character's overall width and height envelope. Very restrained hand-drawn motion, not elastic squashing, not jelly, no rotation of whole character. Same childlike simple ink doodle, NOT anime or detailed fantasy art. Flat front view. Each cell square and exactly the same size; frame order top left, top right, bottom left, bottom right. Cell center anchors, scale and body outline closely matched. Canvas 2048 by 2048, each cell 1024 by 1024; each whole creature fits centered within x100..924 and y152..866 in its cell, ample equal margins, no overlap. Genuine PNG alpha transparency around all characters and between cells. No actual background, no white matte, no checkerboard pixels, no drawn grid, no text, no numbers, no labels, no ground shadow, no human hand.

실제 출력 크기는 요청 크기와 달라 1254×1254다. 빌더는 실제 크기로 슬라이싱하며, 이미지 자체를 리사이즈하지 않는다.

## blue_wisps 최종 생성 프롬프트

A transparent PNG sprite animation sheet, 2 by 2 grid, FOUR consecutive frames. Each square cell has FIVE small turquoise blue cartoon spirit flames arranged in an arch: centers at (10%,55%),(27%,31%),(50%,20%),(73%,31%),(90%,55%). Each little flame only14% of cell width, leaving all the bottom third and center completely empty. Flat coloring, thick uneven black charcoal crayon outline, pale cyan inner tongue, clumsy cute child drawing. Four distinct fire shapes: short curved tip, forked rising tip, upright wavy flame, left curl returning. Flame bases stay at fixed coordinates; slightly different timing for each little flame. No character, no scene, no letters. All negative space is real alpha transparency, export an isolated RGBA cutout asset. 2048x2048.

## butterfly_wings 최종 생성 프롬프트

A transparent PNG sprite animation sheet, 2 by 2 grid, FOUR consecutive drawings of goofy butterfly WINGS ONLY. Four large grey-blue rounded lobes per cell, two left and two right. Each top lobe has one ivory dot, thick rough black charcoal outline, flat childlike crayon doodle, no detailed veins. Pair of roots stays fixed at x39% and61%, y52%, transparent gap between. Distinct 4-frame flap: wings spread wide, tips tilted up partlyfolded, tips tilted down, halfopen recovery. SAME anchor and maximum width90% every cell. No insect body, no head, no character, no scene or labels. Export isolated RGBA cutout with all empty space actual transparent alpha. Eachcell wide4:3, total2048x1536.

## moon_clouds 프롬프트

Use case: identity-preserve. Production transparent 2D game animation asset. Create a FOUR-frame 2x2 grid sprite sheet, reading order top left, top right, bottom left, bottom right. Preserve the exact accessory identity and childlike rough charcoal crayon style, minimal flat dull colors of the reference. Do not add a character, scenery, text, labels, cell lines or drawn checkerboard. Real alpha transparency in all empty areas. Four visibly different hand-redrawn animation poses with identical root/anchor, overall size, camera and cell bounds, modest motion, never generic rubbery warping. Allow5%margin. One ivory crescent at top center and THREE grey-lavender crayon clouds circling its sides. Crescent MUST stay perfectly still, same size/silhouette/place allframes. Redraw trailing small cloud curls rolling gently left, upward, right and settling. Cloud centers remain at same anchors as reference, big transparent central gap for character. Do not shrink or inflate wholeclouds. Squarecells,canvas2048x2048.

## storm_scarf 프롬프트

Use case: identity-preserve. Production transparent 2D game animation asset. Create a FOUR-frame 2x2 grid sprite sheet, reading order top left, top right, bottom left, bottom right. Preserve the exact accessory identity and childlike rough charcoal crayon style, minimal flat dull colors of the reference. Do not add a character, scenery, text, labels, cell lines or drawn checkerboard. Real alpha transparency in all empty areas. Four visibly different hand-redrawn animation poses with identical root/anchor, overall size, camera and cell bounds, modest motion, never generic rubbery warping. Allow5%margin. A curved WIDE grey forehead headband with a knot on its RIGHT and two short dull-blue cloth tails extending down/right. Keep the forehead strip and knot absolutely still and identically shaped across allframes. ONLY redraw the two loose cloth ends fluttering slightly up-right, outward-right, down-right and return, subtle creases with no fine details. No back strap, no extra loop, no character. Each cellwide3:2,wholecanvas2048x1366. Allfitwithin95%cells.

## dragon_tail 프롬프트

Use case: identity-preserve. Production transparent 2D game animation asset. Create a FOUR-frame 2x2 grid sprite sheet, reading order top left, top right, bottom left, bottom right. Preserve the exact accessory identity and childlike rough charcoal crayon style, minimal flat dull colors of the reference. Do not add a character, scenery, text, labels, cell lines or drawn checkerboard. Real alpha transparency in all empty areas. Four visibly different hand-redrawn animation poses with identical root/anchor, overall size, camera and cell bounds, modest motion, never generic rubbery warping. Allow5%margin. A stubby ivory dragon tail, root on LEFT at x10% y48%, three crude triangular back spikes, a tiny wavy black line on its belly and three-pointed tip curling upward on RIGHT. EXACTLY same simple tail identity. Four drawings of tip wagging: upright curl, tip a little inward, tip a little outward lower, then recovery. Fixed leftroot, base half absolutelystill; redraw curvedtip rather than stretchwholetail. No dragonbody, noface, noscales, noglow. Squarecells,canvas2048x2048.

## 투명 배경 재제작 지시

Remove the entire grey checkered/paper background from this sprite sheet and deliver an actual RGBA PNG with transparent background (alpha=0 in empty areas). Keep every ink outline and filled colored shape unchanged, exact grid, exact position and size, all four frames. Do NOT draw transparency as a pattern; the exported file must have real alpha transparency. Preserve the black ink outlines and ivory/color fills. No other edits.

다른 장식에도 동일하게 단순한 먹 그림체, 2×2 네 포즈, 변하지 않는 부착점, 배경 없는 알파 PNG 조건을 적용했다. 원화 시트가 재제작 기준이며, 움직임·속도·적용 화면은 `animated-accessories.md`에 명시한다.
