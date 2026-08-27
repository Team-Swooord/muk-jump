# 먹점프 (MukJump)

> 선 하나가 발판이 되고, 발판 하나가 그림이 된다.

먹점프는 자동으로 뛰는 작은 먹방울을 위해 손가락으로 발판을 그리는 세로형 수묵
드로잉 클라이밍 게임입니다. 캐릭터를 직접 움직이는 대신 붓 한 획의 위치, 기울기,
길이로 다음 점프를 설계하고 먹떼와 함께 더 높은 산수화 세계에 도전합니다.

## 게임 소개

| 항목 | 내용 |
|---|---|
| 장르 | 캐주얼 아케이드 · 드로잉 플랫포머 |
| 목표 | 살아 있는 먹방울을 지키며 최고 고도 갱신 |
| 조작 | 화면을 드래그해 임시 발판 생성 |
| 플레이 시간 | 한 판 1~3분, 반복 도전형 |
| 화면 | 세로 풀스크린 |
| 배포 대상 | Apple App Store · Google Play · Apps in Toss |

## 플레이 방법

1. `시작`을 누르면 먹방울이 약 1초 주기로 자동 점프합니다.
2. 착지 지점을 예상해 화면에 선을 그립니다.
3. 발판의 기울기와 길이로 다음 점프의 방향과 힘을 바꿉니다.
4. 먹 게이지, 바람, 장애물과 아이템을 활용해 더 높이 올라갑니다.
5. 모든 먹방울이 쓰러지면 고도와 먹빛을 정산하고 다시 도전합니다.

그린 발판은 아래에서 통과할 수 있고, 내려올 때 윗면에만 착지합니다. 발판은 잠시
유지된 뒤 처음 그린 방향부터 마르듯 사라지므로 짧고 정확한 획을 이어 그리는 것이
핵심입니다.

## 주요 기능

- 손가락 궤적을 다듬어 실제 `EdgeCollider2D` 발판으로 만드는 드로잉 시스템
- 발판의 각도, 길이, 표면 방향을 반영하는 자동 점프 물리
- 먹물방울, 황금 붓, 먹 방어막, 먹분신의 네 가지 아이템
- 이동 먹가시, 낙묵석, 어린 용, 먹해태와 고도별 바람·날씨 변화
- 최대 24마리까지 늘어나는 먹떼와 개체별 체력·방어막
- 먹빛으로 생존, 도약, 먹 운용을 강화하는 39개 영구 성장 먹나무
- 고도에 따라 이어지는 일곱 종류의 수묵 배경과 무한 순환 구간
- BGM·효과음 설정, 일시정지, Safe Area, 백그라운드 정지·복귀 지원
- 메인 로비 적응형 배너와 게임오버 선택형 광고 부활
- 최고 기록과 영구 성장 데이터를 기기에 저장하고 선택적으로 뒤끝 계정에 동기화
- 게스트·Google·Apple 로그인, 전체 최고 고도 TOP 10, 앱 안의 계정 삭제

플레이에 별도 계정이나 API 키는 필요하지 않습니다. 첫 실행은 로컬 또는 뒤끝 게스트로
즉시 열리고 Google·Apple 연결은 선택 사항입니다. 발판의 수묵 표현은 로컬 스타일러로
처리되어 네트워크 연결 없이도 코어 게임이 동작합니다.

## 개발 환경

- Unity `6000.5.9f1`
- Universal Render Pipeline 2D `17.5.0`
- Unity Input System `1.20.0`
- Google Mobile Ads Unity Plugin `11.4.0` (iOS·Android)
- Unity iOS 14 Advertising Support `1.0.1` (ATT)
- BACKND Base SDK `5.18.13`와 공식 Google·Apple 로그인 툴킷
- Sign in with Apple Unity `1.5.0`
- iOS · Android, 9:16 세로 화면

### 로컬 실행

```bash
git clone https://github.com/Team-Swooord/muk-jump.git
```

