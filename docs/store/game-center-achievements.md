# Game Center 업적 — 스킨 개발 브랜치

게임 내 업적과 의상은 먹점프 프로필이 원본이다. Game Center에는 저장된 진행률만 단방향으로 전송하며 기존 뒤끝 리더보드는 그대로 사용한다. Game Center의 Apple 계정은 게임의 Apple 로그인/뒤끝 UID와 별개다. 게임 계정 삭제로 Game Center 업적을 초기화하지 않는다.

## 등록 상태

2026-09-13: 사용자가 Game Center 업적을 의상 해금과 1:1로 요청했다. 기본 먹을 제외한 45개만 등록·전송하도록 연결했다. App Store Connect 세션 만료로 **Apple 콘솔 업적 등록과 실기기 전송 검증은 아직 미완료**다. 현재 심사 중인 버전/빌드와 배포 설정은 변경하지 않았다. 아래 등록과 기기 검증이 끝나기 전 Game Center 동기화 완료 또는 출시 준비 완료로 보고하지 않는다.

## 등록 자료

Unity 메뉴 `MukJump > Export Game Center Achievement Catalog`는 `output/store/game-center-achievements/`에 catalog.json과 RGB 1024×1024 PNG를 생성한다. 기존 원먹·의상 스프라이트를 그대로 사용하며 외부 에셋을 도입하지 않는다.
용꼬리·날개를 포함한 전체 의상 외곽으로 크기와 중심을 계산해 원형 배지에서도 잘리지 않게 한다.
아이콘과 점수 규격은 [Apple 업적 등록 기준](https://developer.apple.com/help/app-store-connect/reference/game-center/achievements/)을 따른다.

- 앱: 먹점프 / App Store ID 6804614765. SHIFT와 혼동하지 않는다.
- 의상 해금 45개: `mukjump.achievement.skin.01`~`.45`. 기존 ID는 재배열하지 않는다.
- 기본 먹은 처음부터 보유하므로 업적을 만들지 않는다. 해금 보상이 없는 `mukjump.achievement.wall`, `mukjump.achievement.flight3`도 Apple 등록·전송에서 제외한다. 게임 내 기존 알림과 저장 비트는 유지한다.
- 각각 20점, 총 900점, 반복 달성 불가. 숨김 여부와 한·영·일 이름/달성 전후 설명은 catalog.json 기준.
- 숨김 업적은 잠겨 있는 동안 0%도 보내지 않으며, 달성한 뒤 100%만 전송한다.

App Store Connect 로그인 후 해당 ID의 기존 정의를 먼저 확인하여 중복 생성 없이 등록한다. 세 언어와 아이콘까지 저장하고 Game Center 사용을 다음 스킨 버전에 연결한다. 현재 제출한 버전을 취소/교체하거나 심사에 추가 제출하지 않는다.

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
