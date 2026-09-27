# 먹점프 1.0 운영 기준

이 문서는 출시 뒤 운영 질문에 대한 현재 답이다. 새로운 SDK나 데이터 수집을 추가할 때는
개인정보처리방침과 스토어 데이터 공개 응답을 먼저 함께 갱신한다.

2026-09-23 정리: 출시 후 기존 계정·기록을 보존한다. 아래 날짜별 배포 기록과 출시 전
초기화 절차는 과거 증거이며 현재 배포 지시가 아니다. 플랫폼별 현재 구현은 기능별 원본과 코드를 확인한다.

## iOS 광고 제거 · 금색 먹 상품

- iOS 비소모품 `com.CYSB.MukJump.removeads`는 한 번 구매로 배너·전면·보상형 광고를 제거하고
  금색 먹 외형을 제공한다. 부활은 기존 조건과 한 판 1회 제한을 유지한다. 몸체 색과 착지
  입자만 바꾸며 성장·능력·기존 63개 의상 ID와 계정 기록은 변경하지 않는다. 토스에는 노출하지 않는다.
- Unity IAP 5.4.3의 StoreKit 2 검증된 현재 거래 조회로 실행·복귀·복원 시 권한을 확인한다.
  직접 구매 성공 콜백이나 PlayerPrefs 표식만으로 지급하지 않는다. 저장 후 미확정 거래를
  확정하며 재전달은 중복 처리하지 않는다. 상품 조회 실패와 권한 회수를 구분한다.
- 구매창은 iOS 로비 오른쪽 아래 먹 번짐 버튼, 꾸미기의 금색 먹 카드 또는 옵션에서 열고
  `구입 복원`을 제공한다. 가격은 StoreKit의
  현지 가격을 표시한다. 구매 확인 중에는 광고를 보류하되 금색 먹이나 무료 부활을 선지급하지 않는다.
- 2026-09-25: ASC 상품 6815755914와 한국어·영어·일본어 설명, 한국 기준 4,900원과 Apple
  자동 환산 가격을 저장·재조회했다. 미국 $2.99, 일본 ¥500, 영국 £2.99, 캐나다 C$3.99,
  호주 A$4.99, 뉴질랜드 NZ$4.99이며 판매 국가는 기존 앱과 같은 이 7개국으로 제한했다.
  앱 출시 국가 자체는 변경하지 않았고 향후 국가 자동 추가도 선택하지 않았다.
- 실제 구매 안내 UI를 Unity Editor에서 1170×2532 원본 PNG로 촬영해 심사 이미지로 등록했다.
  ASC에 저장된 한국 가격을 표시 전용 어댑터로 제공했으며 미구매 상태를 유지했다.
  편집기 촬영임을 심사 메모에 명시했다. 이미지 등록은 StoreKit 거래 검증 결과가 아니다.
- 2026-09-25 13:10 KST 앱 1.0.3(43)과 첫 IAP를 함께 제출했다. 9월 26일 Apple은 앱 미리보기의
  기기 프레임을 이유로 2.3.4 반려를 통보했다. 9월 26일 12:00 KST 재접수한 영상을 이후
  사용자 지정 수정본 3개로 다시 교체하고, 기존 스킨 모음 스크린샷도 추가했다.
  **9월 26일 19:27 KST** 같은 빌드·IAP 두 항목을 새 접수
  `27f360ad-699e-4c28-a14a-8930636430f1`로 제출했고, 새로고침 후 둘 다 `심사 대기 중`을 확인했다.
  이전 접수 `016ef546-7276-4cd6-8392-0e16bed682d6`는 교체를 위해 정상 취소했다. 앱은 수동 출시이며 공개 출시 전
  실기기 StoreKit 상품 조회·구매·복원·재실행 권한 및 취소·중복·회수 검증을 완료해야 한다.
  현재 연결된 iPhone이 없어 이 실기기 검증과 실제 공개 출시는 아직 완료 전이다.
