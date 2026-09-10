# 먹점프 Unity 플러그인 운영

검토 기준: 2026-09-11. 목적은 기능을 무작정 추가하는 것이 아니라, 현재 씬·컴파일·테스트 결과를 직접 확인하며 수정하는 것이다.

## 설치 구성과 적용 범위

| 구성 | 확인한 상태 | 역할 |
| --- | --- | --- |
| Codex Unity 플러그인 | 0.1.4-beta, 스킬 31개 | Unity 작업 지침. 이 패키지 자체에는 MCP 설정이 없다. |
| Unity CLI | 1.0.0-beta.5 → 1.0.0-beta.8 | 프로젝트를 지정해서 에디터 명령 실행 |
| Unity Pipeline | com.unity.pipeline@0.6.0-exp.1 | 먹점프 에디터 연결. UPM Client.Add로 설치, 버전 고정 |
| Unity Editor | 기존 6000.5.9f1 유지 | iOS·URP 2D·Input System 유지 |
| 프로젝트 로컬 MCP | .codex/config.toml | 먹점프 경로와 CLI 절대 경로 지정. 기기별 파일이므로 Git 제외 |

CLI 명령 실행과 MCP 초기 연결·도구 목록 조회를 직접 검증했다. 로컬 MCP 설정 작성만으로 현재 대화의 도구가 다시 로드됐다고 가정하지 않는다. 다음 세션에서는 실제 연결 가능 여부를 다시 확인한다.

Pipeline은 실험 버전이므로 자동으로 최신 버전을 따라가지 않는다. 기존 Firebase·AdMob·Apple 로그인·뒤끝·DOTween·일본어 폰트 패키지는 변경하지 않았다. 기존 의존 패키지 버전도 유지했다. 패키지 원본은 저장소에 복사하지 않고 공식 레지스트리에서 받는다.

## 공식 자료에서 확인한 것

