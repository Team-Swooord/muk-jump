# Game Center 업적 — 스킨 개발 브랜치

게임 내 업적과 의상은 먹점프 프로필이 원본이다. Game Center에는 저장된 진행률만 단방향으로 전송하며 기존 뒤끝 리더보드는 그대로 사용한다. Game Center의 Apple 계정은 게임의 Apple 로그인/뒤끝 UID와 별개다. 게임 계정 삭제로 Game Center 업적을 초기화하지 않는다.

## 등록 상태

2026-09-13: 기본 먹을 제외한 의상 해금 60개를 [먹점프 App Store Connect](https://appstoreconnect.apple.com/apps/6804614765/distribution/gamecenter)에 등록했다. `mukjump.achievement.skin.01`~`.60`의 중복·누락이 없으며 모두 **제출 준비 중**이다.

- 한국어·영어(미국)·일본어 이름과 달성 전후 설명, 의상 아이콘을 각각 저장했다.
- 60개 상세 페이지를 다시 열어 현지화 180개와 PNG 처리 완료, 저장 완료를 확인했다.
- 총 975점(Apple 화면에서 25점 남음), 숨김 21개, 반복 달성 불가 설정을 확인했다.
- 기존 Game Center 순위표는 변경하지 않았다. 게임의 뒤끝 리더보드·계정 데이터와 현재 심사본 1.0.0(29)도 변경하지 않았다.

같은 날 로비 쓰리쿠션 보상 `mukjump.achievement.skin.61`([쓰리쿠션 당구공 상세](https://appstoreconnect.apple.com/apps/6804614765/distribution/gamecenter/achievements/99759c33-bf1a-4ea7-aa57-9f1501238c33))을 추가했다.
숨김·반복 불가·5점으로 저장했고 한·영(미국)·일 이름, 달성 전후 설명과 동일한 RGB 의상 아이콘 3개를 저장했다. 총 의상 업적은 61개, 현지화 183개, 980점(20점 남음), 숨김 22개다. 이번에는 신규 1개의 상세/현지화와 저장 상태를 확인했으며 기존 60개를 다시 전수 검사하지 않았다. 상태는 **제출 준비 중**이며 현재 심사본에는 추가하지 않았다.

**콘솔 등록은 완료됐지만 실기기 전송 검증과 다음 스킨 버전의 업적 심사 제출은 아직 미완료**다. 아래 기기 검증이 끝나기 전 Game Center 동기화 완료 또는 출시 준비 완료로 보고하지 않는다.

## 등록 자료

Unity 메뉴 `MukJump > Export Game Center Achievement Catalog`는 `output/store/game-center-achievements/`에 catalog.json과 RGB 1024×1024 PNG를 생성한다. 기존 원먹·의상 스프라이트를 그대로 사용하며 외부 에셋을 도입하지 않는다.
내보내기는 Apple 콘솔을 조회하지 않는다. 생성 파일의 `registrationStatus`만으로 재등록 여부를 판단하지 말고 이 문서와 실제 콘솔의 동일 ID를 먼저 확인한다.
용꼬리·날개를 포함한 전체 의상 외곽으로 크기와 중심을 계산해 원형 배지에서도 잘리지 않게 한다.
아이콘과 점수 규격은 [Apple 업적 등록 기준](https://developer.apple.com/help/app-store-connect/reference/game-center/achievements/)을 따른다.

- 앱: 먹점프 / App Store ID 6804614765. SHIFT와 혼동하지 않는다.
- 의상 해금 61개: `mukjump.achievement.skin.01`~`.61`. 기존 ID는 재배열하지 않는다.
- 기본 먹은 처음부터 보유하므로 업적을 만들지 않는다. 해금 보상이 없는 `mukjump.achievement.wall`, `mukjump.achievement.flight3`도 Apple 등록·전송에서 제외한다. 게임 내 기존 알림과 저장 비트는 유지한다.
- 기존 45개는 각각 20점 유지, 십이지신·신화 15개와 쓰리쿠션 1개는 각각 5점으로 총 980점이다. 반복 달성 불가. 숨김 여부와 한·영·일 이름/달성 전후 설명은 catalog.json 기준.
- 숨김 업적은 잠겨 있는 동안 0%도 보내지 않으며, 달성한 뒤 100%만 전송한다.

이후 변경은 등록된 동일 ID를 갱신하며 중복 생성하지 않는다. Game Center 사용과 업적 심사 제출은 다음 스킨 버전에 연결한다. 현재 제출한 버전을 취소/교체하거나 심사에 추가 제출하지 않는다.

## 필요한 실기기 확인

1. 새 게스트 튜토리얼/플레이/광고/계정 창 중 Game Center 인증창이 갑자기 끼어들지 않는지 확인한다.
2. 안전한 로비에서 Game Center 인증 후 업적을 달성하고, 게임 내 알림 1회와 Apple 진행률을 확인한다. 네이티브 업적 배너는 중복 방지를 위해 끈다.
3. 오프라인 달성 → 재실행 → 온라인 복귀에서 현재 계정의 저장된 업적만 재전송되는지 확인한다.
4. 게임 계정과 Game Center 계정을 각각 바꾸며 이전 요청 응답이 새 프로필을 변경하지 않는지 확인한다.
5. Game Center 기존 달성 업적을 로컬 의상/점수/성장으로 가져오지 않는지 확인한다.
# 내부 TestFlight 의상 시험 창

`MukJump/Release/Build iOS Skin Debug TestFlight (Preserve Accounts)`는 일회성
`MUKJUMP_SKIN_DEBUG`와 테스트 광고를 포함한 비 Development iOS 빌드다.
로비 왼쪽 아래 DEBUG → 일반/히든 해금 연출, 전체 의상 해금 ON/OFF, 닫기를 제공한다.
전체 해금과 시험 착용은 계정별 메모리에서만 유지하며 앱 재실행·계정 교체·OFF 때 폐기한다.
시험 중 점수·성장·업적·리더보드는 저장하지 않고, 실제 해금과 장착 저장을 덮어쓰지 않는다.
Game Center 동기화는 실제 저장된 업적만 사용한다.

이 빌드는 **ExportOptions와 UploadOptions 모두 `testFlightInternalTestingOnly=true`**로 배포한다.
정식 심사본 1.0.0(29)는 변경하지 않는다. 28번부터 정식 심사 전환된 기록 보존 정책을
따르므로 서버 전원 삭제와 빌드별 로컬 초기화는 다시 켜지 않는다.
정식 App Store 내보내기에서는 디버그 심볼을 거부하며 시험 창을 생성하지 않는다.
