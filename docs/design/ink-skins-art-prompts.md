# 스킨 원화 생성 프롬프트

내장 image_gen 사용. 원화는 Assets/Resources/MukJump/Skins에 복사해 프로젝트 안에서 참조한다.

## 용꼬리의 두꺼운 뿌리 — dragon_tail_sheet.png

2026-09-14, 내장 imagegen 사용. 적용 파일은 `Assets/Art/Character/Animations/dragon_tail_sheet.png`이다.
기존 원화 편집 결과 두 개는 체크무늬가 실제 픽셀로 그려져 제외했고, 얇은 중간 시안도 제외했다.
최종 1254×1254 RGBA는 굵은 뿌리·배에서 끝으로 좁아지는 네 프레임이며, 실제 투명 픽셀을 검사했다.
목록과 동작 줄이기 그림도 이 시트의 첫 프레임을 사용한다. 원먹·의상 ID·해금 조건·능력은 바꾸지 않는다.
행·열의 실제 원화 간격으로 같은 접점을 정렬하고, 등록 위치만 보정한다. 프레임 메시 변형은 없다.

최종 생성 프롬프트:

> A transparent-background PNG game sprite sheet: exactly FOUR gently wagging poses of a small cute IVORY DRAGON TAIL in a clean 2x2 grid. Real transparent alpha outside the silhouettes. Each tail is a chunky curved CRESCENT/SAUSAGE: rounded thick LEFT attachment base, broad thick curved belly in the MIDDLE, then a smooth slender upward-curving RIGHT end with a little three-point flame-shaped tip. The proximal LEFT TWO THIRDS must stay substantial and FAT, including the section under the THIRD spike: approximately 150 pixels of ivory flesh thickness at the left base and 115 pixels at the middle in each 625-pixel-wide cell. ONLY the final quarter narrows to the delicate flame-tipped stem. NOT a triangle that immediately thins into a cord, NOT a long thin string, NOT a flat axe or paddle. Three SMALL triangular dorsal spikes along the top of the thick belly, one tiny black wavy doodle on the side. Funny humble Korean ink game art: childlike slightly uneven charcoal/crayon black outlines, flat warm ivory paper-white filling, no realistic scales, no gradients or highlights. Same scale and same rounded root position in all four frames; only the distal end swishes a LITTLE through up / middle / slightly down / middle. Broad belly and root do not wobble or change size. Each entire tail comfortably contained inside its own square cell with 5% transparent margins and a clear center gutter. Transparent background; no paper, no floor, no background color, no checker pattern, no characters, no bodies, no heads, no legs, no lettering, no gridlines, no watermarks. Square image 1254x1254 if possible, 2 rows and 2 columns.

## 팔번공 — skin_three_cushion_v1.png

2026-09-13, 내장 imagegen으로 새 투명 원화 생성. 최종 에셋은 `Assets/Resources/MukJump/Skins/skin_three_cushion_v1.png`이다. 실제 출력은 1280×1280 RGBA이며 기존 씬 빌더가 Single Sprite, 최대 512, mipmap/readable OFF로 임포트한다. 원래 먹의 몸체·눈·팔다리는 그대로 유지하고 머리에 겹치는 팔번 공 모자만 추가했다. 로비 해금 알림, 꾸미기 착용 화면과 RGB 1024 Game Center 아이콘에서 맞춤을 확인했다.

최종 생성 프롬프트(1회, 외부 캐릭터 원화 없이 신규 생성):

