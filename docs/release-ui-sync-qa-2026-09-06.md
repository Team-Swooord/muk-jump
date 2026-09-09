# 먹점프 UI·동기화·TestFlight 검증

상태: 진행 중. 출시 승인 또는 전체 테스트 통과 문서가 아니다.
주 작업: `01a02807-53b4-7300-82fd-74057eea1a57`
읽기 전용 독립 검토: `01a072dd-363c-76f1-8242-a26297e99e90`

## 절차와 증거 기준

주 작업 검증 → 독립 검토의 승인/수정 필요/증거 부족 → 주 작업 수정 → 재검증 → 재검토.
검토자의 승인은 사용자 권한을 대신하지 않는다. 파일 수정자는 주 작업 한 명이다.
각 항목은 정적 코드, 자동 테스트, 실제 UI, 실기기/서버 왕복을 구분한다.
이전 날짜 테스트나 DLL을 현재 변경의 통과 증거로 쓰지 않는다. 미확인 항목을 통과로 바꾸지 않는다.
GUI 직접 조작은 프로젝트 규칙에 따라 이번 작업에 대한 사용자 확인을 요청한 상태다.
기존 수백 개 변경과 staged 파일은 보존한다. 이번 요청에는 커밋/push가 포함되지 않았다.

## 사용자 의도에 따른 UI 체크리스트

| ID | 검증 대상 | 통과 기준 | 상태 |
| --- | --- | --- | --- |
| U01 | 메인 | 기존 검정 붓 버튼 유지, 중앙 정렬, 최고 기록 탭으로 순위, 입력이 게임으로 누출되지 않음 | 대기 |
| U02 | 성장 | 제목 성장만, 위쪽 작은 초기화 2회 확인, 비용은 키우기 위, 큰 이름/요약/진행 먹방울, 한지 버튼 | 대기 |
| U03 | 옵션 | 한지 두루마리, BGM/SFX/진동 및 아이콘, 한국어/메일/튜토리얼/순위 배치, 불필요한 안내 제거 | 대기 |
| U04 | 옵션 제거 항목 | DEBUG, 광고 개인정보, 움직임 줄이기, 소리·화면 진입 제거; 미지원 기능 목업 없음 | 대기 |
| U05 | 약관·닫기 | 정책 링크 맨 아래 좌우, 닫기는 종이 바깥 아래, 유효 URL·터치 영역 | 대기 |
| U06 | 공통 모달 | 젖은 한지 재질, 열기/역순 닫기, 무한 펄럭임 없음, 전환 중 연타·재열기·입력 잠금 정상 | 대기 |
| U07 | dim | 일반 팝업 바깥 탭 닫기, 안쪽 빈 곳·드래그 제외, 기록 충돌/필수 안내를 무심코 폐기하지 않음 | 대기 |
| U08 | 결과 | 제목·이번/최고 고도 가운데 Bold, 부활은 광고 시청하고 부활하기만, 반복 정산 설명 없음 | 대기 |
| U09 | 튜토리얼 | 처음/옵션에서 내용·배치 일치, 이전/다음/닫기/건너뛰기, 월드 정지와 복귀 | 대기 |
| U10 | HUD | 먹 게이지와 붓 연결, 점수/풍향/체력 가독성, 노치·홈 인디케이터 겹침 없음 | 대기 |
| U11 | 순위 | 메인/옵션 진입과 복귀, 순위·이름·출처·m, 빈 목록/긴 이름/실패/연타, 실제 공급자 구분 | 대기 |
| U12 | 계정 | 로그인/로그아웃/삭제/충돌/복구의 짧고 선명한 안내, 중요한 위험 설명은 유지 | 대기 |
| U13 | 화면 규격 | 9:16·19.5:9·20:9와 작은 iPhone Safe Area, 최소 터치 크기, 잘림·겹침 없음 | 대기 |
| U14 | 배경/VFX | 맵별 매우 느린 배경 유지, UI 반복 모션은 제거, 획득·점프·피격 효과가 입력과 가독성을 가리지 않음 | 대기 |

문서의 39노드 먹나무와 현재 4개 선택형 성장 화면이 공존한다. UI 검수라는 이유로 성장 규칙을
임의 회귀시키지 않는다. 현재 실행 코드와 사용자 요청을 대조하고 의도가 불명확하면 질문한다.

## 데이터·플레이 체크리스트