1. Unity Hub에 Unity `6000.5.9f1`을 설치합니다.
2. 저장소 루트를 Unity 프로젝트로 엽니다.
3. `Assets/Scenes/Main.unity`를 열고 Play를 누릅니다.
4. Game View를 9:16 세로 비율로 맞춥니다.

메인 씬은 에디터 빌더가 관리합니다. 씬을 다시 생성해야 할 때는 Unity 메뉴에서
`MukJump > Build Main Scene`을 실행합니다.

### iOS App Store 빌드

1. Unity Hub에서 `6000.5.9f1`의 **iOS Build Support**를 추가합니다.
2. `MukJump > Release > Configure Mobile Store Settings`를 실행합니다.
3. `MukJump > Store > Google Ads > 설정 만들기 및 SDK 동기화`를 실행하고 먹점프
   전용 AdMob 앱 ID·배너·보상형 단위를 입력합니다. Development Build는 공식 테스트
   ID를 강제로 사용하며, 운영값이 검증되지 않은 Release Build는 중단됩니다.
4. [뒤끝 콘솔 설정](docs/backend-console-setup.md)에 따라 먹점프 전용 앱·테이블·리더보드를
   만든 뒤 `MukJump > Store > Backend > Validate Release Setup`을 통과시킵니다.
5. `MukJump > Release > Validate App Store Readiness`로 씬·아이콘·빌드 모듈을 검사합니다.
6. `MukJump > Release > Build iOS Xcode Project`로 Xcode 프로젝트를 생성합니다.
7. 생성된 `output/ios/MukJump/Unity-iPhone.xcworkspace`를 Xcode에서 엽니다.
   CocoaPods를 사용하는 빌드이므로 `Unity-iPhone.xcodeproj`를 직접 열면 Google 로그인과
   광고 헤더를 찾지 못합니다.
8. 실기기 Archive 검증 후 App Store Connect에 업로드합니다.

기본 번들 ID는 `com.CYSB.MukJump`, Apple Team ID는 `8AU359WZZ2`, 버전은
`1.0.0`, 빌드 번호는 `1`입니다.
환경 변수 `MUKJUMP_BUNDLE_ID`, `MUKJUMP_APP_VERSION`,
`MUKJUMP_IOS_BUILD_NUMBER`, `MUKJUMP_APPLE_TEAM_ID`로 빌드 전에 덮어쓸 수 있습니다. 배포용 Xcode 프로젝트는
Apple의 현재 제출 요건에 맞는 Xcode와 iOS SDK로 Archive해야 합니다.

뒤끝 콘솔값을 입력하기 전에도 네이티브 코드와 CocoaPods 컴파일을 확인하려면
`MukJump > Release > Build iOS Local Validation Project`를 실행합니다. 이 산출물은
`output/ios/MukJumpValidation/Unity-iPhone.xcworkspace`에 생성되며 검증용일 뿐 스토어에
업로드할 수 없습니다.

### Google Play 빌드

1. Unity Hub에서 `6000.5.9f1`의 **Android Build Support**, Android SDK & NDK,
   OpenJDK를 설치합니다.
2. `MukJump > Release > Configure Mobile Store Settings`를 실행합니다. 빌드는 ARM64,
   최소 Android 8(API 26), 대상 Android 16(API 36)으로 고정됩니다.
3. 뒤끝·Google 로그인·AdMob 운영 설정을 완료합니다.
4. keystore 비밀값을 저장소가 아닌 현재 셸의 환경 변수로 설정합니다.

```bash
export MUKJUMP_ANDROID_KEYSTORE_PATH="/absolute/path/mukjump.keystore"
export MUKJUMP_ANDROID_KEYSTORE_PASS="..."
export MUKJUMP_ANDROID_KEY_ALIAS="mukjump"
export MUKJUMP_ANDROID_KEY_ALIAS_PASS="..."
```

5. `MukJump > Release > Validate Google Play Readiness`를 통과시킵니다.
6. `MukJump > Release > Build Android App Bundle`을 실행하면
   `output/android/MukJump.aab`이 생성됩니다.

