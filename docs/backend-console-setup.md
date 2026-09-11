# 먹점프 뒤끝 출시 콘솔 설정

## 기기 국가·지역 표시

- `MukJumpPlayer`의 문자열 `deviceRegion` 열을 최고 고도 리더보드의 **추가 항목**으로 연결한다.
- 앱은 설정된 지역 코드(KR/US/JP 등)를 저장·순위 갱신 요청에 포함하고 `item.extraData`로 읽는다.
- 언어·GPS·IP로 국가를 추정하지 않는다. 빈 값·기존 미등록 행은 지구 아이콘을 표시한다.
- 설정 이전에는 지구 아이콘이 표시된다. 기존 점수·랭킹·계정은 삭제하거나 다시 만들지 않는다.
- 현재 연결 보드는 `세계 최고의 먹`(`01a08afd-b237-7723-9e6a-b8d7950285fd`)이다.
  미사용 `먹점프 최고 고도`(`01a04198-3705-7286-bce6-651ddcd17e2f`)는 사용자 요청으로
  2026-09-11 콘솔에서 영구 삭제했다. 활성 보드·게임 계정·게임정보 행은 삭제하지 않았다.
- 완료한 판이 있는 기존 사용자는 다음 정상 기록 동기화에서 지역 정보도 함께 순위에 반영한다.

이 문서는 **iOS·Android 네이티브 빌드**에만 적용한다. Apps in Toss WebGL에서는 뒤끝
네이티브 SDK를 포함하지 않고 토스 앱 세션과 공식 게임센터를 사용한다.

## 첫 실행과 계정 연동

2026-09-10 최신 네이티브 UI는 **게스트 기본·Apple 연동 선택**이다. Google 진입은
현재 비활성이고 아래 Google 관련 콘솔 항목은 과거 구성/후속 플랫폼 참고용이다.
로그인·동기화 감사의 재현 결과와 미검증 실기기 항목은
[계정 동기화 점검표](qa/account-sync-audit-2026-09-10.md)에서 구분한다.

- 네이티브 Release/Development 빌드 모두 첫 실행에 저장 토큰을 확인하고, 신규 설치는
  `GuestLogin`으로 SDK가 UUID 기반 게스트 ID를 생성·보관한다. 에디터 자동 테스트는 접속하지 않는다.
- 게스트도 `MukJumpPlayer`에 기존 성장·먹빛·최고 기록 스냅샷을 저장한다. 계정 창은
  서버 인증 후 실제 `Backend.UID`를 표시·복사하고, Apple/Google 버튼은
  `ChangeCustomToFederation`으로 같은 게스트 계정을 전환한다. 가입 UUID와 문의용 UID는 서로 다른 값이다.
- 이미 가입된 소셜 계정은 확인 UI로 선택하며, 성장 재화를 자동 병합하지 않는다.
  토큰 만료 시 저장된 게스트 자격 정보가 있을 때만 재로그인하고 서버 소유자를 재검증한다.
- 최초 연결 실패는 로컬 플레이를 유지하며 로비에서 제한적으로 재시도한다. 로그아웃한
  사용자를 임의로 재로그인하거나 플레이 중 계정 기록을 교체하지 않는다.
- 게스트 자격 정보는 앱 삭제·기기 변경 시 사라질 수 있다. 그 전에 소셜 연동이 필요하다.
- 이 설명은 구현 경로다. 실제 서버 계정 생성·재실행·소셜 전환·기록 복구 성공은
  실기기 왕복 확인 전까지 완료로 표시하지 않는다. Android OAuth 미설정 항목은 아래와 같다.

SHIFT나 던던던과 같은 개발자 계정은 사용할 수 있지만, 두 게임의 앱 ID·서명키·OAuth
Client·리더보드 UUID를 복사하지 않는다. 운영 AdMob 게시자 계정은 사용자가 확인한
`cysbandcs@gmail.com`이고, Google Cloud OAuth 프로젝트를 관리하는 계정은 이 계정과
같을 필요가 없다. 현재 로그인된 다른 Google 계정의 프로젝트를 확인해 번들 ID가
`com.CYSB.MukJump`인 먹점프 전용 OAuth 값을 만든 뒤 Unity 설정 에셋에는 공개 식별자만
저장한다.

연락처는 용도별로 분리한다. 광고 게시자와 공개 고객지원·개인정보 문의에는
`cysbandcs@gmail.com`을 사용하고, Apple·Google 개발자 계정의 공식 연락처에는
`choiysband@gmail.com`을 사용한다. 뒤끝 앱 ID·키·정책 URL은 먹점프 전용으로 만든다.

## 1. 뒤끝 앱

