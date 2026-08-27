# 먹점프 스토어 개인정보 응답 기준

대상: 먹점프 1.0 / 뒤끝 로그인·저장·리더보드 + Google Mobile Ads 11.4.0

이 문서는 콘솔 입력용 기준이다. SDK나 광고 정책을 바꾸면 실제 빌드를 다시 확인한 뒤
응답도 함께 수정한다.

뒤끝의 `Send Log Report`와 `Auto Load Location Properties`는 꺼 둔다. 따라서 게임이
사용하지 않는 추가 요청 로그와 IP 기반 국가·도시·지역 자동 조회를 의도적으로 수집하지
않는다. 릴리스 검증기가 두 설정이 다시 켜지면 실패한다.

## App Store Connect — 앱이 수집하는 개인정보

`예, 이 앱에서 데이터를 수집합니다`를 선택한다.

| 데이터 유형 | 수집 | 사용자 연결 | 추적 | 목적 |
|---|---:|---:|---:|---|
| 이름 | 예 | 예 | 아니요 | Google 계정 인증 |
| 이메일 주소 | 예 | 예 | 아니요 | Google·Apple 계정 인증 및 계정 기능 |
| 전화번호 | 예 | 예 | 아니요 | Google 로그인 SDK가 계정 인증 과정에서 처리할 수 있음 |
| 사용자 ID | 예 | 예 | 아니요 | 로그인, 클라우드 저장, 리더보드 |
| 게임 플레이 콘텐츠 | 예 | 예 | 아니요 | 최고 고도와 성장 데이터 저장 |
| 제품 상호 작용 | 예 | 광고 데이터는 기기 기준 | 아니요 | 앱 기능, 광고, 분석 |
| 광고 데이터 | 예 | 광고 SDK 기준 | 아니요 | 비맞춤 광고, 빈도 제한, 집계 |
| 기기 ID | 예 | 광고 SDK 기준 | 아니요 | 광고, 집계, 부정 이용 방지 |
| 대략적인 위치 | 예 | 광고 SDK 기준 | 아니요 | IP 기반 광고 제공·부정 이용 방지 |
| 충돌·성능·기타 진단 | 예 | SDK 기준 | 아니요 | 앱 기능, 분석, 오류 대응 |
| 기타 사용 데이터·기타 데이터 | 예 | Google 로그인 SDK 기준 | 아니요 | 인증 기능과 SDK 분석 |

주소·정확한 위치·연락처 목록·사진·오디오·건강·금융·결제·검색 기록은 수집하지 않는다.
고객지원 이메일은 사용자가 선택적으로 문의할 때만 발생하며 App Store의 선택적 공개
조건을 충족하는지 최종 지원 흐름을 기준으로 판단한다.

현재 생성된 iOS Xcode 프로젝트의 `GoogleSignIn 7.1.0` 개인정보 매니페스트는 이름,
이메일, 전화번호, 기타 데이터, 대략적 위치, 사용자 ID, 기기 ID, 기타 사용 데이터를
선언한다. 앱 코드가 전화번호를 읽거나 화면에 표시하지 않더라도 제3자 SDK 선언을 빠뜨리지
않는 보수적 기준으로 위 항목을 선택한다. 최종 Archive에서는 Xcode의 Privacy Report와
포함된 `PrivacyInfo.xcprivacy`를 다시 생성해 이 표와 대조한다.

### 추적 질문

- `추적에 사용되는 데이터`: 없음
- ATT 권한 요청: 하지 않음
- 근거: 모든 광고 요청에 `npa=1`을 붙이고, SDK에서 기본 활성화되는
  `PublisherFirstPartyIdEnabled`를 `false`로, 퍼블리셔 개인화 처리를 `Disabled`로
  고정한다. `NSUserTrackingUsageDescription`과 ATT 요청 코드를 넣지 않는다. iOS 빌드
  후처리와 산출물 검증은 Google Mobile Ads 설정에 남은 예전 추적 문구를 제거하고
  해당 키가 다시 생기면 빌드를 실패시킨다.

비맞춤 광고도 광고 식별자를 빈도 제한과 집계 보고에 사용할 수 있으므로 기기 ID와 광고
데이터 자체는 숨기지 않는다.