Use case: stylized-concept. Create ONE production 2D game accessory sprite for the little Korean ink-blot character in an achievement-unlocked billiards costume. Asset: a squat black EIGHT BALL HELMET/CAP worn over the top of a round head, NOT a separate full ball perched on top. Front view. A broad simple domed black cap with a gently curved open lower edge; one large off-white round badge on its front with a rough hand-drawn black numeral '8'. No straps, no visor, no brim, no human face, no eyes, no body, no cue sticks, no extra props. Naive child's charcoal/crayon doodle style, funny and humble, very thick wobbly black outlines, rough matte charcoal flat fill, one ivory flat badge, slightly asymmetrical. No realistic highlights or gradients, no polished 3D, no fine textures. Centered square 1024x1024 canvas; entire cap silhouette approximately x=150..874,y=300..700 with generous empty margins. This will overlay the top of the original spiky ink body while leaving its eyes and limbs completely visible. Isolated accessory only. GENUINELY TRANSPARENT PNG ALPHA BACKGROUND, no white rectangle and no painted checkerboard, no shadows, no text except the single digit 8.

## 바람 갓 — skin_gat_v1.png

Use case: stylized-concept. Asset type: production transparent 2D game accessory sprite, NOT a whole character. Create one small traditional Korean black gat hat, straight-on frontal view with only a little visible top, very wide low elliptical brim and squat cylindrical crown. Hand brushed sumi-e black ink, rough dry-brush edges, subtle warm gray wash, readable simple silhouette. A short hanging dark-red ribbon at either side. Cute compact proportions for a round ink-drop creature. Restrained palette #1C1B1A, #EAE3D2, very small #AE1C3C accent. Isolated SINGLE hat centered, filling 85% canvas width. Genuine alpha transparent background, no white rectangle, no shadow/background, no text, no body, no face, no branding, no extra objects. All details within frame. This is an original accessory for an Eastern ink painting mobile game.

## 거미탈 — skin_spider_mask_v1.png

Use case: stylized-concept. Asset type: ONE original transparent game accessory mask sprite, not a full character. A compact traditional East Asian theatrical SPIDER tal half-mask that covers only the forehead and area around two large circular eye holes; lower edge shallow so creature body remains visible. Straight-on symmetrical front view. Warm ivory handmade paper with rough charcoal ink dry-brush edges, four simple brushstroke spider legs radiating on each temple, a tiny original red spider-ink mark on the forehead. The two large tall OVAL EYE HOLES must be transparent cut-outs, not painted eyeballs. Each hole at x 35% and 65%, y 58%. Short red cloth ties on outer temples. No nose, no mouth, no face underneath. Adorable restrained ink-wash illustration, not realistic, not horror. Readable at small scale. Isolated centered single mask, true alpha transparent background and transparent eye holes. No white background rectangle, no shadows, no labels, no text, no character, no superhero suit, no Marvel or Spider-Man graphic design, no web pattern. Palette charcoal #1C1B1A, parchment #EAE3D2, small #AE1C3C accent. Simple broad handmade shapes with ink grain. Fill 85% canvas width.

마지막 투명 배경 편집: “Use case: background-extraction. Edit only background and eye hole cut-outs of this original parchment spider mask. Remove EVERY dark gray/black/red gradient, glow, blur and backdrop around the mask AND inside both eye holes. Leave them truly empty alpha transparent pixels. No soft halo or cast shadow. Keep the exact mask illustration, its two eye-hole outline edges, rough ink texture, red spider, ribbons, silhouette, proportions and pose unchanged. Output clean transparent PNG accessory cutout on transparent canvas. Must have real transparency outside the silhouette and throughout both eye apertures, not a painted black/gray background.”


## 확장 의상 — 최종 선택 원화

2026-09-12 수정: 35개 의상 모두 원래 먹 몸체·눈·동작 위에 붙이는 장식이다.
전신 로봇·고양이·용 시안은 채택하지 않았다. 아래는 최종 Assets 원화에 사용한 생성/수정 프롬프트다.
외부 캐릭터·상표 디자인을 사용하지 않는다. 모든 원화는 PNG 알파 투명 이미지다.