- Unity의 [플러그인 발표](https://unity.com/blog/unity-plugin-for-claude-code)는 작업 지침과 CLI/MCP 활용을 소개한다. 발표의 Claude Code용 29개 스킬과 이 기기의 Codex용 31개 스킬은 배포 채널·버전이 다르므로 숫자만으로 동일성이나 누락을 판단하지 않는다.
- [Pipeline 공식 설명](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)에 따라 Unity 6 에디터와 로컬 도구를 연결했다. 설치본의 명령 목록·매개변수를 먼저 조회하고 사용한다.
- [CLI beta.8 릴리스 노트](https://discussions.unity.com/t/unity-cli-1-0-0-beta-8-is-rolling-out/1735542)에는 macOS 자격 증명 처리, 명령 완료 후 멈춤, MCP 타입 처리 등의 수정이 설명돼 있다. 에디터 버전을 바꾸지 않고 CLI만 해당 수정판으로 올렸다.
- Pipeline 패키지의 [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license)를 확인했다. 원본 소스를 별도 배포하지 않으며, 프로젝트에 통합된 바이너리 배포 조건과 패키지 고지를 유지한다.

## Reddit 경험담과 먹점프의 판단

커뮤니티 글은 통제된 성능 실험이나 제품 보증이 아니다. 아래 적용은 경험담을 참고해 이 프로젝트에서 별도로 검증한 결정이다.

| 참고 글 | 참고한 경험 | 적용 |
| --- | --- | --- |
| [Unity3D: Unity AI/도구 사용 경험](https://www.reddit.com/r/Unity3D/comments/1vm26dn/unity_advertises_ai_everywhere_so_i_decided_to/) | 댓글에 MCP 재컴파일 지연과 에디터 포커스를 바꿔야 했다는 사례가 있다. | 명시적 recompile → 완료 상태 확인. Play를 눌러 컴파일을 대신하지 않는다. |
| [Codex: CLI로 마을 만들기](https://www.reddit.com/r/codex/comments/1vj26st/using_codex_and_the_new_unity_cli_i_gave_sol/) | 에디터 제어가 가능해도 배치·방향 같은 시각적 결과에는 오류가 남을 수 있다. | 스크린샷과 구조·게임플레이 검사를 함께 사용한다. 연결 성공을 품질 검증으로 취급하지 않는다. |
| [Codex: 게임 개발 경험](https://www.reddit.com/r/codex/comments/1vg14n5/is_anyone_using_codex_for_game_dev_if_so_hows_it/) | 반복 호출 비용, 빠른 기능 추가에 비해 검증이 뒤처지는 문제, 측정 도구의 필요성이 언급된다. 일부는 Unreal 경험이다. | 한 번의 mukjump_audit로 핵심 상태를 읽고, 관련 회귀 검사를 묶는다. 다른 엔진의 성능 주장을 그대로 적용하지 않는다. |

## 작업 순서

CLI 경로는 이 기기에서 `/Users/seungyeoning/.unity/bin/unity`다. 다른 기기는 설치 경로를 확인한다. 아래 예시는 저장소 루트에서 실행한다.

```sh
MUKJUMP_PROJECT="$PWD"
unity command --query recompile --detail compact --project-path "$MUKJUMP_PROJECT" --format json
unity command recompile --project-path "$MUKJUMP_PROJECT" --format json
unity command recompile_status --project-path "$MUKJUMP_PROJECT" --format json
unity command mukjump_audit --project-path "$MUKJUMP_PROJECT" --format json
```

1. 먼저 실제 프로젝트 경로, 프로세스, Git 변경을 확인한다. 다른 프로젝트에 fallback하지 않는다.
2. `recompile_status`의 `data.result`가 JSON 문자열이면 한 번 더 파싱한다. 내부 `status=completed` 또는 `up_to_date`와 오류 없음까지 확인한다. 최상위 `success=true`만 확인하면 컴파일 실패를 놓친다.
3. `mukjump_audit`는 프로젝트·에디터·타깃, 빌드 씬, 저장된 씬의 소스 일치 여부, 로드된 씬의 누락 스크립트·미저장 상태, Player 안전 설정을 읽는다. 계정·UID·PlayerPrefs·토큰은 읽거나 반환하지 않는다.
4. `readyForValidation=false`면 해당 항목부터 해결한다. 누락 스크립트 검사는 **로드된 씬** 범위이며, 모든 프리팹 검사나 출시 승인 결과가 아니다.
5. 패키지·meta 변경으로 씬 소스 검증값이 바뀌면 미저장 씬이 없는지 확인한 뒤 `MukJump/Build Main Scene` 메뉴로 재생성한다. 씬 YAML을 수정하지 않는다.

빠른 연결 검사:

```sh
unity command run_tests --mode editor --filter AgentToolingTests --async_tests true --project-path "$MUKJUMP_PROJECT" --format json
unity command test_status --project-path "$MUKJUMP_PROJECT" --format json
```

`running` 또는 0개 성공은 완료가 아니다. 내부 결과의 완료 상태, 실제 실행 수, 실패 수를 확인한다. 다음 검사를 시작하면 이전 결과가 교체되므로 `Temp/pipeline_test_status.json`을 검증 출력 폴더에 보관한다. 테스트 실행 중에는 다른 테스트를 겹쳐 시작하지 않는다.

긴 컴파일/빌드를 동기 eval에 넣으면 도구 응답 제한(이번 설치본 기본 5초)에 걸려도 에디터 작업은 계속될 수 있다. 타임아웃 직후 같은 쓰기 명령을 반복하지 않는다. 기존 빌드 요청 처리기나 비동기 작업을 사용하고, 실행 중 상태·산출물·최종 결과를 확인한다.

프로젝트 회귀 검사:

```sh
unity command menu --path 'MukJump/Diagnostics/Run Agent Tooling Regression' --project-path "$MUKJUMP_PROJECT" --format json
```

이 메뉴는 기존 먹점프 TestRunner를 사용한다. 결과는 `Temp/MukJumpRunAllTests.result`, 실패는 `.failures`, 상세 XML은 `Temp/MukJumpEditModeResults.xml`이다. 플러그인 자체 검사와 스플래시·튜토리얼·닉네임·저장 실패·일본어·지역·iOS 설정·게임플레이 회귀를 묶었다. 실제 Apple/뒤끝 서버 통합 검증을 대신하지 않는다.

로그는 `console --level error --tail 20`처럼 범위를 좁히고 반환 cursor 이후를 조회한다. 반복 상태 확인은 짧은 간격으로 몰아치지 않는다. 사용자 콘솔을 임의로 지우지 않는다.

**먹점프 UI 촬영에는 기본 screenshot을 쓰지 않는다.** 설치본 ScreenshotCommand는 카메라만 렌더링하여 Screen Space - Overlay Canvas가 빠진다. 실제 검증에서도 배경만 찍혔다. `capture_game_view --source screen`은 합성 화면을 지원하지만 이번 배치 에디터에서는 빈 프레임이 반환됐고, save_path의 output/qa도 Assets/output/qa로 해석됐다. 이 경로는 에셋·씬 검증값을 불필요하게 바꾸므로 사용하지 않는다.

먹점프 전용 명령은 기존 실제 플레이 테스트와 같은 ScreenCapture 프레임 말미 캡처를 사용한다. UI는 Play Mode에서 다음 방식으로 요청한다.

```sh
unity command mukjump_capture_ui --project-path "$MUKJUMP_PROJECT" --format json
```

`queued`는 촬영 완료가 아니다. 다음 프레임 뒤 반환된 절대 경로의 PNG가 실제 생성됐는지 확인하고 직접 연다. 저장 경로는 가져오기 대상 밖의 output/qa/pipeline-captures로 고정하고 매번 새 파일을 만든다. 에디터 일시정지·Edit Mode에서는 거절하며, 자동으로 게임을 시작하거나 재개하지 않는다. 파일 경로만 반환받아 불필요한 base64 출력을 피한다. Game 뷰의 원래 해상도로 촬영하며 에디터 캡처를 실기기 안전 영역 검증이라고 보고하지 않는다.

## Player 안전 경계

- `ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json`의 `enableInBuilds`와 `autoStart`를 명시적 Boolean false로 유지한다.
- `MukJumpAgentSafety`가 일반 스토어 사전 검사와 Unity Player 빌드 시작에서 동일하게 검사한다. Development/QA 빌드에도 적용한다.
- 설정 누락, 잘못된 JSON, 중복 키, 문자열 false, 활성화 설정, 미검증 패키지 버전, Resources의 잔여 Pipeline 설정을 발견하면 빌드를 중단한다. 무단 삭제·복구·경고 무시는 하지 않는다.
- 공식 패키지 코드의 로컬 요청 제한·브라우저 Origin 거부·bearer 인증을 확인했다. 인증 토큰을 공유하거나 외부 인터페이스로 우회 연결하지 않는다.
- **패키지 전체가 Editor 전용인 것은 아니다.** 설정이 꺼져 있으면 런타임 서버·드라이버를 생성하지 않지만, 런타임 콘솔 버퍼 코드는 존재한다(최대 2,000개). 출시 앱에 패키지 코드가 전혀 포함되지 않는다고 표현하지 않는다. 다음 실제 모바일 빌드에서도 크기·시작 시간·로그 메모리를 확인한다.
- 원격 hot reload나 Player 제어가 필요해져도 안전 설정을 먼저 켜지 말고 별도 요구사항과 배포 경계를 설계한다.

## 스킬 선택 기준

- UI·노치·정렬: unity:ui → 현재 Canvas 기반에 맞는 unity:ui-ugui.
- 일본어·폰트: unity:localization. 현재 커스텀 번역 체계를 유지하며 필요한 경우만 패키지 변경.
- 소리 메모리·품질: unity:optimize-audio. 믹서 경로를 바꾸는 요청이면 audio-setup-mixers.
- 그림 자산·프레임: sprite-editor / manage-sprite-atlas. 픽셀 아트가 아닌 수묵 그래픽에 pixel-perfect 설정을 강제하지 않는다.
- 기존 뒤끝 동기화: 뒤끝 구현·공식 SDK부터 진단. Unity Services를 새로 넣어 교체하지 않는다.
- 현재 사용하지 않는 LevelPlay·IAP·Vivox·멀티플레이·URP 전환은 자동 적용하지 않는다.

스킬은 해당 작업의 SKILL.md를 먼저 읽고 적용한다. 전체 31개를 매 작업에 모두 로드하지 않는다. AGENTS.md의 단일 작성자·main·씬 빌더·배포 규칙이 우선한다.

## 이번 적용의 검증 결과

- 묶음 회귀 278개 통과, 실패·건너뜀 0. 낮/밤 각 10개 구간의 드로잉·아이템·바람·일시정지·사망·재시작을 실제 Play Mode에서 검사했다.
- 촬영 보완 후 도구 검사 22개 통과. 로비 자동 재시작 방지 시나리오도 CLI를 통해 실제 Play Mode에서 다시 통과했다.
- 전용 촬영 명령의 PNG를 열어 로고·점수·시작/성장/옵션 버튼이 포함되는지 확인했다. 테스트용 메모리 저장소를 사용했으며 실제 서버 계정을 삭제하거나 수정하지 않았다.
- iOS 비개발 조건의 managed 코드 36개 어셈블리 컴파일 확인. 네이티브 Xcode 빌드·서명·TestFlight 업로드 검증은 이번 작업 범위가 아니다.
- 최종 감사: 6000.5.9f1/iOS, 저장된 씬 소스 일치, 로드된 Main 누락 스크립트 0, Player 안전 검사 문제 0.

로컬 증거: `output/qa/unity-plugin-20260911/`의 `regression.xml`, `agent-tooling-final.json`, `cli-play-boundary.json`, `ios-managed-result.json`. 원본 UI 캡처는 `output/qa/pipeline-captures/`에 있다. 시험 중 만든 일회성 설치 스크립트는 제거했고, Assets에 잘못 생성된 촬영 파일은 검증 출력 폴더로 이동했다.
