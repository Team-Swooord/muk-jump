# 설정 수묵 아이콘과 계정 정리 (2026-09-06)

## 적용

| Before | After | Why |
| --- | --- | --- |
| 설정을 닫으려면 ×만 사용 | 바깥 dim 탭도 같은 Close 경로 | 종이 안/드래그는 제외하고 동기화 잠금·역순 말림은 보존 |
| 움직임 줄이기 토글과 상세 버튼 | 모두 제거, 배경음/효과음/진동 3개 가운데 정렬 | 불필요한 선택지 제거 |
| 열린 두루마리의 무한 펄럭임·롤 흔들림 | 시간에 무관한 정지 자세 | 상시 반복 장식을 없애고 열기/닫기/눌림 반응만 유지 |
| 절차적 선 조합 아이콘 | 투명 수묵 원화 9종 | 한지·먹 농담과 굵기를 통일 |
| 소리·화면 / 광고 선택 메뉴 | 한국어 / 고객센터 / 튜토리얼 / 닉네임 두 열 | 닉네임은 붓 아이콘, 계정 연동은 바로 아래 중앙 |
| 설정 안의 순위 메뉴 | 메인 최고 기록 왼쪽의 월계관 아이콘 | 글자·버튼 배경 없이 표시, 닫으면 메인 복귀 |
| 로그인 수단·긴 지원 코드 상시 나열 | 상태별 로그인/재로그인, 문의 코드 복사 | 정상 화면의 정보량 축소 |
| 광고 개인정보 버튼 | 메인·상세 설정에서 생성 자체 제거 | 사용자 요청 반영. UMP Required 환경의 대체 경로는 출시 전 미해결 항목 |
| 큰 약관/개인정보 한지 버튼 | 종이 안 맨 밑 좌우의 32px 밑줄 링크 | 보조 정보의 시각 크기만 줄이고 340×120 터치 영역은 보존 |
| 종이 안 큰 닫기 버튼 | 종이 밖 아래 중앙 96×96 한지 ×, 120×120 터치 영역 | 콘텐츠와 닫기 행동 분리, Safe Area는 하단 190까지 포함 |
| 공통 CTA의 굵은 검정/빨강 외곽선 | 옵션과 같은 한지 한 장, PPU 배율 3 | 모서리를 덧그리지 않고 조용한 종이 결로 통일 |
| 구매의 진한 붉은 밑줄 | 따뜻한 종이 tint와 안쪽의 낙관색 7.5%·먹 2.5% 번짐 | 중요한 행동만 조금 다르게, 글자와 외곽은 선명하게 유지 |
| 성장 선택 탭·원형 일시정지 | 한지 면 위의 기존 내용·기호 | 메인을 제외한 플레이어용 버튼 재질 연결 |

emil-design-eng의 시각적 일관성·불필요한 정보 축소·입력 안정성 원칙을 적용했다.
닉네임 아이콘은 `Assets/Art/UI/muk_brush_icon.png`를 그대로 재사용한다. 생성한 이름표 시안은
투명 배경이 없어 적용하지 않았다. 월계관은 기존 `settings_icon_rank_v1`을 그대로 사용한다.
두루마리 펼침/역순 닫힘, 눌림 피드백, 실제 저장/충돌/삭제 처리 로직은 유지한다.
설정 한국어 버튼은 실제 현재 언어만 안내한다. 다른 언어 번역, 알림, 쿠폰을 구현한 것으로 표시하지 않는다.
상세 소리·화면 구현은 남겨 두되 메인 진입 버튼은 제거했다. 기존 마지막 음량 복구는 보존한다.
메인 로비 3개 먹물 버튼·공식 Apple 로그인·보조 약관 텍스트 링크는 예외다.
새 래스터를 만들지 않고 기존 한지와 공통 먹 마스크를 재사용했다. 번짐은 종이 안쪽의 정적 장식 두 장이며 클릭을 받지 않고 역할 전환 시 재사용한다.
설정 닫기는 OptionsPage 하위에 유지해 하위 페이지에서 숨김/입력 차단과 기존 역순 닫힘을 그대로 상속한다. 실제 위치만 종이 바깥 y=-850으로 옮긴다.

