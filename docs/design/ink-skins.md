# 먹 장식과 자동 능력

개발 브랜치: `codex/ink-skins`. 심사 제출한 main/1.0.0(29)에는 반영하지 않는다.

## 적용 범위

- 로비의 성장 아래, 옵션 위에 꾸미기. 기존 붓 전환·가운데 한지 두루마리·안전영역을 재사용한다.
- 원래 먹: 장식/추가 능력 없음.
- 바람 갓: 일반 자동 점프의 정점 직전 한 번 더 도약(첫 수직 속도의 40%). 새 착지마다 재충전.
- 거미탈: 화면 좌우 벽에 붙어 기존 자동 점프 충전 뒤 화면 안쪽으로 도약한다. 거미줄 발사/공격 기능은 없다.
- 모두 무료. 구입·해금·재화 소비·광고·서버 스키마 변경은 없다.
- 기존 먹 몸체 프레임·충돌 크기·성장/점수 정산은 유지한다. 한 개의 장식 렌더러를 본체와 각 분신에서 재사용한다.
- 먹물방울·풍맥 특수 상승에는 이단 점프를 중복 적용하지 않는다. 사망/로비에서 장식만 남지 않는다.
- 선택은 미리보기, 착용 버튼은 저장. 저장 실패는 이전 값으로 복원하고 오류를 표시한다.
- 착용은 기기 설정이며 계정에 종속되거나 서버로 업로드되지 않는다. 재설치 후 복원은 보장하지 않는다.
- 판 시작에만 고정하고 판중 변경은 거부한다. 분신과 광고 부활은 같은 판의 장착값을 사용한다.
- 한·영·일 번역과 기존 언어별 서체를 사용한다.

## 원화

내장 이미지 생성 도구로 이 프로젝트용 원화를 새로 제작했다. 외부 캐릭터/상표 원화는 사용하지 않았다.
Unity 씬 빌더에서 Sprite 단일 이미지로 가져오며, 모바일 최대 512px·밉맵 없음·읽기 불가로 설정한다.

- `Assets/Resources/MukJump/Skins/skin_gat_v1.png`: 정면의 낮은 검정 갓, 넓은 챙, 짧은 붉은 끈, 수묵 갈필, 투명 배경.
- `Assets/Resources/MukJump/Skins/skin_spider_mask_v1.png`: 한지색 거미탈, 먹선 거미 다리 무늬, 작은 붉은 거미, 투명 눈 구멍/배경. 몸체·눈 자체는 포함하지 않는다.

프롬프트 요약: “Original Eastern ink-wash accessory sprite; charcoal #1C1B1A, parchment #EAE3D2,
small #AE1C3C accent, front view, isolated accessory only, true alpha transparency, no text,
no body, no branding.” 갓은 넓고 낮은 챙과 짧은 끈, 탈은 타원형 투명 눈 구멍을 지정했다.
탈은 마지막에 background-extraction 편집으로 외곽/눈 구멍의 투명을 다시 지정했다.

## 검증 범위

`InkSkinTests`: 선택 저장·실패 복원·잘못된 ID 폴백, 미리보기 취소, 판중 변경 차단,
광고 부활/다음 판 스냅샷, 기본 점프 유지, 이단 점프 1회/특수 상승 취소,
거미탈 벽 접착/자동 이탈, 본체 보존/분신 렌더러 소유권, 3개 언어와 문구 크기.
실제 휴대폰 플레이·최종 밸런스·새 스킨을 포함한 스토어 업로드는 별도다.

### 확인 결과 (2026-09-12)

- 스킨 17개, 실제 Main 플레이/캡처 1개와 관련 로비·성장 검사 합계 42개 통과.
  일시정지 중 이단 도약 미소모, 장식 위 체력 바, 사망 시 장식 숨김,
  실제 분신 생성 후 동일 스킨 유지까지 포함한다.
- iOS Player 스크립트 36개 어셈블리 컴파일 성공. Xcode 빌드/기기 검증을 뜻하지 않는다.
- 생성 Main 씬 감사: 소스 최신, 누락 스크립트 0, Player 안전성 문제 0.
- 한·영·일 꾸미기 화면을 실제 Unity Game 뷰에서 확인했다. 캡처는 645×924 에디터 화면이며
  실제 iPhone 스크린샷 또는 스토어 제출용 이미지가 아니다.
- 결과/캡처: `output/qa/ink-skins/verified-tests.xml`, `scene-audit.json`,
  `player-compile.log`, `lobby.png`, `wardrobe-Korean.png`, `wardrobe-English.png`,
  `wardrobe-Japanese.png`, `spider-player.png`.
- 중간에 포함했던 기존 전체 `LobbyMenuTests`에는 리더보드 새로고침 버튼이 없어야 한다는
  오래된 단언(`OptionsUsesCalmHanjiHierarchyWithOnlyPrimaryActionInInk`)이 실패했다.
  현재 main은 이미 그 버튼을 생성한다. 스킨과 무관한 기존 UI/검사는 이번에 변경하지 않았고,
  최종 검사는 스킨의 메뉴 배치·화면 전환·성장 연결 범위로 한정했다.
- 배치 에디터 Game 뷰가 출력하는 memoryless depth load/store 경고는 남는다.
  스킨 코드 컴파일 오류나 실행 예외는 없으며 전수 검사 완료를 주장하지 않는다.

UI/uGUI 스킬에 따라 기존 Canvas·한지 UI를 재사용하고, Localization 스킬에 따라
기존 번역 테이블과 언어별 폰트를 유지했다. Unity CLI 스킬의 에디터/Player 검증 지침을
적용하되 반복 키체인 팝업을 피하려고 직접 Editor 실행을 사용했다.
이미지 생성 스킬로 새 원화를 생성하고 투명 배경을 편집했다.
전체 생성/편집 프롬프트는 [원화 프롬프트](ink-skins-art-prompts.md)에 있다.
