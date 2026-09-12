# 손그림 의상과 착용 기준

2026-09-12. 원래 먹의 몸통·눈·다리·물리 판정은 유지하고, 의상 39종을 전부
어린아이가 그린 용·해태와 어울리는 굵은 낙서 선·큰 색면·불규칙한 실루엣으로 교체했다.
금속 광택·보석·잔주름·가는 깃털 묘사는 쓰지 않는다. 단순화는 저해상도화가 아니다.

모자통과 이마 띠는 실제 머리를 넉넉히 감싼다. 뒤쪽 끈은 앞면에 그리지 않는다.
탈은 원래 눈이 보이는 투명 구멍, 날개는 몸통 뒤의 붙는 지점,
신발은 8개 동작과 피격 프레임의 양발 접점을 기준으로 한다.
`InkSkinCatalog`의 크기·위치를 게임/꾸미기/해금 화면/업적 아이콘이 공유한다.
`InkFootwearPose`와 `InkAccessoryImage`가 같은 발 앵커를 사용한다.
붉은 버선은 원화의 좌우 조각·발목 피벗을 함께 교환해 발끝이 바깥쪽을 향한다.
게임/꾸미기/해금/로비/리더보드 모두 이 조각 선택을 공유한다. 구름 장화 방향은 유지한다.
각 수집 카드는 자체 경계만 맞추므로 큰 날개 때문에 다른 의상이 작아지지 않는다.

저장 ID 0~35는 그대로다. 추가 ID는 36 학의 날개, 37 먹나비 날개,
38 붉은 버선, 39 구름 장화다. 신규 네 의상은 외형 장식이다.
신발의 좌우 Sprite 4개를 캐시하며 매 프레임 텍스처나 오브젝트를 만들지 않는다.
씬 빌더에서 Single/FullRect/최대512px로 임포트하며 원화 파일을 임의 리사이즈하지 않는다.

## 제작 및 확인

내장 `imagegen` 스킬/이미지 생성 도구 사용. 그림체 기준은
`Assets/Resources/MukJump/Obstacles/child_ink_dragon.png`와
`child_ink_haetae_4frame_v2.png`다. 참조 이미지 입력에서 배경 체크무늬가 실제 픽셀로
그려진 결과는 제외하고, 최종본은 같은 그림체를 글로 지정해 투명 PNG로 새로 생성했다.
머리띠와 우주 헬멧은 먼저 생성·검수한 동일 그림체 원화를 사용했다.

최종 파일은 모두 `Assets/Resources/MukJump/Skins/skin_<이름>_v1.png`다.
각 원화의 실제 알파, 탈의 원래 눈 가림, 발 프레임 추적, 수집 목록과 해금 화면을 검증한다.
검수용 게임 렌더링은 `output/qa/ink-showroom/`에 저장한다.

## 공통 제작 프롬프트

Generate a single isolated wearable accessory sprite for a Korean 2D ink doodle game. References are ONLY drawing-style references, do not draw any referenced animal or character. Match the little child's hand-drawn dragon and haetae: naive, endearingly clumsy kindergarten drawing, very thick wobbly black crayon outline, broad flat off-white or grey fills, at most ONE muted accent color. Approximately 6-12 simple strokes, deliberately uneven, chunky shapes, tiny asymmetries, lots of visual breathing room. NOT detailed, NOT professional fantasy illustration, NOT realistic, NO fine feather/cloth texture, NO shading/lighting/gradient, NO embroidery, NO ornate tassels, no gold. This should look as though the same child drew a costume for their black ink blob. No eyes, character or mannequin unless specifically an opening is described. Deliver actual PNG alpha transparent background; NEVER draw checkerboard squares. Tight centered crop with 5% transparent margin. Front view flat 2D. 

## 의상별 최종 프롬프트

### 1 — skin_gat_v1.png

Subject: A roomy Korean gat hat drawn badly but charmingly by a child. The LOW CYLINDER is VERY WIDE, 75% of the whole hat width, with a thin broad brim sticking out just a little at each side. This is NOT a narrow top hat. Squat cylinder height only 28% of total width. Charcoal grey flat fill, one rough ivory stripe, NO hanging strings, no ornaments. Wide 2:1 layout. No background checkerboard.

### 2 — skin_spider_mask_v1.png

