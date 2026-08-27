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

- 최종 제출은 배포용 워크스페이스에서 Team `8AU359WZZ2`를 확인하고 Archive, TestFlight
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
