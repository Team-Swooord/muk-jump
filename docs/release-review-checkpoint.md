# 출시 전 교차 검수 체크포인트

최신 iOS 후속 상태(2026-09-04 15시): 아래 과거 Unity 정체는 해소됐다.
실제 전체 테스트 1043/1043 통과, QA 1.0.0(8) Unity export·Archive 및 Apple 서버
Validate 성공. 최종 Upload 전 암호화 면제 근거 확인 대기이며 아직 업로드하지 않았다.
상세 증거와 경고는 `docs/ios-testflight-build-2026-09-04.md`를 우선 참고한다.

갱신: 2026-09-04 11:56 KST. 전체 검수 완료 또는 출시 승인 문서가 아니다.
다음 작업은 실제 워킹트리·Unity 상태를 다시 확인하고 이 지점부터 이어간다.
수정자는 주 에이전트 한 명이며 독립 검토자는 읽기 전용이었다.

## 이번 수정 및 소스 승인

- `MukJumpAccountRuntime.cs`: Apple 삭제 진행 중 재시도 차단. 요청 세대,
  계정 세션 세대, 현재 소유자와 저장된 삭제 소유자를 콜백마다 확인한다.
- 서버 GetUserInfo의 inDate·subscriptionType·federationId를 확인하고 재인증한
  Apple credential.User가 동일할 때만 revoke를 요청한다. 서버 소유자 확인 실패와
  손상 응답은 삭제 의도를 보존하고 세션을 차단한다.
- 기존 AppleRevokeRequired 저장값은 0=불필요/완료, 1=구버전 불명,
  2=전송 가능, 3=새 전송 전으로 구분한다. 1·2·알 수 없는 값은 재시작·취소·실패
  응답으로 제거하지 않는다. 전송 가능 상태를 저장·확인하기 전에는 SDK를 호출하지 않는다.
- revoke 성공 상태 저장 실패 시 2로 복원하고 뒤끝 Withdraw를 호출하지 않는다.
  중복 콜백과 성공 콜백 뒤 SDK 예외가 다음 단계 요청을 취소하지 않도록 단계도 검사한다.
- `MukJumpAccountTests.cs`: 정책 20케이스와 지연/중복 콜백, 재시도, 계정 변경,
  전송 전후 저장 실패, 손상 응답, 동기 콜백 후 예외 회귀를 추가했다.
  실제 개발자 삭제 표식과 순위 업로드 대기값은 snapshot/격리/finally 복원한다.
- `IosReleaseBuildTests.cs`: 변경된 OAuth 누락/중복 검사 메시지와 기대문구를 맞췄다.
- 독립 검토 결과: 위 소스·테스트 계약 승인. 추가 확정 P0/P1 없음.
  **Unity 콜백 테스트 실행 승인을 의미하지 않는다.**

## 실제 검증 증거

- `git diff --check` 통과(위 세 파일).
- Unity 6000.5.9f1에 포함된 Roslyn과 현재 프로젝트 rsp로 런타임·에디터 C# 컴파일 성공.
- iOS player rsp(`900b0aP.dag/Assembly-CSharp.rsp`)에서 QA 광고 define을 제거한
  네이티브 iOS 분기 C# 컴파일도 성공. Xcode 링크·서명·Archive 성공과는 다르다.
- rsp에서 빠진 신규 소스 AppsInTossIdentityPolicy, MukJumpLegalUrls,
  RecordingScenarioDirector를 명시적으로 포함했다.
- 실제 새 어셈블리의 NUnit 메서드를 별도 Mono로 실행:
  빌드/서명/plist 등 엔진 비의존 45케이스 + Apple 정책 20케이스 = **65 PASS / 0 FAIL**.
  Unity 네이티브 API를 호출하는 fixture Setup/콜백 테스트는 이 실행에서 제외했다.
- 임시 runner 및 DLL: `/private/tmp/mukjump-managed-review.oi9AyB/`.
  최신 DLL 생성 시각은 11:55:10~12 KST. 기존 39/45케이스도 별도 검토자가 독립 재현했다.