### 첫 먹매듭 (FirstRibbon, ID 3)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE two short red silk ribbons tied in a traditional Korean knot at the top of the head. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 선비 관 (Scholar, ID 4)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE traditional black scholar headpiece with two small side wings and one ivory paper scroll tucked across the crown. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 빈 먹 유령 (InkGhost, ID 5)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE small white ragged paper ghost hood with two generous transparent oval eye holes, playful rather than scary. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 대나무 삿갓 (BambooHat, ID 6)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE wide woven bamboo conical hat with rough brushed straw texture and charcoal edge. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 수련 머리띠 (RedHeadband, ID 7)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE red martial arts forehead band with off-center tied knot and two short tails. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 연꽃 관 (LotusCrown, ID 8)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE layered ivory and muted rose lotus blossom crown, three broad petals and ink outline. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 고양이 귀 (CatEars, ID 9)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE two pointed black cat ears on a subtle narrow charcoal headband, paper pink inner ears. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 방울 매듭 (BellRibbon, ID 10)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE small bronze bell hanging from deep red knotted ribbon, compact forehead accessory. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 구름 두건 (CloudCap, ID 11)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE soft white cloud shaped bandanna with swirling dark gray ink line edges. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 종이배 모자 (PaperBoat, ID 12)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE folded ivory origami paper boat worn as a hat, charcoal creases, red tiny seal. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 찻잔 모자 (TeaCupHat, ID 13)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE small antique celadon teacup balancing above the head, dark tea interior, one simple steam curl. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 깃털 비녀 (FeatherHairpin, ID 14)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE ivory crane feather angled gently right on a thin ink-black hairpin with small red knot. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 복사꽃 (PeachFlower, ID 15)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE compact cluster of three muted rose peach blossoms and dark branches as head ornament. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 여우탈 (FoxMask, ID 16)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE white fox half-mask with pointed ears and red markings, two large transparent oval eye holes. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 학의 볏 (CraneCrest, ID 17)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE graceful ivory crane wing feather headdress, central small crimson crest. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 야차탈 (YakshaMask, ID 18)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE charcoal and red theatrical yaksha half-mask with small blunt horns, broad transparent oval eye holes. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 거북 투구 (TurtleShell, ID 19)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE rounded moss jade turtle shell helmet with segmented brush lines and small side flaps. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 질풍 두건 (StormScarf, ID 20)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE charcoal ninja headscarf with muted teal edging, forehead knot and two windswept fabric tails. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 달빛 관 (MoonCrown, ID 21)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE thin crescent moon crown of ivory paper and dark silver ink, central tiny red knot. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 붓 장인 (BrushMaster, ID 22)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE two miniature calligraphy brushes crossed behind a compact black topknot headpiece. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 도깨비 불 (SpiritHalo, ID 23)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE small floating ring of five stylized teal ink flames above the head, broad handmade shapes. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 잉어탈 (KoiMask, ID 24)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE ivory koi fish half-mask with red patches and two small side fins, broad transparent oval eye holes. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 비취 용관 (JadeCrown, ID 25)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE jade dragon inspired small crown with two elegant curved antler horns and black dry brush detail. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 봉황 관 (PhoenixCrown, ID 26)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE majestic compact phoenix headdress, red and ivory sweeping feathers with charcoal contour. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 산군 탈 (TigerMask, ID 27)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE stylized amber ivory tiger half-mask, charcoal stripes and two transparent oval eye holes. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 산왕 관 (MountainKing, ID 28)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE rugged dark stone mountain crown with three peaks, a tiny red rising sun ornament. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 구름 용 (CloudDragon, ID 29)

A game-ready standalone single sprite on a TRANSPARENT BACKGROUND: a tiny ivory paper cap with two short blunt charcoal twig horns and one tiny muted red knot. Dragon-themed headpiece, not a dragon creature. Extremely simple FLAT silhouette art, opaque solid charcoal #1C1B1A and ivory #EAE3D2 with one small red accent. Shape language like cut paper or a simple ink splat cartoon. No detailed painting, no surface textures, no rendered fur or metal, no gold, no shadows, no glow, no background. It will be worn at the top of an existing round black ink splat character with two big white oval eyes; DO NOT include that character, eyes, face, mouth, body, torso, paws or legs. Keep the lower middle face area completely empty and transparent. This accessory forms a broad shallow upside-down U arch across just the upper edge of a circular silhouette. Plain front view, very low detail readable at 48 pixels. Only this accessory. Tight crop with clear transparent padding, GENUINE transparent PNG alpha channel. Do not draw checkerboard background.