Subject: A simple roomy off-white spider masquerade mask for a round blob, two very large round TRANSPARENT eye holes centered side-by-side; holes together span middle 58% width. One clumsy little red spider doodle above eyes with 6 straight legs. No human face, NO eyeballs painted into holes, no pointed long nose, no extra tassels. Rounded wide mask, roughly 4:3. CRITICAL FIT: giant oversized goggle-like eye openings, extremely thin central nose bridge. Both round empty transparent eye holes take up nearly the entire face. EACH hole is 34 percent of TOTAL PNG WIDTH (not of mask), and centers are at x32 percent and x68 percent, leaving only 2 percent gap. Avoid small normal eye holes; these are absurdly giant cartoon eye HOLES. Mask fill exists only as a narrow frame around these enormous openings, plus top ears/forehead decoration. PNG alpha transparent holes and background, no painted black inside openings, no character.

### 3 — skin_first_ribbon_v1.png

Subject: A single chunky red ribbon bow: two big uneven red loops, square central knot and two short floppy tails. Simple lopsided crayon doodle, no patterns or filigree. This clips to SIDE of round head, not whole-head mask. Square.

### 4 — skin_scholar_v1.png

Subject: A roomy squat black scholar cap with a WIDE LOWER BAND curved across the forehead, wider than tall, two short flat side flaps. One crooked off-white square on front. No jewels, scrolls, tassels or cords. Broad 4:3 low cap shape.

### 5 — skin_ink_ghost_v1.png

Subject: A very simple loose little ghost cloth HOOD wearable on round black blob: large rounded off-white scalloped outer outline, TWO BIG ROUND TRANSPARENT eye holes side by side occupying middle55% width. No drawn pupils, no character body, no arms/legs, no bows, no folds except two crude black lines. Broad bell shape rather than tight sack. Square.

### 6 — skin_bamboo_hat_v1.png

Subject: A child's very simple oversized bamboo cone hat, wide shallow triangle with softly uneven curved bottom edge. Flat warm ivory fill, four thick crayon spokes and a single off-center black line. NO hanging strings or rear loops, no straw weave, no detailed texture, no red bows. Wide 2:1.

### 7 — skin_red_headband_v1.png

Subject: A single solid low red forehead cloth strip, broad and gently curved, a lopsided small side knot and two short rectangular tails on right. ONLY front strip, no ring, no rear loop, no hole. A thin broad cloth headband wrapping generously around a round forehead. Wide 3:1 silhouette. Only flat dusty vermilion fill and about three black crayon crease marks.

### 8 — skin_lotus_crown_v1.png

Subject: A simple lotus crown consisting of exactly THREE large rounded ivory petals with muted pink centers on a broad softly curving grey forehead band. No dangling strings or ribbons. Broad 2:1, thick uneven black outline.

### 9 — skin_cat_ears_v1.png

Subject: A pair of chunky triangular grey cat ears joined by a wide low OPEN U-shaped grey headband. Simple pink triangles inside. The BIG central U-shaped face opening is completely transparent; no ears of a real cat or face. No bows, bells or hanging strips. Broad 4:3.

### 10 — skin_bell_ribbon_v1.png

Subject: ONE large goofy round off-white bell, a black slit and dot at bottom, one simple red bow above it. No metallic gold, strings, chains, highlights or tiny decoration. Chunky crayon doodle, for side of a head. Square.

### 11 — skin_cloud_cap_v1.png

Subject: A roomy low half-dome off-white cloud hood with a BROAD curved bottom opening like a loose beanie. Top edge three lumpy bumps, one grey spiral scribble. Front lower edge nearly horizontal, no bandana triangle descending between eyes. Open bottom face area transparent. Wide 3:2.

### 12 — skin_paper_boat_v1.png

Subject: One simple child's folded paper boat hat, off-white with bold crooked black fold lines, VERY WIDE low opening/brim along bottom, low wide triangle peaks. No dangling strings or bow. Wide2:1.

### 13 — skin_tea_cup_hat_v1.png

Subject: A chunky squat ivory teacup hat with VERY WIDE bottom base, tiny awkward side handle, one black spiral on cup and one wavy steam squiggle. No hanging ornaments, no pattern, no realistic liquid. Wide3:2.

### 14 — skin_feather_hairpin_v1.png

Subject: One large blunt off-white feather and a short thick black hairpin with a tiny simple red knot. Feather only three broad lobes and one crooked central line, not individual strands. Side-head accessory, diagonal, compact square.

### 15 — skin_peach_flower_v1.png

Subject: A broad flower headband: three simple FIVE-PETAL off-white flowers with dusty pink centers on a thick low curved grey strip. No branches, foliage, ribbons, hanging tassels. Wide2:1.

### 16 — skin_fox_mask_v1.png