뒤끝 운영값이나 keystore를 넣기 전에는 `Build Android Local Validation APK`로 Google
공식 테스트 광고가 포함된 개발용 APK만 만들 수 있습니다. 이 APK는 Play Console에
업로드하지 않습니다. 2026년 8월 31일부터 새 앱과 업데이트는 API 36 이상을 요구하는
[Google Play 공식 기준](https://developer.android.com/google/play/requirements/target-sdk)에
맞췄습니다.

### Apps in Toss 빌드

Apps in Toss는 네이티브 앱과 별개의 WebGL 배포 타깃입니다. 일반 서비스 출시에도
공식 Unity SDK를 사용하며, Unity 메뉴 `AIT > Configuration`의 앱 ID는 콘솔의
`appName`인 `muk-jump`와 정확히 일치해야 합니다. `MukJump > Release > Configure Apps
in Toss Settings`에서 표시 이름·색상·Production 프로필을 맞춘 뒤, `Build Apps in Toss
Size Probe`로 용량을 검사하고 `Build Apps in Toss Package`로 `.ait` 파일을 만듭니다.
패키징 뒤에는 원래 iOS 빌드 타깃으로 자동 복귀합니다.

Apps in Toss 빌드에서는 뒤끝 네이티브 SDK를 제외하고 공식 게임센터에 완료된 정상 판의
최고 고도만 제출합니다. 광고도 토스 공식 배너·보상형 API를 사용합니다. 2026-08-27
Production 사전 빌드 용량은 **32.57MB/100MB**였으며, 공개 HTTPS 아이콘 URL과 먹점프
전용 토스 배너·보상형 광고 그룹 ID 입력 후 최종 `.ait`를 생성해야 합니다.

## 프로젝트 구조

```text
Assets/
├─ Art/                 캐릭터·배경·UI 아트
├─ Editor/              씬 빌더·스토어 빌드 설정·에디터 테스트
├─ Resources/MukJump/   런타임 배경·UI·장애물·오디오
├─ Scenes/Main.unity    실행 씬
└─ Scripts/             게임플레이 코드

docs/
├─ project-brief.md     게임 기획과 구현 현황
├─ architecture.md      시스템 경계와 생명주기
└─ VFX/                 모바일 VFX 기준과 적용 내역
```

## 기술 문서

- [프로젝트 브리핑](docs/project-brief.md)
- [게임 아키텍처](docs/architecture.md)
- [영구 성장 설계](docs/design/permanent-growth-trait-tree-v3.md)
- [모바일 VFX 구현 기준](docs/VFX/PROJECT_IMPLEMENTATION.md)
- [광고 배치와 안전 규칙](docs/design/monetization-plan.md)
- [출시 직전 질문표](docs/release-readiness-checklist.md)
- [뒤끝 출시 콘솔 설정](docs/backend-console-setup.md)
- [개인정보처리방침 원문](docs/legal/privacy-policy.md)
- [계정 및 데이터 삭제 안내](docs/legal/account-deletion.md)
- [스토어 개인정보 응답 기준](docs/store/privacy-disclosures.md)
- [App Store·Google Play 등록 문구](docs/store/listing-metadata.md)
- [스토어 심사 메모](docs/store/app-review-notes.md)
- [출시 후 운영 기준](docs/store/release-operations.md)
- [외부 콘솔 최종 인계표](docs/store/external-console-handoff.md)
- [외부 에셋 고지](THIRD_PARTY_NOTICES.md)

## 개발팀

Team-Swooord · 김승연 / 최성빈

외부 에셋의 출처와 재배포 조건은 `THIRD_PARTY_NOTICES.md`와 동봉된 라이선스 원문에서
확인할 수 있습니다.

## 고객지원

게임 실행, 결제, 광고 또는 저장 데이터와 관련된 문의와 의견은
`cysbandcs@gmail.com`으로 보내주세요.
