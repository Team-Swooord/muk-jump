# 결과 두루마리 — 한지와 하강 롤

사용자 요청(2026-09-05): 젖은 듯한 한지 내부만 유지하고 사각 판·양끝 장식을 새로 제작.
접힌 두루마리가 위에서 아래로 펼쳐지고 한지가 작게 계속 펄럭인다.

- 원래 `pg_hanji_background.png`의 섬유·물 번짐을 그대로 사용한다. 불투명 사각 덮개와 외곽선은 제거한다.
- 종이는 고정 18×28 메시, 가장자리 알파·비대칭 곡선으로 표현한다. 종이와 그림자 총 1,102 정점.
- 0.56초 상단 고정 하강 펼침. 종이 크기를 늘리지 않고 UV를 위에서부터 공개한다.
- 펼침 뒤 종이 가장자리 약 7px 이내, 하단 롤 이동 3px 이내·회전 0.22도 이내의 느린 반복.
- 제목·점수·버튼은 별도 정적인 계층. Bold·가운데 정렬·기존 크기와 광고/저장 동작을 유지한다.
- 움직임 줄이기는 정적인 열린 상태. 앱 비활성/닫힘에는 애니메이션 시계를 진행하지 않는다.
- 원화는 생성 원본을 보존하며 `MukJumpSceneBuilder.ConfigureHanjiScrollRoll`이 투명 여백만 슬라이스한다.
- 검증: 메시 UV·상단 고정·하단 하강·정점 상한·장식 입력 차단·텍스트 고정·재노출·글자 잘림.
- 실제 촬영 메뉴: `MukJump > 검증 > 두루마리 펼침 펄럭임 촬영` (계정 저장을 바꾸지 않는 메모리 저장소).

## 원화 생성

Codex 내장 imagegen 사용(CLI/API 미사용). 출력 파일:
`Assets/Resources/MukJump/UI/Common/scroll_roll_hanji_v2.png`.
기존 원화를 덮어쓰지 않은 신규 2172×724 RGBA 에셋이다.

전체 프롬프트:

> Use case: stylized-concept. Asset type: transparent 2D game UI sprite, a single horizontal rolled edge of a Korean hanji hanging scroll. This is NOT a whole scroll, NOT a mockup: draw ONE long narrow paper cylinder only, isolated in transparent space, suitable for attaching to the top or bottom of a separate animated paper sheet in Unity. Front-facing orthographic view, level horizontal, approximately 12:1 object width to height, spanning almost the entire canvas width; modest transparent padding, keep all endpoints visible. The central cylinder is tightly rolled warm ivory Korean mulberry hanji, a little damp, with very soft grey water-wash mottling and visible tiny paper fibres. Gentle cylindrical shading: luminous broad top/middle, soft grey-beige fold shadow along lower edge, extremely fine charcoal brush line under lip. Slightly irregular organic paper lip, elegant and understated, high craft 2D hand-painted ink-and-watercolor aesthetic. Thin dark brown charcoal wooden dowel peeks out just a tiny amount at each end, understated small oval flat endpoints, NOT large knobs. Palette ivory #EAE3D2, pale warm grey, charcoal #2B2620. Avoid thick black outlines, hexagonal caps, knobs, wheels, screws, badges, white/black rectangle background, drop shadow outside the object, labels, lettering, other objects. Genuine transparent alpha outside sprite, no checkerboard. Large clean production asset, preferably wide landscape canvas. The scroll PAPER SHEET IS NOT ATTACHED; only the narrow rolled edge.

emil-design-eng 원칙을 따라 장식과 읽기/입력 계층을 분리하고 움직임 줄이기를 지원한다.
사용자가 명시한 두루마리 펼침을 알아볼 수 있도록 펼침 시간은 일반 버튼 전환보다 길게 둔다.

## 확인한 결과 (2026-09-05)

- Unity 전체 EditMode 1,216/1,216 통과, 실패·건너뜀 0. 결과: `output/quality-polish/hanji-scroll-editmode-1216.xml`.
- 종이의 `CanvasRenderer` 필수 구성과 VertexHelper 생성 경로를 명시하고, 실제 렌더러에 551개 정점이 전달되는 회귀 검사를 추가했다.
- 실제 Game View 촬영: `output/quality-polish/captures/20260905-165425` (540×960, 20fps). 합성/보간 프레임 없이 GIF로 인코딩했다.
- 펼침 + 8초 대기: `output/quality-polish/hanji-scroll-unroll.gif`.
- 한지 면만 6초 확인: `output/quality-polish/hanji-paper-flutter.gif`.
- 광고 대기·저장 복구·포기 확인·재노출 후 메인 버튼 콜백 1회도 검증용 메모리 저장소에서 확인했다. 실제 광고나 계정 저장은 호출하지 않았다.
- 움직임 줄이기 상태는 열린 정적 화면으로 촬영했다. 이번 변경의 iOS/Android 실기기 실행과 TestFlight 업로드는 아직 수행하지 않았다.
