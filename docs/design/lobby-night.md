# 밤 이스터에그

메인 먹점프 로고를 **10번 탭**한다. 광고에 가려질 수 있는 해·달은 직접 누르지 않는다. 누를 때마다 현재 천체가 원래 색을 유지하며 곡선으로 조금씩 내려간다. 열 번째에는 현재 위치·속도를 이어서 마저 지고 반대편 천체가 올라온다. 밤에도 같은 로고를 10번 누르면 낮으로 돌아온다. 크기 변화·펄스·대기 중 반복 움직임은 없다.

매 탭의 하강은 0.24초 SmoothDamp로 이어 붙이고 열 번의 탭으로 전체 하강의 72%를 진행한다. 열 번째부터 전환은 5.8초이며, 출발 대기 뒤 진행률 34%에 기존 천체를 숨기고 38%부터 반대편 천체가 올라온다. 오른쪽 우하향과 왼쪽 우상향은 홈 중점을 축으로 반사한 같은 2차 곡선이다. 들어오는 천체는 양 끝의 속도·가속도가 0인 smootherstep으로 도착 직전 감속한다. 이동 중 전체 알파 페이드는 쓰지 않고 원화의 가까운 능선 아래 픽셀을 가린다. 진행 중 위치·속도는 로비 왕복과 재활성화에도 유지한다.

로비에서는 원화 좌표 대신 실제 광고 하단과 최고 기록 패널 상단 사이의 중간 높이에 양쪽 도착점을 맞춘다. 노치·배너 높이가 달라도 같은 규칙을 쓰며, 공간이 좁으면 양쪽 천체를 같은 크기로 줄여 광고와 기록에 닿지 않게 한다.

## 아트 방향

오른쪽 해는 원래 한지색, 왼쪽 달은 붉은색을 유지한다. 탭 횟수로 색을 바꾸지 않는다. 원화를 뒷배경과 `LobbyForegroundMountains` 앞산 Mesh로 분리하고, 사이에 달 SpriteRenderer를 놓는다. 128개 구간의 공통 능선으로 뒷배경의 앞산 부분을 제외하고 앞산 Mesh가 그 픽셀을 대신 그린다. 두 레이어는 원래 텍스처·UV·밤 색조를 공유하므로 산을 새로 그리거나 위치를 바꾸지 않는다. 앞산 Mesh 1개(258개 정점)를 재사용하며 별도 비트맵·전체 화면 덮개는 만들지 않는다.

새로 그린 정교한 일월오봉도풍 원화는 사용하지 않는다. 기존 7개 맵의 산·소나무·한지 여백·거친 붓결을 그대로 유지하고 `BackgroundNight.shader`로 하늘을 먹청색, 아래 플레이 영역을 달빛 한지색으로 어둡게 한다. 캐릭터·먹선·UI를 검은 덮개로 가리지 않는다. 맵별 구름도 같은 밤 색조를 공유한다.

해·달은 같은 크기의 작은 원형을 유지한다. 해는 기존 스프라이트를 쓰고, 달만 128px 한지 원화로 분리한다. 달의 테두리는 완만한 작은 굴곡과 짧은 번짐으로 마감하고 안쪽에는 넓고 은은한 얼룩만 둔다. 명암 차이는 7% 이내로 제한하고 픽셀 단위의 빽빽한 점무늬는 쓰지 않는다. 한 번 생성한 텍스처를 재사용하므로 무늬가 흔들리거나 깜빡이지 않는다. 0번 맵의 원래 해 영역에만 깨끗한 하늘 텍스처를 feather 방식으로 덧씌워 움직이는 천체와 중복되지 않게 한다. iPad cover crop에서는 표시 천체만 화면 안으로 보정하고 원화 보정 UV는 그대로 둔다.

## 상태와 입력

- `LobbyNightState`: 실행 중에만 저장. 게임·맵 변경·로비 복귀에 유지되고 다음 앱 실행은 낮. 계정·성장·리더보드 데이터와 무관하다.
- `LobbyNightSkyView`: 카메라의 공통 Background에 하나. 교체되는 두 실제 배경 renderer 모두에 적용하며 맵 교차 전환 alpha를 보존한다.
- `LobbyLogoTapTarget`이 로고 글씨 띠만 UI raycast 대상으로 만들고, 메뉴 버튼과 같은 누름·해제·클릭 이벤트로 탭을 전달한다. 별도 LateUpdate 포인터 폴링과 중복 집계하지 않는다. 이미지의 투명 여백, 해·달, 드래그·긴 누름·팝업·성장·게임 입력은 제외한다. 전환 중 새 탭은 무시한다. 부분 탭을 취소하면 현재 천체는 부드럽게 원위치로 돌아간다.
- 시작·성장·옵션 버튼을 차단하지 않는다. 게임 진입 후에도 진행 중인 천체 연출이 이어진다.
- 일시정지와 앱 비활성 상태는 시간을 멈춘다. 큰 복귀 delta는 제한한다. 기존 움직임 줄이기 설정이 켜진 경우 이동 없이 0.35초 색 전환만 한다. 설정 UI는 추가하지 않는다.

## 이미지 제작 정보

- 도구: built-in image generation/editing mode.
- 입력: `Assets/Art/Background/Maps/map_00_quiet_mountain.png`.
- 최종 사용 에셋: `Assets/Resources/MukJump/Background/Lobby/lobby_day_sky_v1.png`.
- 생성된 이미지 전체로 맵을 교체하지 않고 해 주변의 작은 영역만 샘플링한다.
- 채택하지 않은 정교한 밤 시안은 `output/quality-polish/lobby-night/rejected/`에만 있으며 앱 Resources에는 포함되지 않는다.

최종 사용 에셋의 프롬프트:

> Use case: precise-object-edit. Image 1 is the edit target, an existing portrait Korean ink-wash game background. Remove ONLY the small pale round sun in the upper-right sky (approximately x68.5%, y9.8%). Seamlessly fill that circular region with the immediately surrounding warm beige hanji sky, matching fibers, mottling and subtle cloud wash. Preserve all mountain silhouettes, all trees, mist, paper grain, overall colors, positions, framing and the large open lower half exactly. This is a clean background plate so a separate animated sun can move over it. Do not add a moon, sun, stars, text, UI, borders or any other objects. Keep original 9:16 portrait composition. One full background image only.

## 검증

`LobbyNightSkyTests`에서 9/10회 탭, 해·달 왕복, 드래그/팝업 차단, 게임 진입, 정지/복귀, 세로 화면 비율, 7개 맵 및 양쪽 renderer 색상·알파, 원화 유지, 로비 버튼, 셰이더 컴파일과 렌더를 검사한다. `night-sky-regression` 범위는 구름·로비·화면 전환·영문 UI 회귀 검증도 실행한다. 미리보기는 `output/quality-polish/lobby-night/`에 생성한다.