- 2026-09-27 설정창 겹침으로 43이 Guideline 4 반려된 뒤, 기존 기능에 배치만 수정한
  **1.0.3(44)와 같은 IAP를 오전 10:01 KST 재제출했다.** 기존 접수 ID
  `27f360ad-699e-4c28-a14a-8930636430f1`는 유지되며 두 항목 모두 `심사 대기 중`이다.
  아래 빌드 44 기록이 현재 바이너리 기준이며 43의 과거 반려와 구분한다.

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

- 네이티브 분석은 사용자 동의를 확인하는 `FirebaseAnalyticsRuntime`을 사용한다.
  토스는 `AppsInTossAnalyticsRuntime`으로 분리한다. 기존의 “AdMob 집계만 사용”은 초기 1.0 계획이다.
  현행 이벤트·동의·개인정보 경계는 [Firebase 운영](../firebase-analytics.md)과 [토스 지표](apps-in-toss-metrics.md)를 따른다.
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
- 최고 고도 순위와 Apple 업적 연동을 구분한다. 현재 코드에는 `MukJumpAchievements.mm`과
  iOS 후처리의 Game Center capability가 있으므로 “Game Center 브리지·권한 전체 제거”라는
  초기 1.0.0(28) 설명을 현행으로 적용하지 않는다. 업적은 [등록 안내](game-center-achievements.md)를 따른다.
  Sign in with Apple 계정 연동과 뒤끝 저장/순위 동기화는 별도 기능이다.
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

### 설정창 최소 수정 — 1.0.3 (44)

- 2026-09-27 Apple의 Guideline 4 반려는 iPad Air 5 / iPadOS 27.0에서 설정창 하단 구매 버튼과
  약관 링크가 겹치는 문제였다. 기존 43 소스 `26c627f86167373baabb9e0dcce392814b85d42a`를
  별도 비 Git 빌드 입력으로 내보내 구매 행 공간과 상태 문구 높이·위치만 수정했다.
  현재 main의 미출시 성장·꾸미기·구매 흐름은 포함하지 않았다. 재현 소스와 검사 커밋은
  `87863bc3`, 빌드·서명·업로드 도구 및 검증 설명은 `879948fa`다.
- 원본의 언어별 겹침 실패를 재현한 뒤 수정본 Edit 검사 5/5, Play 검사 1/1과 실제 프레임
  캡처 18개가 통과했다. iPad Air 5 / iOS 18.4 Simulator의 기본·확대 iPhone 호환 모드,
  배너 표시, 한국어·영어·일본어 및 구매·개인정보·약관 버튼의 개별 화면 이동을 확인했다.
  작은 iPhone 크기와 상태 문구 유무는 Editor에서 검사했다. iPadOS 27.0은 설치되지 않았고,
  실제 iPhone StoreKit 구매·복원은 미검증이다. 이를 Simulator 결과로 대체하지 않는다.
- 기존 43과 같은 Pod·Swift 패키지 버전으로 고정한 뒤 Unity 새 내보내기, Pods workspace
  실기기 Release, Archive와 배포 IPA의 서명·Apple 로그인·Game Center·암호화 false·
  get-task-allow false 검증을 통과했다. **2026-09-27 09:49 KST 업로드 성공**을 확인했다.
  UnityRuntime.framework dSYM 경고는 업로드를 막지 않았으며 앱·UnityFramework dSYM을 보존했다.
  Apple 처리 완료와 TestFlight `제출 준비 완료`를 직접 확인한 뒤, 같은 버전의 빌드를 44로
  교체·저장하고 기존 IAP와 함께 **10:01 KST 재접수**했다. 앱·IAP 모두 `심사 대기 중`이며
  접수 ID는 `27f360ad-699e-4c28-a14a-8930636430f1`, 빌드 ID는
  `bd3dcb4e-edb1-49cc-a92d-b0665b308d6a`다. 심사 메모에 실제 수정과 검증 환경만 추가했다.
  접수 상세 새로고침과 제출 후 버전 재조회로 빌드 44·두 항목 심사 대기·수동 출시·평가 유지,
  기존 스크린샷 7장과 스킨 이미지 세 번째 순서가 저장돼 있음을 확인했다.