## 참고와 경계

- 사용자가 제공한 《용사님 돌아왔어요》 화면: 4개 빠른 설정과 두 열 메뉴, 하단 계정 연동이라는 배치만 참고했다. 타 게임 픽셀 원화는 포함하지 않았다.
- [Supercell 공식 계정 안내](https://support.supercell.com/clash-of-clans/en/articles/supercell-id-playing-with-multiple-game-accounts.html): 설정에서 연동 상태를 확인하고 별도 계정 화면으로 들어가는 동선 참고. 먹점프에서는 계정/순위 분리를 설계 판단으로 적용했다.
- [Google UMP 개인정보 진입점](https://developers.google.com/admob/unity/privacy#privacy_options): Required일 때 보이고 동작하는 재선택 경로가 필요하다. 요청대로 설정 버튼은 제거했지만 대체 진입점은 구현하지 않았다. **해당 네이티브 환경은 출시 검수 미완료**이며, UMP 상태를 NotRequired로 위조하거나 초기 동의를 건너뛰지는 않는다. 앱인토스에는 네이티브 UMP가 없다.
- [토스 외부 URL 연결](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/화면%20이동/openURL.html): 토스 고객센터는 AIT.OpenURL(mailto, 10000). 실패 시 짧은 실패 상태와 이메일 주소를 표시한다.
- 고객센터: cysbandcs@gmail.com. 제목만 포함한 메일 작성 화면으로 연결한다. 계정 ID/토큰 자동 첨부와 자동 전송은 없다.
- 네이티브 Google/Apple 흐름은 유지한다. 이미 연동된 제공자는 다른 로그인 수단을 나열하지 않으며 인증 만료 시 원래 제공자로 재연결할 수 있다.
- 계정/서버 기록 삭제의 2회 확인, 지연, 비동기 상태 변경 취소, 충돌 선택/복구 모달은 보존한다.
- 앱인토스는 기존 게임 hash·공식 순위 정책 그대로다. 이번 작업은 성장 클라우드 저장을 새로 구현하거나 배포하지 않았다.

## 원화

내장 image_gen 도구로 신규 생성한 9개 PNG를 원본 그대로 저장했다. 모두 1254×1254 RGBA이며 각 원화의 실루엣/여백을 눈으로 확인했다.
게임 내에서는 MukJumpSceneBuilder.ConfigureSettingsIcons가 256px Sprite/Single/FullRect, no mipmap, alpha transparency, clamp, bilinear, CompressedHQ를 관리한다.
Build Main Scene과 에디터 도메인 로드 후 설정 경로에 연결했다. 씬 YAML이나 PNG의 픽셀은 직접 수정하지 않았다.
설정 버튼 86px, 빠른 토글 64px, 계정 대표 96px. 흰 tint로 크림색/먹 농담을 보존하며 꺼짐은 알파 65%와 문구로 구분한다.

### 계정 창 간결화

- 제목은 가운데 정렬한다. 종이 안의 `설정`·`완료` 버튼은 제거하고 종이 밖 96px ×(터치 120px)와 기존 바깥 DIM 닫기를 사용한다.
- 종이 높이는 표시되는 행동 행 수로 계산한다. 로그인·로그아웃/삭제·문의 코드 복사 행은 145px 간격이며 마지막 행과 종이 하단 사이에는 125px 여백을 둔다.
- iOS 공식 Apple 로그인 버튼과 원래 제공자 재로그인 경로는 유지한다. 정상 게스트의 서버 설정 안내는 숨기되 연결 실패·저장 오류·영구 삭제 및 기록 선택 경고는 보존한다.
- 계정 충돌·기록 선택·동기화 대기 모달도 같은 종이 높이에 맞추고, 닫기 제한과 기록 처리 로직은 변경하지 않는다.

- [music](../../Assets/Resources/MukJump/UI/Common/settings_icon_music_v1.png)
- [sound](../../Assets/Resources/MukJump/UI/Common/settings_icon_sound_v1.png)
- [haptics](../../Assets/Resources/MukJump/UI/Common/settings_icon_haptics_v1.png)
- [motion](../../Assets/Resources/MukJump/UI/Common/settings_icon_motion_v1.png)
- [language](../../Assets/Resources/MukJump/UI/Common/settings_icon_language_v1.png)
- [support](../../Assets/Resources/MukJump/UI/Common/settings_icon_support_v1.png)
- [tutorial](../../Assets/Resources/MukJump/UI/Common/settings_icon_tutorial_v1.png)
- [account](../../Assets/Resources/MukJump/UI/Common/settings_icon_account_v1.png)
- [rank](../../Assets/Resources/MukJump/UI/Common/settings_icon_rank_v1.png)

## 검증

- 최종 런타임 / Editor 및 테스트 / WebGL 플레이어 분기 Roslyn 컴파일: 모두 종료 코드 0, 컴파일 오류 0건. 기존 미사용 필드 경고 등은 남아 있다.
- 두 열 메뉴·아이콘/색/터치·메일 URI·계정 상태·원화 임포트·순위 정렬/복귀·약관 하단 링크에 이어 외부 닫기 위치/터치·하위 페이지 Fit 복귀 검사를 추가했다. UMP 버튼 생성 없음, 공통 외곽선 제거/번짐 재사용, 성장/일시정지 스킨 검사도 최신화했다. 테스트 코드 컴파일과 실행 통과는 구분한다.
- 열린 Unity Editor의 별도 실행 로그/이전 테스트 결과는 이번 최종 코드의 테스트 통과 증거로 사용하지 않는다. 현재 변경의 EditMode 실행 및 실기기 화면 검증은 아직 남아 있다.
- 프로젝트 잠금 때문에 같은 프로젝트 배치모드는 실행하지 않았고 GUI 원격 조작도 하지 않았다. 최신 원화 임포트·실제 화면·메일 앱/토스 QR 확인이 남아 있다.
- 기존 테스트 XML/GIF는 이번 변경 검증으로 사용하지 않는다. 커밋/배포/외부 계정 설정 변경은 하지 않았다.

## 최종 생성 프롬프트 (내장 도구)

### music

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: a single beamed pair of musical eighth notes with round ink drop note heads. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### sound

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: a compact loudspeaker facing right with two curved sound waves. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### haptics

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: a simple rounded vertical phone silhouette with two short curved vibration strokes on each side. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### motion

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: one softly curving feather with two short gentle breeze strokes. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### language

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: a simple round globe showing latitude and longitude arcs, no land detail. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### support

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: a closed folded hanji paper envelope, flap clearly visible, with a tiny dark red wax seal. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### tutorial

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: one half-unrolled vertical hanji scroll, small dark wooden rollers, no writing. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### account

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. Subject: two interlocking rounded chain links, symbolizing account linking and safekeeping. Original Korean sumukhwa / East Asian ink wash painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside object, tactile dry-brush fibers but clean readable silhouette at 36px. One cohesive simple icon only, no surrounding badge or frame, no ground or cast shadow, no scenery, no splatters outside the form, no text or letters, no watermark, not pixel art, not photorealistic, no glossy 3D. Square 1024 canvas; centered subject occupying approximately 72% of width and height with clear empty margin on every side. Genuinely transparent background with alpha, not white or checkerboard painted into the image. High contrast against pale paper; restrained elegant mobile game icon.

### rank

Use case: stylized-concept. Asset type: production mobile game settings UI icon, single isolated PNG. A simple laurel wreath surrounding a small mountain peak, representing high-score rankings in an East Asian ink-wash climbing game. Original Korean sumukhwa painting, confident dark charcoal #1C1B1A brush contours, restrained warm hanji #EAE3D2 washes inside shapes, tactile dry-brush fibers but clean readable silhouette at 36px. Cohesive simple icon only, no badge background or outer frame, no ground shadow, no scenery, no splatters, no text, no numerals, no watermark, not pixel art, not photorealistic or glossy 3D. Square canvas, centered icon occupying 72% with generous margin all sides. Genuinely transparent alpha background, not a painted checkerboard. High contrast on pale paper. Match a set of cream-filled charcoal brush icons.
