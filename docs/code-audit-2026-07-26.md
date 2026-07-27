# 먹점프 제출 전 코드 감사 보고서

- 감사일: 2026-07-26
- 기준 브랜치: `feature/ui-polish`
- 기준 커밋: `fa77c42`
- 대상: 런타임 C# 47개(10,868줄), 에디터 씬 빌더, 프로젝트 설정, 패키지,
  EditMode 테스트, 제출 문서와 외부 에셋 출처
- 판정: **코드 결함은 제출 가능한 수준까지 보강됨. 라이선스·실기기 검증은 제출 전
  반드시 별도로 완료해야 함**

## 1. 감사 방법

1. 모든 `Assets/Scripts/**/*.cs`와 `Assets/Editor/MukJumpSceneBuilder.cs`를 기능 경계,
   Unity 생명주기, 예외 경로, 일시정지, 풀 재사용, 물리, 입력, 저장, 릴리스 빌드 기준으로
   검토했다.
2. Unity 6000.3.10f1 배치 Test Runner로 전체 EditMode 테스트를 실행했다.
3. 런타임 소스에서 구형 `Input.*`, `UnityEditor`, 네트워크/API 호출, 비밀 키 패턴,
   미구현 예외, TODO/FIXME, 빈 메서드와 고비용 검색·할당 패턴을 정적 검색했다.
4. `jscpd`로 C# 중복도를 측정하고, 패키지 manifest와 lock 파일을 비교해 실제 사용하지
   않는 직접 의존성을 제거했다.
5. Unity 공식 Game Programming Patterns 예제와 Open Project, 공개 Doodle Jump
   샘플의 구조를 비교했다. 공개 샘플은 정답을 복사하는 용도가 아니라, 수명주기·입력·
   생성 비용·의존성 관리 수준을 상대 비교하는 기준으로만 사용했다.

## 2. 최종 검증 결과

| 검증 | 결과 |
|---|---|
| Unity EditMode 전체 | **105/105 통과**, 실패·건너뜀 0(실제 Play 상태 물리 통합 1건 포함) |
| Unity C# 컴파일 | 오류·경고 0 |
| 관련 변경 `git diff --check` | 통과 |
| 런타임 비밀 키 패턴 | 발견 0 |
| 런타임 원격 통신 코드 | 발견 0 |
| 구형 `Input.*` 사용 | 발견 0 |
| 런타임의 `UnityEditor` 의존 | 발견 0 |
| TODO/FIXME/빈 메서드 | 발견 0 |
| C# 중복도 | 1.14%(123/10,821줄), 낮음 |
| 씬 빌더의 테스트 격리 | preview scene에서 검증, Main·BuildSettings·PlayerSettings 불변 |
| Main 씬 Missing Script | fileID 0 및 해석 불가 스크립트 GUID 0건 |

테스트 로그의 콜백 예외 스택 한 건은 `BrushTransitionView`가 내부 콜백 실패 후에도
입력 차단을 해제하는지 확인하기 위해 테스트가 의도적으로 발생시킨 예외다. 테스트 실행
결과는 통과이며 처리되지 않은 런타임 예외가 아니다. 배치 실행 중 Unity Licensing의
access token 갱신 경고도 로컬 Hub 인증 상태에 관한 메시지로 게임 코드와 무관하다.

## 3. 아키텍처 판정

현재 구조는 기능 폴더 기준의 **모듈형 모놀리스**다.

- `Core`: 세션 상태, 점수, 입력 어댑터, 난수 스트림, 공용 풀 계약
- `Player`: 캐릭터 물리, 자동 점프, 사망·보호
- `Drawing`: 포인터 획, 스무딩, 먹 자원, 드로잉 발판 수명
- `Items`: 아이템 스폰·획득·표현
- `Obstacles`: 이동 장애물과 낙묵석
- `Presentation`: UI·VFX·Audio. 현재는 `Core` 폴더 안에도 일부 존재
- `Editor`: 코드 기반 씬 조립과 회귀 테스트

세션 전체 사실은 `GameManager`의 상태·일시정지·고도 순간이동 이벤트로 전달하고,
반복 객체는 기능별 `ComponentPool<T>`가 소유한다. 이 방향은 Unity 공식 패턴 예제의
이벤트·오브젝트 풀·상태 패턴과 일치한다. 다만 현재 `Assembly Definition`이 없어 경계가
컴파일 단계에서 강제되지는 않고, 일부 `Core` UI가 플레이어·드로잉 타입을 직접 참조한다.
제출 직전에 이를 한 번에 쪼개면 직렬화 GUID와 빌드 의존성 위험이 더 크므로 이번에는
아키텍처 회귀 테스트로 금지 의존성을 고정하고, 제출 후 다음 순서로 분리하는 편이 안전하다.