Subject: A roomy broad oval ivory fox masquerade mask, short rounded triangle ears, TWO VERY LARGE ROUND TRANSPARENT EYE HOLES centered side by side in lower middle, no drawn eyeballs. One tiny red triangle nose, two simple red cheek strokes. Thick crooked black outline. No ornament. Square. CRITICAL FIT: giant oversized goggle-like eye openings, extremely thin central nose bridge. Both round empty transparent eye holes take up nearly the entire face. EACH hole is 34 percent of TOTAL PNG WIDTH (not of mask), and centers are at x32 percent and x68 percent, leaving only 2 percent gap. Avoid small normal eye holes; these are absurdly giant cartoon eye HOLES. Mask fill exists only as a narrow frame around these enormous openings, plus top ears/forehead decoration. PNG alpha transparent holes and background, no painted black inside openings, no character.

### 17 — skin_crane_crest_v1.png

Subject: A broad forehead crown with two little ivory wings, each ONLY THREE chunky feathers, meeting at one blunt red triangle in center. Lower band very wide and softly curved across forehead. No tassels, no detail. Wide2:1.

### 18 — skin_yaksha_mask_v1.png

Subject: A friendly silly not-scary grey oni/yaksha mask: broad oval with two short ivory blunt horns, TWO VERY LARGE ROUND TRANSPARENT eye holes occupying central55% width, one flat red nose and two blunt tiny fang marks. No pupils/face, no ornate detailing. Square. CRITICAL FIT: giant oversized goggle-like eye openings, extremely thin central nose bridge. Both round empty transparent eye holes take up nearly the entire face. EACH hole is 34 percent of TOTAL PNG WIDTH, centers x32 percent and x68 percent. Only 2 percent gap between holes. Grey mask fill forms a narrow simple frame with two ivory horns and a tiny red nose. Outer oval occupies 94 percent of canvas width. Transparent PNG alpha holes and background, no character, no painted eyes.

### 19 — skin_turtle_shell_v1.png

Subject: Roomy oversized round green-grey TURTLE HELMET with wide arch face opening at bottom, not a small cap. Shell exactly four large crooked polygon patches, thick black outline. Off-white padded bottom rim and short side ear flaps, no ribbons. Front view broad4:3.

### 20 — skin_storm_scarf_v1.png

Subject: A loose roomy charcoal-grey head scarf, a big curved forehead band and one side knot with two short blue-grey tails, SINGLE FRONT BAND ONLY, no rear ring, no hole. Simple clumsy black lines, no cloth folds or detail. Wide3:2.

### 21 — skin_moon_crown_v1.png

Subject: One chunky ivory crescent moon sitting on a BROAD grey curved forehead strip, the crescent tipped a little crookedly. Two tiny red dots, NO tassels, ropes, jewels or dangling things. Wide3:2.

### 22 — skin_brush_master_v1.png

Subject: A loose wide black painter's cap with TWO stubby broad brush tips poking upward from sides, off-white bristles drawn as three blunt strokes each. One simple red square at front. Wide open lower band; no hanging cloth or cords. Broad4:3.

### 23 — skin_spirit_halo_v1.png

Subject: A single oversized OPEN halo ring drawn with a shaky blue-grey crayon line, three very simple small grey flame blobs on top/side of ring. Empty center fully transparent, no fire texture, no glow, no intricate red ornaments. This ring sits BEHIND a round black blob. Square.

### 24 — skin_koi_mask_v1.png

Subject: A broad off-white fish masquerade mask with TWO LARGE ROUND TRANSPARENT eye holes, two simple curved cheek fins, one red patch on forehead and one black mouth line. Childlike, no fish body/real eyes/scales. Broad4:3. CRITICAL FIT: giant oversized goggle-like eye openings, extremely thin central nose bridge. Both round empty transparent eye holes take up nearly the entire face. EACH hole is 34 percent of TOTAL PNG WIDTH (not of mask), and centers are at x32 percent and x68 percent, leaving only 2 percent gap. Avoid small normal eye holes; these are absurdly giant cartoon eye HOLES. Mask fill exists only as a narrow frame around these enormous openings, plus top ears/forehead decoration. PNG alpha transparent holes and background, no painted black inside openings, no character.

### 25 — skin_jade_crown_v1.png

Subject: A low broad simple dull jade-grey crown with three stubby curved prongs, one off-white circle in middle. Forehead strip spans whole width and curves softly, no shiny gems, no gold, no cords. Broad3:2.

### 26 — skin_phoenix_crown_v1.png

Subject: A loose broad crown made of only FIVE BIG rounded feather shapes, alternating ivory and dull red, thick crayon outlines. Wide grey forehead strip. No detailed feather veins, no tassels or gems. Broad3:2.

### 27 — skin_tiger_mask_v1.png

