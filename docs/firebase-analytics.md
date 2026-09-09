# Firebase Analytics 운영 안내

## 현재 범위

- iOS·Android: 공식 Firebase Unity SDK **13.16.0** App + Analytics 설치, 게임 이벤트 연결.
- 토스 WebGL: 네이티브 Firebase를 실행하지 않는다. 토스용 웹 분석 브리지는 이번 범위가 아니다.
- Editor: 실서버 접속 없이 메모리 수신기로 테스트한다. Development Build도 기본 수집 금지.
- Firebase `MukJump` (`mukjump-3751d`, 무료 Spark) 프로젝트를 생성했고 iOS·Android 구성 파일을 `Assets/`에 연결했다.
  실제 기기 이벤트 수신·AdMob 수익 연동·DebugView 검증은 아직 미완료다. 빌드·업로드 중단 요청은 유지한다.
- 분석은 저장 서버가 아니다. 뒤끝 로그인·저장·순위 구조를 대체하거나 변경하지 않는다.

## 콘솔 연결

현재 프로젝트: [MukJump Firebase 콘솔](https://console.firebase.google.com/project/mukjump-3751d/overview)

- iOS 앱: `1:82898003095:ios:68c9a4b6d1a20133a2b5d2`
- Android 앱: `1:82898003095:android:5dec4e01abb1de1ca2b5d2`
- 두 플랫폼의 Bundle ID/package name: `com.CYSB.MukJump`
- Google Analytics는 기존 `Default Account for Firebase` 아래 새 속성을 생성하도록 설정했다.
  기존 다른 게임의 속성·Firebase 프로젝트는 변경하지 않았다. Gemini는 생성 과정에서 해제했다.
- 아래 절차는 설정을 다시 받거나 검증할 때 사용한다. 서비스 계정 비밀키는 생성하지 않았다.

1. Firebase에서 Google Analytics가 활성화된 먹점프 프로젝트를 선택한다.
2. iOS와 Android 앱을 각각 등록한다. 실제 Bundle ID/package name `com.CYSB.MukJump`와 대소문자까지 일치해야 한다.
3. 해당 앱의 `GoogleService-Info.plist`와 `google-services.json`을 **Assets 바로 아래**에 둔다.
   서비스 계정 키·Admin SDK 키는 필요 없으며 게임에 넣지 않는다.
4. Unity 메뉴 `MukJump > Store > Firebase > 설정 확인`을 실행한다.
   `MukJumpAnalyticsSettings.asset`은 ID가 아니라 플랫폼별 구성 유효성 결과만 보관한다.
5. 해당 플랫폼의 설정이 없거나 Bundle ID가 다르면 네이티브 빌드를 중단한다. 토스 빌드는 이 검사에서 제외한다.
6. AdMob 콘솔에서 해당 먹점프 앱을 Firebase에 연결한다. 기존의 다른 게임과 연결하지 않는다.

SDK는 Google 공식 [UPM 아카이브](https://developers.google.com/unity/archive#firebase) 원본을
`Packages/Firebase/`에 고정했다. App 13.16.0, Analytics 13.16.0이며 기존 EDM4U 1.2.187을 유지한다.
iOS 15·Android API 26·Xcode 26.2라는 현 프로젝트 하한을 올리지 않는다.
iOS는 CocoaPods 설치 전에 `Firebase/Analytics`를 `FirebaseAnalytics/Core`,
`Firebase/Core`를 `Firebase/CoreOnly`로 바꿔 기본 Analytics의 IDFA 의존성이 다시 들어오지 않게 한다.
AdMob 자체의 ATT/AdSupport 설정은 유지한다.

### Xcode 준비

- 다운로드한 `firebase_unity_sdk`도 13.16.0이다. 현재 App/Analytics UPM 설치와 같으므로
  `.unitypackage`를 중복 임포트하지 않는다. Auth·Firestore·Crashlytics 등은 추가하지 않는다.
- 다음 Unity iOS 내보내기에서 구성 plist의 메인 앱 리소스 포함, `-ObjC`/기존 링커 옵션 상속,
  모듈 사용, Bitcode 해제, Swift 런타임의 메인 앱 포함·UnityFramework 중첩 포함 방지를 자동 적용한다.
- 기존 iOS 15 Pod 보정·Apple 로그인 entitlement·면제 암호화 선언과 `.xcworkspace` 배포 흐름을 유지한다.
- 예전 Xcode 출력에는 새 SDK가 없으므로 이번 소스로 다시 내보낸 작업공간을 사용해야 한다.
  이번 준비 작업에서는 네이티브 빌드·Archive를 실행하지 않았으며 성공을 보장한 상태는 아니다.

## 선택과 개인정보

`설정 → 개인정보처리방침 → 플레이 분석`에서 명시적으로 켜야 수집한다.
개인정보처리방침 전문은 같은 화면에서 열 수 있다. 기본은 꺼짐이며 끄거나 dim으로 돌아가도 게임은 이용 가능하다.
기존 두루마리 전환·바깥 dim 복귀를 공유하고 한국어·영어를 지원한다.

- 분석 동의는 기기별 별도 값이며 뒤끝 클라우드 설정과 섞지 않는다. 광고 UMP·Apple ATT 동의를 분석 동의로 간주하지 않는다.
- 네이티브 시작 전 collection/consent 기본값도 꺼 둔다. Unity 초기화 전 자동 이벤트 수집을 방지한다.
- AnalyticsStorage만 사용자가 허용할 수 있다. AdStorage·AdUserData·AdPersonalization은 항상 거부한다.
- Firebase가 생성하는 설치 식별자와 SDK의 기기·앱·대략적 지역 등 기술 정보가 분석에 사용될 수 있다.
  익명 데이터 또는 개인정보 비수집이라고 표현하지 않는다.
- 계정 UUID·토스 hash·닉네임·Apple 토큰·메일·문의 코드·원문 오류·터치 좌표는 이벤트에 넣지 않는다.
  Firebase `SetUserId`도 실제 계정으로 연결하지 않는다. iOS IDFV 수집도 비활성화한다.
- 철회 시 앱의 미전송 큐·진행 중 분석 상태를 폐기하고 SDK 수집 중단/로컬 분석 데이터 초기화를 수행한다.
  이미 서버에 도착한 기록까지 즉시 삭제하는 기능은 아니다. 서버 삭제 요청은 별도 운영 절차로 처리한다.
- 연결 전 큐는 동의 후 이벤트만 최대 128개 보관한다. 설정 누락·초기화 실패·SDK 예외가 게임을 막지 않는다.
- 수집을 꺼 둔 기간의 플레이를 나중에 소급 전송하지 않는다.

주의: 기본 수집이 선택형이므로 리텐션·첫 실행·튜토리얼 통계는 **분석에 동의한 사용자 표본**이다.
모든 설치의 통계로 해석하지 않는다. 처음부터 동의하지 않은 튜토리얼 행동은 기록되지 않는다.

## 이벤트 사전

모든 직접 전송 이벤트에는 `schema_version=1`이 포함된다. 이름·파라미터는 40자 이하,
문자열 값은 100자 이하, 한 이벤트는 25개 이하 파라미터를 유지한다.

| 이벤트 | 실제 전송 시점 | 주요 값 |
|---|---|---|
| `screen_view` | 실제 UI 화면 변경, 같은 화면 갱신 제외 | `screen_name`, `screen_class` |
| `level_start` | 새 정상 판 시작 1회 | `level_name=endless`, `growth_level` |
| `first_stroke` | 한 판의 첫 유효 먹선 | 없음 |
| `height_milestone` | 판당 각 고도 첫 통과 | `height_m` 50/100/250/500/750/1000/1500/2000/3000/5000/10000 |
| `item_first_pickup` | 판당 아이템 종류별 첫 실제 픽업 | `item_name` inkdrop/goldenbrush/inkshield/inkclone |
| `run_death` | 마지막 개체가 죽어 결과창에 진입 | 판 요약 |
| `level_end` | 실제 결과 정산 완료 또는 일시정지에서 판 포기 | 판 요약, `end_reason=death/abandon`, `success=0` |
| `post_score` | 포기가 아닌 최종 결과 | `score`, `level_name`, `new_best` |
| `game_pause`, `game_resume` | 사용자 일시정지/재개 | 없음 |
| `revive` | 실제 광고 보상 부활 적용 완료 | 없음 |
| `tutorial_begin` | 첫 실행 설명 시작 | 없음 |
| `tutorial_step` | 설명의 각 장 최초 도달 | `step` 1~3 |
| `tutorial_complete` | 마지막 설명 완료 | `progress_saved` |
| `tutorial_skip` | 두 번 확인한 건너뛰기 완료 | `progress_saved` |
| `tutorial_exit` | 완료 없이 설명이 중단됨 | 없음 |
| `tutorial_review` | 설정에서 설명을 다시 봄 | `step` |
| `earn_virtual_currency` | 거리 보상 또는 초기화 환급 저장 성공 | `virtual_currency_name=inklight`, `value`, `balance`, `source=distance/growth_refund` |
| `spend_virtual_currency` | 성장 구매 저장 성공 | `virtual_currency_name`, `item_name`, `value`, `balance` |
| `level_up` | 성장 구매 성공 | `character`(성장 종류), `level` 1~8 |
| `growth_reset` | 실제 구매 성장 환급 성공 | 환급량/잔액 |
| `setting_change` | 음악·효과음·진동·언어 선택 | `setting`, `setting_value` (토글 0/1, 언어 0=한국어·1=영어) |
| `ad_flow` | 배너/보상 광고 로드·표시·클릭·보상·닫기 콜백 | `ad_format`, `placement`, `stage` |
| `account_action` | 게스트/계정 인증 성공, Apple 연동/삭제 요청 | `action`, `result` |
| `account_state` | 실제 계정 런타임 단계 변경 | `phase`(enum), 원문 메시지 없음 |
| `easter_egg` | 열 번째 탭으로 밤/낮 전환을 시작 | `theme=night/day` |

`ad_flow.stage`: `loadrequested`, `loaded`, `loadfailed`, `showrequested`, `opened`, `clicked`,
`rewardearned`, `closed`, `failed`. 배너는 로드 요청/성공/실패, 나머지 표시는 보상형에 적용한다.
자동 재시도도 실제 요청 단위로 기록하므로 unique 사용자 수와 요청 수를 구분한다.
보상형을 보상 없이 닫는 것은 `closed`이며 SDK 표시 실패와 구별한다.

판 요약: `score`, `duration_seconds`(활성 게임 시간), `stroke_count`, `ink_length_m`,
`fall_damage_count`, `obstacle_damage_count`, `death_count`, `death_cause`(최종 개체: fall/obstacle/other),
`revive_count`, `peak_swarm`, `inkdrop_count`, `goldenbrush_count`, `shield_count`, `clone_count`.

## 중복·볼륨 규칙

- 자동 점프·매 프레임·매 포인터 이동을 전송하지 않는다. 먹선·반복 픽업·피격은 정수 합계로만 모은다.
- 광고 부활은 같은 판이다. 새 `level_start`를 만들지 않고 `run_death` 후 `revive`로 이어 간다.
- `level_end`는 판 최종 종료이고 `run_death`는 광고 부활 가능한 중간 결과일 수 있다. 둘을 합산하지 않는다.
- 끝없는 모드에 클리어 성공은 없으므로 `level_end.success`는 0, 실력 지표는 고도·시간이다.
- 재화는 저장 성공 이후 전송한다. 저장 실패·중복 run 정산·디버그 재화는 수익/소비를 만들지 않는다.
- 저장 복구로 늦게 확정된 재화는 실제 발생 이벤트를 기록할 수 있지만, 실행 중이지 않은 판의 시작/종료를 꾸며 만들지 않는다.
- 이벤트는 회계 장부가 아니다. 강제 종료·네트워크/SDK 실패·도메인 리로드에서는 일부 분석 기록이 누락될 수 있다.
  백그라운드 진입을 판 포기로 단정하지 않고 SDK의 세션/engagement와 마지막 도달 이벤트로 이탈을 분석한다.

## 자동 이벤트와 수익

`first_open`, `session_start`, `user_engagement`, `app_update` 같은 SDK 자동 이벤트를 직접 다시 보내지 않는다.
AdMob 연결 후 `ad_impression`·광고 수익·클릭 등도 Firebase/AdMob 자동 집계를 사용한다.
직접 만든 `ad_flow`는 보상 퍼널 디버깅용이며 자동 광고 노출 수와 합산하지 않는다.
현재 IAP·상점 결제·공유 기능이 없으므로 가짜 `purchase`, `in_app_purchase`, `share` 이벤트는 넣지 않았다.

## DebugView·배포 점검

- 에디터: `Temp/MukJumpRunAllTests.request`에 `analytics-only` 또는 `analytics-regression` 작성 후 Assets Refresh.
- 네이티브 개발 검증: 설정 에셋의 `allowDevelopmentCollection`을 테스트용 Firebase 프로젝트에서만 켠다.
- iOS: Xcode Run 인수 `-FIRDebugEnabled`. 완료 후 인수 제거 또는 `-FIRDebugDisabled`.
- Android: `adb shell setprop debug.firebase.analytics.app com.CYSB.MukJump`; 완료 후 `.none.`으로 복구.
- 실기기에서 분석 끄기 → 켜기 → 한 판 → 성장 구매 → 광고 부활 → 분석 끄기를 검사한다.
  꺼진 동안 전송 없음, 재동의 시 과거 큐 없음, 부활 뒤 시작 중복 없음, SDK 자동 수익과 중복 없음 확인.
- Firebase 콘솔 DebugView에서 이벤트 수신을 직접 확인해야 서버 연결 완료다. 일반 보고서는 즉시 갱신되지 않을 수 있다.
- GA4 사용자 지정 정의에 `screen_name`, `end_reason`, `death_cause`, `source`, `setting`, `phase`,
  `ad_format`, `placement`, `stage`, `growth_level` 등 필요한 항목만 등록한다.
  `duration_seconds`, `stroke_count`, `peak_swarm`, `ink_length_m` 등은 이벤트 범위 측정항목으로 등록한다.
- 공개 개인정보처리방침과 스토어 데이터 수집 답변을 최종 SDK/실제 콘솔 보관기간에 맞춰 검토·게시해야 한다.
  저장소 문서 수정이 현재 공개 정책 URL을 자동으로 바꾸지는 않는다.
- 기존 UMP 개인정보 재선택 진입점 누락은 별도 출시 차단 사항이며 이번 분석 UI는 UMP를 대체하지 않는다.

## 설계 근거

- [Firebase Unity 설치](https://firebase.google.com/docs/unity/setup), [공식 13.16.0 릴리스](https://github.com/firebase/firebase-unity-sdk/releases/tag/v13.16.0)
- [Google 게임 추천 이벤트](https://support.google.com/analytics/answer/9267735), [Unity 이벤트 기록](https://firebase.google.com/docs/analytics/unity/events)
- [iOS 수집 제어](https://firebase.google.com/docs/analytics/ios/configure-data-collection), [Android 수집 제어](https://firebase.google.com/docs/analytics/android/configure-data-collection)
- [광고 수익 자동 집계](https://firebase.google.com/docs/analytics/measure-ad-revenue), [DebugView](https://firebase.google.com/docs/analytics/debugview)
- [게임 개발자들의 튜토리얼 이탈 분석 사례](https://www.reddit.com/r/gamedev/comments/1bm49yr/):
  단계·완료·건너뛰기를 구분하는 참고 사례. 업계 전체의 이벤트 빈도 순위나 성공 보장 근거로 사용하지 않는다.