- D01 저장: 재실행, 저장 실패, 백업 복구, 손상, 같은 run 중복 정산, 강제 종료 경계.
- D02 뒤끝: 게스트/연동 계정 소유권, 로그인 전후 최고 기록, 성장 교체 확인, 지연·중복 콜백, 이전 계정 응답 폐기.
- D03 계정 변경/삭제: 재로그인, Apple 철회, 삭제 실패 재시도, 다른 계정으로 pending 이전 금지.
- D04 순위: 실제 제출/조회 왕복, 낮은 기록으로 최고값 저하 금지, 오프라인 재시도, 계정 전환.
- D05 Toss: 검증된 사용자 소유권과 공식 순위. 성장 서버 동기화·전체 통합 순위는 미구현임을 유지.
- D06 광고: 테스트 광고만 사용, 보상 1회, 늦은 콜백/배경 복귀/시간 초과/중복/새 판 경계.
- G01 드로잉: 캐릭터와 겹친 획 유지, 개별 충돌 유예, 빠른 포인터 해제, 총량/수명 단조 소멸.
- G02 게임 루프: 첫 시작/일시정지/배경 복귀/분신 사망/마지막 사망/부활/로비/재도전.
- G03 장시간: 여러 맵과 아이템, 분신 상한, 오브젝트 풀/메모리/프레임 저하, 앱 비활성 중 정지.

## iOS 배포 게이트

1. 새 Unity 테스트 결과·씬 빌더 생성·scene attestation 확인. 현재 파일 해시/결과 시각을 기록.
2. 먹점프 `com.CYSB.MukJump`, 초이성빈 Team `8AU359WZZ2`, 버전/중복 없는 빌드 번호 확인.
3. Apple 로그인과 Game Center는 각각 필요한 capability/entitlement 확인. Apple 보드 ID 미설정 해소.
4. 기존 SDK만 사용. Facebook 존재 여부를 확인하고 있을 때만 최종 앱의 FBAEMKit 등 포함/서명 검수.
5. 실제 암호화 사용과 Boolean ITSAppUsesNonExemptEncryption, PrivacyInfo.xcprivacy 검증.
6. CocoaPods는 xcworkspace, Archive, 서명·프레임워크·가능한 실기기 실행 확인.
7. App Store Connect Upload → 업로드 성공 → TestFlight 처리 상태 확인. 실패 원인을 숨기거나 임의 제출하지 않음.

## 알려진 차단점 / 아직 확보하지 못한 증거

- Apple 콘솔 세션 만료: Game Center 보드 생성/ID 확인과 이후 배포 인증에 로그인 필요.
- Toss 성장 동기화 및 Toss→뒤끝 공통 서버 브리지는 없음. 별도 순위 연결과 단일 통합 순위를 혼동하지 않는다.
- 옵션 광고 개인정보 버튼 제거 후 UMP가 재선택 진입점을 요구하는 환경의 대체 경로 미확정.
- 이전 전체 테스트/기존 TestFlight 1.0.0(8)은 이번 UI 변경의 실기기 검증 증거가 아니다.
- 새 전체 자동 테스트, 실제 화면 캡처, 서버 왕복, 실기기 실행은 아직 대기.

## 검토 왕복

- R1: 독립 검토는 출시 보류. 아래 1~3 수정안 승인, 4는 null 대상 특정 후 수정 조건.
- 2026-09-06 03:41~03:45 새 Unity 전체 실행: 1,286개 중 1,282 통과 / 4 실패 / skip 0.
  원본 증거는 `Temp/MukJumpEditModeResults.xml`, `Temp/MukJumpRunAllTests.failures`.
  DLL/PDB 03:41:02~03, 테스트 source 이후 새 컴파일 확인.

| Before | After | Why |
| --- | --- | --- |
| 성장 선택 카드의 한지 스킨을 실패로 간주하는 구 테스트 | 한지 종이·투명 입력 영역·아이콘/효과/선택 표시 보존 검사 | 최신 사용자 요구와 테스트 계약 일치 |
| 실제 로컬 성장 레벨을 읽는 먹 게이지 테스트 | MemoryPermanentGrowthStore 격리 후 원래 저장소 복구 | 사용자 구매 레벨로 기대 용량이 달라지지 않음 |
| 동봉 THIRD_PARTY_NOTICES에서 신규 설정 아이콘 7줄 누락 | canonical과 동봉본 동기화 | 새 원화의 고지가 앱에도 포함되어야 함 |
| EnterPlayMode를 가로지르는 캡처 closure에 completions 저장 | 캡처 없는 콜백, 리로드 후 초기화, 종료 전 값 보존 | UI 생성 전 테스트 하네스 NRE 제거; 실제 역순 닫기 검증 유지 |

