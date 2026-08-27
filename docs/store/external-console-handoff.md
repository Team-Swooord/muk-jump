# 먹점프 외부 콘솔 인계표

이 문서는 로컬에서 대신할 수 없는 콘솔·서명·실기기 작업의 현재 인계표다. `[완료]`로
표시되지 않은 항목은 실제 콘솔이나 산출물로 확인해야 한다. 아래 값은 저장소에 임의로
만들거나 다른 게임의 값을 복사하지 않고 각 콘솔에서 **먹점프 전용**으로 발급한다.
비밀번호, p8, 서명키, keystore는 저장소에 커밋하지 않는다.

## 1. Android 모듈과 서명

1. [완료] Unity Hub가 등록한 `/Applications/Unity/Hub/Editor/6000.5.9f1`에 Android
   Build Support, SDK/NDK Tools, OpenJDK를 설치했다. 대상 API 36, ARM64 로컬 빌드에
   필요한 도구가 해당 Unity 설치 안에 있다.
2. [완료] `MukJump > Release > Build Android Local Validation APK`로
   `output/android/MukJumpValidation.apk`를 실제 빌드했다. 패키지 ID
   `com.CYSB.MukJump`, minSdk 26, targetSdk 36, ARM64, Google 공식 테스트 광고 App ID,
   AD_ID 권한, GMA·UMP·Google 로그인·뒤끝 네이티브 포함을 확인했다. 이 APK는 Play
   Console에 제출하지 않는다.
3. 먹점프 전용 업로드 keystore를 안전한 로컬 위치에 만들고 아래 환경 변수로만 전달한다.
   - `MUKJUMP_ANDROID_KEYSTORE_PATH`
   - `MUKJUMP_ANDROID_KEYSTORE_PASS`
   - `MUKJUMP_ANDROID_KEY_ALIAS`
   - `MUKJUMP_ANDROID_KEY_ALIAS_PASS`
4. Unity에서 `MukJump > Release > Validate Google Play Readiness`를 통과한 뒤
   `Build Android App Bundle`로 `output/android/MukJump.aab`을 만든다.

## 2. Google 로그인 OAuth

1. AdMob 게시자 계정과 Google Cloud OAuth 프로젝트는 별개다. 운영 AdMob은 사용자가
   확인한 `cysbandcs@gmail.com` 계정을 사용하지만, OAuth 콘솔은 현재 다른 창에 로그인된
   Google 계정에서 확인한다. SHIFT OAuth를 재사용하지 말고, 먹점프 전용 Cloud 프로젝트를
   만들거나 던던던 프로젝트가 실제로 보이는 올바른 Google 계정에서 Android 로그인용
   먹점프 전용 Web Client와
   Bundle ID `com.CYSB.MukJump`용 iOS OAuth Client를 만든다.
2. Unity `The Backend > ToolKit > GoogleLogin > Android Settings`의 `Web Client ID`와
   `MukJumpBackendSettings.androidGoogleWebClientId`에 같은 Web Client ID를 입력한다.
3. Unity `The Backend > ToolKit > GoogleLogin > iOS Settings`에 iOS Client ID와 뒤집은
   URL Scheme을 입력한다. 예를 들어 Client ID가
   `123-example.apps.googleusercontent.com`이면 URL Scheme은
   `com.googleusercontent.apps.123-example`이다.
4. 최종 iOS `Info.plist`에 `GIDClientID`와 위 URL Scheme이 생성됐는지 확인한다.

## 3. 뒤끝

1. [완료] 먹점프 앱의 Client App ID와 Signature Key를 Unity
   `The Backend > Edit Settings`에 연결했다.
2. [완료] 비공개·스키마 미정의 `MukJumpPlayer` 테이블과 `bestHeight` 정수 컬럼 기준
   내림차순·초기화 없음·보상 없음 리더보드를 만들었다.
3. [완료] 리더보드 UUID를 `MukJumpBackendSettings`에 넣고
   `productionConfigurationVerified`를 켰다.
4. [부분 완료] Android 패키지 `com.CYSB.MukJump`, iOS Bundle ID
   `com.CYSB.MukJump`, Apple 로그인 Service ID `com.CYSB.MukJump.applelogin`, Team ID
   `8AU359WZZ2`를 등록했다. Google OAuth, Android 서명 해시, Apple 토큰 철회용 Key
   이름·p8, Apple 계정 변경 웹훅은 아직 남았다.
5. Apple Developer에서 먹점프 App ID에 연결된 Sign in with Apple Key를 발급하고 p8을
   안전하게 보관한다. 뒤끝에 Team ID·Key 이름·p8을 넣은 뒤 실제 Apple 계정 탈퇴로
   `RevokeAppleToken` 성공을 확인한다.