`Core.Contracts → Gameplay(Player/Drawing/Items/Obstacles) → Presentation → Bootstrap`

`GameFeedbackController` 874줄과 `PauseMenuView` 543줄은 현재 가장 큰 유지보수 부채다.
둘 다 코드 생성 UI와 표현을 한 클래스가 소유해 기능 응집도는 있으나, 향후에는 Audio,
Haptics, CameraFeedback, TransientVfx와 PauseView/Presenter로 분리해야 한다.

## 4. 이번 감사에서 수정한 결함

### 릴리스 안전성

- DEBUG 패널과 치트 호출을 Editor/Development Build에서만 허용했다.
- 디버그 아이템·무적·고도 이동을 사용한 판은 로컬 최고 기록 저장 대상에서 제외했다.
- 기본 회사명과 번들 식별자를 `Team-Swooord`,
  `com.teamswooord.mukjump`로 교체했다.
- 미구현 원격 API 필드·코루틴을 제거해 API 키가 없는 로컬 수묵 스타일 경로만 남겼다.

### 상태와 예외 경계

- `IsGameplayTicking`을 게임 규칙의 공통 경계로 두어 로비·전환·일시정지 중 점수,
  자동 점프, 날씨, 아이템, 장애물이 진행되지 않게 했다.
- 일시정지는 자동 점프 충전, 낙묵석 경고·낙하, 풍향·상승기류 타이머를 초기화하지 않고
  그대로 보존하며 아이템 진입 예고도 새로 시작하지 않는다.
- 붓 화면 전환 콜백이 실패하거나 진행 중 컴포넌트가 비활성화돼도 오버레이·raycast
  차단과 `transitionInProgress`가 반드시 복구된다.
- 풀의 factory/acquire/release 콜백이 예외를 던지면 손상 인스턴스를 폐기하고 보존
  상한을 유지한다.
- 아이템 효과가 실제 적용됐을 때만 픽업을 반납한다. 같은 물리 틱에 사망한 캐릭터,
  실패한 분신 생성, 필수 드로잉 시스템 부재 때문에 효과가 거부되면 아이템은 소비되지
  않는다.
- Play 중 스크립트 재컴파일로 static 참조가 사라져도 먹물점프 공용 풀의 기존 비활성
  인스턴스를 다시 편입한다. 비활성 서비스 자체는 재사용하지 않고, 뒤늦게 켜진 중복
  서비스가 현재 공용 풀을 덮어쓰지 못하게 한다.

### 물리와 게임 규칙

- 먹분신끼리 충돌하지 않도록 `Player` 레이어를 추가하고 자기 충돌을 차단했다. 분신이
  늘 때마다 모든 콜라이더 쌍에 `IgnoreCollision`을 호출하던 O(n²) 경로를 제거했다.
- 화면 양옆 벽을 움직이는 static collider가 아니라 독립 kinematic Rigidbody2D와
  `MovePosition`으로 추적하게 했다. 마찰 0 전용 물리 재질로 카메라 상승 속도가
  캐릭터에 전달되지 않게 하고 domain reload 뒤 기존 벽을 복구한다.
- 좌우 이동 장애물도 움직이는 static trigger를 제거하고 kinematic Rigidbody2D와
  FixedUpdate `MovePosition`으로 전환했다.
- 방어막의 1회 보호와 짧은 무적 시간을 장애물과 추락에 동일하게 적용했다.
- 드로잉 발판은 페이드 시작 즉시 물리 콜라이더를 끄고, 충돌 가능한 발판을 엄격히
  최근 4개로 제한한다.
- 장애물은 실제로 사용하던 좌우 이동 한 종류만 남기고 사용되지 않던
  `Static/Vertical` 모드를 제거했다.
- 풍맥 생성 간격이 0·음수·역순·NaN·Infinity로 손상돼도 최소 양수 간격으로 복구한다.
  큰 고도 이동의 과거 예약도 프레임당 8개까지만 따라잡고 현재 고도 이후로 재예약해
  무한 루프와 한 프레임 생성 폭증을 함께 막는다.

### 입력·수치 방어

- 한 프레임의 긴 포인터 이동이 남은 먹보다 긴 선을 만들지 못하도록 입력 구간을 남은
  먹 길이에서 자르고 즉시 획을 종료한다.