NRE는 Cecil로 현재 DLL/PDB를 읽어 IL_00a9~00b0의 null `<>c__DisplayClass0_0` 접근으로
확인했다. UI 생성은 IL_00bd 이후다. 조사 도구: `/tmp/mukjump-ui-il.jqEHO7/Probe.csproj`.
R2: 03:55 완료한 재실행은 1,286/1,286 통과, 실패·스킵·미결정 0.
`output/release-qa/2026-09-06/pass-02` 원본과 03:49:49 Editor DLL/PDB를 독립 검토자가
직접 확인하여 위 4개 수정 묶음을 자동 테스트 범위에서 승인했다. 실제 UI·서버·출시 승인은 아니다.

R3 진행: 순위 이름 셀 RectMask2D와 실제 폰트 폭 기반 생략, 공급자 라벨 `뒤끝 TOP 10`,
iPhone 테스트 플랫폼 분기 보강. 최악 이름 6건/플랫폼 3건을 추가하여 전체 재검증한다.
자동 렌더 fixture는 화면 크기와 safe area 동시 주입·격리 씬·메모리 저장소·외부 호출 금지
조건으로 독립 검토 사전 승인. 아직 캡처 증거는 생성하지 않았다.

R3 독립 정적 검토의 후속 수정(전체 테스트 종료 후, 실행 중 소스 수정 금지):
- LobbyOptionsView의 계정 화면 2곳도 `ShouldOfferAppleSignIn(UiPlatform)`으로 통일.
  Android/iPhone/Toss 계정 UI 분기 integration assertion 추가.
- WorstCaseNames 테스트에 비어 있지 않음과 원문 overflow 때 말줄임표 필수 assertion 추가.
  90px stress 외 실제 270px 셀도 검증. 현재 폭만 검사하면 빈 문자열 회귀가 통과할 수 있음.
- 3차 완료 시 pass-03 새 폴더에 결과 보존하고 위 변경 후 4차 전체 실행 및 재승인 요청.
- GameCenter.mm iOS15 arm64 clang 구문 검사 재통과. Assets/Editor/Packages/ProjectSettings에
  자체 UserDefaults privacy 선언은 검색되지 않음. 최종 export/Archive manifest 게이트 유지.
- GUI 원격 조작 명시 승인은 아직 없음. 자동 렌더는 실제 기기 조작 증거와 구분한다.

04:37 heartbeat: 3차 전체 결과 1,295/1,295 통과, 실패·스킵·미결정 0을 확인하고
`output/release-qa/2026-09-06/pass-03`에 원본 보존. 실행 표식 없음 확인 후 위 두 후속 수정을 반영했다.
계정 Apple 노출 2곳을 UiPlatform으로 통일하고 iPhone/Android/Toss 생성 통합 검사를 추가했다.
6종 이름 각각 실제 270px와 stress 90px에서 비어 있지 않음·필요할 때 말줄임·폭/높이를 검사한다.
4차 전체 테스트 요청은 `Temp/MukJumpRunAllTests.request`. 아직 4차 통과 판정하지 않음.

R4 완료: 04:44:36 결과 1,295/1,295 통과, 실패·스킵·미결정 0. 독립 검토자가
04:38:30 Editor DLL/PDB 이후 실행된 XML과 해당 9개 parameterized case의 실제 통과를
직접 확인하여 정적 코드·전체 자동 테스트 범위에서 승인했다. pass-04에 원본 보존.
다음 검증은 승인된 격리 화면 렌더 fixture. 실제 기기·서버·Game Center 초기 인증과 ID,
Archive privacy 및 TestFlight 게이트는 계속 미완료다.

R5 준비(05:15 heartbeat): 격리 렌더 fixture의 사전 조건부터 구현.
LobbyOptionsView의 화면 크기/safe area 읽기를 한 경로로 모으고 UNITY_EDITOR에서만
인스턴스별 SetDisplayMetricsForTests를 제공한다. 실제 Screen 또는 전역 safe area는 변경하지 않는다.
1080×1920, 1170×2532(상하 inset), 1080×2400에서 anchor/패널 fit/전역 불변성 3건 추가.
현재 단계는 배치 주입 경로 검증이며 PNG 렌더 캡처나 실기기 검증이 아니다.
5차 전체 테스트 요청 후 독립 정적/결과 검토를 요청했다. 후속: pass-05 보존 →
승인 확인 → 격리 preview camera로 리더보드 첫 PNG부터 실제 렌더한다.

