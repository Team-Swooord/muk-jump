# 먹점프 1.0 운영 기준

이 문서는 출시 뒤 운영 질문에 대한 현재 답이다. 새로운 SDK나 데이터 수집을 추가할 때는
개인정보처리방침과 스토어 데이터 공개 응답을 먼저 함께 갱신한다.

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

- 보상이 없는 전체 최고 고도만 운영하고 공개 닉네임은 수집하지 않는다. iOS·Android는 뒤끝
  TOP 10, Apps in Toss는 공식 게임센터 화면을 사용한다.
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
