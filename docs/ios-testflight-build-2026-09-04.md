# iOS TestFlight QA — 2026-09-04

## 검증된 준비 상태

- Unity 6000.5.9f1 / Xcode 26.2 (17C52), iPhoneOS SDK 26.2.
- 먹점프 Bundle ID `com.CYSB.MukJump`, Team `8AU359WZZ2` (Seongbin Choi).
- 앱 버전 `1.0.0`, 이번 로컬 QA 빌드 번호 `8`. Apple 서버 Validate는 중복 오류 없이
  통과했지만, App Store Connect 기존 빌드 목록을 직접 대조하지는 못했다.
- `Build iOS TestFlight QA (Test Ads)` 경로의 일회성 `MUKJUMP_TEST_ADS` 사용.
  일반 출시 심볼에 테스트 광고를 영구 추가하지 않았다.
- Apple 로그인 사용 중. 기존 `MukJump App Store Connect` 프로파일
  `f6431819-66e1-45f3-932d-4e7c74ff38ef`에 해당 Bundle ID·Team·Apple 로그인
  entitlement 및 beta-reports-active가 포함됨. 만료 2027-08-28.
- 같은 팀의 유효한 Apple Distribution 인증서 확인. 신규 인증서·프로파일 생성 없음.
- Facebook SDK 의존성 없음. FBAEMKit 등 별도 SDK를 설치하지 않았다.

## 자동 검증

- 14:49 KST 실제 Unity 전체 EditMode 테스트 **1043/1043 PASS, FAIL 0, SKIP 0**.
  EnterPlayMode 기반 Physics2D 통합 테스트도 이 실행에 포함된다.
- 증거: `output/ios/evidence-20260904/TestResults.xml`, `editmode-result.txt`.
- 최초 10개 실패 후 분신 생성 실패 정리(즉시 등록 해제/비활성화 및 환경별 파괴),
  EditMode 생명주기, BGM fixture, 전체 editor-only 소스 경계 검사, 발판 고도 기준 및
  독립 Physics2D 테스트를 보완했다. 런타임 단방향 물리 설정은 변경하지 않았다.
- 독립 읽기 전용 검토를 받았고, 씬은 빌더로 14:50 KST 재생성했다.
- 이전 QA 산출물은 `output/ios/MukJumpTestFlightQA.before-20260904`로 보존했다.

## 진행 중 / 배포 전 확인

- Unity iOS QA export 및 CocoaPods 생성 성공 (14:51 KST).
- `output/ios/MukJump-1.0.0-8.xcarchive` Archive 성공 (14:54 KST).
  앱 ID·Team·버전/빌드·arm64 확인, Apple 로그인 서명 entitlement 포함.
  `codesign --verify --deep --strict` 통과. UnityFramework/UnityRuntime가 앱에 포함된다.
  Google SDK는 UnityFramework에 정적 링크되므로 별도의 Google 동적 framework가 없는 것이 정상이다.
- Organizer에서 Custom → 빌드 번호 자동 변경 해제 → Manually manage signing을 선택했다.
  기존 Apple Distribution(2026-08-28 생성)과 `MukJump App Store Connect`로 재서명 완료.
  IPA 검토 화면에서 version 1.0.0(8), get-task-allow=false, beta-reports-active=true,
  Apple 로그인, Team 8AU359WZZ2 및 인증서/프로파일 2027-08-28 만료를 확인했다.
- Organizer Validate App 서버 검증 성공 (15:00 KST, Validation succeeded).
  차단 오류는 없으나 UnityRuntime.framework dSYM 누락 경고 1건이 있다.
  누락 UUID: `96D3E33E-3773-3B69-BA75-0A336A6AA44C`.
  본체·UnityFramework dSYM은 존재한다. 가짜 심볼을 생성하거나 실제 framework를 제거하지 않았다.
  추가 조사: export의 UnityRuntime은 정적 라이브러리이며, archive의 경고 대상은
  실행 코드 `__text` 크기가 0인 dylib이다. Runtime은 UnityFramework에 정적 링크되고
  해당 dSYM UUID는 `508B9366-2BD3-35F8-B1C5-141E362E802F`이다.
  빈 embed stub의 심볼 경고로 판단되며, 엔진 코드 전체의 심볼 유실로 단정할 수 없다.
  Upload·TestFlight 완료 증거는 아직 없다.
- `Distribute App → Custom → App Store Connect → Upload` 경로에서 수동 재서명을
  완료하고 최종 `Review ProductName.ipa content`의 **Upload 버튼 직전**에 대기 중이다.
  아래 암호화 면제 분류 근거를 확인하지 못해 실제 Upload는 누르지 않았다.
- 인앱 브라우저는 로그인 화면이나, Xcode 저장 세션의 앱 정보 조회는 성공했다.
- 등록된 iPhone/iPad는 현재 모두 unavailable. 실기기 실행 미검증.
- `ITSAppUsesNonExemptEncryption`은 기존 후처리에서 Boolean false로 기록된다.
  다만 기존 "SDK HTTPS만" 주석은 충분한 근거가 아니다. 뒤끝 SDK에는 로컬
  인증정보 암호화가 있으며 공식 Inspector 문서에는 전송 데이터 암호화 설명도 있다.
  포함 SDK의 실제 기능을 근거로 면제 분류를 최종 확인해야 하며 AES가 있다는 사실만으로
  비면제라고 단정하지 않는다. 외부 수출규정 답변·법적 동의는 아직 수행하지 않았다.
  기존 면제 판단 기록 또는 SDK 공급자 안내가 있는지 사용자에게 질문했다.

참고: [Apple 암호화 지침](https://developer.apple.com/documentation/security/complying-with-encryption-export-regulations),
[뒤끝 파일시스템](https://docs.backnd.com/sdk-docs/backend/base/sdk-utils/filesystem/),
[뒤끝 Inspector](https://docs.backnd.com/sdk-docs/backend/base/setting-inspector/).

심볼 참고: [Unity Apple 런타임 정적 framework 전환](https://docs.unity3d.com/6000.5/Documentation/Manual/UpgradeGuideUnity64.html),
[Apple DTS의 정적 framework dSYM 경고 설명](https://developer.apple.com/forums/thread/761589).