R5 완료: 1,298/1,298 통과·실패/스킵/미결정 0, 독립 검토 승인. pass-05 원본 보존.
R6: 검토 권고에 따라 화면 footprint 검사에 종이 밖 닫기 footer 190px와 양쪽 padding 12px,
패널 y offset 95×scale을 포함했다. 잘못된 height 입력의 예외 parameter 이름도 수정.
6차 전체 테스트 요청. 완료 후 pass-06 보존 및 독립 승인 확인; 실제 PNG 캡처는 아직 미완료.

R6 완료: 결과 05:32:10, DLL/PDB 05:25:01~02 이후 1,298/1,298 통과,
실패·스킵·미결정 0. viewport 3건 실제 수집과 실행 표식 정상 종료를 독립 검토자가 확인·승인.
pass-06 원본 보존. 배치 seam 검증은 완료했으므로 다음 작업에서 동일 테스트 보강만 반복하지 말고
승인 조건을 지킨 격리 렌더 PNG(리더보드 1장)부터 생성·육안 검토한다.

R7(06:03 heartbeat): LeaderboardRenderFixtureTests를 추가했다. PreviewRenderUtility 격리 씬,
전용 layer/camera, 1170×2532+safe area 주입, 비활성 View와 합성 10행을 사용해 서버 호출 없이
실제 Unity PNG를 Assets 밖 timestamp 폴더에 생성한다. 메모리 저장소 및 finally 정리 포함.
전체 실행 요청 중이며 PNG 생성/컴파일/육안 승인은 아직 확인 전. 다음은 이 테스트의 결과와
output/release-qa/2026-09-06/render-* 첫 이미지를 읽고 실패 시 fixture를 수정한다.

R7 판정: 1299개 자동 suite는 통과했으나 PNG는 직접 확인 결과 단색 배경뿐이므로 렌더 검증 실패.
render-20260905-211305-335는 UI 증거로 사용 금지. Retina로 2340×5064가 출력된 것도 확인.
R8: BeginPreview 이후 카메라/Canvas를 설정하도록 순서를 고치고 GUI point에 Retina 배율 제거.
실제 출력 해상도 manifest/일치 assertion 및 먹색 픽셀 최소량 assertion 추가. 초기화/복원도
try/nested finally로 보호. 재실행 요청; 여전히 빈 화면이면 실패로 남기고 명시적 RT 경로 조사.

R9: 명시적 RT+Camera.Render도 먹색 픽셀 0으로 단독 실패(전체 아님, total=1).
render-20260905-212615-506 역시 UI증거 사용금지. 현재 추가 진단은 같은 격리 preview scene의
WorldSpace canvas에 1920 기준 논리 크기를 직접 적용하는 경로로 단독 실행 요청 중이다.
manifest에 WorldSpace projection이라고 명시. 단독 요청은 정확히 `render-fixture-only`인 경우만
적용하며 그 외는 전체 실행한다. 현재 Temp 결과는 단독 결과이므로 전체 회귀 결과와 혼동 금지.
후속 권고: 결과 파일에 scope 명시 보강, 여전히 빈 경우 preview.camera 대신 별도 Game Camera를
같은 격리 씬에 생성하여 진단. 유효 PNG 후 전체 suite 재실행 필수.

R10: 별도 Game camera(cameraType==Game)도 preview scene에서는 먹색 픽셀0 단독실패.
result/active에 scope를 기록하도록 보강했고 선택범위 로그를 수정했다.
현재 R11은 저장하지 않는 임시 additive empty scene에 fixture root+Game camera를 옮기며
원래 active scene을 즉시 복원하고 finally에서 임시 씬을 닫는 방식으로 단독 진단 요청 중.
원본 씬 저장/편집 없음. Main.unity 재생성도 하지 않는다. 새 PNG도 직접 확인 전까지 증거 보류.

R11 오류: EditorSceneManager.NewScene은 TestRunner untitled 미저장 씬 때문에 실패.
SceneManager.CreateScene도 EditMode에서는 사용할 수 없다는 실제 예외를 확인했다.
R12 현재: UnityTest EnterPlayMode → RenderFixture의 일반 임시씬 → ExitPlayMode로
실행 전환. fixture active에서 모든 객체 생성 후 원래active복원, dirty불변assert, finally정리.
단독 실행 요청 중. 다음 결과의 예외/PNG부터 확인; 실제 기기 증거는 아니다.

