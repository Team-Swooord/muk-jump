# 먹점프 1.0 운영 기준

이 문서는 출시 뒤 운영 질문에 대한 현재 답이다. 새로운 SDK나 데이터 수집을 추가할 때는
개인정보처리방침과 스토어 데이터 공개 응답을 먼저 함께 갱신한다.

## 2026-09-23 생성 산출물 정리

- 사용자 지시에 따라 재생성 가능한 `output/ios/DerivedData-AppStore-42`, `output/ios/MukJump`와
  `Export-AppStore-29/38/40/41`, `Export-SkinDebug-39` 안의 이전 `ProductName.ipa`만 macOS 휴지통으로 옮겼다.
  내보내기 로그·설정은 유지했다. 최신 `Export-AppStore-42/ProductName.ipa`, Xcode Archives의 기존
  `.xcarchive`와 최종 dSYM, 서명자료는 보존했다. 다음 iOS 빌드는 Unity 내보내기부터 다시 생성한다.
- DerivedData의 Archive 중간 dSYM은 최종 Archive와 UUID가 일치한다. 별도 Release 검증 빌드의
  서로 다른 dSYM 두 개는 `output/release-appstore/2026-09-21-build-42/release-dsyms/`에 보존했다.
  UnityFramework UUID는 `9B7D69F6-216F-3855-8173-BD9615011D42`, 앱 UUID는
  `570F9108-3C5F-380E-B119-BFAA80FBADCD`다.
- `output/qa/pipeline-captures` PNG 중 이름 붙여 보관한 `skin-skills`, `night-secret` 이미지와
  SHA-256이 같은 중복 43개(24,198,301 bytes)만 휴지통으로 옮겼다. Git 추적 문서·소스에서
  개별 파일명을 참조한 파일은 제외했다. 폐기 이미지 없이 남은 `output/qa/ink-skins/rejected-art`
  메타 파일 3개도 정리했다. 나머지 검증 원본·로그·제출 이미지·소스는 그대로다.
- 프로젝트 안 점유량은 약 8.88 GiB(9.53 GB) 줄었다. 휴지통은 비우지 않았으므로 복구할 수 있으며,
  동일 디스크의 실제 여유 공간이 그만큼 늘었다는 의미는 아니다. `.git` 이력과 Unity Library는 삭제하지 않았다.
- 최신 토스 `ait-build/mukjump.ait`는 정리 전후 SHA-256
  `f1aeb227f6d2b1611458e98b3bdb33231aa5fd98eabb2417b8e66e166545264f`가 같고,
  `output/qa/toss-exposure` 검증 자료를 보존했다. 정리만을 이유로 새 빌드·업로드를 반복하지 않았다.

## 지표와 개인정보

- 1.0은 계정에 연결되는 자체 행동 분석 SDK를 넣지 않는다.
- 광고 수익·노출·완료율은 AdMob의 집계 보고서만 사용한다. 광고 부활 선택률을 사용자 ID와
  결합해 별도로 저장하지 않는다.
- 밸런스는 버전별 익명 플레이테스트 시트로 확인한다. 첫 30m 도달, 100m·250m·1,000m
  도달, 판 길이, 성장 갈래와 부활 선택만 기록하며 이메일·뒤끝 ID·광고 ID는 적지 않는다.

## 장애와 공지

- 뒤끝·로그인 장애가 있어도 로컬 게스트 플레이를 먼저 연다. 저장 재시도는
  5→10→20→40→60초로 늘리고 60초를 넘기지 않는다.
- 저장 직전에 서버 `revision`을 다시 확인한다. 다른 기기 기록이 바뀌었으면 자동 병합을
  금지하고 서버 기록 또는 이 기기 기록을 선택하게 한다. 충돌 전 로컬 기록은 기기 복구본에
  남긴다.
- 광고가 없거나 실패하면 무료 부활을 지급하지 않고 `로비로` 경로를 계속 제공한다.
- 장기 장애·점검 공지는 GitHub README의 고객지원 섹션과 각 스토어 업데이트 설명에 게시한다.
  게임을 막는 강제 원격 공지 기능은 1.0에 넣지 않는다.

## 리더보드와 치팅