2026-08-28 기준 먹점프 전용 앱 인증값, 플랫폼 식별자, 아래 게임정보 테이블과 리더보드는
콘솔과 Unity에 반영됐다. Apple 토큰 철회용 Key ID·p8와 Apple 계정 변경 웹훅도 콘솔에
등록했고 먹점프 전용 iOS Google OAuth도 Unity와 생성된 `Info.plist`에 반영했다. 남은
항목은 실제 iOS 탈퇴의 `RevokeAppleToken` 성공, iOS Google 로그인 실기기 검증,
Android Google Web OAuth와 서명 해시 검증이다.

1. 뒤끝 콘솔에서 먹점프 앱을 생성한다.
2. Unity `The Backend > Edit Settings`에 먹점프 Client App ID와 Signature Key를
   입력한다. 비밀값은 문서·소스·로그에 남기지 않는다.
3. 인증 설정에 iOS Bundle ID `com.CYSB.MukJump`, Apple Team ID `8AU359WZZ2`,
   Android 패키지와 서명 해시를 등록한다.
4. [부분 완료] Google Cloud 프로젝트 `mukjump`에서 Bundle ID `com.CYSB.MukJump`, Team
   ID `8AU359WZZ2`용 iOS OAuth Client를 만들고 Unity iOS Settings에 Client ID와 뒤집은
   URL Scheme을 입력했다. 2026-08-30 생성한 iOS Xcode 프로젝트의 `Info.plist`에서도
   두 값을 확인했다. Android 로그인용 먹점프 전용 Web Client와 뒤끝 인증 정보 등록은
   아직 남았다.
5. Apple Developer에서 먹점프 App ID에 연결된 Sign in with Apple Key를 만들고 한 번만
   내려받을 수 있는 p8을 안전하게 보관한다. 뒤끝 콘솔 라벨은 `Apple 프로젝트의 Key 이름`
   이지만, 뒤끝 공식 `RevokeAppleToken` 안내 이미지가 가리키는 값은 Apple 다운로드 화면의
   **Key ID**다. 먹점프는 Key ID `ZJX8C5MFMR`, p8, Team ID `8AU359WZZ2`를 등록했다.
   Apple 표시명 `MukJump Sign in with Apple`이나 p8 본문은 저장소·Unity 에셋에 넣지 않는다.
6. 뒤끝 인증 정보에 표시되는 Apple 계정 변경 웹훅 URL을 Apple Developer의 먹점프
   App ID > Sign in with Apple 설정에 그대로 등록한다. 자동 탈퇴는 실제 운영 정책을
   확인한 뒤 켜고, `account-delete` 수신 시 최대 1시간 내 탈퇴되는지 테스트한다.
7. Google Web Client ID와 Client Secret을 뒤끝 구글 로그인 인증 정보에 등록한다.
   Android 1.0은 게스트·Google만 제공해 Apple 철회 사각지대를 만들지 않는다.

## 2. 약관 및 개인정보 정책 페이지

뒤끝 공식 안내에 따라 콘솔 `운영 → 약관 및 정책`에서 먹점프 전용 정책 페이지를 만든다.
던던던의 공개 URL이나 본문을 복사하지 않는다.

1. `간편 생성`을 켜고 서비스 이용약관에는 `docs/legal/terms-of-service.md`,
   개인정보처리방침에는 `docs/legal/privacy-policy.md`의 최신 원문을 입력한다.
2. 서비스명은 `먹점프`, 운영자는 `최성빈(CYSBand)`, 고객지원·개인정보 문의는
   `cysbandcs@gmail.com`으로 통일한다.
3. 저장 후 뒤끝이 생성한 이용약관·개인정보처리방침 HTTPS URL을 복사한다. URL은
   문서에서 추측해 만들지 않는다.
4. 두 URL을 비로그인 브라우저에서 열어 먹점프 이름, 시행일, 운영자와 문의처가 보이는지
   확인한다. 생성된 개인정보처리방침 URL을 App Store Connect와 AdMob UMP에 사용한다.
5. 앱에서 정책 동의 화면을 서버 정책으로 운영할 경우 `Backend.BMember.GetPolicyV2()`가
   반환하는 최신 필수 정책을 표시하고 동의 결과를 저장한다. 현재 1.0은 게스트 플레이와
   소셜 연결을 선택형으로 유지하므로, 콘솔 정책을 필수로 바꾸기 전 실제 첫 실행 흐름을
   다시 검증한다.

2026-08-28 생성·비로그인 검증을 마친 먹점프 전용 공개 URL은 다음과 같다.

- 서비스 이용약관: `https://storage.thebackend.io/27f4347cc58b6eca8349b49f00b25a0a9f7c92836f10ec5f6385356867184326/terms.html`
- 개인정보처리방침: `https://storage.thebackend.io/27f4347cc58b6eca8349b49f00b25a0a9f7c92836f10ec5f6385356867184326/privacy.html`