R12 결과: PlayMode 진입 시 MukJumpAccountRuntime이 자동 생성되어 선행 안전검사에서 중단.
단독 total1/fail1, PNG 없음, ExitPlayMode 완료(result존재/active없음). 실제 계정 singleton을
삭제·비활성·null주입하여 우회하지 않았다. 다음은 프로젝트가 제공하는 공식 테스트 격리/bootstrap
경로가 있는지 읽기전용 확인하거나, 이미 요청한 GUI 원격 허가 후 실제 UI 캡처로 전환한다.
이 실패는 게임 회귀가 아니라 안전한 캡처 격리 미완성이다. 현재 전체 suite 통과로 표시하지 않는다.

R12 추가 P1: 계정 Bootstrap은 BeforeSceneLoad에서 생성되어 Play 진입 후 null검사보다
먼저 백엔드 초기화를 시작할 수 있다. 이번에 실제 원격 호출이 발생했는지는 확인하지 못했다.
이를 막기 위해 캡처 UnityTest에 명시적 Ignore 사유를 추가하여 자동 실행을 중단했다.
이 1건의 skip은 승인/통과가 아닌 격리 차단이다. 전체 결과 보고에 반드시 별도로 표시한다.
계정 Bootstrap을 시작 전에 억제하는 검증된 테스트 경로 또는 사용자 허가 후 실제 UI 검증 필요.
실제 계정 런타임 삭제/비활성/저장 데이터 변경은 하지 않았다.

07:07 예약 점검: 캡처 안전 중단은 독립 검토 승인(비활성화 조치만). 자동화 automation을
PAUSED로 전환하여 동일 차단점의 반복 실행을 중단했다. 재개에는 GUI 원격 조작 허가와
필요한 Apple 인증이 우선 필요하며, 실제 UI·서버·TestFlight 완료를 주장하지 않는다.
마지막 승인된 전체 suite는 R6의 1298건이며 이후 캡처 fixture 1건은 격리 차단으로 Ignore 상태다.

사용자 `ㄱㄱ` 및 `하라니가?`로 GUI 원격 조작 허가 확인 후 실제 연결 시도.
Unity 6000.5.9f1은 실행 중이지만 CUA getApp은 timeout/failedToCreateImageDestination,
macOS screencapture도 `could not create image from display`로 실패했다.
디스크 여유 109Gi로 저장 공간 부족은 아니다. 현재 차단은 허가 미응답이 아니라 화면 캡처 접근 불가.
Mac 디스플레이/잠금/화면 기록 접근 상태 확인 후 실제 UI 검수 재시도 필요.

최신 사용자 요청: 리더보드 뒤로/공급자탭/서버 안내/새로고침/완료 및 출처 열을 제거.
순위·이름·고도 3열만 표시하며 UNITY_EDITOR 전용 더미 10행을 추가했다. 서버에 제출하지 않으며
Release에는 더미가 포함되지 않는다. 이름 340px/40 크기, 행94 간격. 외부 dim 닫기 유지.

| Before | After | Why |
| --- | --- | --- |
| 두루마리에 끼인 뒤로와 중복 완료·새로고침 | 페이지 내부 버튼 제거 | 사용자가 요청한 간결한 기록 화면 |
| 서버 이름·개발 연결 안내·출처 | 제목과 순위/이름/고도만 유지 | 내부 구현 정보를 사용자 UI에서 제거 |
| 빈 대시 10행 | 에디터 전용 합성 기록 10행 | 실제 서버 기록으로 오인하지 않는 화면 확인 |

설정 UI 단독 테스트 요청은 아직 처리 대기. CUA는 noWindowsAvailable로 화면검증불가.
Roslyn 런타임 컴파일은 통과(기존 deprecated 경고 존재); 실제 Unity UI 테스트/육안 완료 주장은 보류.

독립 검토 추가 게이트: 성장 제품 기준 확인, UMP required 대체 진입점, dim 적용 범위,
Game Center fresh-launch 인증/런 소유자, 최악 닉네임 렌더, 뒤끝 2기기 동시 충돌,
Toss 지원 범위, 최종 Archive privacy manifest. 사용자에게 제품 기준·dim 범위·UMP 링크 확인을 요청함.