- 먹 용량 0, 음수 설정, NaN/Infinity 좌표, 잘못된 스무딩 반복·간격을 방어한다.
- 자동 점프 간격과 사망 애니메이션 FPS의 음수·NaN·Infinity 값을 안전한 기본값으로
  정규화해 0 나눗셈, 영구 충전, 음수 프레임 인덱스를 막는다.
- Chaikin/리샘플링 결과는 최대 32,768점으로 제한해 비정상 입력의 메모리·CPU 폭주를
  막는다.
- 캐릭터 안전 구간 계산에서 매 호출 장면 검색과 임시 배열, O(n²) 누적 길이 계산을
  제거하고 등록 목록·재사용 버퍼·선형 계산을 사용한다.

### 결정론과 재현성

- 아이템, 이동 장애물, 낙묵석, 날씨, 특수 발판, 플레이어 행동에 독립 난수 스트림을
  적용했다.
- VFX·사운드용 `UnityEngine.Random` 호출 수가 바뀌어도 게임 난이도와 스폰 순서는
  바뀌지 않는다.
- 세션 seed와 세대 번호로 같은 문제 상황을 테스트에서 재현할 수 있다.

### 자원과 성능

- `GameFeedbackController`가 런타임에 만든 AudioClip, 점 스프라이트와 Texture2D를
  비활성화·파괴 시 명시적으로 해제한다. 공용 UI 먹 마스크와 `FallbackInkStyle`의
  Material·소유 Texture도 subsystem 재시작 때 해제하고 외부 텍스처 소유권은 침범하지
  않는다.
- 장애물의 공유 붉은 한지 Material과 하단 HUD의 황금 붓 폴백 Texture2D도 각 소유
  생명주기에서 해제한다.
- 512×512 개별 `SetPixel`로 첫 프레임을 막던 먹 blob은 128×128로 줄이고 픽셀 배열을
  `SetPixels32` 한 번으로 올린다. 갈필 텍스처도 같은 배치 경로를 사용한다.
- 발판 붓선의 두께는 정점 수만큼 배열·Keyframe을 만들지 않고 4키 테이퍼와
  `widthMultiplier`로 적용한다. 바람 물리도 플레이어가 캐시한 Rigidbody를 사용한다.
- 오디오 동시 재생은 빈 source를 우선 사용하고 모두 사용 중일 때만 가장 오래된 source를
  재사용한다.
- 아이템·장애물·낙묵석·단기 VFX의 풀 보존 상한과 반납 상태를 회귀 테스트로 고정했다.
- 직접 사용하지 않는 2D Animation/Aseprite/PSD Importer/SpriteShape/Tilemap Extras,
  Collaborate, Multiplayer Center, Timeline, Visual Scripting 패키지를 제거했다.

### 씬 생성과 테스트

- 씬 빌더가 기존 `Main.unity`의 UI를 읽어 다시 덮어쓰던 Capture/Restore 경로를
  제거했다. 코드 상수가 씬 구성의 단일 진실 공급원이다.
- `BuildForTests()`는 저장할 수 없는 preview scene에 새 루트만 격리하며 에셋 importer,
  PlayerSettings, BuildSettings와 실제 Main 씬을 변경하지 않는다.
- HUD 텍스처 importer 구성 플래그를 Systems 생성까지 전달하고 테스트 전후 dependency
  hash를 비교해 테스트 경로의 `SaveAndReimport`도 차단했다.
- 테스트가 전역 장면 검색이나 사용자의 열린 씬에 의존하지 않게 했다.
- 실제 `Main.unity`의 모든 `m_Script` GUID를 AssetDatabase에서 역조회하고 fileID 0
  참조가 없음을 회귀 테스트로 고정했다.
- `EnterPlayMode`로 실제 Physics2D 트리거를 진행해 좌우 이동 장애물의 첫 접촉이
  아이템식 반동이 아니라 사망으로 이어지는 통합 경로를 검증했다.

## 5. 의미 없는 코드와 중복 검토

제거한 항목:

- 실제 호출되지 않던 카메라 target setter, 로비 랭킹 숨김 필드, 신기록 숨김 텍스트,
  풍향 강도 fill/brush 필드, 맵 stage/zone 노출 프로퍼티
- `GameManager`의 사용하지 않는 낙하 콜백·일시정지 view 필드
- 피드백 overlay canvas와 먹물점프의 미사용 coroutine 필드
- 장애물의 생성되지 않는 정적·수직 이동 분기
- 원격 img2img API의 미구현 endpoint/request scaffold
- 결과창에서 생성 직후 영구 비활성화하던 현재 고도 highlight와 인자를 무시하던 장애물
  `SetVisible(bool)` 계약