두 URL 모두 인증 정보 없이 HTTPS 200으로 열리고, 먹점프 이름·시행일·운영자·문의처와
Markdown 제목·목록·표가 HTML로 렌더링되는 것을 확인했다. 스토어와 광고 콘솔에는 위
개인정보처리방침 URL을 사용한다.

## 3. 게임정보 테이블

비공개·스키마 미정의 사용자 테이블 `MukJumpPlayer`를 사용한다. 리더보드 생성 시에만
`bestHeight` 정수 스키마를 임시로 정의한 뒤 던던던과 동일하게 스키마 미정의로 되돌렸으며,
런타임의 첫 저장이 다음 필드를 한 행에 만든다.

| 필드 | 형식 | 의미 |
|---|---|---|
| `schemaVersion` | Number | 스냅샷 형식 버전 |
| `bestHeight` | Number | 전체 최고 고도 |
| `growthJson` | String | 검증된 영구 성장 세대 |
| `bgmVolume`, `sfxVolume` | Number | 음량 설정 |
| `tutorialVersion` | Number | 완료 튜토리얼 버전 |
| `revision` | Number | 단조 동기화 세대 |
| `updatedAtUtc` | String | ISO 8601 저장 시각 |
| `lastOperationId` | String | 중복 저장 식별자 |

클라이언트 쓰기는 본인 행만 허용하고 테이블은 공개 조회하지 않는다.
런타임은 저장 직전에 본인 행을 다시 읽어 `inDate`와 `revision`을 확인한다. 서버 세대가
달라졌으면 전체 `growthJson`을 자동 병합하거나 덮어쓰지 않고 기록 선택 UI를 표시하며,
행이 0개 또는 2개 이상이면 저장을 중단한다. 콘솔에서도 사용자별 행이 정확히 1개인지
확인한다.

실제 갱신 요청의 `Where`에는 기본 키 `inDate`를 넣지 않는다. 뒤끝 서버는 이를
`400 ValidationException`으로 거절한다. 사전 조회에서 행 식별자를 검증한 뒤,
비기본 키인 `revision`과 직전 `lastOperationId`를 조건으로 갱신한다.
요청 ID가 없는 구형 행은 `revision` 조건을 사용한다.

응답 유실 뒤 서버의 `lastOperationId`가 기기에 남긴 요청 ID와 일치하고 `revision`이
정확히 한 단계 증가했으면 본인의 직전 저장으로 인식한다. 최신 로컬 변경은 유지하고
다음 저장은 새 요청 ID로 보낸다. 다른 요청 ID/추가 세대 변화에는 기록 선택을 유지한다.
일반 저장은 계정 제공자 확인·전환·로그아웃·탈퇴 정리 중에는 진행하지 않는다.

콘솔의 사용자 삭제와 TestFlight 업데이트는 기기 로컬 저장 삭제가 아니다.
오프라인 상태의 이전 Apple/Google 계정을 다시 인증할 때도 기기 기록을 먼저 백업한다.
계정 전환은 서버 응답 전에 점수·성장을 비우지 않는다. 새 계정의 서버 행이 없으면
백업 기록과 완료 판 이력을 복원해 최초 저장하고, 이미 행이 있으면 기기 완료 판 기록과
자동으로 교체하지 않고 기록 선택을 요청한다. 인증 전에는 이전 UID·닉네임을 현재
연동 계정처럼 표시하지 않는다. 앱 안에서 명시적으로 계정 삭제한 경우의 삭제 정책은 유지한다.

## 4. 리더보드

1. `MukJumpPlayer.bestHeight` 기준 내림차순 사용자 리더보드 1개를 만든다.
2. 초기 버전은 전체 기간 최고 고도만 사용하고 주간 보상·친구·채팅은 만들지 않는다.
3. 생성된 UUID를 `MukJumpBackendSettings.allTimeRankUuid`에 입력한다.

운영 UUID는 `01a08afd-b237-7723-9e6a-b8d7950285fd`로 고정한다. 삭제한 구 보드를
재생성하거나 예전 UUID로 되돌리지 않는다. 행의 출처나 OS를 로그인 수단으로 추측하지 않는다.
현재 FREE 요금제는 한도 소진 시 접속이 중단될 수 있으므로 출시 전 용량/요금 확인이 필요하다.
요금제는 변경하지 않았다.

게임정보 행이 있다고 순위 등록이 완료된 것은 아니다. 서버 행 저장/복구를 확인하면
소유자별 최고 기록 제출 예약을 먼저 보존하고 리더보드 API 성공까지 재시도한다.
등록 실패 HTTP 코드/대기 상태를 빈 순위 목록 안내로 덮어쓰지 않는다. 순위 쓰기와
동일 행의 클라우드 조회도 직렬화하며 대기 중인 조회는 소유자와 함께 보존한다.
순위창의 작은 상태 문구는 표시하지 않는다. 내부 실패 상태·재시도 및 로그는 유지한다.