- 보상이 없는 전체 최고 고도를 운영하며 순위표에 공개 닉네임을 표시한다. iOS·Android는 뒤끝
  TOP 10 하나만 사용하고 Apps in Toss는 공식 게임센터 화면을 사용한다.
- Apple Game Center 탭·자동 인증·점수 전송·네이티브 브리지·빌드 권한은 제거했다.
  Sign in with Apple 계정 연동과 뒤끝 저장/순위 동기화는 유지한다.
  1.0.0 (27)은 Game Center를 포함하며, 제거 변경은 정식 심사용 1.0.0 (28)부터 적용된다.
- 완료된 정상 판만 제출하며 디버그·실패 정산 기록은 제출하지 않는다.
- 비정상 기록은 뒤끝 리더보드 콘솔에서 해당 항목을 숨기거나 삭제한다. 대회·현금성 보상을
  연결하기 전에는 서버 권위 검증과 신고 절차를 별도 출시 조건으로 추가한다.
- 리더보드 초기화가 필요하면 먼저 현재 목록을 내보내 보관하고, 스토어 공지 후 새 랭킹 UUID로
  교체한다. 클라이언트에 임의 초기화 권한을 두지 않는다.

## Apps in Toss 패키징

- `MukJump > Release > Build Apps in Toss Size Probe`는 Production 프로필로 WebGL을 새로
  만들고 100MB 제한을 검사한 뒤 원래 iOS 빌드 타깃으로 돌아온다.
- 2026-08-27 측정한 압축 해제 WebGL 사전 용량은 **32.57MB/100MB**다. 최종 `.ait`
  생성 때도 같은 검사를 다시 통과해야 한다.
- `MukJump > Release > Build Apps in Toss Package`는 공개 HTTPS 아이콘 URL, 토스 배너 광고
  그룹 ID, 토스 보상형 광고 그룹 ID가 없으면 패키징을 중단한다.
- 뒤끝 네이티브 DLL 15개와 `TheBackend/Toolkit` assembly는 WebGL에서 제외한다. Apps in
  Toss 리더보드는 공식 게임센터 API만 사용한다.
- `.ait` 생성 후 콘솔 업로드와 QR 실기기 테스트에서 세로 Safe Area, 로비 배너, 판당 1회
  광고 부활, 백그라운드 오디오 정지를 확인한다.

## iOS 빌드와 Xcode 인계

### 정식 심사 전환 — 1.0.0 (28)

- 2026-09-11 사용자 요청에 따라 정식 App Store 빌드로 전환한다. 테스트 광고와 빌드별
  로컬 초기화 심볼을 넣지 않고, 내부 TestFlight 전용 제한 없이 내보낸다.
- 심사 준비를 위한 마지막 서버 정리는 별도 콘솔 작업으로 수행한다. 이후 배포에서는
  전원 삭제를 반복하지 않으며 정상적인 사용자 기록 보존 정책을 적용한다.
- 서버 삭제는 휴대폰에 남은 저장·OS 권한·Apple ID 자체 삭제를 의미하지 않는다.

### 정식 출시 전 TestFlight 전원 초기화 — 필수 배포 조건

2026-09-11 사용자 지시: 출시 전 새 TestFlight 빌드마다 로컬 게스트·서버 게스트·Apple
연동 계정을 예외 없이 새 사용자 상태로 시작한다. 아래는 배포 요구 사항이며,
문서에 기록한 것만으로 런타임 초기화가 구현되었거나 서버 삭제가 완료된 것으로 간주하지 않는다.

1. 먹점프 뒤끝 프로젝트와 전체 대상 계정 수를 먼저 확인한다. 동기화·순위 검증을 마친 뒤
   콘솔에서 전체 게임 계정·게임 정보·리더보드를 영구 삭제하고 잔존 데이터를 다시 확인한다.
   삭제를 앱이나 일반 빌드 스크립트에 자동 삽입하지 않는다. Apple ID 자체·개발자 계정과
   SHIFT 등 다른 프로젝트는 대상이 아니다.