- 맵 DEBUG 버튼의 익명 리스너와 `RemoveAllListeners` 조합(다른 리스너까지 지울 수 있음),
  정점마다 다시 만들던 동일 붓 두께 키 배열

동일 조건(`min-lines=5`, `min-tokens=50`)의 `jscpd`가 찾은 중복 10그룹은 주로
`ItemSpawner`·`ObstacleSpawner`의 화면 범위 예약·이벤트 구독·풀 반납 대칭 코드와
런타임 UI 생성 헬퍼의 짧은 보일러플레이트다. 총 1.14%로 낮고, 두 스포너를 범용 상속
계층으로 합치거나 서로 다른 UI를 억지로 공용화하면 소유권 결합이 더 커진다. 현재는
의도적 대칭 코드로 유지하는 편이 더 명확하다.

## 6. 보안·무결성 검토

- 게임은 계정, 개인정보, 서버, 결제, 파일 업로드를 사용하지 않는다.
- 런타임 네트워크 호출과 API 키가 없으므로 키 유출·원격 요청 위조 공격면이 없다.
- 스트로크 점 수와 입력 값을 제한해 비정상 입력에 의한 메모리 폭주를 방어한다.
- DEBUG 치트는 릴리스 빌드에서 비활성화된다.
- `.gitignore`가 Android/iOS 서명 키·개인 인증서와 로컬 Claude 설정을 차단한다.
- 최고 기록은 `PlayerPrefs` 로컬 값이므로 사용자가 수정할 수 있다. 온라인 랭킹이나
  보상 권한의 근거로 사용하면 안 된다. 현재는 기기 내 표시용이라 허용 가능한 위험이다.
- 오픈소스 저장소이므로 클라이언트에 비밀을 넣어 보호할 수 없다. 향후 원격 API를
  추가한다면 키를 APK에 포함하지 말고 서버 중계·요청 제한을 사용해야 한다.

## 7. 공개 구현과 비교

| 기준 | 먹점프 | Unity 공식 패턴 예제 | 공개 Doodle Jump 샘플 |
|---|---|---|---|
| 입력 | Input System + `PointerInput` | 입력 추상화 권장 | 구형 `Input.*` |
| 반복 객체 | 기능별 지연 풀 + 상한 | Object Pool 패턴 | `Instantiate` 중심 |
| 상태 | `GameManager` 상태·이벤트·pause 경계 | State/Event 패턴 | 컴포넌트에 분산 |
| 재현성 | 규칙별 deterministic RNG | 데이터·서비스 분리 권장 | 전역 Random |
| 씬 구성 | 코드 빌더 단일 기준 | 데이터 중심 조립 권장 | 씬 수동 의존 |
| 테스트 | 105개 + Play 상태 물리·불변성 검사 | 패턴별 테스트 가능한 구조 | 테스트 없음 |
| 모듈 강제 | 논리 경계, asmdef 미적용 | assembly/package 분리 가능 | 분리 없음 |

참고 자료:

- Unity Game Programming Patterns:
  https://github.com/Unity-Technologies/game-programming-patterns-demo
- Unity Open Project 1(Chop Chop):
  https://github.com/UnityTechnologies/open-project-1
- Unity `ObjectPool<T>` 문서:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Pool.ObjectPool_1.html
- 비교용 MIT Doodle Jump 샘플:
  https://github.com/ehsan-mohammadi/Doodle-Jump-Unity3D-Game

공개 Doodle Jump 샘플은 작은 학습용 프로젝트라 직접 비교에 한계가 있다. 먹점프는
입력·풀·상태·테스트에서 더 안전하지만, Unity 공식 Open Project처럼 ScriptableObject
이벤트 채널과 asmdef로 완전히 분리된 대규모 구조는 아니다. 현재 규모에서는 단순한
이벤트 계약과 기능별 소유권이 과설계 없이 적절하다.

## 8. 남은 제출 리스크

### 제출 전 반드시 해결

1. **캐릭터 독자 디자인성을 최우선으로 재검토한다.** 초기 AI 활용 기록에는 유명
   제3자 캐릭터를 먹빛으로 재해석하라는 지시가 남아 있고, 현재 검은 가시형 몸·큰 흰
   눈·가는 다리 조합에도 유사성 판단 위험이 있다. 침해 여부를 이 감사에서 단정할 수는
   없지만 NAN 약관은 참가자가 순수 창작물과 타인의 저작권·상표권 등 비침해를 보증하게
   한다. 제출 전 특정 작품과 무관한 실루엣·눈·다리 비율로 리디자인하거나 전문 검토로
   독자성을 확인한다. 참고: https://nan2026.nhn.com/terms