Subject: A broad ivory round tiger mask with two tiny round ears, TWO LARGE ROUND TRANSPARENT eye holes centered side by side, exactly three bold crooked black stripes on forehead, little black triangular nose. No pupils, no body, no ropes or gold. Broad4:3. CRITICAL FIT: giant oversized goggle-like eye openings, extremely thin central nose bridge. Both round empty transparent eye holes take up nearly the entire face. EACH hole is 34 percent of TOTAL PNG WIDTH (not of mask), and centers are at x32 percent and x68 percent, leaving only 2 percent gap. Avoid small normal eye holes; these are absurdly giant cartoon eye HOLES. Mask fill exists only as a narrow frame around these enormous openings, plus top ears/forehead decoration. PNG alpha transparent holes and background, no painted black inside openings, no character.

### 28 — skin_mountain_king_v1.png

Subject: A wide simple grey mountain crown with THREE blunt peaks like a child's zigzag, thick black outline, broad soft off-white bottom band, one dull red circle in center. NO gems, hanging strings or shading. Wide3:2.

### 29 — skin_cloud_dragon_v1.png

Subject: A roomy off-white dragon HOOD accessory, round arch opening for entire black blob face; BIG TRANSPARENT face opening at bottom center taking70% width. Two short crooked ivory horns and two grey cloud bumps; one clumsy red square on broad forehead. No dragon face, no pupils, no body. Broad4:3.

### 30 — skin_ink_mecha_v1.png

Subject: A roomy child-made cardboard robot helmet frame: thick grey roughly square border with rounded corners, HUGE TRANSPARENT round face opening75% width, one uneven ivory zigzag mark at top and two square ear flaps. No glowing eyes, no electronics, no complex robot detail, no body. Square.

### 31 — skin_night_bat_v1.png

Subject: A silly round black bat masquerade mask, two short blunt upward points, TWO VERY LARGE ROUND TRANSPARENT EYE HOLES occupying center55% width. Tiny ivory triangular mark at forehead. No nose/mouth/eyeballs, no winged body, no fine feathers. Broad4:3.

### 32 — skin_astronaut_v1.png

Subject: A BIG roomy goofy astronaut helmet enclosing a round head, with a VERY LARGE genuinely transparent circular FACE OPENING taking up 75% of helmet width and height. Thick wobbly off-white rim, black crayon outline, one tiny dull blue square, two very simple circles for ear discs. Flat naive doodle, no glass/reflection, no head/face/body in opening, no complex controls, no antenna. Square silhouette.

### 33 — skin_clockwork_cat_v1.png

Subject: A loose big grey cat-ear hood FRAME with two blunt triangle ears, a huge TRANSPARENT round face opening70% width, a single off-white simple wind-up key on right side drawn as two round loops and one line. No cat face/body, no mechanical texture. Broad4:3.

### 34 — skin_lucky_dice_v1.png

Subject: A low wide off-white paper crown band carrying THREE lumpy little cube dice, only a few big black dots on each, one red dot. Simple chunky uneven crayon doodle, no ornate trim or gems, forehead band broad and curved. Wide3:2.

### 35 — skin_origami_wings_v1.png

Subject: A PAIR of broad off-white folded paper wings, each just TWO simple triangle panels with one crooked black fold line. No detailed feathers. Inner roots near center, entire central25% of image truly transparent. This attaches BEHIND a round ink blob. Wide2:1.

### 36 — skin_crane_wings_v1.png

Subject: A PAIR of broad funny little ivory crane wings, each ONLY FOUR very chunky rounded feathers with grey tips, thick crooked black crayon outline. No individual feather details, no bird/body. Central20% gap transparent, wing roots tucked behind round blob. Wide2:1.

### 37 — skin_butterfly_wings_v1.png

Subject: A PAIR of goofy butterfly wings, FOUR large uneven grey-blue round petal-shaped lobes, only TWO off-white spots total, thick black crayon outlines. No veins, no detailed texture. Central20% gap fully transparent, no butterfly body, eyes or antenna. Broad4:3.

### 38 — skin_red_shoes_v1.png

Subject: PAIR of funny soft red ankle shoes with thick wobbly black outline, each just a red rounded L shape and off-white top cuff. No stitches, laces, shading. A wide2:1 sheet: left shoe centered at20% width, right shoe at80%, each20% of canvas width, bothcentered60% down canvas and same bottom at78% height. All middle empty transparent, no legs/body.

### 39 — skin_cloud_boots_v1.png

Subject: PAIR of goofy short grey boots with off-white cuffs, each ONE large ivory cloud spiral and blunt outward toes, thick crooked crayon outlines. No patterns/details/shadows. Wide2:1 sheet: left shoe centered at20% width, right at80%, each20% canvas width, bothcentered55% down and same bottomat78% height. Center entirely transparent, no body/legs.