- 빌드 입력·로그·실패 기록·검증 캡처는 `output/release-appstore/2026-09-27-build-44/`에,
  Archive는 `~/Library/Developer/Xcode/Archives/2026-09-27/MukJump-1.0.3-44.xcarchive`에 있다.
  배포 IPA SHA256은 `d12e1a73a358aac3bea56ad000a09308c5304fad415dbb44f58db28c9b215b6c`다.
  기존 영상 3개·한국어 스크린샷 7장(스킨 모음 세 번째)·상품 가격과 판매 국가·설명·심사 이미지를
  유지하며, 수동 출시와 실제 StoreKit 검증 전 공개 금지도 유지한다.

### 이전 업데이트 준비 — 1.0.3 (43)

- 소스 `26c627f8`에서 정식 iOS를 클린 내보내고 Pods 워크스페이스의 실기기 Release,
  Archive, 배포 IPA를 검증했다. 로비의 광고 제거 입구, StoreKit 2 비소모성 구매와 금색 먹,
  의상 보존·이동 버튼 수정이 포함된다. 토스 전용 시작 화면 생략은 iOS에 적용되지 않는다.
- 2026-09-25 04:08:50 KST App Store Connect가 업로드 성공과 처리 시작을 반환했다.
  이후 TestFlight 처리 완료·`제출 준비 완료`와 기존 내부 그룹 `11` 연결을 확인했다.
  같은 날 13:10 KST 1.0.3과 첫 인앱 구매를 함께 심사 제출했고 두 항목의 접수를 확인했다.
  가격과 심사 이미지는 등록 완료이며 앱은 수동 출시다. 실기기 StoreKit 구매·복원 검증은
  연결된 iPhone이 없어 미완료로 유지하며, 심사 접수를 공개 출시나 실기기 검증 완료로 보지 않는다.
- 2026-09-26 12:00 KST 당시: 2.3.4 반려 대상인 한국어·영어(미국)·일본어 앱 미리보기 3개를 실제 iOS 앱
  실행 영상으로 교체했다. 같은 소스·버전·빌드의 촬영용 Release를 iOS 18.4 x86_64 Simulator에서
  실행했으며 화면 전체를 녹화했다. 기기 프레임·홍보 카드 없이 로비와 플레이를 보여 준다.
  한국어·일본어 23초, 영어 15.3초, 886×1920·30fps H.264/AAC이며 앱에 포함된 배경음을 사용한다.
  6.5형 원본과 6.3·6.1·5.5·4.7·4형 상속 슬롯의 새 파일을 재조회했다. 기존 스크린샷·빌드 43·IAP는 유지했다.
  새 App Store 바이너리는 올리지 않았고 Unity 설정은 촬영 전과 바이트 단위로 같게 복원했다.
  원본 영상·최종 MP4·출처와 SHA-256·검증 증거는 `output/release-appstore/2026-09-26-preview-fix/`에 보존한다.
  12:00 KST 정상 재제출 1회가 수락됐고 기존 제출 ID의 두 항목 모두 `심사 대기 중`으로 바뀌었다.
  영상은 제출 직전 Apple 후처리 중이었으며 업로드·제출 오류는 없었다. 접수 성공과 영상 후처리
  완료는 별도로 확인한다. Simulator 촬영은 실기기 StoreKit 검증을 대신하지 않는다.