### 플랫폼 리더보드 연결 범위

- 메인 최고 기록 → 공통 한지 두루마리. 뒤끝은 전체 기간 TOP 10의 닉네임·순위·고도를 표시한다.
- iOS는 Game Center 별도 탭에서 `GKLeaderboard`의 global/allTime 목록을 조회한다.
  `MukJumpGameCenter.mm`는 네이티브 GameKit을 사용한다. Unity의 deprecated Social API는 쓰지 않는다.
- 2026-09-11 App Store Connect에서 `com.CYSB.MukJump.bestHeight`를 생성하고
  `MukJumpBackendSettings.appleGameCenterLeaderboardId`에 연결했다. Classic, 정수 미터,
  최고값 우선·내림차순, 범위 1–2147483647이다. 한국어·영어(미국)·일본어 이름과 `m` 접미사를 등록했다.
  앱 버전 1.0.0의 Game Center 체크는 켜졌으며 구성요소 상태는 **제출 준비 중**이다.
  Team `8AU359WZZ2`, Bundle ID `com.CYSB.MukJump`. 심사 제출/새 TestFlight 업로드는 이번 연결 작업에 포함하지 않았다.
- 첫 튜토리얼·계정 팝업을 마친 로비에서 실행당 한 번 연결을 시도한다. 취소해도 게임이나 뒤끝 저장을 막지 않는다.
  `먹점프`/`Game Center` 탭은 서로 다른 계정·순위 목록이다. GameKit 플레이어에게 기기 국가를 임의로 붙이지 않는다.
- Apple에 제출하는 값은 계정이 확인된 상태에서 시작한 정상 플레이의 `Height`다.
  뒤끝/로컬에서 합쳐진 `Best`를 다른 Game Center 계정으로 올리지 않는다.
  실패한 점수는 Game Center 사용자·보드별 최고값으로 기기에 보관하고 다음 조회/정산에서 재시도한다.
- Toss SDK는 현재 사용자 프로필·점수 제출·공식 리더보드 열기를 제공한다. 다른 플레이어 행 조회는
  제공되지 않아 두루마리 버튼으로 공식 화면을 연다. 토스→뒤끝 서버 브리지와 동일인 매핑이 없으므로
  **토스까지 포함한 단일 전체 순위는 아직 구현되지 않았다**. 네이티브의 뒤끝 TOP 10을 그렇게 부르면 안 된다.

연결 변경 검증: 집중 검사 17개, iOS Player 스크립트 36개 어셈블리 컴파일, ObjC++ 구문 확인.
임시 Xcode 프로젝트에서 Game Center/Apple 로그인 entitlement 공존 및 중복 후처리를 확인했다.
실기기 로그인·계정 변경·오프라인 재시도·리더보드 점수 왕복·UI 클릭 테스트는 완료 전까지 출시 게이트다.
네이티브 Game Center의 소유자별 재시도는 앱 내부 `NSUserDefaults`를 사용하므로 최종 앱의
PrivacyInfo.xcprivacy에 해당 required-reason API 사용 사유가 반영됐는지도 Archive에서 확인한다.
공식 근거: [뒤끝 목록 API](https://docs.backnd.com/en/sdk-docs/backend/base/leaderboard/user/get-list/),
[GameKit](https://developer.apple.com/documentation/gamekit/gkleaderboard).

## 5. Unity 공개 설정

`Assets/Resources/MukJump/Settings/MukJumpBackendSettings.asset`에 다음만 입력한다.

- `productionConfigurationVerified: 1`
- `playerTableName: MukJumpPlayer`
- `bestHeightColumn: bestHeight`
- 먹점프 리더보드 UUID
- Android Google Web Client ID
- `TheBackendGoogleSettingsForAndroid`의 Web Client ID와 위 값은 동일해야 함
- `TheBackendGoogleSettingsForIOS`의 먹점프 전용 iOS Client ID와 URL Scheme
- Android Apple Service ID는 이후 Android Apple 로그인과 철회를 함께 제공할 때만 입력
- Apple 토큰 철회용 Team ID·Key ID·p8 입력과 실제 철회 성공을 실기기에서 확인한 뒤
  `appleRevocationConfigurationVerified: 1`
- Apple 계정 변경 웹훅 등록을 확인한 뒤 `appleAccountChangeWebhookVerified: 1`
- Android 디버그·출시 서명 해시를 뒤끝에 모두 입력한 뒤
  `androidSigningHashesVerified: 1`

마지막으로 `MukJump > Store > Backend > Validate Release Setup`을 실행한다. 이 검사가
통과하기 전에는 Release Archive를 만들지 않는다.