### 먹빛 기갑 (InkMecha, ID 30)

Edit this asset for game production: remove EVERY gray-and-white checkerboard pixel and ALL paper texture outside the black headpiece. Those are an incorrect baked background. Return the SAME small black helmet and its two small side tabs isolated on a GENUINE TRANSPARENT ALPHA PNG. The large central face opening, the side gaps, and everything outside the accessory must have alpha zero. No replacement backdrop of any color and no checkerboard drawn into pixels. Preserve the existing flat charcoal and ivory marks, simplify to solid-color black cut-paper shapes with just a tiny red mark. Crop canvas tightly around the helmet with 8% transparent padding. No new character, no new eyes or body. Only the small helmet accessory.

### 밤의 파수꾼 (NightBat, ID 31)

Create a standalone ORIGINAL NIGHT OWL CLOTH MASK accessory for a cute round ink-blob 2D mobile game character. A charcoal cloth mask with two little feather tufts, subtle ivory feather markings and two large fully transparent round eye cutouts. Gentle guardian owl mood. Hand-painted sumi-e ink on paper style, rough charcoal outlines, restrained cream and crimson accents. Centered symmetrical front view, isolated true transparent alpha background, no character body, no opaque backdrop, no text, no logo. Single game-ready sprite, comfortable transparent padding. This is an original owl design, not any existing fictional character.

### 달 여행자 (Astronaut, ID 32)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE IRREGULAR: original ivory retro astronaut helmet rim with small side antenna and generous transparent open face window, cyan accents, no branding. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 태엽 고양이 (ClockworkCat, ID 33)

A game-ready standalone single sprite on a TRANSPARENT BACKGROUND: a tiny charcoal cat-ear headband, two triangular ears with ivory inner triangles, and a small ivory winding-key ornament on the right. Cat-themed costume accessory, not a cat. Extremely simple FLAT silhouette art, opaque solid charcoal #1C1B1A and ivory #EAE3D2 with one small red accent. Shape language like cut paper or a simple ink splat cartoon. No detailed painting, no surface textures, no rendered fur or metal, no gold, no shadows, no glow, no background. It will be worn at the top of an existing round black ink splat character with two big white oval eyes; DO NOT include that character, eyes, face, mouth, body, torso, paws or legs. Keep the lower middle face area completely empty and transparent. This accessory forms a broad shallow upside-down U arch across just the upper edge of a circular silhouette. Plain front view, very low detail readable at 48 pixels. Only this accessory. Tight crop with clear transparent padding, GENUINE transparent PNG alpha channel. Do not draw checkerboard background.

### 일곱 번째 눈 (LuckyDice, ID 34)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE IRREGULAR: little ivory lucky dice crown with three floating miniature cubes, red and black painted pips, broad readable silhouettes. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.

### 접힌 차원 (OrigamiWings, ID 35)

Use case: stylized-concept. Production transparent 2D game ACCESSORY sprite only, NOT a whole character. ONE IRREGULAR: angular silver ivory origami wing headpiece shaped like an impossible folded paper bow, subtle violet edges and tiny red knot. Straight-on frontal view with balanced proportions for a small round ink creature. Isolated centered accessory fills 80% of canvas, all ribbon ends within frame. Hand-brushed sumi-e charcoal #1C1B1A, warm parchment #EAE3D2, restrained #AE1C3C accents; rough ink edges, broad readable handmade shapes. Original design. TRUE alpha transparent background, no painted gradient, no shadow or glow backdrop, no body or head or face underneath, no text, no branding. Any eye/face openings must be transparent cut-outs with no eyeballs. Cute, polished, readable at 128px.