Google Mobile Ads의 범용 개인정보 매니페스트에는 맞춤 광고 경로까지 포함하는
`Device ID / Tracking` 선언이 있어, 매니페스트 자체만으로 먹점프의 실제 런타임
처리를 판정하지 않는다. 먹점프 Release는 ATT·IDFA 접근을 요청하지 않고,
퍼블리셔 1차 식별자와 개인화를 끄며 모든 요청을 `npa=1`로 제한하므로 콘솔
응답은 `추적 없음`을 사용한다. 단, 최종 Archive의 Privacy Report와 실기기 프락시
검증에서 식별자가 교차 앱 광고 측정에 사용되는 것으로 확인되면 제출하지 말고,
광고 구성을 추적 없이 작동하는 제한 광고로 바꾸거나 ATT·추적 공개를 함께 적용한다.

## Google Play — 데이터 보안

- 데이터 수집 또는 공유: `예`
- 전송 중 암호화: `예`
- 사용자가 데이터 삭제 요청 가능: `예`
- 계정 생성: `예` — Android 앱은 게스트·Google, iOS 앱은 게스트·Google·Apple
- 독립 보안 검토: 별도 인증을 받지 않았다면 `아니요`

| Play 데이터 유형 | 수집 | 공유 | 목적 |
|---|---:|---:|---|
| 사용자 ID | 예 | 아니요(뒤끝은 서비스 제공자) | 계정 관리, 앱 기능 |
| 이메일 주소 | 예 | 아니요(인증 제공자 처리) | 계정 관리 |
| 앱 활동 / 앱 상호작용 | 예 | 예(Google Ads) | 앱 기능, 광고, 분석, 부정 방지 |
| 광고 ID 등 기기 식별자 | 예 | 예(Google Ads) | 비맞춤 광고, 집계, 부정 방지 |
| 대략적인 위치(IP 기반) | 예 | 예(Google Ads) | 광고, 부정 방지 |
| 앱 성능·진단 | 예 | 예(Google Ads) | 분석, 오류 대응, 부정 방지 |
| 게임 진행 | 예 | 아니요 | 클라우드 저장, 리더보드 |

Google Mobile Ads가 자동 수집·공유하는 IP 주소, 제품 상호작용, 진단 정보, 기기·계정
식별자는 빠뜨리지 않는다. 보상형 광고는 선택 사항이지만 SDK의 데이터 처리는 지속적으로
발생할 수 있으므로 선택적 공개로 제외하지 않는다.

## 콘솔에 넣을 URL

- 개인정보처리방침 URL: `https://github.com/Team-Swooord/muk-jump/blob/main/docs/legal/privacy-policy.md`
- 계정 삭제 URL: `https://github.com/Team-Swooord/muk-jump/blob/main/docs/legal/account-deletion.md`
- 고객지원 URL: `https://github.com/Team-Swooord/muk-jump#고객지원`

로컬 파일 경로나 GitHub 편집 화면은 스토어 제출 URL로 사용하지 않는다.

## 확인 근거

- [Google Mobile Ads Unity 데이터 공개 안내](https://developers.google.com/admob/unity/privacy/play-data-disclosure):
  IP 주소, 제품 상호작용, 진단 정보, 기기·계정 식별자의 자동 수집·공유 범위를 확인한다.
- [Google UMP Unity 안내](https://developers.google.com/admob/unity/privacy): 앱 시작마다 동의
  상태를 갱신하고, 필요한 동의 화면과 개인정보 선택 재진입을 제공하며,
  `CanRequestAds()` 뒤에 광고를 요청하는 기준을 확인한다.
- [Apple App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/):
  제3자 SDK를 포함한 수집, 사용자 연결, 추적 여부와 게임 플레이 콘텐츠 공개 기준을 확인한다.

Google의 데이터 공개 문서는 현재 최신 플러그인 기준이므로, 먹점프가 사용하는 Unity
플러그인 11.4.0과 iOS 네이티브 Pod 13.7.0을 업데이트할 때 이 표를 다시 검토한다. 현재
응답은 빠뜨리는 것보다 넓게 공개하는 보수적 기준이다.