2. 빌드 번호별 1회 로컬 초기화를 출시 전 TestFlight에만 적용한다. 인증·저장 복원 전에
   먹점프의 모든 계정 프로필, UID/닉네임 캐시, 게임 인증 정보, 최고 기록, 성장·재화,
   튜토리얼 완료, 계정별 백업, 대기 중인 동기화 기록을 지운다. OS 권한은 건드리지 않는다.
   휴대폰의 저장은 빌드 PC에서 지울 수 없으므로 새 빌드의 첫 실행 처리가 필요하다.
3. 이전 기록이 있는 게스트/Apple 연동 상태 각각에서 업데이트·오프라인 첫 실행·재접속을
   검사한다. 이전 기록이 복구/재업로드되지 않고 Splash → 새 게스트 첫 튜토리얼로 진입해야
   한다. 서버 연결 후 새 UID 배정을 확인하되 오프라인에서 UID를 임의 생성하거나 튜토리얼을
   막지 않는다. 같은 빌드 재실행은 초기화를 반복하지 않으며 새로 얻은 기록을 유지해야 한다.
4. 위 구현·검증과 서버 삭제 확인이 끝난 뒤에만 업로드한다. 빌드 번호, 서버 삭제 전후 수,
   로컬 초기화 검증 범위와 복구 불가를 보고하며 확인하지 못한 기기를 검증 완료로 적지 않는다.
5. 정식 출시용 빌드에는 초기화 동작이 포함되지 않는지 검증한다. 정식 출시 전환 후에는
   이 전원 삭제 정책을 중단하고 정상적인 기록 보존·동기화 정책을 유지한다.

### 출시 전 초기화 구현

- `PrereleasePlayerReset`은 출시 전 iOS TestFlight의 1회용 빌드 심볼과 네이티브
  `MukJumpPrereleaseReset` Boolean 플래그가 모두 있는 경우에만 작동한다. 정식 App Store,
  Android, 에디터에는 초기화 호출이 포함되지 않으며 전역 초기화 심볼은 빌드 검증에서 거부한다.
- 인증 부트스트랩보다 먼저 PlayerPrefs의 모든 계정 저장과 뒤끝 `backend.dat`, 네이티브
  게임 대기 기록을 제거한다. Apple ID·OS 권한·다른 앱 저장은 건드리지 않는다.
- 삭제와 디스크 완료 표식 저장이 모두 성공해야 메인 씬/게임 인증으로 진행한다.
  실패 시 스플래시 재시도를 제공하며 완료 표식은 빌드 번호별로 유지하여 같은 빌드의
  새 기록을 반복 삭제하지 않는다.
- 자동 검증은 `prerelease-reset` 범위로 실행한다. 2026-09-11 에디터 검증은
  초기화·계정 전환·튜토리얼·로고·iOS 설정 회귀 **644개 통과, 실패 0개**다.
  이는 실물 iPhone 업데이트/오프라인 실행 결과를 대신하지 않으며 네이티브 빌드 및
  해당 기기 검증 범위는 배포별 결과에 별도로 남긴다.

### 빌드·서명

- 뒤끝 운영값까지 입력한 배포 산출물은 `MukJump > Release > Build iOS Xcode Project`로
  만든다. 아직 외부 콘솔값이 없다면 `Build iOS Local Validation Project`로 네이티브 코드와
  CocoaPods 컴파일만 먼저 확인한다. 로컬 검증 산출물은 App Store에 업로드하지 않는다.
- Xcode에서는 반드시 `Unity-iPhone.xcworkspace`를 연다. `.xcodeproj`를 직접 열면
  `GoogleSignIn/GoogleSignIn.h` 같은 Pod 헤더를 찾지 못한다.
- Unity 빌드 직후 `Podfile.lock`의 Google Mobile Ads, Google Sign-In, UMP와 워크스페이스
  존재를 자동 검사한다. 하나라도 없으면 Xcode 인계 전에 빌드를 실패시킨다.
- iOS는 Google Mobile Ads 초기화 전에 ATT 권한을 요청한다. 응답 뒤 UMP 동의 절차를
  진행하고 `CanRequestAds()`가 허용할 때만 광고를 초기화한다. 빌드 후처리는 ATT 안내
  문구와 `ITSAppUsesNonExemptEncryption=NO`를 선언한다. 암호화 SDK 구성이 바뀌면 App
  Store Connect의 수출 규정 질문을 다시 검토한다.