- 2026-09-26 19:27 KST: 사용자가 다시 지정한 `output/teaser/appstore-2.3.4/`의
  `mukjump_teaser_KR_appstore.mp4`, `mukjump_teaser_US_appstore.mp4`,
  `mukjump_teaser_JP_appstore.mp4`를 재인코딩·추가 편집 없이 업로드했다.
  세 파일 모두 29.5초, 886×1920, 30fps, H.264 High Level 4.0/AAC stereo 44.1kHz이며
  전체 디코드 오류 0. 전 구간 1초 간격 화면 검사에서 종전 둥근 기기 프레임·바깥 여백 제거를 확인했다.
  기존 홍보 문구·로고·스킨 모음 연출은 유지됐으며 원촬영 출처는 이 MP4만으로 확인하지 못했다.
  심사 메모의 이전 Simulator 촬영 주장도 제거하고 확인한 수정 사실만 적었다.
  한국어·영어(미국)·일본어 6.5형 원본과 6.3·6.1·5.5·4.7·4형 상속 슬롯에서 새 파일명을 재조회했다.
  사용자 추가 요청으로 `store-screenshots/app-store-connect/07-all-skins.png`를 한국어 6.5형에 등록했다.
  기존 6장은 모두 보존하며 총 7장 중 스킨 모음이 세 번째다. 사용자가 화면에서 조정한 순서를 유지했다.
  19:13 KST 기존 접수를 한 번 취소한 뒤 같은 앱 1.0.3(43)과 기존 비소모성 IAP를 새 초안에 함께 추가했고,
  19:27 KST 제출 한 번이 수락됐다. 새 접수 ID는 `27f360ad-699e-4c28-a14a-8930636430f1`이며
  새로고침 후 두 항목 모두 `심사 대기 중`이다. 수동 출시·기존 IAP 가격/지역/설명·빌드 43을 유지했다.
  새 바이너리 업로드나 Unity 실행은 없었고 기존 IPA SHA-256도 변하지 않았다.
  영상의 Apple 후처리·스트리밍 재생 완료는 아직 확인하지 않았다. 심사 승인·실기기 StoreKit 검증·공개 출시는 미완료다.
  세 영상의 SHA-256, 로컬 검수, 스크린샷과 접수 근거는 `output/release-appstore/2026-09-26-user-preview/`에 보존했다.
- 배포 IPA의 Bundle ID `com.CYSB.MukJump`, 팀 `8AU359WZZ2`, Apple 로그인·Game Center,
  암호화 Boolean false, `get-task-allow=false`, 전체 코드 서명을 확인했다. 기존 저장 초기화는 없다.
- UnityRuntime 심볼 업로드 경고는 설치본이 정적 라이브러리인 것과 일치하는
  [Unity 공식 이슈 UUM-141444](https://issuetracker.unity.com/issues/8590/upload-symbols-failed-is-thrown-when-archive-validation-is-being-done-in-xcode)다.
  업로드는 성공했고 앱과 UnityFramework의 dSYM을 보존했다. 임의의 빈 dSYM을 만들지 않았다.
- Archive: `/Users/seungyeoning/Library/Developer/Xcode/Archives/2026-09-25/MukJump-1.0.3-43.xcarchive`.
  검증·업로드 증거: `output/release-appstore/2026-09-25-build-43/`.
  기존 1.0.3 초안에 빌드 43을 연결하고 세 언어 업데이트 설명과 구매 심사 메모를 저장했다.
  심사 접수 증거는 같은 폴더의 `app-review-submission.json`, 최종 IAP 심사 이미지는
  `review-screenshot/iap-review-ko-4900-editor-1170x2532.png`이며 캡처·가격 출처를 함께 보존했다.

### 정식 심사 전환 — 1.0.0 (28)

- 2026-09-11 사용자 요청에 따라 정식 App Store 빌드로 전환한다. 테스트 광고와 빌드별
  로컬 초기화 심볼을 넣지 않고, 내부 TestFlight 전용 제한 없이 내보낸다.
- 심사 준비를 위한 마지막 서버 정리는 별도 콘솔 작업으로 수행한다. 이후 배포에서는
  전원 삭제를 반복하지 않으며 정상적인 사용자 기록 보존 정책을 적용한다.
- 서버 삭제는 휴대폰에 남은 저장·OS 권한·Apple ID 자체 삭제를 의미하지 않는다.

### 과거 기록: 정식 출시 전 TestFlight 초기화 (현재 적용 금지)

다음은 2026-09-11 출시 전 한정 지시의 보존본이며 현재 업데이트에는 적용하지 않는다.
당시 출시 전 새 TestFlight 빌드마다 로컬 게스트·서버 게스트·Apple
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

### 과거 기록: 출시 전 초기화 구현

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