- SDK 응답 fixture는 실제 BackendReturnObject의 StatusCode/ReturnValue 속성 setter로
  구성했다. 200 응답의 IsSuccess 및 JSON 파서를 별도 Mono로 검증했고, 잘못된 JSON은
  해당 SDK에서 예외 대신 null을 반환함을 확인했다. 외부 계정 호출은 하지 않았다.

### 검증된 소스 SHA-256

```text
a2e72b2ecb509f15104118169ab8f0b5ed130d52c1ad685115eeadd6193cb09a  MukJumpAccountRuntime.cs
73cc3d71320c2d88afafbe8f689e6d7c03ae53ad958e8752dd3b5ec1452dfc34  MukJumpAccountTests.cs
9deaf79f51523a0eacb7a1a718072139574f070a33ec134928c46821abc8a9cd  IosReleaseBuildTests.cs
```

## Unity 검증 차단점

- 에디터가 열려 있으므로 batchmode를 실행하지 않았다. 강제 종료하지 않았다.
- `Temp/MukJumpRunAllTests.request`는 9월 3일 19:59:14 이후 처리되지 않았다.
- 실제 TestResults.xml은 9월 3일 19:37:13 결과이며 현재 소스 검증 증거가 아니다.
- 활성 로그는 `Logs/Editor.log`이고 마지막 갱신은 9월 4일 11:36:14이다.
  과거 테스트 예외 문자열은 있으나 새 소스의 Unity 재컴파일/재실행 결과는 없다.
- 작업을 저장한 뒤 에디터 재실행이 필요하다. 이후 새 전체 EditMode/PlayMode 결과,
  씬 빌더 재생성 및 scene attestation, iOS 테스트 광고 실기기 증거를 확보해야 한다.
- 기존 iOS 출력은 오래된 빌드다. 현재 수정이 반영된 TestFlight 빌드로 취급하지 않는다.

## 확인됐으나 아직 수정하지 않은 P1

광고 표시 watchdog에서 늦은 보상 이벤트가 유실된다.

- GoogleMobileAdsProvider와 AppsInTossAdProvider는 90초 뒤 terminal false 처리하면서
  콜백·세대·광고 객체/구독을 폐기한다. 이후 earned 이벤트는 무시된다.
- GameManager도 120초 뒤 요청과 보상권을 함께 폐기하므로 provider만 고쳐서는 부족하다.
- 다음 수정은 단일 bool 완료 콜백의 의미를 바꾸기보다, 표시 종료/soft timeout/
  earned/실제 표시 실패를 분리하는 요청 단위 ticket을 검토한다.
- 입력 잠금과 보상권을 분리하고, 같은 요청·같은 run·미정산 GameOver에만 보상을
  적용한다. timeout은 보상이나 화면 종료를 뜻하지 않는다.
- 메인 선택(정산 전), 새 판, 다음 요청, disable, 보상 소비 시 해당 ticket을 취소한다.
  local generation을 먼저 무효화하고 SDK 정리를 하여 재진입을 막는다.
- timeout→earned→dismissed, dismissed→earned, 중복 이벤트, background 복귀,
  timeout→메인 정산→late earned, 다음 판의 이전 callback, cleanup 예외와 동기 callback을
  회귀 검증해야 한다. 표시 요청 정리는 멱등적이고 이전 요청이 다음 판 provider를
  영구 busy로 남기지 않아야 한다.

## 아직 완료로 표시하면 안 되는 범위

전체 코드 범위(코어/계정/광고/플랫폼, 플레이/물리/스폰, UI, 에디터/리소스)의 최종
통합 검수, Unity 전체 테스트, Device Simulator, iOS/Android 실기기, 외부 콘솔 검증.
후속 플랫폼 점검에는 Android OAuth identity pin, 광고 plugin 설정 readback,
Android upload 인증서 확인도 남아 있다. 사용자 확인 없이 외부 보안 설정을 바꾸지 않는다.

기존 야간 자동검수 예약은 9월 4일 오전 9시 종료 조건으로 만료되었다.
9월 5일 오전 9시까지 같은 간격으로 연장할지는 사용자 확인 대기이며 아직 변경하지 않았다.
