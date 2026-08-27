# 먹점프 뒤끝 출시 콘솔 설정

이 문서는 **iOS·Android 네이티브 빌드**에만 적용한다. Apps in Toss WebGL에서는 뒤끝
네이티브 SDK를 포함하지 않고 토스 앱 세션과 공식 게임센터를 사용한다.

SHIFT나 던던던과 같은 개발자 계정은 사용할 수 있지만, 두 게임의 앱 ID·서명키·OAuth
Client·리더보드 UUID를 복사하지 않는다. 운영 AdMob 게시자 계정은 사용자가 확인한
`cysbandcs@gmail.com`이고, Google Cloud OAuth 프로젝트를 관리하는 계정은 이 계정과
같을 필요가 없다. 현재 로그인된 다른 Google 계정의 프로젝트를 확인해 번들 ID가
`com.CYSB.MukJump`인 먹점프 전용 OAuth 값을 만든 뒤 Unity 설정 에셋에는 공개 식별자만
저장한다.

## 1. 뒤끝 앱

2026-08-27 기준 먹점프 전용 앱 인증값, 플랫폼 식별자, 아래 게임정보 테이블과 리더보드는
콘솔과 Unity에 반영됐다. 남은 항목은 Google OAuth, Android 서명 해시, Apple 토큰 철회용
Key 이름·p8와 Apple 계정 변경 웹훅이다.

1. 뒤끝 콘솔에서 먹점프 앱을 생성한다.
2. Unity `The Backend > Edit Settings`에 먹점프 Client App ID와 Signature Key를
   입력한다. 비밀값은 문서·소스·로그에 남기지 않는다.
3. 인증 설정에 iOS Bundle ID `com.CYSB.MukJump`, Apple Team ID `8AU359WZZ2`,
   Android 패키지와 서명 해시를 등록한다.
4. Google Cloud에서 Android 로그인용 Web Client와 Bundle ID `com.CYSB.MukJump`용
   iOS OAuth Client를 만든다. Unity의 `The Backend > ToolKit > GoogleLogin`에서
   Android Settings에는 Web Client ID, iOS Settings에는 iOS Client ID와 뒤집은 URL
   Scheme을 입력한다. iOS Client가
   `123-example.apps.googleusercontent.com`이면 URL Scheme은
   `com.googleusercontent.apps.123-example`이다.
5. Apple Developer에서 먹점프 App ID에 연결된 Sign in with Apple Key를 만들고 한 번만
   내려받을 수 있는 p8을 안전하게 보관한다. Key 이름과 p8, Team ID를 뒤끝 콘솔의
   Apple 토큰 철회 항목에 등록한다. 이 키는 저장소나 Unity 에셋에 넣지 않는다.
6. 뒤끝 인증 정보에 표시되는 Apple 계정 변경 웹훅 URL을 Apple Developer의 먹점프
   App ID > Sign in with Apple 설정에 그대로 등록한다. 자동 탈퇴는 실제 운영 정책을
   확인한 뒤 켜고, `account-delete` 수신 시 최대 1시간 내 탈퇴되는지 테스트한다.
7. Google Web Client ID와 Client Secret을 뒤끝 구글 로그인 인증 정보에 등록한다.
   Android 1.0은 게스트·Google만 제공해 Apple 철회 사각지대를 만들지 않는다.

## 2. 게임정보 테이블

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

## 3. 리더보드

1. `MukJumpPlayer.bestHeight` 기준 내림차순 사용자 리더보드 1개를 만든다.
2. 초기 버전은 전체 기간 최고 고도만 사용하고 주간 보상·친구·채팅은 만들지 않는다.
3. 생성된 UUID를 `MukJumpBackendSettings.allTimeRankUuid`에 입력한다.

## 4. Unity 공개 설정

`Assets/Resources/MukJump/Settings/MukJumpBackendSettings.asset`에 다음만 입력한다.

- `productionConfigurationVerified: 1`
- `playerTableName: MukJumpPlayer`
- `bestHeightColumn: bestHeight`
- 먹점프 리더보드 UUID
- Android Google Web Client ID
- `TheBackendGoogleSettingsForAndroid`의 Web Client ID와 위 값은 동일해야 함
- `TheBackendGoogleSettingsForIOS`의 먹점프 전용 iOS Client ID와 URL Scheme
- Android Apple Service ID는 이후 Android Apple 로그인과 철회를 함께 제공할 때만 입력
- Apple 토큰 철회용 Team ID·Key 이름·p8 입력을 실기기에서 확인한 뒤
  `appleRevocationConfigurationVerified: 1`
- Apple 계정 변경 웹훅 등록을 확인한 뒤 `appleAccountChangeWebhookVerified: 1`
- Android 디버그·출시 서명 해시를 뒤끝에 모두 입력한 뒤
  `androidSigningHashesVerified: 1`

마지막으로 `MukJump > Store > Backend > Validate Release Setup`을 실행한다. 이 검사가
통과하기 전에는 Release Archive를 만들지 않는다.