- 자동 서명 없이 컴파일만 확인할 때는 생성 폴더에서 아래 명령을 사용한다.

```sh
xcodebuild -workspace Unity-iPhone.xcworkspace -scheme Unity-iPhone \
  -configuration Release -sdk iphoneos \
  -derivedDataPath /tmp/mukjump-xcode-derived \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
```

- 최종 제출은 배포용 워크스페이스에서 Seongbin Choi의 CYSBand Team `8AU359WZZ2`를
  확인한다. 같은 이름의 Nvibe Corporation Team `4QY9W8JDW6` 인증서·프로파일은 사용하지
  않고, 8AU 팀의 유효한 Apple Development·Apple Distribution 자산으로 Archive, TestFlight
  설치, Apple·Google·게스트 로그인, 배너·보상형 광고, 계정 삭제를 실기기에서 통과해야 한다.

## Android 빌드와 서명

- Google Play 빌드는 ARM64 App Bundle만 사용하고 최소 API 26, 대상 API 36으로 고정한다.
  2026-08-31부터 새 앱과 업데이트에 API 36 이상이 필요한
  [공식 정책](https://developer.android.com/google/play/requirements/target-sdk)을 기준으로 한다.
- keystore와 비밀번호는 저장소나 `ProjectSettings`에 고정하지 않는다. 빌드 직전에
  `MUKJUMP_ANDROID_KEYSTORE_PATH`, `MUKJUMP_ANDROID_KEYSTORE_PASS`,
  `MUKJUMP_ANDROID_KEY_ALIAS`, `MUKJUMP_ANDROID_KEY_ALIAS_PASS` 네 환경 변수로만 전달한다.
- `Validate Google Play Readiness`는 Android 모듈, 씬·아이콘·법적 문서, API 36, ARM64,
  뒤끝 운영 설정, keystore 파일과 네 서명값을 검사한다.
- `Build Android App Bundle`은 `output/android/MukJump.aab`을 만들고 빌드가 끝나면
  keystore 설정과 활성 빌드 타깃을 이전 상태로 복원한다.
- `Build Android Local Validation APK`는 운영 콘솔값이 없어도 공식 테스트 광고로
  설치·입력·Safe Area를 확인하는 개발용 APK다. Play Console에는 업로드하지 않는다.
- 최종 AAB는 Play Console 내부 테스트에서 게스트·Google 로그인, 기기 간 기록 충돌 선택,
  TOP 10, 배너·부활 광고, 탈퇴, 백그라운드 복귀를 실기기로 확인한다.

## 버전과 롤백

- 2026-09-21 해금 곡선 조정 정식 업데이트 **1.0.2(42)**를 `main`에서 클린 빌드했다.
  일반 첫 플레이의 대량 해금을 줄이고 누적·최고 기록 보상을 단계화했다.
  용꼬리는 용에게 당해 종료된 플레이 3회, 십이지·신화 의상은 시작 의상과
  한 판 도전 조건으로 변경했다. 기존 보유 의상·계정·기록은 유지하며
  서버 삭제·빌드별 로컬 초기화는 적용하지 않았다.
  해금·이전 저장 보존·차트 호환·계정·리더보드·출시 관련 자동 검사
  **1,022개 통과, 실패/건너뜀 0개** 및 씬 감사를 확인했다.
  스킨 관리 엑셀과 JSON도 동일 목표로 동기화했다. 구버전 클라이언트 호환을 위해
  뒤끝 운영 차트에는 새 파일을 게시하지 않았으며, 빌드 42는 구형 기본 목표를 자동 보정한다.
  이전 생성 Xcode 출력·빌드 41 DerivedData와 중간 출력은
  `/Users/seungyeoning/.Trash/mukjump-clean-build-42.y1ttCq/`로 옮겼으며 복구 가능하다.
  소스·기존 Archive·검증 로그는 보존했다.
  CocoaPods 설치가 끝난 `.xcworkspace`에서 실기기 Release 빌드·Archive·배포를 수행했다.
  Archive와 배포 IPA의 버전, Bundle ID `com.CYSB.MukJump`, 팀 `8AU359WZZ2`,
  Apple 로그인 `Default`·Game Center entitlement, 암호화 Boolean false,
  엄격한 전체 코드 서명을 검증했다. 배포 IPA는 `get-task-allow=false`,
  `MukJumpPrereleaseReset=false`, 실제 광고·디버그 없음 조건이다. Meta SDK는 사용하지 않는다.
  **23:12:42 KST `Upload succeeded` / `Uploaded package is processing`**를 확인했고
  TestFlight에서 빌드 42 처리 완료 및 기존 내부 그룹 `11` 연결을 확인했다.
  빌드 ID: `d9aeca28-954a-48ca-afbf-abd2fc167cf0`.
  Archive: `/Users/seungyeoning/Library/Developer/Xcode/Archives/2026-09-21/MukJump-1.0.2-42.xcarchive`.
  검증 자료: `output/release-appstore/2026-09-21-build-42/`.
  앞선 41번 빌드의 심사를 취소하고 42번으로 교체했으며 한·영·일 변경 사항과
  심사 메모를 갱신했다. **23:55 KST 앱 1개와 업적 설명 수정본 54개, 총 55개 항목의
  제출 성공 및 심사 대기 중** 상태를 확인했다.
  제출 ID: `05e09c67-a2f7-4a1c-a44a-7c69be9c3f5c`.
  [심사 상세](https://appstoreconnect.apple.com/apps/6804614765/distribution/reviewsubmissions/details/05e09c67-a2f7-4a1c-a44a-7c69be9c3f5c).
  기존 스크린샷·평점·수동 출시 설정은 유지했다. 승인 또는 공개 출시 완료는 아니다.
  UnityRuntime.framework dSYM 누락 경고는 남았으나 업로드는 성공했다.
  실기기 광고 송출과 Game Center 전송은 이번 자동 검사로 검증한 범위가 아니다.

- 2026-09-21 정식 업데이트 `1.0.2(41)`을 `main`에서 클린 빌드했다.
  꾸미기 이름·설명·특수능력 안내, 잠금 조건·진행 게이지·제목 크기와
  시간·리더보드·광고 변경을 포함하며, 실제 광고·디버그 없음 조건이다.
  기존 계정과 기록을 유지하고 서버 삭제·빌드별 로컬 초기화는 적용하지 않았다.
  관련 자동 검사 677개(출시·계정·광고·리더보드·스킨 647, 해금 UI 23,
  제목·실제 화면·특수능력 입력 7)가 최종 통과했다. 안전영역 검사는
  Device Simulator의 Screen 좌표와 Canvas 좌표를 혼용하던 부분만 바로잡았다.
  이전 Xcode 출력과 `DerivedData-AppStore-40`은
  `/Users/seungyeoning/.Trash/MukJump-clean-build-41-20260921/`로 옮겼으며 복구 가능하다.
  소스·기존 Archive·검증 로그는 보존했다.
  실기기 Release 빌드, Archive, 배포 IPA의 엄격한 전체 서명,
  팀 `8AU359WZZ2`, Apple 로그인·Game Center entitlement,
  `ITSAppUsesNonExemptEncryption` Boolean false를 확인했다.
  배포 IPA는 `get-task-allow=false`, `MukJumpPrereleaseReset=false`이며
  생성된 네이티브 코드에서도 스킨 디버그와 강제 테스트 광고가 비활성화되어 있다.
  19:35 KST Apple의 `Upload succeeded` / `Uploaded package is processing` 응답을 확인했다.
  Archive: `/Users/seungyeoning/Library/Developer/Xcode/Archives/2026-09-21/MukJump-1.0.2-41.xcarchive`.
  검증 자료: `output/release-appstore/2026-09-21-build-41/`.
  TestFlight에서 빌드 41의 처리 완료와 기존 내부 그룹 `11` 연결을 확인하고
  테스트 내용을 저장했다. 빌드 ID: `78859521-264f-4c51-9ebb-dd2ba5358d1d`.
  한·영·일 변경 사항과 심사 메모를 저장하고 같은 빌드를 1.0.2에 연결했다.
  19:42 KST Apple의 **1개의 항목 제출됨 / 심사 대기 중** 응답을 확인했다.
  제출 ID: `2c8f05b7-1c5c-4129-a4da-69edbd528abd`.
  [심사 상세](https://appstoreconnect.apple.com/apps/6804614765/distribution/reviewsubmissions/details/2c8f05b7-1c5c-4129-a4da-69edbd528abd).
  기존 스크린샷·평점과 수동 출시 설정을 유지했다. 심사 승인·공개 출시는 아직 아니다.
  UnityRuntime.framework dSYM 누락 경고는 남았으나 업로드는 성공했다.
  AdMob 실광고 송출·실기기 Game Center 전송은 이번 자동 검사로 검증한 범위가 아니다.

- 2026-09-20 스킨 통합 정식 업데이트 `1.0.1(40)`을 `main`에서 빌드했다.
  실제 광고·디버그 없음 조건이며 서버 삭제·빌드별 로컬 초기화는 적용하지 않았다.
  계정/닉네임/광고/리더보드/출시 대상 자동 검사 682개와 플레이 시간 UI 검사 1개가 통과했다.
  실기기 Release 빌드, Archive, 배포 IPA의 Apple 로그인·Game Center entitlement,
  `ITSAppUsesNonExemptEncryption` Boolean false 및 엄격한 전체 서명을 확인했다.
  01:54 KST Apple에서 `Upload succeeded`와 `Uploaded package is processing`을 반환했다.
  Archive: `/Users/seungyeoning/Library/Developer/Xcode/Archives/2026-09-20/MukJump-1.0.1-40.xcarchive`.
  검증 자료는 `output/release-appstore/2026-09-20-build-40/`에 있다.
  UnityRuntime.framework의 dSYM 누락 경고는 남았으며 업로드 실패는 아니다.
  App Store Connect에는 1.0.1 초안, 한·영·일 변경 사항, 심사 메모와 Game Center 사용을 저장했다.
  02:44 KST `1.0.1(40)`과 사용 중인 등록 업적 60개를 같은 제출에 넣고,
  Apple의 **61개 항목 제출됨 / 심사 대기 중** 응답을 확인했다.
  제출 ID: `7c561087-424e-46c5-818a-048ff092393b`.
  [심사 상세](https://appstoreconnect.apple.com/apps/6804614765/distribution/reviewsubmissions/details/7c561087-424e-46c5-818a-048ff092393b).
  기존 스크린샷과 수동 출시 설정을 유지했다. 심사 제출은 승인 또는 공개 출시 완료를 뜻하지 않는다.
  브라우저 연결과 만료된 Apple 로그인 세션을 복구했으며, 첫 먹매듭의 일본어 이미지
  처리 오류는 같은 PNG 재업로드 후 심사 편입 성공으로 확인했다.
  통합 완료된 로컬 `codex/ink-skins` 브랜치 참조만 삭제했다. 해당 커밋
  `2e1b1cdf62fa9e34d6a23a45e1a6befcee72d957`과 모든 변경 이력은 `main`에 남아 있다.
  미등록 천사링 `.62`는 코드에서도 전송하지 않으며, 제외된 질풍 두건 `.20`을 심사에 추가하지 않는다.
  정식 광고 설정과 업로드 성공은 AdMob 실광고 송출 성공을 보장하지 않는다.
  실기기 Game Center 전송과 AdMob 실광고 노출은 별도 미검증 상태다.

- 로컬 성장 저장과 뒤끝 행에는 각각 스키마 버전을 둔다. 기존 스키마를 읽지 못하면 덮어쓰지
  않고 로컬 복구본을 보존한다.
- 출시 뒤 최우선 순위는 실행 불가·저장 손실·계정 삭제 실패·광고 후 진행 불가·결제 오인 순이다.
- 위 P0 문제가 재현되면 신규 버전 배포를 중단하고 스토어 단계적 출시를 멈춘다. 데이터
  마이그레이션이 포함된 버전은 이전 바이너리로 단순 롤백하지 않고 호환 패치를 우선한다.

## 최소 진단

- 1.0은 별도 크래시 수집 SDK를 추가하지 않고 App Store Connect와 Play Console의 집계형
  크래시·ANR 보고서를 사용한다.
- 일반 로그에는 인증 토큰, 이메일, 뒤끝 사용자 ID, 광고 ID를 기록하지 않는다.
- 재현 문의는 앱 버전, 기기 모델, OS 버전, 발생 화면과 재현 순서만 받는다.
