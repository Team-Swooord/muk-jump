# Game Center 업적 — 스킨 개발 브랜치

게임 내 업적과 의상은 먹점프 프로필이 원본이다. Game Center에는 저장된 진행률만 단방향으로 전송하며 기존 뒤끝 리더보드는 그대로 사용한다. Game Center의 Apple 계정은 게임의 Apple 로그인/뒤끝 UID와 별개다. 게임 계정 삭제로 Game Center 업적을 초기화하지 않는다.

## 등록 상태

2026-09-12: 런타임·iOS GameKit 연결·저장/계정 경계 검사를 구현했다. App Store Connect 세션 만료로 **Apple 콘솔 업적 등록과 실기기 전송 검증은 아직 미완료**다. 현재 심사 중인 버전/빌드와 배포 설정은 변경하지 않았다. 아래 등록과 기기 검증이 끝나기 전 Game Center 동기화 완료 또는 출시 준비 완료로 보고하지 않는다.

## 등록 자료

Unity 메뉴 `MukJump > Export Game Center Achievement Catalog`는 `output/store/game-center-achievements/`에 catalog.json과 RGB 1024×1024 PNG를 생성한다. 기존 원먹·의상 스프라이트를 그대로 사용하며 외부 에셋을 도입하지 않는다.

- 앱: 먹점프 / App Store ID 6804614765. SHIFT와 혼동하지 않는다.
- 의상 35개: `mukjump.achievement.skin.01`~`.35`. 기존 InkSkinId 순서를 변경하지 않는다.
- 일반 업적 2개: `mukjump.achievement.wall`, `mukjump.achievement.flight3`.
- 각각 20점, 총 740점, 반복 달성 불가. 숨김 여부와 한·영·일 이름/달성 전후 설명은 catalog.json 기준.
- 숨김 업적은 잠겨 있는 동안 0%도 보내지 않으며, 달성한 뒤 100%만 전송한다.

App Store Connect 로그인 후 해당 ID의 기존 정의를 먼저 확인하여 중복 생성 없이 등록한다. 세 언어와 아이콘까지 저장하고 Game Center 사용을 다음 스킨 버전에 연결한다. 현재 제출한 버전을 취소/교체하거나 심사에 추가 제출하지 않는다.

## 필요한 실기기 확인

1. 새 게스트 튜토리얼/플레이/광고/계정 창 중 Game Center 인증창이 갑자기 끼어들지 않는지 확인한다.
2. 안전한 로비에서 Game Center 인증 후 업적을 달성하고, 게임 내 알림 1회와 Apple 진행률을 확인한다. 네이티브 업적 배너는 중복 방지를 위해 끈다.
3. 오프라인 달성 → 재실행 → 온라인 복귀에서 현재 계정의 저장된 업적만 재전송되는지 확인한다.
4. 게임 계정과 Game Center 계정을 각각 바꾸며 이전 요청 응답이 새 프로필을 변경하지 않는지 확인한다.
5. Game Center 기존 달성 업적을 로컬 의상/점수/성장으로 가져오지 않는지 확인한다.