6. 뒤끝 인증 정보 화면의 Apple 계정 변경 웹훅 URL을 Apple Developer의 먹점프 App ID에
   등록한다. URL은 문서에서 추측하지 말고 콘솔에 표시되는 값을 그대로 사용한다.
7. 위 외부 확인 뒤 `MukJumpBackendSettings`의 대응 확인 플래그를 켠다. OAuth 입력 후
   `MukJump > Store > Backend > Validate Release Setup`을 다시 통과시킨다.
8. 뒤끝 콘솔의 공식 회원 탈퇴 웹 링크를 생성하고, 비로그인 브라우저에서 먹점프 계정
   삭제 요청이 동작하는지 확인한 뒤 계정 삭제 안내와 Play Console 외부 삭제 URL에
   연결한다.

## 4. 광고와 개인정보

1. 던던던과 동일한 `cysbandcs@gmail.com` AdMob 게시자 계정을 사용한다. 먹점프는 이
   계정 안에서 iOS·Android 앱 ID와 로비 배너·게임오버 보상형 광고 단위를 별도로
   만들었으며, 던던던 광고 단위를 재사용하지 않는다.
2. 2026-08-27 해당 계정을 확인한 결과 유럽 규정 메시지는 아직
   없고 `새 메시지 만들기` 상태다. 공개 개인정보처리방침 URL을 준비한 뒤 UMP 메시지를
   작성·게시한다. 게시 버튼은 계정에 즉시 반영되므로 최종 확인 후 실행한다.
3. App Store·Google Play 출시 뒤 AdMob의 먹점프 iOS·Android 앱을 각 스토어 목록에
   연결한다.
4. `docs/legal/privacy-policy.md`와 `docs/legal/account-deletion.md`를 공개 HTTPS 주소로
   배포하고 App Store Connect·Play Console에 입력한다.
   - 2026-08-27 확인 기준 저장소 루트와 고객지원 URL은 공개되지만, 두 문서의 현재
     GitHub URL은 아직 404다. 파일을 원격 저장소에 반영하거나 별도 정적 페이지로
     배포하기 전에는 스토어에 입력하지 않는다.
5. App Store 개인정보와 Play 데이터 보안 답변 초안은
   `docs/store/privacy-disclosures.md`에 있다. iOS는 ATT를 요청하고 포함된 Google Mobile
   Ads 개인정보 매니페스트가 기기 ID 추적을 선언하므로 App Store의 기기 ID 추적은
   `예`로 답한다. 최종 Archive Privacy Report를 다시 대조하고, 법적 운영자·국외 이전·
   서비스 제공자 역할도 공개본과 일치하는지 확인한 뒤 입력한다.
6. 뒤끝 `Send Log Report`와 `Auto Load Location Properties`는 출시 설정에서 꺼 두었고,
   Unity 출시 검증기가 둘 중 하나가 다시 켜지면 실패한다.

## 5. 스토어와 Apps in Toss

1. [완료] 최신 `output/ios/MukJumpValidation`을 Unity에서 재생성하고 Xcode Release
   서명 없는 컴파일까지 통과했다. 번들 ID·버전·실제 iOS AdMob 앱 ID·ATT 안내 문구·
   암호화 면제 선언·Apple 로그인 entitlement를 확인했다. 이 로컬 검증본은 제출하지
   않으며, 동일 검사를 통과한 배포용 `Unity-iPhone.xcworkspace`만 Archive·TestFlight에
   사용한다.
2. Google Play는 내부 테스트에 AAB을 먼저 올린다.
3. 게스트·Google·Apple(iOS), 클라우드 병합, TOP 10, 계정 삭제, 배너와 판당 1회 부활,
   백그라운드 복귀를 실제 기기에서 확인한다.
4. Apps in Toss에는 공개 HTTPS 아이콘 URL과 토스 배너·보상형 광고 그룹 ID를 넣고 최종
   `.ait`을 만들어 QR 실기기 검증을 진행한다. 토스 인앱 광고는 사업자 등록이 끝난
   워크스페이스에서만 운영한다. 비사업자 상태라면 게임센터 리더보드만 연결하고 광고는
   운영 ID 없이 둔다.
5. 게임 제출에는 출시된 App Store 또는 Google Play URL과 자체등급분류 조회 정보·원본
   플레이 화면, 또는 게임물관리위원회 등급분류증명서 PDF를 준비한다. 단순히 모바일
   게임이라는 이유로 증빙이 자동 면제되지는 않는다.

## 완료 판정

- iOS: `Validate App Store Readiness` 통과, Archive 성공, TestFlight 실기기 검증 완료
- Android: `Validate Google Play Readiness` 통과, ARM64/API 36 AAB 내부 테스트 완료
- Apps in Toss: 최종 `.ait` 100MB 이하, QR에서 Safe Area·광고·게임센터 검증 완료