2. `Assets/Resources/MukJump/Fonts/HealthsetJoritdaeStd.otf`는 **Public GitHub에서
   배포할 수 없다.** 공개 라이선스 본문은 프로그램 임베딩은 허용하지만
   무단전제·배포와 폰트 파일 복제·배포를 금지한다. APK에는 임베딩할 수 있어도 전체
   소스 제출 저장소에 OTF 원본을 올릴 수는 없다. 이 파일은 이미 공개 `main`의
   `5e9e3ac` 커밋에 들어가 새 커밋에서 삭제해도 과거 blob으로 내려받을 수 있다.
   커밋 기록이 심사 대상이라 무단 history rewrite도 안전한 해법이 아니다. 가장 안전한
   순서는 권리자에게 Public GitHub 배포의 서면 허락을 받는 것이다. 허락을 받을 수
   없다면 주최 측과 먼저 협의한 뒤 OFL 서체 교체, history purge/cache 정리 또는 저장소
   비공개 전환 범위를 결정한다.
   근거: https://noonnu.cc/font_page/124
3. Pixabay에서 받은 slime·ink spill MP3 2개는 게임에 포함하는 이용과 원본 파일의
   standalone 재배포가 구분될 수 있다. Public 소스에 raw MP3를 두는 것이 허용되는지
   확인하고, 불명확하면 직접 제작·명확한 CC0 음원으로 교체한다. 붓 MP3의 원저작
   `brush.wav`는 Freesound에서 CC0임을 확인했다.
4. 현재 Unity 6000.3.10f1 설치의 `PlaybackEngines`에는 `iOSSupport`만 있고
   `AndroidPlayer`가 없다. 같은 버전의 Unity Hub에서 **Android Build Support,
   Android SDK & NDK Tools, OpenJDK**를 설치해야 APK를 만들 수 있다. 다른 Unity
   버전의 Android 모듈로 대체하지 않는다.
5. Android keystore 서명 빌드와 최소 1대의 실제 Android 기기에서 설치·첫 실행·
   터치 드로잉·일시정지·게임오버·재시작을 검증한다. 프로젝트 설정은 IL2CPP·ARM64·
   Input System 전용으로 맞지만 아직 실제 빌드 증거가 없다.
6. 필수 제출물인 APK/배포 링크, 30~60초 YouTube 실제 플레이 영상, 게임 소개서,
   AI 활용 기술 문서, 팀원 롤 기술서 PDF를 완성한다. 현재 저장소에는 이 산출물과
   공개 링크가 없다.

### 권장

1. 루트 `LICENSE`가 없다. 팀이 실제로 허용할 소스 라이선스를 결정한 뒤 추가한다.
2. Android 앱 아이콘 슬롯이 비어 있고 표시 이름이 `muk-jump`다. 독자 디자인 검토가
   끝난 앱 아이콘과 최종 사용자 표시명을 PlayerSettings에 넣는다.
3. Development Build가 아닌 최종 IL2CPP ARM64 APK에서 DEBUG 버튼이 사라지는지
   확인한다.
4. Profiler를 연결한 Android 실기기에서 10분 이상 플레이하며 GC Alloc, Physics2D,
   활성 풀 개수와 분신 누적을 측정한다.
5. 실제 Play 상태의 이동 장애물 첫 충돌은 자동 검증하지만 범위는 아직 1건이다.
   경계벽·풍맥·일시정지/히트스톱 중첩·다중 분신 장시간 조합 테스트를 추가한다.
6. 제출 후 `GameFeedbackController`와 `PauseMenuView`를 표현 하위 컴포넌트로 분리하고,
   순환 참조를 끊은 뒤 asmdef를 단계적으로 도입한다.
7. 기존 Git 작성자 이력에는 개인 이메일 주소가 포함되어 있다. 심사 대상 커밋을
   rewrite하지 말고, 앞으로는 GitHub noreply 주소를 사용하며 공개 범위를 팀이 확인한다.

## 9. 제출 판정

코드 자체에서 즉시 제출을 막는 컴파일 오류, 미처리 예외, 비밀 키, 원격 의존성,
릴리스 치트 노출은 현재 발견되지 않았다. 전체 자동 테스트 105개가 통과하고 핵심 입력·풀·
물리·상태 전환의 비정상 경로가 테스트로 고정되어 **코드는 조건부 제출 가능**하다.

다만 캐릭터 독자 디자인성, 에셋 라이선스 증빙과 Android 빌드·실기기 검증은 코드
테스트로 대체할 수 없다. 위 “제출 전 반드시 해결” 6개를 완료하기 전에는 최종 제출 완료로
판단하지 않는다.
