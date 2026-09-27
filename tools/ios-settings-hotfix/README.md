# iOS 1.0.3(44) 설정창 최소 수정

제출한 43의 소스 `26c627f86167373baabb9e0dcce392814b85d42a`를 Git 저장소가 아닌 빌드 입력으로 내보낸다. 현재 main의 미출시 기능은 포함하지 않는다. 실행 코드는 `LobbyOptionsView.cs`의 구매 행 높이, 상태 문구 높이와 위치만 바뀐다. 구매 버튼의 기존 동작과 저장·로그인·상품 코드는 유지한다.

`prepare.py export`는 새 입력을 `output/release-appstore/2026-09-27-build-44/source`에 만들며 기존 폴더를 덮어쓰지 않는다. `patch`는 43 원본에만 수정한다. `provision-local`은 Git 제외 뒤끝 연결 값을 기존 Archive 43에서 확인한 뒤, 값은 출력하지 않고 필요한 설정과 meta만 복사한다. 초기 작업의 일부 내보내기를 보충한 `complete-export`는 처음부터 전체를 내보낸 재실행에는 필요 없다.

두 `SettingsPremium*Tests.cs` 파일은 내보낸 입력의 `Assets/Editor/Tests/`에 복사해 사용한다. 원본에서 언어별 겹침 검사가 실패하는 것을 확인한 뒤 수정본으로 같은 검사를 수행한다. Play 검사는 실제 게임 프레임에서 좌표와 터치 대상을 읽으며, Editor 테스트 루프의 Screen 값으로 대신하지 않는다. 메모리 저장소와 결제 불가 테스트 공급자를 사용한다. 실제 StoreKit 검증을 대신하지 않는다.

Unity 6000.5.9f1을 **내보낸 입력 경로**로 열고 모든 CLI 호출에 그 경로를 지정한다. 설치된 CLI는 실행 명령의 `--caller`/`--skill` 인수를 거절하므로 이 작업의 provenance는 이 문서에 기록한다: caller `plugin`, skill `ui-ugui` / `unity-cli`. 다른 에디터와 자동 할당 포트가 겹치면 해당 입력의 Pipeline Editor 설정에서 다른 포트를 지정한다. Player용 Pipeline 설정은 계속 꺼져 있어야 한다.

`BuildSettingsHotfix.cs`는 Assets 밖에서 Pipeline `run_script`로 실행한다. `Prepare`는 빌드 번호 44 설정, Editor 연결 설정의 Editor 폴더 이동, 기존 빌더를 통한 씬 재생성을 수행한다. 이 작업은 약 40초가 걸릴 수 있으므로 CLI와 run_script의 시간 제한을 모두 60초로 지정하고, 응답이 늦어도 재실행 전에 `mukjump_audit`로 실제 완료를 확인한다. `recompile_status` 내부 오류 없음과 `readyForValidation=true`를 확인한다.

`prepare.py verify`는 모든 Git 입력의 해시, 승인된 실행 코드, 빌드 번호만 바뀐 PlayerSettings, 기존 43과 같은 Firebase 설정과 뒤끝 로컬 입력을 검사한다. 씬과 attestation은 43의 동일 빌더로 재생성한다. URP 설정은 Editor/Player 빌드 과정에서 만드는 runtime list 외의 차이를 허용하지 않는다.

`StartSimulator`와 `StartDevice`는 각각 새 Xcode 출력을 만들고 성공/실패 상태를 별도 JSON에 남긴다. 실패한 결과를 확인하고 보존하기 전에는 재실행하지 않는다. Simulator는 기존 Firebase의 x86_64 라이브러리와 호환되는 iOS 18.4를 사용한다. 설치된 iOS 26.2는 arm64만 지원하며, Apple 심사 환경 iPadOS 27.0은 설치돼 있지 않다.

`build_native.py simulator`는 Simulator 네이티브 빌드, `device`는 실기기 대상 Release 빌드 → Archive → 서명·권한·암호화 검증 → 배포 IPA 내보내기를 수행한다. `upload`는 검증한 동일 Archive를 App Store Connect에 업로드한다. 업로드와 재심사 접수는 실제 승인 범위 안에서만 실행한다. 이전 43 IPA·Archive/dSYM, 계정과 검증 실패 증거를 보존한다. 공개 출시와 실제 결제는 이 도구의 범위가 아니다.

네이티브 빌드 전에는 실제 43의 Podfile.lock과 Swift Package.resolved를 재사용한다. 모든 Pod 버전·podspec 체크섬·Podfile 체크섬이 같아야 한다. CocoaPods가 Specs Git 저장소 대신 CDN을 사용하는 조회 위치 메타데이터 차이만 허용한다. Unity의 첫 의존성 해석에서 광고 SDK 13.10.0이 선택됐으나 최종 Simulator·기기 빌드는 모두 기존 13.7.0으로 고정했다.

2026-09-27 검증 결과: 원본의 3개 언어 겹침 재현 후 수정본 Edit 검사 5/5, Play 검사 1/1과 실제 프레임 캡처 18개가 통과했다. iPad Air 5 / iOS 18.4 Simulator에서 기본·확대 iPhone 호환 화면, 배너 표시, 3개 언어와 구매·개인정보·약관 버튼의 서로 다른 화면 이동을 직접 확인했다. Simulator의 테스트 광고는 SDK가 제공했으며 출시 설정을 바꾸지 않았다. 상태 문구 유무와 작은 iPhone 크기는 Editor 검사에 포함됐다. iPadOS 27.0 및 실제 iPhone StoreKit 구매·복원 검증을 완료한 것으로 기록하지 않는다.

Unity 내보내기, Pods workspace Release 실기기 빌드, Archive, 배포 IPA의 deep/strict 서명·Apple 로그인·Game Center·암호화 false·get-task-allow false 검증과 App Store Connect 업로드가 성공했다. IPA SHA256은 `d12e1a73a358aac3bea56ad000a09308c5304fad415dbb44f58db28c9b215b6c`다. UnityRuntime.framework dSYM 누락 경고는 업로드를 막지 않았고 앱·UnityFramework dSYM은 보존했다. Apple 처리와 재심사 접수 상태는 `docs/store/release-operations.md` 및 출력 폴더의 실제 접수 기록으로 따로 확인한다.

검증 근거는 출력 폴더의 `final-source-verification.json`, `fixed-layout-tests.json`, `play-tests-passed.json`, `final-editor-audit.json`, `simulator-ui-verification.json`, `ipa-verification.json`, `upload-result.json`과 원본 로그·캡처다. 초기 누락 입력, 검사 실패, 시간 초과 기록도 보존한다. 최종 소스 대조는 Editor 정상 종료 후 다시 수행했다.
