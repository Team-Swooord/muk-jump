---
name: unity-2d-urp-android-vfx
version: 1.0.0
language: ko-KR
last_verified: 2026-07-27
project_editor_current: 6000.3.10f1
project_editor_recommended: 6000.3.20f1
render_pipeline: Universal Render Pipeline / 2D Renderer
platform: Android
primary_topics:
  - 2D Particle System
  - Shader Graph
  - Card VFX
  - Reward and acquisition VFX
  - UI particles
  - Mobile optimization
  - VFX architecture and pooling
license_note: 프로젝트 내부 운영 규칙 문서. 인용한 외부 저장소의 코드는 각 저장소 라이선스를 따른다.
---

# Unity 2D URP Android VFX Master Skill

> **대상 프로젝트**: Unity 6.3, 2D, URP 2D Renderer, Android, 카드/수집/전투/보상 중심 모바일 게임  
> **현재 프로젝트 버전**: `6000.3.10f1`  
> **권장 검증 버전**: `6000.3.20f1`  
> **문서 목적**: 사람 또는 AI 코딩 에이전트가 이 문서 하나를 읽고, 모바일에서 아름답고 읽기 쉬우며 유지보수 가능한 2D VFX를 설계·구현·검증하도록 만드는 실전 운영 규칙

---

<!-- GENERATED_TOC_START -->
## 전체 목차

- [0. 이 문서를 사용하는 법](#section-0)
- [1. 프로젝트 기준 결정 요약](#section-1)
- [2. Android 목표 기기와 성능 등급](#section-2)
- [3. 프레임·메모리·오버드로 예산](#section-3)
- [4. Android Player/URP 권장 설정](#section-4)
- [5. 패키지 도입 기준](#section-5)
- [6. 프로젝트 폴더·이름·프리팹 표준](#section-6)
- [7. VFX 시각 문법](#section-7)
- [8. Particle System 모듈 실전 지침](#section-8)
- [9. 파티클 텍스처 제작·임포트 표준](#section-9)
- [10. 블렌딩 선택표](#section-10)
- [11. Shader Graph 프로젝트 표준](#section-11)
- [12. Shader Graph 핵심 레시피](#section-12)
- [13. 카드 렌더링 아키텍처](#section-13)
- [14. 카드 이벤트별 VFX 설계](#section-14)
- [15. 획득·보상 VFX 설계](#section-15)
- [16. 전투 VFX 레시피](#section-16)
- [17. UI VFX 레시피](#section-17)
- [18. 2D Light·Normal Map·Bloom](#section-18)
- [19. 런타임 VFX 아키텍처](#section-19)
- [20. 품질 등급과 자동 축소](#section-20)
- [21. 카드 렌더링과 MaterialPropertyBlock](#section-21)
- [22. UI 보상 아이콘 비행](#section-22)
- [23. 카드·보상·전투 VFX 정밀 레시피](#section-23)
- [24. Shader Graph 제작 표준](#section-24)
- [25. Particle System 제작 표준](#section-25)
- [26. VFX 텍스처·Flipbook 제작 파이프라인](#section-26)
- [27. 에디터 자동 검증 도구](#section-27)
- [28. 프로파일링과 성능 승인](#section-28)
- [29. Android 출시 설정과 기기 전략](#section-29)
- [30. 타이밍·모션·카메라 연출](#section-30)
- [31. 오디오·햅틱과 VFX 동기화](#section-31)
- [32. 접근성·가독성·광과민성](#section-32)
- [33. 테스트와 QA 체크리스트](#section-33)
- [34. AI 에이전트 작업 프로토콜](#section-34)
- [35. 개발용 VFX 디버그 도구](#section-35)
- [36. 문제 해결 사전](#section-36)
- [37. 구현 로드맵](#section-37)
- [38. 패키지·라이선스·업데이트 운영](#section-38)
- [39. 참고 자료와 검증 기준일](#section-39)
- [40. 용어집](#section-40)
- [41. 빠른 치트시트](#section-41)
- [42. 최종 마스터 체크리스트](#section-42)

<!-- GENERATED_TOC_END -->

<a id="section-0"></a>
# 0. 이 문서를 사용하는 법

이 문서는 단순한 아이디어 모음이 아니다. 이 프로젝트에서 이펙트를 제작할 때 따라야 하는 **사양서, 스타일 가이드, 구현 규칙, 성능 예산, 코드 패턴, 검수표**다.

AI 에이전트 또는 개발자는 이펙트 작업을 시작할 때 다음 순서를 지킨다.

1. 이펙트의 게임플레이 의미를 한 문장으로 정의한다.
2. 화면 위치가 `World`, `Screen Space UI`, `World Space Canvas` 중 어디인지 결정한다.
3. 목표 품질 등급 `Low / Medium / High`를 동시에 설계한다.
4. 필요한 레이어를 최대 3~6개로 분해한다.
5. 기존 머티리얼·텍스처·프리팹 재사용 가능성을 먼저 확인한다.
6. 새 셰이더가 필요한지, Shader Graph로 충분한지, NovaShader로 충분한지 판단한다.
7. 오브젝트 풀링, 정렬, 마스킹, 타임스케일, 화면 회전·해상도 변화를 고려한다.
8. 실제 저사양 Android 기기에서 프로파일링한다.
9. 성능·가독성·접근성 검수 후 완료 처리한다.

## 0.1 절대 우선순위

아래 우선순위는 충돌할 때 위에서 아래 순서로 적용한다.

1. **게임플레이 정보 전달**: 무엇을 획득했고, 무엇이 맞았고, 어떤 상태가 되었는지 즉시 이해되어야 한다.
2. **시각적 위계**: 중요한 카드·희귀도·보상·치명타가 일반 행동보다 분명히 강해야 한다.
3. **프레임 안정성**: 평균 FPS보다 프레임 타임 급등과 발열 억제가 중요하다.
4. **입력 방해 금지**: 이펙트가 버튼 클릭, 카드 드래그, 텍스트 판독을 방해하면 안 된다.
5. **재사용성**: 색상·크기·희귀도·대상만 바꾸어 재사용 가능한 구조를 우선한다.
6. **아트 품질**: 위 조건을 만족한 뒤 더 아름답게 만든다.

## 0.2 금지 규칙

- 전투 중 반복되는 이펙트에서 `Instantiate`와 `Destroy`를 매번 호출하지 않는다.
- 런타임에 `renderer.material`을 반복 호출해 머티리얼 인스턴스를 무제한 생성하지 않는다.
- 화면 전체를 덮는 반투명 쿼드를 여러 장 겹치지 않는다.
- 저사양 기준을 확인하지 않고 Bloom, Distortion, Noise, Trails, Sub Emitters, 2D Light를 모두 켜지 않는다.
- 동일 기능을 가진 Shader Graph를 효과마다 복제하지 않는다. 공통 Sub Graph 또는 공통 셰이더를 만든다.
- `Packages/manifest.json`에 Git 패키지를 브랜치명이나 `main`으로 고정하지 않는다. 검증한 태그를 명시한다.
- Unity Editor, URP, Shader Graph 패키지를 서로 호환되지 않는 버전으로 수동 조합하지 않는다.
- Unity Editor Game 뷰에서만 보고 성능 완료 판정을 내리지 않는다.
- 희귀도 전달을 색만으로 해결하지 않는다. 형태, 속도, 음향, 텍스트, 입자 밀도를 함께 사용한다.
- 번쩍임을 짧은 시간에 과도하게 반복하지 않는다. 광과민성·멀미 옵션을 제공한다.

---

<a id="section-1"></a>
# 1. 프로젝트 기준 결정 요약

## 1.1 권장 기본값

| 항목 | 프로젝트 기본값 | 이유 |
|---|---|---|
| Unity | `6000.3.20f1`로 검증 후 승격 | 현재 `6000.3.10f1` 이후 2D, URP, Android 관련 수정 포함 |
| Render Pipeline | URP 2D Renderer | Sprite Lit/Unlit, Light2D, 2D Shadow, 2D Shader Graph 사용 |
| Color Space | Linear | 알파·가산 혼합과 조명 결과의 일관성. 아트 색은 실제 기기에서 재보정 |
| Android Minimum API | **API 28 권장** | Unity 자체 최저 API 25보다 테스트 행렬을 줄이는 실무 기준. 서비스 요구에 따라 25~27로 낮출 수 있음 |
| Android Target API | **API 36** | 2026-08-31 이후 Google Play 신규 앱·업데이트 요구사항 대응 |
| Scripting Backend | IL2CPP | Android 릴리스 기본 |
| Target Architecture | ARM64 필수, ARMv7은 실제 사용자 가치가 확인될 때만 추가 | 빌드 크기·테스트 비용 관리 |
| Graphics API | OpenGLES3 우선, Vulkan 보조 검증 | 2D 게임에서 광범위한 드라이버 안정성을 우선. Vulkan 우선이 더 빠른 기기군은 원격 설정으로 분리 가능 |
| Frame target | 중급 이상 60 FPS, 최저 기기 30 FPS 안정 모드 | 16.67 ms와 33.33 ms 예산을 명확히 분리 |
| MSAA | Off 또는 2x | 알파 기반 2D 스프라이트는 4x MSAA 효율이 낮은 경우가 많음 |
| HDR/Bloom | High 또는 일부 Medium에서만 | Low에서는 가짜 글로우 스프라이트로 대체 |
| Depth Texture | 기본 Off | 2D에서는 비용 대비 이득이 낮음. 꼭 필요한 카메라·품질에서만 On |
| Opaque Texture | 기본 Off | 화면 샘플링 효과가 필요할 때만 켜고 비용 측정 |
| Post Processing | 최소 구성 | Bloom/Color Adjustments 정도부터 시작, Full Screen 효과 중첩 금지 |
| UI particle | `ParticleEffectForUGUI` 안정판 | Canvas 정렬·마스크·Trail을 별도 카메라/RT 없이 처리 |
| UI filters | `UIEffect` 안정판 | 카드/버튼의 Grayscale, Dissolve, Blur, Shine 계열 재사용 |
| Particle uber shader | `NovaShader` 선택 도입 | Flow, Flipbook, Dissolve, Emission, Custom Data를 공통화 |

> `API 28`은 “전 세계에서 가장 많이 쓰이는 특정 최저 단말”이라는 의미가 아니다. 공개 Android 대시보드는 단일 모델별 실제 사용자 점유율을 제공하지 않는다. 출시 전에는 아래의 보수적인 기기 등급으로 시작하고, 출시 후 Google Play Console의 **Reach and devices**에서 우리 앱과 유사 앱의 RAM, SoC, GPU API, Android 버전 분포를 확인해 조정한다.

## 1.2 Unity 업데이트 정책

현재 프로젝트는 `6000.3.10f1`이지만, 이 문서 작성일 기준 확인된 6.3 LTS 최신 패치는 `6000.3.20f1`이다. 업데이트는 다음 절차로만 수행한다.

1. Git 작업 트리가 깨끗한지 확인한다.
2. `upgrade/unity-6000.3.20f1` 브랜치를 만든다.
3. 다음 파일을 별도 보관한다.
   - `ProjectSettings/ProjectVersion.txt`
   - `Packages/manifest.json`
   - `Packages/packages-lock.json`
   - URP Asset, Renderer2DData, Volume Profile
   - Android Custom Gradle/Manifest/Proguard 파일
4. 새 Editor로 프로젝트를 연다.
5. 자동 API 업데이터 결과를 커밋과 분리한다.
6. 모든 Shader Graph를 열고 저장하지 말고, 먼저 전체 빌드와 셰이더 컴파일을 확인한다.
7. 아래 스모크 테스트를 수행한다.
   - 메인 메뉴 UI 마스크/파티클
   - 카드 드래그·회전·플립
   - Sprite Lit/Normal Map/Light2D
   - Bloom과 카메라 Volume
   - Android IL2CPP ARM64 Development Build
   - OpenGLES3와 Vulkan 각각 실행
   - 앱 일시정지/복귀, 화면 회전, 백그라운드 복귀
   - 10분 반복 전투 후 메모리 증가 확인
8. 이상이 없을 때만 기본 브랜치에 병합한다.
9. `Library` 폴더 삭제는 첫 해결책으로 사용하지 않는다. 패키지/임포트 캐시 문제로 확인된 경우에만 삭제한다.

### 업데이트 판단 기준

- **즉시 업데이트 후보**: Android 빌드 불가, 2D Renderer 렌더 오류, URP 메모리 누수, 특정 GPU 크래시, Google Play 정책 대응.
- **한 패치 대기 후보**: 신규 기능만 필요하고 현재 프로젝트가 안정적일 때.
- **프리뷰/베타 금지**: 출시 브랜치에는 베타 Editor, preview UPM 패키지, Git main 브랜치를 넣지 않는다.
- “좋은 파티클을 위해 최신 Unity가 필요하다”는 이유만으로 올리지 않는다. 필요한 셰이더/패키지의 최소 버전과 실제 기능 차이를 먼저 확인한다.

---

<a id="section-2"></a>
# 2. Android 목표 기기와 성능 등급

## 2.1 단일 ‘가장 많이 쓰이는 최저 폰’은 존재하지 않는다고 가정한다

국가, 장르, 광고 채널, 스토어, 사용자 연령에 따라 단말 분포가 크게 달라진다. 따라서 특정 모델 하나를 보편적 최저 기준이라고 단정하지 않는다. 공개 Android 분포 자료는 Vulkan/OpenGL ES 같은 전체 생태계 비율을 보여 주지만, 실제 앱의 최저 기기는 Play Console 데이터로 결정해야 한다.

프로젝트 초기에는 아래 세 등급으로 테스트한다.

## 2.2 권장 기기 등급

### Tier L — 출시 하한/스트레스 기기

- RAM: 3 GB
- CPU: 저전력 Cortex-A53/A55 중심 8코어급
- GPU 예시 범주: PowerVR GE8320, Mali-G52 MP2, Adreno 506~610 부근
- 해상도: 720p 또는 1080p 저가 패널
- OS: Android 9~11
- 목표: **30 FPS 안정**, 심한 전투에서도 장시간 프레임 타임 급등 최소화
- 대표 실기 예시: Galaxy A12/A13 저용량 RAM 변형, Redmi 9A 계열과 비슷한 성능대
- 주의: 모델명은 시장 점유율 주장용이 아니라 성능 스트레스 재현용이다.

### Tier M — 주력 대중 기기

- RAM: 4~6 GB
- CPU: Cortex-A55/A76 혼합 또는 동급
- GPU 예시 범주: Adreno 610/619, Mali-G57 계열
- OS: Android 11~15
- 목표: **60 FPS 우선**, 발열 시 30/45 FPS 또는 낮은 VFX 품질로 전환
- 카드 획득·희귀 연출에서 Bloom, 제한적 Distortion, Trail 허용

### Tier H — 고급 기기

- RAM: 8 GB 이상
- 최신 중상급 이상 GPU
- 목표: 60/90/120 Hz 지원 여부는 게임 설계와 배터리 정책에 따라 선택
- 더 높은 파티클 수보다 고해상도, 더 부드러운 Trail, 추가 조명, 후처리 품질에 예산을 사용한다.

## 2.3 필수 실기 테스트 매트릭스

| 구분 | 최소 수량 | 확인 사항 |
|---|---:|---|
| 3 GB 저사양 OpenGLES3 | 1대 | 메모리, 오버드로, 발열, 30 FPS |
| 4 GB 대중 OpenGLES3 | 1대 | 60 FPS, UI 파티클, 장시간 플레이 |
| Vulkan 지원 중급 | 1대 | 셰이더 컴파일, 그래픽 오류, 프레임 타임 |
| 고주사율 중상급 | 1대 | 60/90/120 설정, 배터리, 애니메이션 타이밍 |
| 다양한 화면비 | 최소 3종 | 16:9, 19.5:9~20:9, 태블릿/폴더블 대응 |

에뮬레이터와 Device Simulator는 레이아웃 확인에만 사용하고, GPU/발열/메모리 완료 판정은 실기에서 한다.

---

<a id="section-3"></a>
# 3. 프레임·메모리·오버드로 예산

이 수치는 절대 법칙이 아니라 **프로젝트 시작 예산**이다. 실제 기기 측정값으로 업데이트한다.

## 3.1 프레임 시간

| 목표 FPS | 전체 프레임 예산 | VFX 시작 예산 권장 |
|---:|---:|---:|
| 60 | 16.67 ms | GPU 1.5~2.5 ms, CPU 0.5~1.0 ms |
| 45 | 22.22 ms | GPU 2.0~3.0 ms, CPU 0.7~1.2 ms |
| 30 | 33.33 ms | GPU 2.5~4.0 ms, CPU 0.8~1.5 ms |

VFX만 빠르고 나머지가 느리면 의미가 없다. 위 예산은 카메라 전체 프레임 안에서 VFX가 차지할 수 있는 시작 범위다.

## 3.2 동시 이펙트 예산

### Tier L 권장 시작값

- 화면 내 활성 ParticleSystem: 15~25개
- 실제 살아 있는 총 파티클: 일반 전투 250~500개, 피크 700개 이하부터 시작
- 화면의 25% 이상을 덮는 큰 투명 파티클: 동시 2~4장
- 화면 전체 플래시: 한 번에 1장, 80~150 ms, 알파 0.08~0.22
- Trail 활성 시스템: 0~3개
- Noise 모듈: 핵심 이펙트 0~2개
- Collision 모듈: 기본 금지, 반드시 필요할 때 소수
- Sub Emitter 단계: 최대 1단계
- Particle Light: 기본 0개
- 2D Light: 화면 내 동적 핵심 조명 1~3개부터 시작
- Bloom: Off 또는 매우 낮은 품질의 단일 Volume

### Tier M 권장 시작값

- 활성 ParticleSystem: 25~45개
- 총 파티클: 일반 500~1,000개, 피크 1,500개 이하부터 시작
- 큰 투명 파티클: 4~7장
- Trail: 3~8개
- Noise: 2~5개
- Sub Emitter: 최대 2단계이나 폭발적 증식 금지
- 제한적 Light2D/Bloom/Distortion 허용

### Tier H 권장 시작값

- 숫자를 무작정 2배로 늘리지 않는다.
- 해상도, Flipbook 보간, Trail 부드러움, Bloom 품질, 왜곡 해상도에 예산을 우선 배분한다.

## 3.3 오버드로 규칙

투명 파티클은 파티클 개수보다 **화면을 몇 번 덮는지**가 더 비쌀 수 있다.

- 작은 입자 100개가 화면 5%만 덮는 경우보다, 큰 연기 10장이 화면 전체를 덮는 경우가 더 비쌀 수 있다.
- 불꽃 외곽의 완전 투명 여백을 잘라 텍스처 Tight 영역을 줄인다.
- 큰 부드러운 Glow는 3~5장을 겹치지 말고 1~2장으로 합친다.
- 동일 위치의 Alpha Blend + Additive + Screen 플레어를 모두 쓰지 않는다.
- UI 팝업 뒤의 Dim, Blur, 카드 Glow, 파티클이 동시에 전체 화면을 덮는지 확인한다.
- Rendering Debugger의 Overdraw 모드와 Frame Debugger로 실제 겹침을 본다.
- 알파가 거의 0인 파티클도 프래그먼트 비용을 낼 수 있으므로 수명 말기에 빠르게 제거한다.
- 화면 밖 파티클이 계속 시뮬레이션되는지 확인하고 Culling Mode를 설계한다.

## 3.4 메모리 시작 예산

- VFX 전용 압축 텍스처 총량: Tier L 기준 32~64 MB 이내에서 시작
- 단일 일반 파티클 텍스처: 128~512 px
- 핵심 Flipbook: 512~1024 atlas, 셀 수와 압축 품질을 함께 조절
- UI 카드 희귀도 전용 고해상도 마스크: 512~1024, 가능하면 채널 패킹
- 2048/4096 텍스처는 카드 원화·대형 배경처럼 정당한 이유가 있을 때만 사용
- 런타임 생성 Material 수, RenderTexture 수, 임시 RT 크기를 Memory Profiler로 확인
- Addressables를 사용하더라도 참조가 남아 있으면 메모리가 해제되지 않으므로 핸들 수명 관리

---

<a id="section-4"></a>
# 4. Android Player/URP 권장 설정

## 4.1 Player Settings

```text
Company Name              = 프로젝트 값
Product Name              = 프로젝트 값
Default Orientation       = 게임 설계에 맞춤
Color Space               = Linear
Auto Graphics API         = Off
Graphics APIs             = OpenGLES3, Vulkan  (순서는 실기 데이터로 변경)
Scripting Backend         = IL2CPP
Api Compatibility Level   = .NET Standard 또는 프로젝트 요구값
Target Architectures      = ARM64
Minimum API Level         = Android 9.0 / API 28 권장
Target API Level          = Android 16 / API 36 또는 Highest Installed(API 36 설치 확인)
Managed Stripping Level   = Medium부터 검증
Active Input Handling     = Input System 또는 프로젝트 단일 체계
Optimize Mesh Data        = On 전후 빌드 검증
Multithreaded Rendering   = On 전후 저사양 실기 측정
Vulkan Graphics Jobs      = 기본 Off에서 시작, 별도 브랜치로 측정
```

### Graphics API 선택 원칙

공개 Android 활성 기기 자료에서 OpenGL ES 3.2 지원 비율은 매우 높고, Vulkan 미지원 기기도 여전히 존재한다. 이 2D 프로젝트는 Vulkan 전용 기능을 요구하지 않으므로 다음 정책을 권장한다.

1. 초기 출시: OpenGLES3 우선 + Vulkan 포함.
2. Vulkan에서 명확한 성능 이점이 있고 그래픽 오류율이 낮은 기기군이 확인되면 Vulkan 우선 실험.
3. 원격 설정 또는 기기 등급표로 API/품질 선택을 관리.
4. 셰이더 오류, 검은 스프라이트, Trail 깨짐, UI 마스크 오류를 두 API에서 모두 테스트.

## 4.2 URP Asset — Low

```text
HDR                         = Off
MSAA                        = Disabled 또는 2x
Render Scale                = 0.80~1.00 (픽셀 아트는 별도 규칙)
Depth Texture               = Off
Opaque Texture              = Off
SRP Batcher                 = On
Dynamic Batching            = 측정 후 결정
Main Light Shadows          = Off 또는 최소
Additional Lights           = Per Vertex/Disabled
Additional Light Shadows    = Off
Soft Shadows                = Off
Terrain/HDRP 관련 기능      = 사용하지 않음
Post Processing             = 최소
```

## 4.3 URP Asset — Medium

```text
HDR                         = 필요 시 On
MSAA                        = 2x 또는 Off
Render Scale                = 0.90~1.00
Depth Texture               = 필요한 카메라만
Opaque Texture              = 화면 샘플 효과가 있을 때만
Post Processing             = Bloom + Color Adjustments 중심
```

## 4.4 URP Asset — High

```text
HDR                         = On 가능
MSAA                        = 2x 중심, 4x는 실기 이득 확인 시
Render Scale                = 1.00
Depth/Opaque Texture        = 필요한 연출에서 허용
Bloom                       = 품질 상향 가능
Distortion                  = 제한적 허용
```

## 4.5 2D Renderer Data

- 기본 Renderer는 `Renderer2DData`를 사용한다.
- Light Blend Style은 실제 사용하는 수만 유지한다. 사용하지 않는 Blend Style을 셰이더 변형 이유로 남기지 않는다.
- Foremost Sorting Layer, Camera Sorting Layer Texture 같은 기능은 필요성이 명확할 때만 켠다.
- 2D Light의 Normal Map 품질과 Shadow 사용은 품질 단계별로 분리한다.
- 커스텀 Renderer Feature는 2D Renderer 지원 여부를 반드시 확인한다. Universal Forward Renderer 전용 기능을 2D Renderer에 그대로 적용한다고 가정하지 않는다.
- 카메라 Stack은 UI/이펙트 분리에 편리하지만 추가 렌더와 오버드로를 만들 수 있으므로 기본 해법으로 사용하지 않는다.

---

<a id="section-5"></a>
# 5. 패키지 도입 기준

## 5.1 권장 고정 버전

문서 검증일 기준 권장 시작 태그:

```json
{
  "dependencies": {
    "jp.co.cyberagent.nova": "https://github.com/CyberAgentGameEntertainment/NovaShader.git?path=/Assets/Nova#3.6.0",
    "com.coffee.ui-particle": "https://github.com/mob-sakai/ParticleEffectForUGUI.git#4.13.3",
    "com.coffee.ui-effect": "https://github.com/mob-sakai/UIEffect.git?path=Packages/src#5.11.5"
  }
}
```

> 위 블록을 기존 `manifest.json`에 통째로 덮어쓰지 않는다. 현재 `dependencies` 안에 필요한 항목만 병합한다. 설치 전 Git 커밋을 남기고, 각 패키지를 하나씩 설치하여 컴파일·Android 빌드를 확인한다.

## 5.2 NovaShader 사용 범위

권장:

- 일반 월드 파티클의 Uber Unlit
- Flow Map
- Flipbook
- Dissolve/Alpha Transition
- Emission
- Custom Data/Custom Vertex Streams
- 공통 파티클 머티리얼 수 축소
- 사용하지 않는 참조 제거와 최적화 셰이더 생성

주의:

- Nova 문서의 Screen Space Distortion 설정은 Forward Renderer Data를 기준으로 한다. 2D Renderer에서는 동일하게 동작한다고 가정하지 않는다.
- 2D Renderer 프로젝트에서 Distortion이 필요하면 먼저 작은 실험 씬에서 Android OpenGLES3/Vulkan을 검증한다.
- 동작하지 않거나 비용이 높으면 다음으로 대체한다.
  - 카드 자체 UV 왜곡
  - 왜곡처럼 보이는 노이즈 마스크 애니메이션
  - 작은 국소 RenderTexture
  - High 품질 전용 Universal Renderer 카메라
- Soft Particle/Depth Fade를 쓰지 않으면 Depth Texture를 켜지 않는다.
- Nova Flipbook 모드를 쓸 때는 문서 지침에 따라 Particle System의 Texture Sheet Animation과 중복 사용하지 않는다.

## 5.3 ParticleEffectForUGUI 사용 범위

- 카드 획득 팝업의 별·빛·가루
- 코인/보석이 카운터로 날아가는 연출
- UI 마스크 안에서 보이는 파티클
- RectMask2D 내부 파티클
- Canvas sibling 순서가 필요한 파티클
- UI Trail
- `UIParticleAttractor`를 이용한 흡수/수집 연출
- 동일 효과 대량 표시 시 Mesh Sharing 검토

안정판 `4.13.3`을 우선하고 `5.0.0-preview`는 출시 브랜치에 넣지 않는다.

## 5.4 UIEffect 사용 범위

- 카드 잠금/비활성 Grayscale
- 카드 획득 Dissolve/Revealing
- 선택/호버 Shine
- 희귀 카드 Edge/Glow
- 버튼 눌림 왜곡·색조
- 피격 시 UI RGB Shift를 아주 짧게 사용
- TextMeshPro 희귀도 텍스트 연출
- 반복되는 UI 효과를 Preset으로 관리

UIEffect는 실제 사용하는 Variant 중심으로 빌드하도록 설계되어 있지만, 프로젝트 설정의 Registered/Unregistered Variants를 계속 정리한다.

## 5.5 패키지 라이선스 및 유지보수

- 위 세 저장소는 MIT 라이선스다. 배포물의 Third-Party Notices에 라이선스 사본을 포함한다.
- 패키지 업데이트는 월 1회 확인하되, 출시 직전에는 결함 수정이 아닌 기능 업데이트를 금지한다.
- Git 태그를 올릴 때 changelog, Unity 호환성, Android 빌드, UI 마스크, 메모리 누수를 확인한다.
- 패키지를 수정해야 하면 Embedded Package로 옮기고 변경 이유·원본 태그·커밋을 기록한다.


---

<a id="section-6"></a>
# 6. 프로젝트 폴더·이름·프리팹 표준

## 6.1 권장 폴더 구조

```text
Assets/
└── Game/
    ├── Art/
    │   └── VFX/
    │       ├── Textures/
    │       │   ├── Masks/
    │       │   ├── Noise/
    │       │   ├── Flipbooks/
    │       │   ├── Trails/
    │       │   ├── UI/
    │       │   └── Cards/
    │       ├── Materials/
    │       │   ├── Particles/
    │       │   ├── Sprites/
    │       │   ├── UI/
    │       │   └── Cards/
    │       ├── ShaderGraphs/
    │       │   ├── Master/
    │       │   ├── SubGraphs/
    │       │   ├── Cards/
    │       │   ├── UI/
    │       │   └── Debug/
    │       ├── Animations/
    │       ├── Timelines/
    │       └── Palettes/
    ├── Prefabs/
    │   └── VFX/
    │       ├── World/
    │       ├── UI/
    │       ├── Cards/
    │       ├── Rewards/
    │       ├── Combat/
    │       ├── Environment/
    │       └── Debug/
    ├── Scripts/
    │   └── VFX/
    │       ├── Runtime/
    │       ├── Editor/
    │       ├── Data/
    │       └── Tests/
    ├── Settings/
    │   └── VFX/
    └── Addressables/
        └── VFX/
```

## 6.2 접두사 규칙

| 자산 | 접두사 | 예시 |
|---|---|---|
| VFX Prefab | `VFX_` | `VFX_CardAcquire_Legendary` |
| UI VFX Prefab | `VFX_UI_` | `VFX_UI_CoinFly` |
| Particle Material | `M_VFX_` | `M_VFX_Add_Spark_Gold` |
| Sprite Material | `M_SPR_` | `M_SPR_CardFoil` |
| UI Material | `M_UI_` | `M_UI_CardDissolve` |
| Shader Graph | `SG_` | `SG_CardFoil_URP` |
| Sub Graph | `SGF_` | `SGF_RadialRing` |
| VFX Texture | `T_VFX_` | `T_VFX_Spark_01` |
| Mask | `T_MSK_` | `T_MSK_CardBorder_Rare` |
| Noise | `T_NOI_` | `T_NOI_Flow_Cloud_01` |
| Flipbook | `T_FLIP_` | `T_FLIP_Explosion_4x4` |
| ScriptableObject definition | `VFXD_` | `VFXD_Reward_CardLegendary` |
| Animation Clip | `A_VFX_` | `A_VFX_CardReveal_Rare` |
| Timeline | `TL_VFX_` | `TL_VFX_ChestOpen` |
| Quality Profile | `VFXQ_` | `VFXQ_Android_Low` |

## 6.3 프리팹 계층 표준

```text
VFX_CardAcquire_Legendary
├── Root                       # 위치/회전/스케일, Pool 제어
├── Anchor                     # 카드 또는 UI 기준점
├── CoreFlash                  # 0.00~0.15 s
├── Ring                       # 0.05~0.40 s
├── Rays                       # 0.08~0.80 s
├── SparksNear                 # 작은 고주파 입자
├── SparksFar                  # 큰 저주파 입자
├── BorderGlow                 # 카드 마스크 기반
├── RarityGlyph                # 전설 문양
├── TrailOrOrbit               # Medium/High 전용
├── Light2D                    # High 전용, 기본 Disabled
├── AudioEmitter               # 선택
└── PoolReturn                 # 완료 시 반환
```

원칙:

- 루트에는 시뮬레이션 로직을 최소화한다.
- 각 하위 시스템은 한 가지 역할만 가진다.
- 품질별 오브젝트는 `Quality_Low`, `Quality_Medium`, `Quality_High` 또는 설정 컴포넌트로 제어한다.
- 이름에 `Particle System (1)` 같은 기본 이름을 남기지 않는다.
- 프리팹 루트 스케일은 `(1,1,1)`을 유지한다.
- 월드 크기와 UI 크기를 혼용하지 않는다.
- 프리팹 Variant를 희귀도마다 무한 생성하기보다 `VfxDefinition` 데이터로 색·개수·속도·오디오를 바꾼다.

## 6.4 Sorting Layer 권장안

```text
Background
EnvironmentBack
CharactersBack
Characters
CharactersFront
WorldVFXBack
WorldVFX
WorldVFXFront
Foreground
WorldUI
ScreenTransition
```

UI Canvas 내부는 sibling index를 기본으로 하고, 다음 범위를 문서화한다.

```text
0~99      일반 UI
100~199   카드 본체
200~299   카드 내부 이펙트
300~399   획득/희귀도 오버레이
400~499   팝업 전면 파티클
500+      화면 전환/긴급 알림
```

---

<a id="section-7"></a>
# 7. VFX 시각 문법

## 7.1 좋은 이펙트의 기본 5단계

1. **Anticipation / 예고**: 50~250 ms 동안 힘이 모이거나 방향이 드러난다.
2. **Impact / 타격**: 1~3 프레임 안에 밝기·크기·형태가 급변한다.
3. **Expansion / 확장**: 파편, 링, 광선이 바깥으로 퍼진다.
4. **Follow-through / 잔동작**: Trail, 먼지, 작은 별이 움직임을 이어 준다.
5. **Settle / 정착**: 중요한 결과가 남고 나머지는 빠르게 사라진다.

모든 효과에 5단계가 모두 필요하지는 않다. 일반 버튼은 2~3단계, 전설 카드 획득은 5단계를 쓴다.

## 7.2 시간 주파수 분리

고급스러운 이펙트는 모든 레이어가 같은 속도로 움직이지 않는다.

- **고주파**: 30~120 ms 스파크, 플래시, 작은 파편
- **중주파**: 200~600 ms 링, 광선, 카드 스케일, 텍스트 팝
- **저주파**: 800~2,000 ms 오라, 부유 먼지, 홀로그램 흐름

한 효과에 고·중·저 주파수를 섞으면 풍부해진다. 모든 레이어를 0.5초에 동시에 시작하고 끝내면 기계적으로 보인다.

## 7.3 형태 언어

| 의미 | 주 형태 | 보조 형태 |
|---|---|---|
| 공격/치명타 | 날카로운 삼각형, 선, Slash | 파편, 충격 링 |
| 방어/보호 | 원, 육각형, 닫힌 테두리 | 부드러운 파동 |
| 회복 | 위로 상승, 잎, 십자, 둥근 점 | 밝은 링 |
| 독/저주 | 불규칙 blob, 아래로 흐름 | 거품, 연기 |
| 얼음 | 결정, 직선, 각진 균열 | 서리 마스크 |
| 불 | 위로 휘는 곡선, 찢어진 실루엣 | 재, 연기 |
| 전기 | 불연속 지그재그 | 작은 잔광 |
| 일반 보상 | 원형 Burst | 별, 코인 |
| 희귀 보상 | 대칭, 정교한 문양 | 광선, 오라 |
| 전설 보상 | 화면 중심 집중, 왕관/문장 | 느린 금빛 먼지 |

## 7.4 값과 색의 위계

- 중요 효과는 단순히 채도를 높이지 말고 **밝기 대비와 형태 명확성**을 높인다.
- 흰색 중심부 + 고유 색 외곽 조합은 타격과 희귀도에 효과적이다.
- 일반 등급과 전설 등급을 색만 바꾸지 않는다.
- 배경과 카드 아트가 화려할수록 이펙트 색 수를 줄인다.
- 한 효과의 주색 1개, 보조색 1개, 흰색 포인트를 기본으로 한다.
- Additive 파티클은 Linear Color Space에서 쉽게 과노출되므로 HDR Color 강도를 낮게 시작한다.
- 검은색/어두운 연기는 Alpha Blend 또는 Multiply 계열을 사용한다. Additive로 어두운 색은 표현되지 않는다.

## 7.5 크기와 화면 점유율

- 일반 카드 선택: 카드 너비의 3~8% 테두리 Glow
- 희귀 카드 획득: 카드 너비의 120~180% 링
- 전설 카드 획득: 순간적으로 200~260% 광선 가능, 150 ms 이내 축소
- 일반 히트 스파크: 대상 크기의 20~50%
- 치명타: 70~130%이나 텍스트·HP를 가리지 않게 방향을 둔다.
- 전체 화면 플래시는 “보인다”보다 “느껴진다” 수준으로 낮춘다.

## 7.6 과장 규칙

- 같은 이벤트가 1초에 여러 번 발생하면 각 효과를 줄인다.
- 콤보 수가 높아질 때 파티클 개수보다 리듬·색·텍스트 스케일로 강도를 올린다.
- 여러 보상을 한 번에 받을 때 20개의 완전한 획득 연출을 동시에 재생하지 않는다. 대표 1개 + 요약 스트림으로 묶는다.
- 전설 연출은 플레이어가 스킵할 수 있게 하되, 스킵 시 최종 상태와 보상 적용은 즉시 보장한다.

---

<a id="section-8"></a>
# 8. Particle System 모듈 실전 지침

## 8.1 Main

권장:

- `Duration`: 효과 의미에 맞춘 최소 길이.
- `Looping`: 환경 효과만 사용. 전투/획득 효과는 기본 Off.
- `Start Lifetime`: 입자가 실제로 보이는 시간만. 거의 투명한 꼬리 시간을 줄인다.
- `Start Speed`: 방향성이 필요하면 사용, 궤적 제어가 필요하면 Velocity over Lifetime 사용.
- `Start Size`: Random Between Two Constants/Curves로 반복감 완화.
- `Simulation Space`:
  - 카드에 붙어야 함: Local
  - 카드가 움직여도 잔상이 남아야 함: World
  - 커스텀 기준: Custom
- `Scaling Mode`:
  - UI에서는 Hierarchy/Shape 차이를 테스트.
  - UIParticle 사용 시 Canvas 스케일 변화 검증.
- `Play On Awake`: 풀링 프리팹은 필요에 따라 Off로 통제.
- `Stop Action`: `Callback` 또는 별도 수명 컴포넌트로 Pool 반환.
- `Culling Mode`: 반복 효과는 `Pause And Catch-up`이 갑작스러운 버스트를 만들 수 있으므로 상황별 검증.
- `Max Particles`: 실제 피크의 110~130% 정도로 제한. 무제한 안전망으로 사용하지 않는다.

피해야 할 것:

- 수명 10초인데 1초 이후 거의 안 보이는 입자.
- 모든 효과의 Simulation Space를 무조건 World로 설정.
- `Max Particles`를 10,000으로 두고 안심하는 방식.

## 8.2 Emission

- 짧은 타격: `Burst` 중심.
- 지속 오라: 낮은 `Rate over Time`.
- 이동 궤적: `Rate over Distance`, 단 UI Particle에서는 움직이는 Transform 기준과 스케일을 검증.
- 여러 Burst를 사용해 리듬을 만든다.

예:

```text
Hit Spark
Burst 0.00s: 8~12
Burst 0.04s: 3~5 작은 잔광
Duration: 0.35s
```

저사양 축소 우선순위:

1. Rate 감소
2. Burst count 감소
3. 수명 감소
4. 화면 점유 면적 감소

## 8.3 Shape

- `Cone`: 폭발, 분사, 방향성 히트.
- `Circle/Donut`: 카드 중심 링, 오라.
- `Box`: UI 패널 배경 먼지.
- `Edge`: 선 형태 스파크.
- `Sprite/Sprite Renderer`: 카드 윤곽이나 아이콘 모양에서 방출할 때 유용하나 비용·샘플링을 측정.
- `Mesh`: 복잡한 형태에서만.

Shape Radius와 Start Speed를 동시에 크게 하면 제어가 어려워진다. 하나를 주된 확장 요인으로 정한다.

## 8.4 Velocity over Lifetime

- 탄도감 없이 직선 이동이 필요할 때.
- X/Y 방향 Random을 작게 주어 반복을 줄인다.
- Orbital Velocity는 카드 주위 공전 입자에 유용하지만 저사양에서는 입자 수를 줄인다.
- Speed Modifier Curve로 시작 급가속 후 감속을 만들 수 있다.

## 8.5 Limit Velocity over Lifetime

- 연기·먼지의 끝을 부드럽게 멈출 때.
- Dampen이 높으면 유체처럼 보이지만 과하면 힘이 사라진다.
- Drag를 이용한 자연스러운 감속은 수동 애니메이션보다 재사용성이 좋다.

## 8.6 Inherit Velocity

- 카드가 빠르게 움직일 때 파티클이 움직임을 이어받도록 한다.
- World Space Trail과 함께 쓰면 과도한 길이가 생길 수 있다.
- 드래그 중 생성되는 파티클은 현재 포인터 속도를 제한한 값으로 전달한다.

## 8.7 Force over Lifetime

- 위로 뜨는 보상 먼지, 중력, 바람에 사용.
- 랜덤 Force와 Noise를 동시에 강하게 쓰지 않는다.
- 2D 화면에서는 Z Force가 정렬 문제를 만들지 확인한다.

## 8.8 Color over Lifetime

권장 알파 곡선:

```text
0.00 -> 0
0.05 -> 1
0.55 -> 0.8
1.00 -> 0
```

Impact용:

```text
0.00 -> 1
0.20 -> 1
1.00 -> 0
```

- 가산 파티클은 알파와 RGB 밝기를 함께 낮춘다.
- 색 그라디언트는 2~4개의 의미 있는 키로 제한한다.
- 랜덤 시작색은 프로젝트 팔레트 범위 안에서만 사용한다.

## 8.9 Size over Lifetime

기본 패턴:

- Flash: `0 -> 1.2 -> 0`
- Smoke: `0.3 -> 1.0 -> 1.4`
- Spark: `1.0 -> 0.2`
- Ring: `0.2 -> 1.0`, 알파는 반대로 감소
- Reward star: `0 -> 1.1 -> 0.8 -> 0`

균일 스케일이 아닌 X/Y 분리로 Slash, Ray, Streak를 만든다.

## 8.10 Rotation over Lifetime

- 작은 랜덤 회전은 반복감 제거에 좋다.
- 별, 반짝이처럼 형태 방향이 중요한 입자는 너무 빠르게 돌리지 않는다.
- Billboard Alignment가 Velocity일 때 회전 축의 의미가 달라질 수 있다.

## 8.11 Noise

Noise 모듈은 여러 노이즈 샘플을 사용하므로 모바일에서 공짜가 아니다.

사용:

- 연기, 마법 먼지, 부유 오라.
- 낮은 Frequency와 중간 Strength로 큰 흐름을 만든다.
- 품질 단계에서 가장 먼저 끌 수 있는 장식 기능으로 설계한다.

피함:

- 0.2초 Hit Spark.
- 화면에 수십 개가 동시에 뜨는 코인.
- Strength와 Frequency가 모두 높아 떨리는 효과.

대체:

- 2~3개의 Velocity 랜덤 범위.
- 텍스처 자체 Flow Map.
- 사전에 구운 Flipbook.

## 8.12 Collision

기본 금지에 가깝게 취급한다.

- 꼭 필요한 세계 파편만 2D Collider 또는 Plane을 사용.
- `Collision Quality`, `Radius Scale`, `Max Collision Shapes`를 최소화.
- UI 파티클에는 물리 충돌보다 목표점 보간을 사용.
- 파티클마다 실제 게임플레이 판정을 하지 않는다. 게임플레이 충돌과 시각 파티클을 분리한다.

## 8.13 Triggers

- 특수 상호작용이 필요할 때만.
- 콜백에서 GC 할당, LINQ, 리스트 생성 금지.
- 게임플레이 핵심 판정은 파티클 Trigger에 의존하지 않는다.

## 8.14 Sub Emitters

좋은 용도:

- 큰 불꽃이 죽을 때 작은 잔광 2~3개.
- 카드 획득 중심 코어가 터질 때 링/파편.

금지:

- Sub Emitter가 다시 Sub Emitter를 연쇄 생성해 수가 기하급수적으로 늘어나는 구조.
- 모든 입자 사망 시 무거운 시스템 생성.

Low 품질에서는 Sub Emitter를 비활성화하거나 Burst 수를 줄인다.

## 8.15 Texture Sheet Animation / Flipbook

- 불, 연기, 폭발의 복잡한 내부 운동은 실시간 Noise보다 Flipbook이 예측 가능하다.
- 셀 간 보간은 품질을 높이지만 샘플 비용이 늘 수 있다.
- Flipbook atlas는 셀 패딩과 색 번짐을 확인한다.
- NovaShader Flipbook을 사용할 때 Particle System Texture Sheet Animation과 중복하지 않는다.
- FPS가 낮은 Flipbook은 프레임 랜덤 시작으로 반복감을 줄인다.

## 8.16 Lights

Particle System Light 모듈은 모바일 2D에서는 기본 사용하지 않는다.

대안:

- 작은 Additive Glow Sprite.
- 카드 주변의 단일 Light2D를 짧게 애니메이션.
- 화면 플래시.

Light2D가 반드시 필요하면 최대 동시 수, 영향 Sorting Layer, Normal Map 여부를 품질 설정에 포함한다.

## 8.17 Trails

- 핵심 이동체, 희귀 카드 궤도, 코인 비행에 사용.
- Min Vertex Distance를 너무 작게 두지 않는다.
- Width Curve 키를 최소화한다.
- Texture Mode와 Stretch/Tiled를 의도에 맞게 선택한다.
- Trail Material의 투명 영역과 오버드로를 줄인다.
- Low에서는 Trail을 끄거나 짧은 Streak 파티클로 대체한다.

## 8.18 Custom Data / Custom Vertex Streams

공통 셰이더 하나로 효과를 다양화할 핵심 도구다.

권장 매핑 예:

```text
Custom1.x = Dissolve progress
Custom1.y = Edge width
Custom1.z = Flow strength
Custom1.w = Emission multiplier
Custom2.x = UV rotation
Custom2.y = Distortion strength
Custom2.z = Rarity index normalized
Custom2.w = User seed
```

주의:

- Shader Graph/Nova가 기대하는 Vertex Stream 순서와 ParticleSystemRenderer의 Custom Vertex Streams가 일치해야 한다.
- 사용하지 않는 스트림은 제거한다.
- NovaShader의 `Fix Now` 기능으로 필수 스트림을 검증할 수 있다.
- UI Particle에서는 Animatable Properties와 Custom Vertex Stream 전달을 별도 테스트한다.

## 8.19 Renderer

- 대부분 `Billboard` 또는 `Stretched Billboard`.
- Mesh 모드는 실제 실루엣이나 GPU Instancing 이득이 있을 때.
- `Render Alignment = Velocity`는 Slash/Streak에 유용.
- `Sorting Fudge` 남용 대신 Sorting Layer/Order와 계층을 정리한다.
- Material 수를 줄이고 공통 머티리얼 + Custom Data를 활용한다.
- Shadow Casting/Receive Shadows는 일반 파티클에서 Off.
- Pivot을 조절해 불꽃 뿌리, 상승 연기 중심을 맞춘다.

---

<a id="section-9"></a>
# 9. 파티클 텍스처 제작·임포트 표준

## 9.1 텍스처 종류

1. **Mask**: 흑백 또는 단일 채널, 색은 머티리얼에서 지정.
2. **Shape texture**: 별, 스파크, 원, 링, 광선.
3. **Noise/Flow**: 타일 가능한 왜곡·흐름.
4. **Flipbook**: 폭발, 연기, 화염.
5. **Trail texture**: 가로 방향 흐름과 끝단 알파가 명확.
6. **Card mask**: 테두리, 문양, 희귀도 패턴.

## 9.2 알파 가장자리

- 투명 영역 RGB가 검정으로 채워진 텍스처는 선형 필터링에서 검은 테두리를 만들 수 있다.
- 가장자리 색을 바깥으로 확장하는 Alpha Bleed/Extrude를 적용한다.
- Additive 텍스처도 완전 투명 영역의 RGB를 정리한다.
- Premultiplied Alpha 셰이더를 쓰면 아트 제작과 블렌드 모드를 일관되게 맞춘다.

## 9.3 권장 임포트

### UI 아이콘/카드 마스크

```text
Texture Type       = Sprite (2D and UI)
Sprite Mode        = Single/Multiple
sRGB               = On (색 텍스처), 마스크 데이터는 용도에 따라 Off
Alpha Is Transparency = On
Mip Maps           = Off
Filter Mode        = Bilinear, 픽셀 아트는 Point
Wrap Mode          = Clamp
Compression        = ASTC/ETC2 플랫폼 Override
```

### 월드 파티클

```text
Texture Type       = Default 또는 Sprite
sRGB               = 색 텍스처 On, 데이터/노이즈/마스크 Off
Mip Maps           = 화면 축소가 큰 경우 On 검토
Filter Mode        = Bilinear
Wrap Mode          = Clamp, 타일 Noise는 Repeat
Read/Write         = Off
```

### Flow/Noise 데이터

```text
sRGB               = Off
Wrap Mode          = Repeat
Compression        = 노이즈 대역이 깨지지 않는 수준
Alpha Source       = 필요한 채널만
```

## 9.4 채널 패킹

하나의 RGBA 텍스처에 데이터를 묶어 샘플 수와 메모리를 줄일 수 있다.

```text
R = Dissolve noise
G = Edge mask
B = Foil pattern
A = Card shape/opacity
```

규칙:

- 서로 다른 압축 품질이 필요한 데이터는 억지로 묶지 않는다.
- sRGB를 Off로 설정한다.
- 채널 의미를 파일명 또는 `.md`/Inspector 주석에 기록한다.
- Shader Graph property reference에 `_PackedMask`처럼 명확한 이름을 사용한다.

## 9.5 Sprite Atlas

- 같은 화면에서 함께 쓰는 작은 VFX 텍스처를 묶는다.
- 카드 아트와 반복 VFX 마스크를 무조건 같은 Atlas에 넣지 않는다. 수명과 로딩 단위가 다르다.
- Padding 4~8 px부터 시작.
- 방향성 UV를 쓰는 텍스처, 9-slice, 커스텀 셰이더는 Allow Rotation을 끈다.
- Tight Packing이 메시에 영향을 주는 셰이더는 Full Rect를 사용한다.
- Low/High 품질 Variant Atlas 또는 Addressables 그룹을 고려한다.
- Atlas 하나가 너무 커져 작은 UI 하나 때문에 전체가 메모리에 올라오지 않게 로딩 단위로 분리한다.

## 9.6 Android 압축 전략

- 가장 단순한 광범위 호환 빌드: ETC2.
- 품질/용량 균형: ASTC 6x6 또는 8x8.
- 카드 원화/텍스트에 가까운 선명한 UI: ASTC 4x4~6x6 검토.
- 부드러운 노이즈/연기: ASTC 6x6~8x8.
- 알파 마스크: 압축 아티팩트가 Dissolve Edge에 보이는지 확인.
- Google Play App Bundle의 Texture Compression Targeting을 사용할 수 있으면 ASTC와 ETC2 변형을 제공한다.
- 지원하지 않는 압축 포맷이 런타임에 풀리면 메모리와 속도 비용이 커질 수 있으므로 실제 기기 로그를 확인한다.

---

<a id="section-10"></a>
# 10. 블렌딩 선택표

| 목적 | 블렌딩 | 특징 | 주의 |
|---|---|---|---|
| 불꽃/빛/마법 | Additive | 어두운 배경에서 강함 | 밝은 배경에서 사라짐, 과노출 |
| 연기/먼지/카드 원화 | Alpha | 색과 알파 유지 | 겹치면 오버드로·탁함 |
| 그림자/독/그을음 | Multiply | 아래 색을 어둡게 | 검은 배경에서 약함 |
| 부드러운 Glow | Premultiplied Alpha | 가장자리 품질 우수 | 텍스처 제작/셰이더 일치 필요 |
| 홀로그램/특수 | Screen 또는 커스텀 | 밝은 효과 | 플랫폼·셰이더 복잡도 |
| 컷아웃 파편 | Alpha Clip | 일부 상황에서 투명 비용 감소 | 가장자리 딱딱함, MSAA/해상도 영향 |

한 효과에 세 가지 이상 블렌드 방식을 무분별하게 섞지 않는다. 카드 획득은 보통 Alpha 1개, Additive 1~2개로 충분하다.

---

<a id="section-11"></a>
# 11. Shader Graph 프로젝트 표준

## 11.1 Target 선택

| 대상 | 우선 Target |
|---|---|
| 2D Renderer 조명을 받는 SpriteRenderer | `URP / Sprite Lit` |
| 조명이 필요 없는 SpriteRenderer | `URP / Sprite Unlit` |
| ParticleSystem 일반 빌보드 | `URP / Unlit` 또는 검증된 Particle 셰이더/Nova |
| uGUI Canvas | Unity 6 UI/Canvas Target 또는 UIEffect의 Canvas(UIEffect) Sub Target |
| 카드 본체가 SpriteRenderer | Sprite Lit/Unlit |
| 카드 본체가 `Image` | Canvas/UI 전용 셰이더 |
| 전체 화면 후처리 | Fullscreen Shader Graph + Renderer Feature, High 전용 |

Sprite Target과 Canvas Target을 혼동하지 않는다. Sprite 셰이더를 UI Image에 넣으면 마스킹, 스텐실, CanvasGroup alpha, 배칭이 깨질 수 있다.

## 11.2 Blackboard 명명

```text
_DisplayName        Reference
Base Texture        _BaseMap
Base Color          _BaseColor
Mask Texture        _MaskMap
Noise Texture       _NoiseMap
Packed Mask         _PackedMask
Dissolve            _Dissolve
Dissolve Width      _DissolveWidth
Edge Color          _EdgeColor
Emission Color      _EmissionColor
Emission Strength   _EmissionStrength
Flow Speed          _FlowSpeed
Flow Strength       _FlowStrength
Shine Progress      _ShineProgress
Shine Width         _ShineWidth
Shine Angle         _ShineAngle
Rarity Color        _RarityColor
Foil Strength       _FoilStrength
User Seed           _UserSeed
```

- 스크립트 참조 이름은 출시 후 함부로 변경하지 않는다.
- `Vector1` 같은 이름을 남기지 않는다.
- 범위는 Inspector에서 실수하지 않도록 제한한다.
- 기본값은 효과가 꺼진 상태 또는 안전한 상태로 둔다.

## 11.3 Precision

- 기본은 `Half`를 우선하고 시각 문제나 좌표 정밀도 문제가 있을 때만 `Float`.
- 월드 좌표가 매우 크거나 정교한 UV 계산은 Float 검토.
- 색, 알파, 대부분의 파티클 연산은 Half로 충분한지 확인.
- Custom Function HLSL도 `_half`와 `_float` 버전을 필요에 맞게 제공한다.

## 11.4 Sub Graph 표준

공통 Sub Graph 후보:

```text
SGF_UVRotate
SGF_UVPolar
SGF_RadialMask
SGF_RingMask
SGF_RoundedRectSDF
SGF_DissolveEdge
SGF_ShineBand
SGF_FlowUV
SGF_Fresnel2DApprox
SGF_HSVShift
SGF_Posterize
SGF_SoftClip
SGF_RarityPalette
SGF_HashNoise
SGF_ScreenSafePulse
```

각 Sub Graph는 입력/출력과 좌표 범위를 노드 Sticky Note에 기록한다.

## 11.5 셰이더 변형 관리

- Boolean Keyword를 기능마다 만들면 조합 수가 폭증한다.
- 자주 함께 쓰는 기능은 하나의 Enum Keyword로 묶거나 별도 셰이더로 분리한다.
- 런타임에 자주 바뀌는 단순 기능은 Keyword보다 수치 분기 또는 `lerp`가 나을 수 있다. 실제 GPU 측정으로 결정한다.
- Local Keyword를 우선한다.
- Low/High가 완전히 다른 기능 집합이면 하나의 거대한 Uber Graph보다 두 개의 Master Graph가 낫다.
- 사용하지 않는 URP 기능과 Pass를 stripping하도록 설정한다.
- 빌드 로그의 Shader Variant 수를 추적한다.

## 11.6 모바일에서 피할 노드·패턴

- 픽셀마다 반복되는 복잡한 `Noise` 노드 여러 개.
- `Scene Color` 샘플을 여러 번 사용.
- 화면 전체 Blur를 다중 샘플로 직접 구현.
- 긴 `For` Loop Custom Function.
- 분기 안에서 고비용 텍스처 샘플.
- `Pow`, `Exp`, `Normalize`를 불필요하게 반복.
- World Position 기반 큰 값에 Time을 더해 정밀도 손실.
- 같은 텍스처를 서로 다른 Sampler State로 여러 번 샘플.
- 카드마다 고유 머티리얼을 생성해 배칭을 깨는 구조.

대체:

- Noise 텍스처 1회 샘플.
- 노이즈를 Vertex에서 계산하거나 Flipbook으로 굽기.
- 곱셈/절댓값/Smoothstep 조합.
- 공통 결과를 변수/블록으로 재사용.
- 작은 영역에만 효과 적용.


---

<a id="section-12"></a>
# 12. Shader Graph 핵심 레시피

아래 레시피는 노드 이름보다 **수학과 데이터 흐름**을 이해하는 것이 중요하다. Unity 패치에 따라 메뉴 이름이 조금 달라져도 같은 구조로 구현한다.

## 12.1 UV 회전

목적: 카드 Shine 각도, 소용돌이 마스크, 방향성 노이즈.

```text
UV
→ Subtract (0.5, 0.5)
→ Rotate About Axis 또는 2D 회전 행렬
→ Add (0.5, 0.5)
→ RotatedUV
```

2D 회전식:

```text
p = uv - 0.5
x = p.x * cos(a) - p.y * sin(a)
y = p.x * sin(a) + p.y * cos(a)
rotated = float2(x, y) + 0.5
```

최적화:

- 각도가 고정이면 `sin/cos`를 머티리얼 값으로 미리 전달.
- 파티클마다 각도가 필요하면 Custom Data 또는 vertex color 채널 사용.

## 12.2 원형 마스크

```text
CenteredUV = UV - Center
Distance = Length(CenteredUV / AspectCorrection)
Mask = 1 - Smoothstep(Radius - Softness, Radius, Distance)
```

용도:

- 카드 획득 중심광
- 원형 버튼 터치 Ripple
- 폭발 코어
- 비네트 역마스크

## 12.3 링 마스크

```text
D = Distance(UV, Center)
Ring = 1 - Smoothstep(Width, Width + Softness, Abs(D - Radius))
```

애니메이션:

```text
Radius = Progress * MaxRadius
Alpha = Ring * (1 - Progress)
```

카드 획득은 링 하나를 크게 만드는 것보다 70~120 ms 간격으로 2개를 재생하면 풍부하다. Low는 1개만 사용한다.

## 12.4 사각형/카드 모서리 마스크

Rounded Rectangle SDF:

```hlsl
float sdRoundedBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}
```

Shader Graph Custom Function 입력:

```text
P          = UV - 0.5
HalfSize   = (0.5 - BorderPadding)
Radius     = CornerRadius
Distance   = sdRoundedBox(...)
Inside     = 1 - Smoothstep(0, Softness, Distance)
Border     = 1 - Smoothstep(BorderWidth, BorderWidth + Softness, Abs(Distance))
```

카드 비율이 1:1이 아니면 X/Y를 화면 또는 Rect 비율로 보정한다.

## 12.5 Dissolve + 발광 Edge

```text
Noise = Sample(_NoiseMap, UV * Tiling + FlowOffset).r
Threshold = _Dissolve
Body = Step(Threshold, Noise)
SoftBody = Smoothstep(Threshold - Softness, Threshold + Softness, Noise)
Edge = Smoothstep(Threshold, Threshold + EdgeWidth, Noise)
     - Smoothstep(Threshold + EdgeWidth, Threshold + EdgeWidth + EdgeSoftness, Noise)
Color = BaseColor * SoftBody + EdgeColor * Edge * EdgeIntensity
Alpha = BaseAlpha * SoftBody
```

권장:

- Edge Width `0.02~0.10` 범위에서 시작.
- 카드 획득 Reveal은 아래에서 위로 진행 방향을 추가한다.

```text
Directional = UV.y + Noise * NoiseStrength
Body = Smoothstep(Progress - Softness, Progress + Softness, Directional)
```

- 카드 소멸은 파티클이 Dissolve 경계에서 나온 것처럼 보이게 `Dissolve Progress`와 Burst 타이밍을 맞춘다.
- Alpha Clip은 카드 본체가 UI 마스킹과 호환되는지 확인한다.

## 12.6 대각선 Shine Sweep

```text
RotatedUV = Rotate(UV, ShineAngle)
Line = RotatedUV.x
DistanceToBand = Abs(Line - ShineProgress)
Band = 1 - Smoothstep(ShineWidth, ShineWidth + ShineSoftness, DistanceToBand)
ShapeMask = BaseAlpha 또는 CardMask
Shine = Band * ShapeMask
FinalRGB = BaseRGB + ShineColor * Shine * ShineIntensity
```

좋은 기본값:

```text
ShineWidth      = 0.04~0.12
ShineSoftness   = 0.03~0.10
Duration        = 0.35~0.70s
Delay           = 선택 상태에서 1.5~4.0s 랜덤
```

금지:

- 모든 카드가 같은 프레임에 Shine.
- Shine가 카드 바깥까지 보임.
- 불투명 흰 띠가 텍스트를 완전히 가림.

## 12.7 홀로그램/Foil 카드

구성:

```text
ViewOrPointer = 카드 중심 대비 포인터/카메라 방향
GradientA = Sample(FoilGradient, float2(UV.y + View.x * Amount, 0.5))
Pattern = Sample(FoilPattern, UV * Tiling + View.xy * Parallax)
Rainbow = HSVToRGB(frac(HueBase + UV.x * HueScale + UV.y * HueScaleY + View.x * HueView))
Mask = FoilMask * BaseAlpha
Foil = lerp(GradientA, Rainbow, Pattern.r) * Mask
Final = Base + Foil * _FoilStrength
```

모바일 Low 대체:

- Foil texture 1회 샘플 + 대각선 Shine만 사용.
- HSV 변환, 다중 노이즈, 화면 샘플 제거.

Medium:

- Foil Pattern 1회 + Gradient/LUT 1회.

High:

- Pointer parallax, 이중 패턴, 미세 RGB 분리 허용.

주의:

- UI Image에서는 포인터 위치를 머티리얼별로 전달하려고 새 Material을 매 프레임 만들지 않는다.
- 카드가 동시에 많이 보일 때는 카드별 Foil 애니메이션을 제한하거나 공통 시간 + seed를 사용한다.

## 12.8 카드 테두리 Glow

방법 A — 사전 제작 마스크:

```text
BorderMask = Sample(_BorderMask).r
Pulse = 0.6 + 0.4 * sin(Time * Speed + Seed)
Glow = BorderMask * RarityColor * Pulse * Strength
```

방법 B — 알파 기반 외곽선 다중 샘플:

```text
Outline = max(alpha at 4/8 neighbor UVs) - centerAlpha
```

모바일에서는 다중 샘플 Outline을 카드 수십 장에 적용하지 않는다. 사전 제작 Border Mask 또는 SDF를 우선한다.

## 12.9 SDF Outline

SDF 텍스처가 있을 때:

```text
Inside = Smoothstep(0.5 - Softness, 0.5 + Softness, SDF)
Outer = Smoothstep(0.5 - Width - Softness, 0.5 - Width + Softness, SDF)
Outline = Outer - Inside
```

장점:

- 두께 변경이 쉽다.
- 해상도 변화에 비교적 안정적.
- Glow/Shadow/선택 Pulse를 같은 데이터로 구현.

## 12.10 Hit Flash

가장 저렴하고 효과적인 피격 표현:

```text
Luma = dot(BaseRGB, float3(0.2126, 0.7152, 0.0722))
FlashColor = _HitColor
FinalRGB = lerp(BaseRGB, FlashColor, _HitFlash)
```

타이밍:

```text
0 ms   = 0
1 frame = 1
50 ms  = 0.4
100 ms = 0
```

White Flash와 Red Tint를 함께 쓰려면 2단계 곡선을 사용한다. 카드마다 새 Material을 만들지 말고 SpriteRenderer의 `MaterialPropertyBlock` 또는 UIEffect preset/개별 UI 재질 풀을 사용한다.

## 12.11 Grayscale/비활성

```text
Luma = dot(BaseRGB, float3(0.2126, 0.7152, 0.0722))
Gray = float3(Luma, Luma, Luma)
Result = lerp(BaseRGB, Gray, _GrayAmount)
Result *= lerp(1, _DisabledBrightness, _GrayAmount)
```

비활성은 Grayscale만 사용하지 말고 다음을 함께 고려한다.

- 채도 감소
- 밝기 65~85%
- 테두리 대비 감소
- 잠금 아이콘
- 상호작용 불가 커서/입력 상태

## 12.12 Burn/소각

레이어:

1. 방향성 Dissolve
2. 주황/흰색 Edge
3. 검은 그을음 Alpha/Multiply
4. 위로 뜨는 재 파티클
5. 마지막 작은 연기

Shader:

```text
BurnCoord = UV.y + Noise * 0.25
Body = Smoothstep(Progress - Softness, Progress + Softness, BurnCoord)
HotEdge = band around threshold
AshEdge = second wider band behind HotEdge
```

Low:

- Directional Dissolve + 6~10개 재 입자.

High:

- 이중 Edge + Flow + 연기 + 약한 화면 열기 왜곡.

## 12.13 Freeze/서리

레이어:

- 카드 외곽에서 안쪽으로 번지는 얼음 마스크
- 청백색 Edge
- 결정 스프라이트 4~8개
- 미세한 위/아래 Spark
- 카드 색 채도 감소

```text
FrostMask = Noise * FrostNoiseStrength + EdgeDistance * EdgeBias
Frozen = Smoothstep(Progress - Softness, Progress + Softness, FrostMask)
Final = lerp(Base, FrozenColor, Frozen * TintAmount)
```

얼음은 Additive만 쓰지 말고 Alpha 결정과 어두운 청색 대비를 섞는다.

## 12.14 Poison/부식

- 아래로 흐르거나 불규칙하게 맺히는 마스크.
- 색상은 녹색 하나가 아니라 어두운 보라/검정 보조색 사용.
- 거품 파티클은 4~10개, 수명 랜덤.
- 카드 텍스트 가독성을 훼손하지 않도록 가장자리 중심.

## 12.15 Glitch/RGB Split

```text
Offset = GlitchMask * Strength
R = Sample(Base, UV + float2(Offset,0)).r
G = Sample(Base, UV).g
B = Sample(Base, UV - float2(Offset,0)).b
```

비용이 3샘플이므로:

- 80~160 ms 짧게.
- 화면 전체가 아니라 카드 1장 또는 작은 UI.
- Low에서는 색 틴트 + 위치 흔들림으로 대체.
- 매 프레임 랜덤보다 8~15 Hz 계단형 seed로 디지털 느낌을 낸다.

## 12.16 UV Flow/마법 흐름

```text
Flow = Sample(_FlowMap, UV * FlowTiling + Time * FlowSpeed).rg * 2 - 1
DistortedUV = UV + Flow * FlowStrength
Result = Sample(_BaseMap, DistortedUV)
```

- Flow Map은 sRGB Off.
- Strength는 UV 기준 `0.005~0.05`부터 시작.
- 카드 텍스트/아이콘 본체가 아니라 오버레이 패턴에 적용한다.
- Sample 수를 줄이려면 Base가 아닌 마스크만 왜곡한다.

## 12.17 2D 가짜 Fresnel/Rim

2D 카드에는 실제 표면 Normal이 없을 수 있다. 포인터/기울기 기반으로 가장자리를 강조한다.

```text
Edge = BorderMask 또는 1 - RoundedRectInsideEroded
ViewBias = saturate(dot(normalizedPointer, normalized(UV - 0.5)) * 0.5 + 0.5)
Rim = Edge * pow(ViewBias, Power)
```

카드 기울기 방향의 한쪽 테두리만 밝아져 입체감이 생긴다.

## 12.18 Pulse

`sin(Time)`를 모든 카드가 공유하면 동시에 깜빡인다.

```text
Phase = Time * Speed + UserSeed * 6.28318
Pulse = Remap(sin(Phase), -1, 1, Min, Max)
```

- Seed는 카드 ID를 직접 float로 크게 쓰지 말고 0~1 해시로 변환.
- Pulse는 선택된 카드나 희귀 카드 소수에만.
- Reduce Motion에서는 속도를 낮추거나 정적 Glow로 변경.

## 12.19 화면 좌표 기반 패턴

여러 카드 위에서 하나의 큰 빛이 지나가는 효과:

```text
ScreenUV = Screen Position / W
Band = Shine(ScreenUV, GlobalProgress)
Final = Base + Band * LocalCardMask
```

주의:

- 카드가 이동할 때 패턴이 카드에 고정되지 않는다. 이것이 의도인지 확인.
- Screen Position 정규화와 Y Flip을 Android API별로 테스트.

## 12.20 Fullscreen Flash/Vignette

- 별도 전체 화면 Shader Graph를 여러 개 두지 않고 하나의 공통 ScreenFeedback 컨트롤러로 관리.
- Flash, Vignette, Color Tint, Radial Pulse를 한 패스에서 필요한 기능만 사용.
- Low에서는 단일 UI Image 색/알파 애니메이션으로 대체.
- 입력을 막지 않도록 `Raycast Target = Off`.

---

<a id="section-13"></a>
# 13. 카드 렌더링 아키텍처

## 13.1 카드 한 장의 권장 레이어

```text
CardRoot
├── Shadow
├── BaseFrame
├── Artwork
├── ArtworkEffectOverlay
├── RarityPattern
├── FrameHighlight
├── TextAndIcons
├── StatusOverlay
├── SelectionBorder
├── CardParticlesAnchor
└── InteractionCollider
```

### 레이어 역할

- `Shadow`: 정적인 9-slice 또는 단일 Sprite. 실시간 그림자 금지.
- `BaseFrame`: 등급별 프레임. 가능한 Atlas 공유.
- `Artwork`: 카드 원화. 효과가 텍스트/프레임까지 오염하지 않게 분리.
- `ArtworkEffectOverlay`: Foil, Burn, Freeze, Poison 등.
- `RarityPattern`: 희귀도 마스크/문양.
- `FrameHighlight`: 선택·획득 Shine.
- `TextAndIcons`: Shader 효과의 영향 최소화.
- `StatusOverlay`: 잠금, 사용 불가, 강화 수치.
- `SelectionBorder`: SDF/마스크 기반.
- `CardParticlesAnchor`: UIParticle 또는 World Particle 기준.

## 13.2 카드가 uGUI Image일 때

장점:

- UI 레이아웃, 마스크, ScrollRect, Canvas 정렬이 편하다.

주의:

- MaterialPropertyBlock을 일반 SpriteRenderer처럼 사용할 수 없다.
- `Graphic.material` 변경은 재질 인스턴스/배칭 비용을 만들 수 있다.
- 카드 수가 많으면 다음 전략을 쓴다.
  1. 공통 머티리얼 + Vertex Color/Additional Shader Channels.
  2. UIEffect preset과 Replica.
  3. 활성/선택 카드에만 개별 머티리얼 풀 할당.
  4. 정적 카드 목록은 애니메이션 효과 Off.
  5. Foil 카드만 별도 Overlay Graphic 사용.

## 13.3 카드가 SpriteRenderer일 때

장점:

- MaterialPropertyBlock으로 카드별 값을 효율적으로 전달.
- 2D Light, Sprite Lit/Normal Map과 자연스럽게 통합.

주의:

- ScrollRect/Mask/Canvas 레이아웃 통합이 어렵다.
- UI와 World 좌표 변환 필요.
- 정렬 레이어와 카메라 구성을 명확히 해야 한다.

## 13.4 혼합 방식

카드 목록은 uGUI, 전투 보드의 실제 카드는 SpriteRenderer로 사용할 수 있다. 다만 같은 효과를 두 구현으로 유지해야 하므로 공통 데이터 사양을 둔다.

```text
ICardVfxTarget
- RectTransform 또는 Transform Anchor
- SetFlash(float)
- SetDissolve(float)
- SetShine(float)
- SetRarityColor(Color)
- GetWorldCorners()
- GetCanvasOrCamera()
```

## 13.5 카드 기울기·Parallax

입력 포인터를 카드 로컬 좌표 `[-1,1]`로 변환한다.

```text
local = InverseTransformPoint(pointer)
normalized.x = clamp(local.x / halfWidth, -1, 1)
normalized.y = clamp(local.y / halfHeight, -1, 1)
rotationX = -normalized.y * MaxTilt
rotationY = normalized.x * MaxTilt
```

모바일 권장값:

```text
MaxTilt           = 3~8 degrees
Smoothing         = 10~20
ArtworkParallax   = 1~5 px
FoilParallax      = 3~12 px
ReturnDuration    = 0.12~0.25s
```

- 드래그 중 Tilt가 입력 지연을 만들지 않게 Update 비용을 최소화.
- 카드 수십 장이 동시에 Tilt 계산하지 않는다. 포인터가 닿은 카드만.
- Reduce Motion에서는 Tilt/Parallax를 끈다.
- Canvas가 Screen Space Overlay인지 Camera인지 좌표 변환을 구분한다.

## 13.6 카드 상태 머신

```text
Idle
Hover/Focus
Pressed
Dragging
ValidTarget
InvalidTarget
Played
Resolving
Disabled
Locked
RewardReveal
Upgrade
Burned/Destroyed
```

각 상태는 효과를 직접 생성하지 않고 `CardVfxController`에 의미 이벤트를 전달한다.

```text
OnFocusChanged(bool)
OnPressed()
OnDragStarted()
OnDragValue(Vector2 velocity)
OnTargetValidityChanged(bool)
OnPlayed()
OnHit(int damage, bool critical)
OnStatusApplied(CardStatus status)
OnRewardReveal(Rarity rarity)
OnDestroyed(DestroyStyle style)
```

## 13.7 희귀도 시각 사양

| 등급 | 색만이 아닌 차이 | 지속시간 | 파티클/레이어 |
|---|---|---:|---|
| Common | 작은 흰 팝, 단일 링 | 0.35~0.55s | 1~2 시스템 |
| Uncommon | 고유색 링, 별 4~8 | 0.45~0.70s | 2~3 |
| Rare | 이중 링, Shine, 짧은 Ray | 0.65~0.95s | 3~4 |
| Epic | 문양, Orbit, 지연 Burst | 0.9~1.3s | 4~6 |
| Legendary | 예고→정지→대형 Reveal, 음향/햅틱 | 1.3~2.2s | 5~8, Low에서는 4~5 |
| Mythic/최상위 | 고유 시그니처 연출 | 1.8~3.0s | 이벤트 전용, 스킵 지원 |

희귀도 색 예시는 프로젝트 팔레트에 맞추되 색각 접근성을 위해 문양과 모션을 고유하게 한다.

```text
Common      = 원형 점/짧은 팝
Uncommon    = 4방향 별
Rare        = 8방향 링+별
Epic        = 회전 다이아/룬
Legendary   = 왕관/태양형 광선
Mythic      = 고유 문장+비대칭 시그니처
```

---

<a id="section-14"></a>
# 14. 카드 이벤트별 VFX 설계

## 14.1 카드 선택

목표: 선택 상태를 명확히 보이되 목록을 시끄럽게 하지 않는다.

```text
Duration       = 0.12~0.20s 진입
Scale          = 1.00 → 1.03~1.06
Border         = 0 → 1
Shadow offset  = 2~6 px 증가
Particles      = 최초 선택 시 4~8개만
Loop           = 느린 Border Pulse, 파티클 Loop 금지 권장
```

Low:

- Border + Scale만.

## 14.2 카드 Hover/Focus

모바일에는 Hover가 없을 수 있으므로 Controller/키보드 포커스와 길게 누르기에 연결한다.

- 약한 Shine 1회.
- 카드 Tilt.
- 설명 패널 등장.
- 반복 Shine 간격 2~5초 랜덤.

## 14.3 카드 Press

```text
0 ms    scale 1.00
50 ms   scale 0.96~0.98
100 ms  scale 1.00 또는 Drag 전환
```

- 작은 중앙 Ripple.
- 강한 파티클 금지.
- 햅틱은 Light 수준.

## 14.4 카드 Drag

- 카드 뒤 Trail은 길이보다 입력 응답성이 중요.
- 이동 속도가 임계값 이상일 때만 6~20 particles/s.
- 드래그 속도로 Trail 폭과 Emission을 제한 범위 내 조절.
- 유효 타깃 접근 시 카드 Border 색과 타깃 영역을 바꾼다.
- 화면 전체를 가로지르는 긴 Additive Trail 금지.

## 14.5 유효 타깃

- 타깃 영역에 안쪽으로 흐르는 링.
- 카드 테두리와 타깃 테두리 같은 팔레트.
- 0.8~1.5 Hz 느린 Pulse.
- 파티클은 타깃 주변으로 들어오는 방향.

## 14.6 무효 타깃

- 빨간색만 쓰지 말고 X, 끊긴 테두리, 짧은 좌우 Shake.
- 120~180 ms.
- 화면 흔들림 금지.
- 드래그 입력을 계속 받을 수 있어야 한다.

## 14.7 카드 플레이

일반 흐름:

```text
0.00s 카드가 타깃 방향으로 10~25% 이동
0.05s 카드 Scale 1.05, 밝기 상승
0.10s Impact flash
0.12s 효과가 대상에 생성
0.15s 카드 소모/복귀 애니메이션
0.20~0.60s 잔광
```

카드 효과와 실제 게임 로직의 타이밍을 분리한다. 피해 적용은 연출 이벤트에 종속시키지 말고, 명시적 타임라인 마커 또는 서버 결과에 맞춘다.

## 14.8 카드 드로우

- 덱에서 손패 슬롯까지 곡선 이동.
- 시작은 작은 크기, 중간에서 약한 Trail, 도착 시 Pop.
- 여러 장은 60~120 ms Stagger.
- 10장 이상 대량 드로우는 처음 3장만 완전 연출, 나머지는 속도 증가.

## 14.9 카드 Flip/Reveal

2D UI에서 실제 3D 회전 대신 X Scale을 사용할 수 있다.

```text
Front scaleX 1 → 0  (0.10~0.18s)
중간 지점에 Sprite/데이터 교체
Back scaleX 0 → 1   (0.10~0.18s)
```

고급:

- Y Rotation 0→90→180.
- 중간 90도에서 앞/뒤 전환.
- Foil highlight가 회전에 따라 이동.
- 카드 가장자리 두께용 얇은 Side Sprite.

Low:

- X Scale 방식 + Flash.

## 14.10 카드 강화/Upgrade

레이어:

1. 아래에서 위로 흐르는 에너지.
2. 카드 테두리 룬 점등.
3. 수치 텍스트 Pop.
4. 별/등급 변화.
5. 최종 Shine.

타이밍:

```text
0.00~0.35s Charging
0.30~0.45s Impact
0.40~0.90s New frame reveal
0.70~1.20s Settle
```

강화 수치가 실제로 바뀌는 프레임과 Impact를 맞춘다.

## 14.11 카드 합성/Merge

- 재료 카드가 중심 카드로 곡선 이동.
- 각 재료는 독립 ParticleSystem보다 공통 Trail prefab을 재사용.
- 중심에서 1회 큰 Burst.
- 재료 수가 많으면 Stagger를 압축.
- 합성 완료 후 새 카드 Reveal과 등급 연출을 분리.

## 14.12 카드 소각/파괴

- 게임 규칙이 ‘소멸’인지 ‘묘지 이동’인지 전달한다.
- Burn, Shatter, Fade 중 게임 세계관에 맞는 시그니처를 선택.
- 클릭/드래그 Collider를 연출 시작 즉시 비활성화.
- 풀 반환 전에 모든 Trail/Particle이 끝났는지 확인.

### Burn

- 아래→위 Dissolve 0.45~0.8s.
- Edge + 재 8~20개.
- Low에서는 연기 제거.

### Shatter

- 카드 조각을 실제 Rigidbody 수십 개로 만들지 않는다.
- 6~12개 사전 제작 파편 Sprite + 단순 포물선.
- Low에서는 4~6개.

### Digital Dissolve

- 픽셀/블록 마스크 + 짧은 RGB Shift.
- 픽셀 파티클 10~30개.

## 14.13 카드 피해

- 카드 본체 Hit Flash 80~120 ms.
- 작은 위치 Shake 2~6 px, 2~4회 감쇠.
- 피해 방향에서 들어오는 Slash/Spark.
- Damage number가 카드 아트를 가리지 않도록 위쪽/측면.
- 치명타만 화면 Shake 또는 큰 Ray.

## 14.14 카드 회복

- 밝은 녹색만 사용하지 않고 흰색/청록 중심.
- 아래→위 입자 6~14개.
- 카드 Saturation/Emission 잠시 상승.
- 수치 텍스트는 위로 부드럽게.

## 14.15 카드 Shield

- 둥근/육각형 Barrier.
- 피격 방향에 국소 Ripple.
- 항상 켜진 큰 투명 Shield는 오버드로가 크므로 정적 테두리 + 피격 시 Burst.
- Shield 수치가 줄 때 Crack 마스크 단계 변화.

## 14.16 상태이상

### Fire

- 테두리 하단의 불꽃 2~4개 루프.
- 반복 Damage Tick 때 작은 Burst.
- 카드 아트 전체를 불꽃으로 가리지 않는다.

### Freeze

- 정적 Frost Mask가 기본.
- 적용 순간에만 결정 Burst.
- 지속 중 파티클 루프 최소화.

### Poison

- 가장자리 거품 2~5개, 느린 흐름.
- Tick 때 작은 Splash.

### Stun

- 카드 위 별/번개 2~3개 Orbit.
- 회전 속도 낮음.
- Low에서는 아이콘 Pulse만.

### Curse

- 보라/검정 문양과 역방향 Pulse.
- 화면 전체 어두운 오버레이보다 카드 국소 표현.

## 14.17 카드 잠금/해금

잠금:

- Grayscale 0.7~1.0.
- 자물쇠 아이콘.
- Border 대비 감소.

해금:

```text
0.00s Lock shake
0.15s Lock crack/flash
0.25s Lock icon scale out
0.25~0.65s Color restore
0.40~0.90s Border shine
```

## 14.18 카드 복제/획득 중복

- 중복임을 먼저 표시.
- 카드가 조각/재화 아이콘으로 변환.
- 변환된 아이콘이 보유량 카운터로 이동.
- 전설 중복도 전설 Reveal 전체를 다시 강제하지 않고 선택 가능.


---

<a id="section-15"></a>
# 15. 획득·보상 VFX 설계

## 15.1 보상 연출의 정보 우선순위

플레이어가 1초 안에 알아야 하는 것:

1. 무엇을 얻었는가.
2. 몇 개를 얻었는가.
3. 얼마나 희귀한가.
4. 어디에 저장되었는가.
5. 중복이거나 변환되었는가.

화려하지만 위 정보를 숨기면 실패한 연출이다.

## 15.2 공통 보상 시퀀스

```text
A. Spawn/Appear       0.00~0.20s
B. Recognition Hold  0.15~0.80s
C. Celebration       0.20~1.20s
D. Transfer/Collect  0.60~1.50s
E. Counter Update    도착 직전 또는 도착 시
F. Settle             0.10~0.30s
```

- 네트워크 보상 확정 전에는 최종 카운터를 갱신하지 않는다.
- 시각 연출이 스킵되어도 데이터는 정확히 적용한다.
- 중복 입력으로 같은 보상이 두 번 연출되지 않도록 이벤트 ID를 사용한다.

## 15.3 코인 획득

### 소량 1~5개

- 실제 획득량과 아이콘 수를 1:1로 맞춰도 된다.
- 시작점에서 작은 Burst.
- 0.35~0.65초 Bezier 이동.
- 도착 간격 35~70 ms.
- 카운터는 각 도착마다 일부 증가하거나 마지막에 합산.

### 중량 6~100개

- 아이콘 5~10개로 대표.
- 각 아이콘은 총량의 일부를 나타낸다.
- 카운터 숫자는 EaseOut으로 최종 값까지 증가.
- `+100` 텍스트를 함께 표시.

### 대량 100개 이상

- 아이콘 8~14개 상한.
- 첫 3개는 느리고 나머지는 빠르게.
- 카운터 이동 애니메이션 최대 0.8~1.2초.
- 햅틱을 아이콘마다 울리지 않고 시작/완료 1~2회.

권장 파티클:

```text
Start Burst     4~8 stars
Travel Trail    Low Off / Medium 1 thin trail
Arrival Burst   3~6 sparks
Counter Pulse   scale 1 → 1.12 → 1
```

## 15.4 보석/프리미엄 재화

코인보다 적고 더 무겁게 느껴지게 한다.

- 아이콘 수 3~7개.
- 이동 전 80~150 ms 공중 정지.
- 청록/자주/흰색의 정교한 Spark.
- 도착 시 작은 링과 Medium 햅틱.
- 과도한 금색을 섞어 코인과 혼동시키지 않는다.

## 15.5 경험치 획득

- 경험치 바 방향으로 작은 빛 입자가 흐른다.
- 바가 증가하는 구간을 밝은 Sweep이 따라간다.
- 레벨업 임계점을 넘으면 일반 획득 연출과 레벨업 연출을 분리.
- 대량 경험치에서도 바 Tween은 읽을 수 있는 최소 시간 0.4~1.0초 유지.

## 15.6 아이템 획득

### 일반 아이템

- 아이콘 Pop.
- 등급 색 링 1개.
- 이름/수량 표시.
- 0.6~1.0초.

### 희귀 아이템

- 배경 Dim을 약하게.
- 아이콘 등장 전 100~250 ms Anticipation.
- 이중 링 + 문양.
- 이름이 완전히 보인 뒤 Transfer/닫기.

### 여러 아이템

- 격자 카드가 순서대로 Pop.
- 모든 셀에 독립 파티클 시스템을 두지 않는다.
- 공통 Canvas 위에서 Burst를 위치만 바꿔 재사용.
- 가장 높은 희귀도만 대표 대형 연출.

## 15.7 카드 획득 — Common

```text
Total duration     0.45~0.65s
Card scale         0.80 → 1.05 → 1.00
Core flash         60~100ms
Ring               1개
Stars              4~6
Bloom              Off
Haptic              Light optional
```

## 15.8 카드 획득 — Uncommon

```text
Total duration     0.55~0.80s
Card scale         0.75 → 1.06 → 1.00
Ring               1~2개
Stars              6~10
Shine              1회
Rarity glyph       작은 문양
```

## 15.9 카드 획득 — Rare

```text
Total duration     0.75~1.10s
Anticipation       0.10~0.18s
Card reveal        Flip 또는 Dissolve
Rings              2개, 80ms 간격
Rays               6~10
Stars              10~18
Shine              0.35~0.55s
Audio              2단계 상승음
Haptic             Medium 1회
```

Low 품질:

- Ray 4개.
- 별 8개.
- Bloom Off.
- Flipbook 보간 Off.

## 15.10 카드 획득 — Epic

```text
Total duration     1.0~1.5s
Background dim     alpha 0.10~0.25
Anticipation       룬/원형 에너지 수렴 0.25s
Reveal             카드 Flip + Core flash
Rings              2~3개
Orbit particles    6~12개
Rarity glyph       1개
Foil               0.5~1.2s
Audio              낮은 준비음 + 밝은 해소음
Haptic             Medium + Light tail
```

## 15.11 카드 획득 — Legendary

권장 시퀀스:

```text
0.00~0.20s  배경 소리/조명이 살짝 줄고 중심에 에너지 수렴
0.18~0.35s  카드 실루엣 등장, 시간감이 느려지는 연출
0.32~0.42s  짧은 정지 또는 매우 느린 구간
0.40~0.55s  강한 Core flash + 카드 Reveal
0.48~0.80s  광선/이중 링/문양 전개
0.65~1.20s  Foil, Orbit, 느린 금빛 먼지
0.90~1.60s  이름·희귀도·능력 표시
1.20~2.00s  최종 Shine, 입력 가능 또는 자동 종료
```

구성 레이어:

1. Dim overlay.
2. 카드 뒤 Rarity Sigil.
3. Core flash.
4. Rays.
5. Ring A/B.
6. Border/Foil.
7. Near sparks.
8. Far dust.
9. 이름/등급 UI.
10. Audio/Haptic.

Low 품질 변환:

- Dim 유지.
- Sigil 정적 Sprite.
- Rays 6개.
- 링 2개.
- Sparks 총 12~18개.
- Bloom/Distortion/Light2D Off.
- Foil은 Shine 한 번으로 대체.

강조 원칙:

- 가장 밝은 프레임은 1회.
- 카드 원화와 이름은 0.6초 이내에 읽을 수 있어야 한다.
- 지속적인 화면 번쩍임 대신 시작 대비와 정지 구간을 활용한다.

## 15.12 상자 열기

### 단계

1. 상자 반응/Shake.
2. 틈새 빛.
3. 뚜껑 열림.
4. 빛기둥/보상 등장.
5. 보상 분류.
6. 수집.

희귀도 예고:

- 상자에서 나오는 빛 색/문양으로 최고 희귀도를 암시.
- 결과를 너무 일찍 완전히 노출하지 않는다.
- 랜덤 결과가 서버 확정되기 전에는 희귀도 전용 연출 시작 금지.

성능:

- 상자 내부 Light2D 대신 Additive cone/beam sprite 사용 가능.
- 보상 10개를 동시에 물리적으로 튕기지 않는다.
- 3~5개 대표 아이콘 + 결과 목록.

## 15.13 업적/퀘스트 완료

- 화면 상단 또는 측면에서 패널 Slide.
- 체크 마크 Stroke/Scale.
- 작은 Confetti 8~20개.
- 중요한 전투를 가리지 않도록 Alpha/크기 제한.
- 여러 개면 큐로 재생, 최대 동시 1~2개.

## 15.14 출석/연속 보상

- 획득한 날짜 셀에 Stamp.
- 연속 일수는 흐르는 선 또는 연결 Pulse.
- 7일/30일 큰 보상만 대형 연출.
- 모든 셀에 계속 파티클이 돌지 않는다.

## 15.15 보상 아이콘 이동 경로

Quadratic Bezier:

```text
P(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
```

제어점:

```text
P1 = midpoint(P0, P2) + perpendicular * ArcHeight
```

권장:

- 거리가 짧으면 ArcHeight 40~100 px.
- 긴 이동은 화면 크기의 8~20%.
- 아이콘별 seed로 좌우 분산.
- 도착 직전 속도를 높여 흡수감.
- Reduce Motion에서는 짧은 직선 이동 또는 즉시 Fade/카운터 Pulse.

## 15.16 World → UI 좌표 변환

Screen Space Overlay:

```csharp
Vector3 screen = worldCamera.WorldToScreenPoint(worldPosition);
RectTransformUtility.ScreenPointToLocalPointInRectangle(
    canvasRect,
    screen,
    null,
    out Vector2 localPoint);
```

Screen Space Camera:

```csharp
RectTransformUtility.ScreenPointToLocalPointInRectangle(
    canvasRect,
    screen,
    canvas.worldCamera,
    out Vector2 localPoint);
```

주의:

- 카메라 뒤의 점(`screen.z < 0`) 처리.
- Safe Area.
- Canvas Scaler.
- 화면 회전/해상도 변경 중 활성 Tween 재계산.
- Overlay와 Camera Canvas의 좌표계를 섞지 않는다.

## 15.17 카운터 갱신 규칙

- 숫자는 아이콘 도착보다 너무 먼저 증가하지 않는다.
- 네트워크 데이터는 즉시 확정하되 시각 숫자만 Tween할 수 있다.
- 연출 중 화면을 닫아도 최종 값으로 스냅한다.
- 숫자 Tween은 정수 반올림과 천 단위 표기를 일관되게 한다.
- 중복 이벤트가 들어오면 현재 Tween 시작값을 실제 표시값에서 이어 간다.

---

<a id="section-16"></a>
# 16. 전투 VFX 레시피

## 16.1 일반 Hit Spark

```text
Duration            0.25~0.40s
Burst               8~14
Lifetime            0.12~0.30s
Start Speed         2~6 world units 또는 UI 환산
Size                 대상 크기의 5~18%
Shape                Cone 35~70°
Color                white core → damage color → transparent
Renderer             Stretched Billboard
Material             Additive
```

레이어:

- 짧은 Core flash 1개.
- 방향성 Spark.
- 작은 Ring 선택.

Low:

- Spark 6~8, Ring 제거 가능.

## 16.2 치명타

일반 Hit과 차이:

- 1프레임 White core.
- 더 날카로운 Slash/Ray.
- 80~150 ms Hit stop 느낌. 실제 게임 타임스케일 사용은 시스템과 협의.
- Damage text scale/outline 차이.
- 카메라 Shake는 짧고 감쇠.

```text
Critical Slash      1~2개
Sparks              14~24
Ring                 1개
Screen flash         alpha 0.08~0.18
Shake                2~6 px, 80~140ms
```

Low:

- Slash 1, Sparks 10~14, 화면 Flash 유지, Shake 약화.

## 16.3 베기/Slash

- 사전 제작 Slash texture 또는 Flipbook.
- Velocity Alignment가 아니라 이미 방향이 있는 Sprite를 회전.
- 나타남 1~2프레임, 사라짐 4~8프레임.
- 중심보다 시작/끝이 가늘게.
- Trail이 필요하면 하나의 리본만.

## 16.4 둔기/Impact

- 원형 충격파.
- 짧고 두꺼운 Ray.
- 먼지/파편.
- 베기보다 방향성이 약하고 중심 압축감이 강함.

## 16.5 관통/Pierce

- 좁은 Cone.
- 앞→뒤로 통과하는 Streak.
- 입자 수보다 속도와 정렬이 중요.
- 출구 쪽 작은 Burst.

## 16.6 폭발

레이어 예:

1. Core flash 60~100 ms.
2. Flipbook fire 1개.
3. Ring 1개.
4. Sparks 12~30개.
5. Smoke 3~8개.
6. Debris 4~10개.

Low:

- Fire Flipbook 1, Sparks 10~16, Smoke 2~4, Debris Off.

주의:

- 큰 Alpha smoke가 화면을 오래 덮지 않게 0.5~1.2초.
- Additive fire와 Alpha smoke 텍스처 여백 최소화.
- 파편 Collision 기본 Off.

## 16.7 화염

- 핵심 형태는 위로 상승.
- 노이즈보다 Flipbook/Flow Map 우선.
- 불꽃 2~4개를 서로 다른 속도로.
- 재 4~12개.
- 바닥 Glow 1개.
- 지속 화염은 Light2D 대신 가짜 Glow부터 시작.

## 16.8 얼음 공격

- 충돌 시 결정 4~10개.
- 흰색 core + 청록 외곽.
- Freeze 상태는 정적 Frost로 유지하고 Burst를 루프하지 않는다.
- 깨질 때 역방향 파편 + 빠른 Dissolve.

## 16.9 전기

- 번개 텍스처 1~3개를 50~120 ms씩 랜덤 교체.
- LineRenderer의 포인트를 매 프레임 대량 생성하지 않는다.
- 작은 지점 사이 전기는 사전 제작 4~8 프레임 Flipbook 가능.
- 잔광 Spark 4~10개.
- 화면 RGB Shift는 치명적 전기 공격에서만 80 ms 이하.

## 16.10 독

- Alpha/Multiply Splash.
- 거품 5~12개.
- 어두운 중심 + 채도 높은 외곽.
- 지속 상태는 작은 아이콘/마스크 중심.

## 16.11 회복

```text
Upward particles    6~14
Cross/leaf glyph    1~3
Ring                 1
Color                white + cyan/green
Duration             0.6~1.0s
```

- 회복량 숫자와 같은 방향으로 이동.
- 불꽃처럼 빠르게 튀지 않고 부드럽게.

## 16.12 버프

- 대상 주위 얇은 상승 라인.
- 상태 아이콘 Pop.
- 적용 순간에만 큰 Burst.
- 지속 중에는 아주 약한 오라 또는 아이콘.

## 16.13 디버프

- 아래로 떨어지는 조각/어두운 링.
- 카드/캐릭터 채도 감소.
- 적용 순간과 지속 표현 분리.

## 16.14 Shield Hit

- 피격 방향에 국소 원형 Ripple.
- 전체 Barrier를 매번 크게 Flash하지 않는다.
- 보호막 수치가 낮아질수록 Crack 단계.
- 파괴 시 Crack → Core flash → 조각 6~12개.

## 16.15 사망/소멸

스타일별:

- `Fade`: 가장 저렴, 일반 소환물.
- `Dissolve`: 마법/디지털.
- `Burn`: 화염/저주.
- `Shatter`: 얼음/기계.
- `SoulRise`: 영혼 입자 상승.

게임플레이 Collider, Targetable 상태, VFX 수명을 분리한다.

## 16.16 소환/Spawn

- 출현 위치를 먼저 보여 주어 공정성을 확보.
- 바닥 Sigil 0.3~0.8초.
- 중심 에너지 수렴.
- 캐릭터 Alpha/Dissolve 0→1.
- 완료 전 공격 가능 여부를 명확히.

## 16.17 콤보

- 매 타격 이펙트를 기하급수적으로 키우지 않는다.
- 5/10/20 콤보 같은 임계점에서만 특별 Burst.
- 콤보 숫자, 색, 음향 Pitch, UI Pulse로 강화.
- 연속 Hit Spark는 파티클 수를 자동 축소하는 Burst limiter 사용.

## 16.18 카메라 Shake

- 위치 Shake보다 Cinemachine Impulse 또는 공통 CameraFeedbackService.
- 일반 Hit: 없음 또는 0.5~1.5 px.
- Critical: 2~6 px, 80~140 ms.
- 보스 대공격: 4~10 px, 150~300 ms.
- 여러 Shake가 겹치면 Max clamp.
- Reduce Motion, 화면 흔들림 강도 설정 제공.
- UI는 필요에 따라 Shake에서 제외.

## 16.19 Hit Stop

- 실제 시간 정지보다 애니메이션/파티클 속도 독립 관리가 필요하다.
- `Time.timeScale` 변경은 UI, 네트워크, 코루틴, 오디오에 영향을 준다.
- 가능하면 전투 시스템의 국소 HitStop 서비스 사용.
- 파티클은 `Unscaled Time`이 필요한지 효과별로 결정.
- Critical 기준 30~90 ms부터 시작하고 남용하지 않는다.

---

<a id="section-17"></a>
# 17. UI VFX 레시피

## 17.1 버튼 클릭

```text
Scale       1 → 0.96 → 1.02 → 1
Duration    0.12~0.18s
Ripple      1개, 0.20~0.35s
Spark       중요 버튼만 2~4개
Haptic      Light optional
```

- 일반 버튼마다 파티클 금지.
- 클릭 가능 여부는 색과 상태로 먼저 전달.
- `Raycast Target`과 파티클 오브젝트의 입력 차단 확인.

## 17.2 구매/확정 버튼

- 일반 버튼보다 색·Shine·음향을 강화.
- 성공과 실패 연출을 명확히 분리.
- 서버 응답 전에는 성공 Burst 금지.
- 로딩 상태에서는 반복 파티클보다 Spinner/Progress.

## 17.3 탭 전환

- 선택 탭 밑줄 이동.
- 아이콘 1회 Pop.
- 콘텐츠 Fade/Slide 0.15~0.30초.
- 모든 탭이 동시에 Glow하지 않는다.

## 17.4 팝업 열기

```text
Dim        0 → target alpha, 0.15~0.25s
Panel      scale 0.92 → 1.02 → 1, 0.20~0.32s
Content    30~80ms stagger
VFX        패널 안쪽 또는 제목 주변 소량
```

닫기:

- 열기의 완전 역재생보다 더 짧게 0.12~0.20초.
- 입력 차단 해제 시점 명확히.

## 17.5 숫자 카운터 Pop

- 값 변경 시 `1 → 1.12 → 1`.
- 증가: 위로 작은 +텍스트.
- 감소: 빨간색만 아니라 아래 방향/짧은 Shake.
- 초당 다수 변경은 50~100 ms 이내 이벤트를 합산.

## 17.6 Progress Bar

- 실제 값과 표시 값 분리.
- Fill이 움직이는 구간에 작은 Shine.
- 증가량이 크면 시작점/끝점 강조.
- 매 프레임 파티클을 Fill Edge에 생성하지 않고 낮은 Rate over Distance 또는 단일 Edge Glow.

## 17.7 텍스트 강조

- TextMeshPro Material/Vertex animation 또는 UIEffect.
- 희귀도 이름: Gradient + 짧은 Shine.
- Damage: Scale, 위치, Outline 차이.
- 긴 문장 전체에 Wave/Glitch를 적용하지 않는다.
- Localization 후 텍스트 길이와 마스크 확인.

## 17.8 화면 전환

- Fade가 기본이며 가장 안정적.
- 카드 게임 특화: 카드가 화면을 덮고 뒤집히며 전환.
- Radial wipe, Dissolve는 공통 TransitionService로 관리.
- 전환 중 로딩 완료 여부와 애니메이션 완료를 동기화.
- 저사양에서 Fullscreen Blur/Distortion 제거.

## 17.9 Tutorial Highlight

- 목표 주위 1~2 Hz Pulse.
- 화면 Dim + 구멍 마스크.
- 손가락 아이콘 이동.
- 번쩍임보다 방향성 애니메이션.
- 목표가 이동/스크롤될 때 Rect 갱신.

## 17.10 오류/경고

- 짧은 좌우 Shake.
- 경고 아이콘.
- 낮은 주파수 음향.
- 화면 전체 빨간 Flash는 치명적 경고에만.
- 색각 사용자를 위해 아이콘/텍스트 병행.

---

<a id="section-18"></a>
# 18. 2D Light·Normal Map·Bloom

## 18.1 Light2D 사용 원칙

Light2D는 강력하지만 모든 파티클에 붙일 기능이 아니다.

권장:

- 보스 공격 예고.
- 전설 카드 Reveal 순간.
- 횃불/마법진 같은 소수 환경 광원.
- 중요한 폭발의 100~250 ms Light pulse.

금지:

- 모든 Spark에 Light2D.
- 카드 목록의 모든 희귀 카드에 동적 Light.
- 화면 밖에서도 활성인 Light.
- 영향 Sorting Layer를 필요 이상 넓게 설정.

## 18.2 Normal Map

- 카드/캐릭터 Sprite에 Normal Map을 쓰면 Light2D 반응이 풍부해진다.
- 모든 UI 아이콘에 Normal Map을 만들지 않는다.
- 타일/스프라이트 flip과 normal 방향을 실제 빌드에서 확인.
- Normal Map의 압축으로 banding/블록이 생기는지 확인.
- Low 품질에서는 Normal Map 조명을 끄고 Unlit로 대체할 수 있게 한다.

## 18.3 Bloom

Bloom은 ‘빛을 만드는 기능’이 아니라 밝은 픽셀을 번지게 하는 기능이다.

규칙:

- Bloom 없이도 형태와 색이 읽혀야 한다.
- Threshold를 낮춰 화면 전체가 흐려지지 않게 한다.
- 카드 원화의 밝은 부분이 의도치 않게 Bloom되지 않는지 확인.
- UI와 월드가 같은 카메라 Post Processing을 공유할 때 텍스트 번짐 확인.
- Low에서는 Bloom Off + 사전 제작 Glow Sprite.
- Medium/High에서도 Bloom을 이유로 모든 색을 HDR 강도 10 이상으로 두지 않는다.

## 18.4 가짜 Glow

가장 저렴한 대안:

- 원본보다 1.1~1.4배 큰 Blur Sprite.
- Additive 또는 Premultiplied Alpha.
- 낮은 알파.
- 중심 Sprite와 함께 스케일/알파 애니메이션.

한 오브젝트에 Glow Sprite를 여러 겹 겹치지 않는다.

## 18.5 Distortion

우선순위:

1. UV 오버레이 왜곡.
2. 작은 카드/오브젝트 내부 왜곡.
3. 국소 화면 왜곡.
4. 전체 화면 왜곡.

Low에서는 1만 허용하는 것을 기본으로 한다. 화면 샘플링/Renderer Feature 방식은 모바일 GPU와 2D Renderer 호환성을 검증한다.


---

<a id="section-19"></a>
# 19. 런타임 VFX 아키텍처

## 19.1 절대 원칙

런타임 VFX는 다음 구조를 기본으로 한다.

```text
Gameplay / UI Event
        ↓
VfxService.Play(VfxDefinition, context)
        ↓
품질 등급·동시 재생 예산 확인
        ↓
해당 프리팹 Pool에서 대여
        ↓
위치·정렬·색·시드 적용
        ↓
ParticleSystem / Animator / Shader 재생
        ↓
재생 종료 감지
        ↓
Pool로 반환
```

핵심 규칙:

1. 전투 중 반복되는 이펙트에 `Instantiate`/`Destroy`를 직접 사용하지 않는다.
2. `Resources.Load`를 재생 순간에 호출하지 않는다.
3. 프리팹 참조는 `VfxDefinition` 또는 Addressables 사전 로딩 결과로 보관한다.
4. `ParticleSystem.Stop(true, StopBehavior.StopEmittingAndClear)` 후 재사용한다.
5. 풀 반환 시 Transform, Scale, Color, Material Property, Trail 잔여 상태를 초기화한다.
6. `WaitForSeconds`, 람다 콜백, LINQ를 대량 재생 루프에서 남발하지 않는다.
7. 중요한 연출은 풀 고갈 시에도 보이게 예약 용량을 둔다.
8. 장식 이펙트는 예산 초과 시 버릴 수 있어야 한다.
9. UI 좌표와 월드 좌표를 섞지 않는다.
10. 정렬 기준을 호출자가 명시하거나 프리팹에 고정한다.

## 19.2 이벤트 중요도

```csharp
public enum VfxImportance
{
    Decorative = 0, // 없어도 게임 정보가 유지됨
    Normal = 1,     // 일반 타격, 작은 보상
    Important = 2,  // 카드 사용, 큰 보상, 핵심 상태 변화
    Critical = 3    // 위험 경고, 보스 필살기, 전설 획득
}
```

운영 원칙:

| 중요도 | 예산 초과 시 | 예시 |
|---|---|---|
| Decorative | 즉시 생략 가능 | 배경 먼지, 작은 반짝이 |
| Normal | 간소화 또는 일부 생략 | 일반 타격, 코인 1개 |
| Important | Low 변형으로라도 재생 | 카드 사용, 레벨업 |
| Critical | 예약 풀을 사용해 반드시 재생 | 즉사 경고, 전설 카드 Reveal |

## 19.3 `VfxDefinition` ScriptableObject

아래 코드는 파일명을 `VfxDefinition.cs`로 만든다.

```csharp
using System.Collections.Generic;
using UnityEngine;

public enum VfxQualityTier
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum VfxSpace
{
    World,
    ScreenUI
}

[CreateAssetMenu(
    fileName = "VFX_",
    menuName = "Game/VFX/VFX Definition")]
public sealed class VfxDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string stableId = "vfx.unassigned";
    [SerializeField] private VfxImportance importance = VfxImportance.Normal;
    [SerializeField] private VfxSpace space = VfxSpace.World;

    [Header("Quality Prefabs")]
    [Tooltip("가장 가벼운 필수 표현. Important 이상은 가능한 한 지정한다.")]
    [SerializeField] private GameObject lowPrefab;
    [SerializeField] private GameObject mediumPrefab;
    [SerializeField] private GameObject highPrefab;

    [Header("Pooling")]
    [Min(0)]
    [SerializeField] private int prewarmCount = 2;
    [Min(1)]
    [SerializeField] private int maxPoolSize = 12;
    [Min(1)]
    [SerializeField] private int maxConcurrentPerEffect = 8;

    [Header("Lifetime")]
    [Tooltip("ParticleSystem이 끝나지 않는 잘못된 프리팹에 대한 안전 종료 시간. 0이면 비활성.")]
    [Min(0f)]
    [SerializeField] private float hardLifetimeSeconds = 8f;
    [SerializeField] private bool useUnscaledTime;

    [Header("Behavior")]
    [SerializeField] private bool allowDropWhenBusy = true;
    [SerializeField] private bool randomizeParticleSeed = true;

    public string StableId => stableId;
    public VfxImportance Importance => importance;
    public VfxSpace Space => space;
    public int PrewarmCount => prewarmCount;
    public int MaxPoolSize => maxPoolSize;
    public int MaxConcurrentPerEffect => maxConcurrentPerEffect;
    public float HardLifetimeSeconds => hardLifetimeSeconds;
    public bool UseUnscaledTime => useUnscaledTime;
    public bool AllowDropWhenBusy => allowDropWhenBusy;
    public bool RandomizeParticleSeed => randomizeParticleSeed;

    public GameObject GetPrefab(VfxQualityTier tier)
    {
        // 현재 등급 이하에서 가장 좋은 프리팹을 고른다.
        if (tier >= VfxQualityTier.High && highPrefab != null)
            return highPrefab;

        if (tier >= VfxQualityTier.Medium && mediumPrefab != null)
            return mediumPrefab;

        if (lowPrefab != null)
            return lowPrefab;

        // 누락된 경우에도 가능한 변형을 사용한다.
        if (mediumPrefab != null)
            return mediumPrefab;

        return highPrefab;
    }

    public void CollectUniquePrefabs(List<GameObject> output)
    {
        AddUnique(output, lowPrefab);
        AddUnique(output, mediumPrefab);
        AddUnique(output, highPrefab);
    }

    private static void AddUnique(List<GameObject> output, GameObject prefab)
    {
        if (prefab != null && !output.Contains(prefab))
            output.Add(prefab);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        stableId = stableId.Trim();
        prewarmCount = Mathf.Clamp(prewarmCount, 0, maxPoolSize);
        maxConcurrentPerEffect = Mathf.Clamp(
            maxConcurrentPerEffect,
            1,
            maxPoolSize);
    }
#endif
}
```

### Definition 제작 규칙

- `stableId`는 저장 데이터나 분석 이벤트에 쓸 수 있게 변경하지 않는 문자열로 만든다.
- 예: `vfx.card.acquire.legendary`, `vfx.combat.hit.fire.small`.
- Low 프리팹은 중요한 연출에서 절대 비워 두지 않는다.
- High 프리팹이 없으면 Medium을 재사용해도 된다.
- 프리팹별 재질 인스턴스를 무분별하게 만들지 않는다.
- 동일 효과를 단순 색상만 바꿀 경우 프리팹 복제보다 재생 컨텍스트 색상 전달을 우선한다.
- 구조가 크게 다르면 별도 Definition으로 분리한다.

## 19.4 전역 품질 상태

파일명: `VfxQualityRuntime.cs`

```csharp
using System;
using UnityEngine;

public static class VfxQualityRuntime
{
    private static VfxQualityTier tier = VfxQualityTier.Medium;

    public static event Action<VfxQualityTier> Changed;

    public static VfxQualityTier Tier => tier;

    public static void SetTier(VfxQualityTier value)
    {
        if (tier == value)
            return;

        tier = value;
        Changed?.Invoke(tier);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        tier = VfxQualityTier.Medium;
        Changed = null;
    }
}
```

주의:

- 정적 이벤트는 Domain Reload 비활성 환경에서 구독 누수가 생기기 쉽다.
- 위 `SubsystemRegistration` 초기화와 각 컴포넌트의 `OnEnable`/`OnDisable` 구독 해제를 함께 사용한다.
- 게임 설정에 저장된 품질 값을 부팅 초기에 `SetTier`로 적용한다.

## 19.5 풀 인스턴스

파일명: `VfxInstance.cs`

```csharp
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VfxInstance : MonoBehaviour
{
    private ParticleSystem[] particleSystems;
    private TrailRenderer[] trailRenderers;
    private Animator[] animators;

    private Action<VfxInstance> releaseAction;
    private float hardLifetime;
    private float age;
    private bool useUnscaledTime;
    private bool playing;
    private int activeToken;

    public bool IsPlaying => playing;
    public int ActiveToken => activeToken;

    private void Awake()
    {
        CacheComponents();
    }

    public void Configure(Action<VfxInstance> onRelease)
    {
        releaseAction = onRelease;
        CacheComponents();
    }

    public void Play(
        Vector3 worldPosition,
        Quaternion worldRotation,
        Transform parent,
        Vector3 localScale,
        float hardLifetimeSeconds,
        bool unscaledTime,
        bool randomizeSeed,
        int token)
    {
        activeToken = token;
        hardLifetime = hardLifetimeSeconds;
        useUnscaledTime = unscaledTime;
        age = 0f;
        playing = true;

        transform.SetParent(parent, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        transform.localScale = localScale;

        gameObject.SetActive(true);

        ClearTrails();
        RestartAnimators();
        RestartParticles(randomizeSeed);
    }

    public void StopAndRelease(bool clearParticles = true)
    {
        if (!playing)
            return;

        if (particleSystems != null)
        {
            ParticleSystemStopBehavior behavior = clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps != null)
                    ps.Stop(true, behavior);
            }
        }

        Release();
    }

    private void Update()
    {
        if (!playing)
            return;

        age += useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (hardLifetime > 0f && age >= hardLifetime)
        {
            StopAndRelease(true);
            return;
        }

        // 첫 프레임 직후 IsAlive가 안정되도록 아주 짧은 유예를 둔다.
        if (age < 0.05f)
            return;

        if (!AnyParticleAlive() && !AnyAnimatorStillRelevant())
            Release();
    }

    private bool AnyParticleAlive()
    {
        if (particleSystems == null || particleSystems.Length == 0)
            return false;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps != null && ps.IsAlive(true))
                return true;
        }

        return false;
    }

    private bool AnyAnimatorStillRelevant()
    {
        // Animator만으로 구성된 프리팹은 안전 수명(hardLifetime)을 사용한다.
        // ParticleSystem이 하나라도 있으면 Particle 생존 여부를 우선한다.
        return particleSystems != null && particleSystems.Length == 0
            && animators != null && animators.Length > 0
            && hardLifetime > 0f && age < hardLifetime;
    }

    private void RestartParticles(bool randomizeSeed)
    {
        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (randomizeSeed)
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = unchecked((uint)UnityEngine.Random.Range(1, int.MaxValue));
            }

            ps.Play(true);
        }
    }

    private void RestartAnimators()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            animator.Rebind();
            animator.Update(0f);
            animator.enabled = true;
        }
    }

    private void ClearTrails()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail != null)
                trail.Clear();
        }
    }

    private void Release()
    {
        if (!playing)
            return;

        playing = false;
        ClearTrails();
        releaseAction?.Invoke(this);
    }

    private void CacheComponents()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        animators = GetComponentsInChildren<Animator>(true);
    }
}
```

### Animator 전용 이펙트 주의

- Animator만 있고 ParticleSystem이 없는 프리팹은 `hardLifetimeSeconds`를 반드시 지정한다.
- Animation Event에서 반환하는 구조를 쓰더라도 비정상 이벤트 누락에 대비해 안전 수명을 둔다.
- Loop 상태를 실수로 넣으면 풀로 돌아오지 않는다.

## 19.6 `VfxService`와 풀

파일명: `VfxService.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct VfxPlayRequest
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Transform Parent;
    public readonly Vector3 Scale;

    public VfxPlayRequest(
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        Vector3? scale = null)
    {
        Position = position;
        Rotation = rotation;
        Parent = parent;
        Scale = scale ?? Vector3.one;
    }

    public static VfxPlayRequest At(Vector3 position)
    {
        return new VfxPlayRequest(position, Quaternion.identity);
    }
}

[DefaultExecutionOrder(-500)]
public sealed class VfxService : MonoBehaviour
{
    [Header("Bootstrap")]
    [SerializeField] private List<VfxDefinition> preloadDefinitions = new();
    [SerializeField] private Transform worldPoolRoot;
    [SerializeField] private Transform uiPoolRoot;

    [Header("Global concurrent limits")]
    [Min(1)] [SerializeField] private int lowGlobalLimit = 24;
    [Min(1)] [SerializeField] private int mediumGlobalLimit = 48;
    [Min(1)] [SerializeField] private int highGlobalLimit = 80;

    private readonly Dictionary<GameObject, PoolBucket> buckets = new();
    private readonly Dictionary<VfxDefinition, int> activePerDefinition = new();
    private readonly List<GameObject> prefabScratch = new(3);

    private int globalActiveCount;
    private int nextToken = 1;

    public int GlobalActiveCount => globalActiveCount;

    private void Awake()
    {
        EnsureRoots();

        for (int i = 0; i < preloadDefinitions.Count; i++)
        {
            VfxDefinition definition = preloadDefinitions[i];
            if (definition != null)
                Register(definition);
        }
    }

    public void Register(VfxDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        prefabScratch.Clear();
        definition.CollectUniquePrefabs(prefabScratch);

        for (int i = 0; i < prefabScratch.Count; i++)
        {
            GameObject prefab = prefabScratch[i];
            PoolBucket bucket = GetOrCreateBucket(definition, prefab);
            bucket.Prewarm(definition.PrewarmCount);
        }
    }

    public VfxInstance Play(
        VfxDefinition definition,
        in VfxPlayRequest request)
    {
        if (definition == null)
            return null;

        GameObject prefab = definition.GetPrefab(VfxQualityRuntime.Tier);
        if (prefab == null)
        {
            Debug.LogWarning($"VFX prefab is missing: {definition.name}", definition);
            return null;
        }

        int activeForEffect = GetActiveCount(definition);
        bool perEffectBusy = activeForEffect >= definition.MaxConcurrentPerEffect;
        bool globallyBusy = globalActiveCount >= GetGlobalLimit();

        if ((perEffectBusy || globallyBusy)
            && definition.AllowDropWhenBusy
            && definition.Importance <= VfxImportance.Normal)
        {
            return null;
        }

        PoolBucket bucket = GetOrCreateBucket(definition, prefab);
        VfxInstance instance = bucket.Rent();

        if (instance == null)
        {
            // 중요한 연출이라도 하드 풀 상한을 넘겨 생성하지 않는다.
            // Important/Critical은 충분한 예약 크기로 설계해야 한다.
            return null;
        }

        int token = NextToken();
        globalActiveCount++;
        activePerDefinition[definition] = activeForEffect + 1;

        instance.SetReleaseMetadata(
            definition,
            OnInstanceReleased);

        instance.Play(
            request.Position,
            request.Rotation,
            request.Parent,
            request.Scale,
            definition.HardLifetimeSeconds,
            definition.UseUnscaledTime,
            definition.RandomizeParticleSeed,
            token);

        return instance;
    }

    public void Stop(VfxInstance instance, bool clearParticles = true)
    {
        if (instance != null)
            instance.StopAndRelease(clearParticles);
    }

    private void OnInstanceReleased(
        VfxInstance instance,
        VfxDefinition definition)
    {
        globalActiveCount = Mathf.Max(0, globalActiveCount - 1);

        if (definition != null
            && activePerDefinition.TryGetValue(definition, out int count))
        {
            count = Mathf.Max(0, count - 1);
            if (count == 0)
                activePerDefinition.Remove(definition);
            else
                activePerDefinition[definition] = count;
        }

        if (instance != null && instance.SourcePrefab != null
            && buckets.TryGetValue(instance.SourcePrefab, out PoolBucket bucket))
        {
            bucket.Release(instance);
            return;
        }

        if (instance != null)
            Destroy(instance.gameObject);
    }

    private int GetActiveCount(VfxDefinition definition)
    {
        return activePerDefinition.TryGetValue(definition, out int count)
            ? count
            : 0;
    }

    private int GetGlobalLimit()
    {
        return VfxQualityRuntime.Tier switch
        {
            VfxQualityTier.Low => lowGlobalLimit,
            VfxQualityTier.Medium => mediumGlobalLimit,
            _ => highGlobalLimit
        };
    }

    private PoolBucket GetOrCreateBucket(
        VfxDefinition definition,
        GameObject prefab)
    {
        if (buckets.TryGetValue(prefab, out PoolBucket existing))
            return existing;

        Transform root = definition.Space == VfxSpace.ScreenUI
            ? uiPoolRoot
            : worldPoolRoot;

        PoolBucket created = new(
            prefab,
            root,
            definition.MaxPoolSize);

        buckets.Add(prefab, created);
        return created;
    }

    private int NextToken()
    {
        if (nextToken == int.MaxValue)
            nextToken = 1;

        return nextToken++;
    }

    private void EnsureRoots()
    {
        if (worldPoolRoot == null)
            worldPoolRoot = CreateRoot("VFX_POOL_WORLD");

        if (uiPoolRoot == null)
            uiPoolRoot = CreateRoot("VFX_POOL_UI");
    }

    private Transform CreateRoot(string rootName)
    {
        GameObject root = new(rootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private sealed class PoolBucket
    {
        private readonly GameObject prefab;
        private readonly Transform root;
        private readonly int maxSize;
        private readonly Stack<VfxInstance> inactive = new();
        private int createdCount;

        public PoolBucket(GameObject prefab, Transform root, int maxSize)
        {
            this.prefab = prefab;
            this.root = root;
            this.maxSize = Mathf.Max(1, maxSize);
        }

        public void Prewarm(int requestedCount)
        {
            int target = Mathf.Min(requestedCount, maxSize);
            while (createdCount < target)
            {
                VfxInstance instance = Create();
                instance.gameObject.SetActive(false);
                inactive.Push(instance);
            }
        }

        public VfxInstance Rent()
        {
            if (inactive.Count > 0)
                return inactive.Pop();

            if (createdCount >= maxSize)
                return null;

            return Create();
        }

        public void Release(VfxInstance instance)
        {
            if (instance == null)
                return;

            instance.transform.SetParent(root, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.gameObject.SetActive(false);
            inactive.Push(instance);
        }

        private VfxInstance Create()
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, root);
            go.name = prefab.name;

            VfxInstance instance = go.GetComponent<VfxInstance>();
            if (instance == null)
                instance = go.AddComponent<VfxInstance>();

            instance.ConfigureSource(prefab);
            createdCount++;
            return instance;
        }
    }
}
```

위 서비스 코드가 동작하도록 `VfxInstance.cs`에 다음 멤버를 추가한다.

```csharp
private VfxDefinition sourceDefinition;
private Action<VfxInstance, VfxDefinition> serviceReleaseAction;

public GameObject SourcePrefab { get; private set; }

public void ConfigureSource(GameObject sourcePrefab)
{
    SourcePrefab = sourcePrefab;
    CacheComponents();
}

public void SetReleaseMetadata(
    VfxDefinition definition,
    Action<VfxInstance, VfxDefinition> onReleased)
{
    sourceDefinition = definition;
    serviceReleaseAction = onReleased;
}
```

그리고 기존 `Release()`의 마지막 부분을 다음처럼 교체한다.

```csharp
private void Release()
{
    if (!playing)
        return;

    playing = false;
    ClearTrails();

    if (serviceReleaseAction != null)
    {
        Action<VfxInstance, VfxDefinition> callback = serviceReleaseAction;
        VfxDefinition definition = sourceDefinition;
        serviceReleaseAction = null;
        sourceDefinition = null;
        callback.Invoke(this, definition);
        return;
    }

    releaseAction?.Invoke(this);
}
```

> 프로젝트에서는 두 콜백 방식을 동시에 유지하기보다 하나로 정리하는 것이 좋다. 위 코드는 단계별 설명을 위해 기존 단순 풀 콜백과 서비스 메타데이터 방식을 함께 보여준다. 최종 구현에서는 `serviceReleaseAction` 하나만 남기는 것을 권장한다.

## 19.7 사용 예시

```csharp
using UnityEngine;

public sealed class CardCombatPresenter : MonoBehaviour
{
    [SerializeField] private VfxService vfxService;
    [SerializeField] private VfxDefinition cardPlayVfx;
    [SerializeField] private VfxDefinition hitVfx;
    [SerializeField] private Transform cardAnchor;

    public void PlayCard()
    {
        VfxPlayRequest request = new(
            cardAnchor.position,
            cardAnchor.rotation,
            null,
            Vector3.one);

        vfxService.Play(cardPlayVfx, request);
    }

    public void ShowHit(Vector3 worldPosition)
    {
        vfxService.Play(hitVfx, VfxPlayRequest.At(worldPosition));
    }
}
```

## 19.8 지속형 이펙트 Handle

버프 Aura, 선택 테두리, 타겟팅 선처럼 계속 유지되는 이펙트는 자동 종료형과 분리한다.

```csharp
private VfxInstance activeAura;

public void BeginAura(VfxService service, VfxDefinition definition, Transform owner)
{
    if (activeAura != null)
        return;

    VfxPlayRequest request = new(
        owner.position,
        owner.rotation,
        owner,
        Vector3.one);

    activeAura = service.Play(definition, request);
}

public void EndAura(VfxService service)
{
    if (activeAura == null)
        return;

    service.Stop(activeAura, clearParticles: false);
    activeAura = null;
}
```

지속형 프리팹 규칙:

- Loop Particle은 유지 레이어에만 사용한다.
- 종료 시 `StopEmitting` 후 남은 입자가 사라질 시간을 줄 것인지 정한다.
- 소유 카드가 파괴/재활용될 때 반드시 Handle을 종료한다.
- 화면 밖 카드에는 Aura를 끈다.
- 리스트 스크롤 셀에서 `OnDisable` 반환을 보장한다.

## 19.9 랜덤 시드와 리플레이

일반 연출은 시드 랜덤화로 반복감을 줄인다.

결정적 리플레이나 네트워크 동기화가 필요하면:

- 게임플레이 판정과 VFX 랜덤을 분리한다.
- `UnityEngine.Random` 전역 상태에 의존하지 않는다.
- 이벤트 ID와 카드 인스턴스 ID로 `uint seed`를 생성한다.
- ParticleSystem의 `randomSeed`에 명시적으로 넣는다.
- VFX 시드는 판정 결과를 바꾸지 않는다.

예시:

```csharp
public static uint MakeVfxSeed(int battleTurn, int eventIndex, int cardId)
{
    unchecked
    {
        uint hash = 2166136261;
        hash = (hash ^ (uint)battleTurn) * 16777619;
        hash = (hash ^ (uint)eventIndex) * 16777619;
        hash = (hash ^ (uint)cardId) * 16777619;
        return hash == 0 ? 1u : hash;
    }
}
```

---

<a id="section-20"></a>
# 20. 품질 등급과 자동 축소

## 20.1 기본 등급표

| 항목 | Low | Medium | High |
|---|---:|---:|---:|
| 목표 FPS | 30 | 60 또는 안정 30 | 60/고주사율 옵션 |
| 동시 VFX 권장 상한 | 24 | 48 | 80 |
| 일반 타격 파티클 | 4~10 | 8~20 | 14~36 |
| 카드 획득 파티클 | 12~24 | 20~48 | 36~90 |
| Trail | 핵심 1개만 | 1~3개 | 2~6개 |
| Noise | Off 또는 매우 적게 | 핵심 레이어만 | 선택적으로 사용 |
| Collision | 기본 Off | 필요한 소수 | 필요한 소수 |
| Distortion | Off | 국소·작게 | 국소 허용 |
| Bloom | Off 또는 약함 | 약함 | 연출별 허용 |
| 2D Light | 치명적 순간만 | 중요 순간 | 중요 순간 + 일부 환경 |
| Flipbook 크기 | 4×4 중심 | 4×4~8×8 | 8×8 선택 |
| 텍스처 해상도 | 128~512 | 256~1024 | 512~1024 |
| UI 보상 아이콘 동시 수 | 5~8 | 8~14 | 12~20 |
| 화면 Shake | 축소 | 표준 | 표준, 강도 제한 유지 |

이 값은 출발점이며 실제 기기 프로파일 결과로 조정한다.

## 20.2 Tier Gate 컴포넌트

프리팹 안에서 품질별 자식 레이어를 켜고 끄는 가장 단순하고 안전한 방식이다.

파일명: `VfxTierGate.cs`

```csharp
using UnityEngine;

public sealed class VfxTierGate : MonoBehaviour
{
    [SerializeField] private VfxQualityTier minimumTier = VfxQualityTier.Low;
    [SerializeField] private VfxQualityTier maximumTier = VfxQualityTier.High;
    [SerializeField] private GameObject[] targets;

    private void OnEnable()
    {
        VfxQualityRuntime.Changed += Apply;
        Apply(VfxQualityRuntime.Tier);
    }

    private void OnDisable()
    {
        VfxQualityRuntime.Changed -= Apply;
    }

    private void Apply(VfxQualityTier tier)
    {
        bool enabledForTier = tier >= minimumTier && tier <= maximumTier;

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target != null && target.activeSelf != enabledForTier)
                target.SetActive(enabledForTier);
        }
    }
}
```

프리팹 예:

```text
VFX_CardAcquire_Legendary
├─ CoreFlash                 [Low~High]
├─ Ring                      [Low~High]
├─ SparkEssential            [Low~High]
├─ SparkExtra                [Medium~High]
├─ RibbonTrail               [Medium~High]
├─ Distortion                [High only]
├─ Light2DPulse              [High only]
└─ SecondaryConfetti         [High only]
```

## 20.3 런타임 자동 등급 선택

첫 실행에서 단순한 하드웨어 정보로 초기값을 정하되, 절대 최종 판정으로 믿지 않는다.

```csharp
using UnityEngine;

public static class InitialVfxQualitySelector
{
    public static VfxQualityTier Recommend()
    {
        int memoryMb = SystemInfo.systemMemorySize;
        int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
        int shaderLevel = SystemInfo.graphicsShaderLevel;

        if (memoryMb <= 3500
            || graphicsMemoryMb <= 512
            || shaderLevel < 45)
        {
            return VfxQualityTier.Low;
        }

        if (memoryMb <= 6000 || graphicsMemoryMb <= 1500)
            return VfxQualityTier.Medium;

        return VfxQualityTier.High;
    }
}
```

주의:

- `SystemInfo` 값은 제조사/드라이버마다 부정확할 수 있다.
- GPU 이름 문자열 화이트리스트는 유지보수 비용이 크다.
- 최초 권장값을 사용자 설정으로 덮어쓸 수 있어야 한다.
- 프레임 안정성·열 상태 기반으로 런타임에 한 단계 내리는 정책을 별도로 둔다.
- 플레이 중 품질을 올리는 것보다 내리는 것이 안전하다.

## 20.4 프레임 시간 기반 완만한 강등

갑작스러운 한두 프레임 스파이크로 등급을 바꾸지 않는다.

권장 로직:

1. 60 FPS 목표라면 16.67 ms가 이상적이다.
2. 5~10초 이동 평균이 목표보다 지속적으로 나쁠 때 경고 상태.
3. 10~20초 이상 나쁘고 로딩/전환 중이 아닐 때 한 단계 강등.
4. 전투 중 즉시 품질을 올리지 않는다.
5. 품질 회복은 메뉴/스테이지 종료 시점에만 검토.
6. Thermal Warning이 있으면 즉시 High → Medium 또는 Medium → Low.
7. FPS가 낮은 이유가 CPU 게임 로직인데 파티클만 내려도 효과가 제한적임을 기록한다.

간단한 샘플:

```csharp
using UnityEngine;

public sealed class VfxFrameTimeGovernor : MonoBehaviour
{
    [SerializeField] private float sampleWindowSeconds = 12f;
    [SerializeField] private float lowFpsThresholdFor60Target = 48f;
    [SerializeField] private float lowFpsThresholdFor30Target = 26f;
    [SerializeField] private int targetFps = 60;

    private float elapsed;
    private float accumulatedUnscaledDelta;
    private int frameCount;
    private bool alreadyDowngradedThisSession;

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        accumulatedUnscaledDelta += Time.unscaledDeltaTime;
        frameCount++;

        if (elapsed < sampleWindowSeconds)
            return;

        float averageFps = frameCount / Mathf.Max(0.001f, accumulatedUnscaledDelta);
        float threshold = targetFps >= 60
            ? lowFpsThresholdFor60Target
            : lowFpsThresholdFor30Target;

        if (!alreadyDowngradedThisSession && averageFps < threshold)
        {
            DowngradeOneStep();
            alreadyDowngradedThisSession = true;
        }

        elapsed = 0f;
        accumulatedUnscaledDelta = 0f;
        frameCount = 0;
    }

    private static void DowngradeOneStep()
    {
        VfxQualityTier current = VfxQualityRuntime.Tier;
        if (current == VfxQualityTier.High)
            VfxQualityRuntime.SetTier(VfxQualityTier.Medium);
        else if (current == VfxQualityTier.Medium)
            VfxQualityRuntime.SetTier(VfxQualityTier.Low);
    }
}
```

프로덕션에서는 다음을 추가한다.

- 로딩 화면/앱 Resume 직후 샘플 제외.
- 전투 시작 첫 몇 초의 Shader warm-up 제외.
- CPU/GPU 병목 구분.
- Adaptive Performance Android Provider가 제공하는 열 상태와 결합.
- 사용자에게 자동 품질과 수동 품질 중 선택 제공.
- 분석 이벤트에 `deviceModel`, tier 변화, 평균 FPS를 익명 통계로 기록.

## 20.5 품질 강등 우선순위

품질을 낮출 때 다음 순서로 줄인다.

1. 화면 밖 VFX 중지.
2. 장식 파티클 Spawn Rate 감소.
3. Secondary Spark/Confetti 제거.
4. Distortion 제거.
5. Particle Noise 제거 또는 품질 감소.
6. Trail 수/세그먼트 감소.
7. 2D Light 제거.
8. Bloom 제거.
9. Flipbook 해상도 감소.
10. 핵심 Silhouette와 Impact Flash는 유지.

게임 정보 전달용 경고 색·형태·타이밍은 품질 때문에 제거하면 안 된다.

---

<a id="section-21"></a>
# 21. 카드 렌더링과 MaterialPropertyBlock

## 21.1 SpriteRenderer 카드

월드 또는 Screen Space Camera에 배치한 `SpriteRenderer` 카드에는 `MaterialPropertyBlock`을 사용해 카드별 파라미터를 전달한다.

장점:

- `renderer.material` 호출로 재질 인스턴스를 만들지 않는다.
- 선택, 피격, Shine, Dissolve 값을 카드마다 다르게 줄 수 있다.
- 동일 Material과 Texture Atlas를 공유하기 쉽다.

파일명: `CardVfxPropertyBlock.cs`

```csharp
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CardVfxPropertyBlock : MonoBehaviour
{
    private static readonly int BaseTintId = Shader.PropertyToID("_BaseTint");
    private static readonly int SelectionId = Shader.PropertyToID("_Selection");
    private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private static readonly int ShineProgressId = Shader.PropertyToID("_ShineProgress");
    private static readonly int FoilStrengthId = Shader.PropertyToID("_FoilStrength");
    private static readonly int GrayAmountId = Shader.PropertyToID("_GrayAmount");
    private static readonly int RarityId = Shader.PropertyToID("_Rarity");

    [SerializeField] private SpriteRenderer targetRenderer;

    private MaterialPropertyBlock block;

    private Color baseTint = Color.white;
    private float selection;
    private float hitFlash;
    private float dissolve;
    private float shineProgress;
    private float foilStrength;
    private float grayAmount;
    private float rarity;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        block = new MaterialPropertyBlock();
        ApplyAll();
    }

    public void SetBaseTint(Color value)
    {
        baseTint = value;
        ApplyAll();
    }

    public void SetSelection(float value)
    {
        selection = Mathf.Clamp01(value);
        ApplyAll();
    }

    public void SetHitFlash(float value)
    {
        hitFlash = Mathf.Clamp01(value);
        ApplyAll();
    }

    public void SetDissolve(float value)
    {
        dissolve = Mathf.Clamp01(value);
        ApplyAll();
    }

    public void SetShineProgress(float value)
    {
        shineProgress = value;
        ApplyAll();
    }

    public void SetFoilStrength(float value)
    {
        foilStrength = Mathf.Max(0f, value);
        ApplyAll();
    }

    public void SetGrayAmount(float value)
    {
        grayAmount = Mathf.Clamp01(value);
        ApplyAll();
    }

    public void SetRarityNormalized(float value)
    {
        rarity = Mathf.Clamp01(value);
        ApplyAll();
    }

    public void ResetVisualState()
    {
        baseTint = Color.white;
        selection = 0f;
        hitFlash = 0f;
        dissolve = 0f;
        shineProgress = -1f;
        foilStrength = 0f;
        grayAmount = 0f;
        rarity = 0f;
        ApplyAll();
    }

    private void ApplyAll()
    {
        if (targetRenderer == null)
            return;

        block ??= new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(block);
        block.SetColor(BaseTintId, baseTint);
        block.SetFloat(SelectionId, selection);
        block.SetFloat(HitFlashId, hitFlash);
        block.SetFloat(DissolveId, dissolve);
        block.SetFloat(ShineProgressId, shineProgress);
        block.SetFloat(FoilStrengthId, foilStrength);
        block.SetFloat(GrayAmountId, grayAmount);
        block.SetFloat(RarityId, rarity);
        targetRenderer.SetPropertyBlock(block);
    }
}
```

### 최적화 개선

위 샘플은 이해하기 쉽도록 Setter마다 모든 값을 적용한다. 호출이 매우 많다면:

- Setter는 값과 `dirty`만 변경.
- `LateUpdate`에서 한 번만 `ApplyAll`.
- 값 변화가 없는 경우 적용 생략.
- 카드가 화면 밖이면 Update 비활성.
- Property ID는 반드시 캐시.

## 21.2 카드 상태 애니메이터

Coroutine을 매번 생성하지 않고 하나의 Update 루프로 상태를 애니메이션한다.

파일명: `CardVfxAnimator.cs`

```csharp
using UnityEngine;

[RequireComponent(typeof(CardVfxPropertyBlock))]
public sealed class CardVfxAnimator : MonoBehaviour
{
    [SerializeField] private CardVfxPropertyBlock properties;
    [SerializeField] private AnimationCurve hitCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    [SerializeField] private float hitDuration = 0.18f;
    [SerializeField] private float shineDuration = 0.65f;

    private float hitTime = -1f;
    private float shineTime = -1f;

    private void Awake()
    {
        if (properties == null)
            properties = GetComponent<CardVfxPropertyBlock>();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (hitTime >= 0f)
        {
            hitTime += dt;
            float t = Mathf.Clamp01(hitTime / Mathf.Max(0.001f, hitDuration));
            properties.SetHitFlash(hitCurve.Evaluate(t));

            if (t >= 1f)
                hitTime = -1f;
        }

        if (shineTime >= 0f)
        {
            shineTime += dt;
            float t = Mathf.Clamp01(shineTime / Mathf.Max(0.001f, shineDuration));
            properties.SetShineProgress(Mathf.Lerp(-0.3f, 1.3f, t));

            if (t >= 1f)
            {
                shineTime = -1f;
                properties.SetShineProgress(-1f);
            }
        }
    }

    public void PlayHitFlash()
    {
        hitTime = 0f;
    }

    public void PlayShine()
    {
        shineTime = 0f;
    }

    public void SetSelected(bool selected)
    {
        properties.SetSelection(selected ? 1f : 0f);
    }
}
```

## 21.3 uGUI Image 카드의 제한

`MaterialPropertyBlock`은 일반적으로 uGUI `Graphic`의 카드별 속성 전달 방식으로 사용할 수 없다.

uGUI 카드에서는 다음 우선순위를 사용한다.

1. **UIEffect로 상태 효과를 조합**한다.
2. 카드 전체가 아니라 필요한 오버레이 Image만 별도 Material을 사용한다.
3. 화면에 실제로 보이는 카드 셀 수만큼만 Material 인스턴스를 풀링한다.
4. 동일 상태의 카드끼리 Material을 공유한다.
5. Shader Graph UI Target과 Additional Shader Channels를 사용할 때 Canvas 배칭을 측정한다.
6. 카드 100장 각각에 독립 Material을 상시 생성하지 않는다.

`Image.material`에 접근해 생성된 인스턴스의 수명 관리 없이 계속 바꾸는 패턴을 피한다.

## 21.4 카드 프리팹 권장 구조

```text
CardView
├─ ArtRoot
│  ├─ Art                  SpriteRenderer 또는 Image
│  ├─ ArtStatusOverlay     상태 이상용
│  └─ ArtFoilOverlay       희귀도용, 필요 시만 활성
├─ FrameRoot
│  ├─ Frame
│  ├─ RarityGem
│  └─ SelectionOutline
├─ TextRoot
│  ├─ Cost
│  ├─ Name
│  └─ Description
├─ VfxAnchorRoot
│  ├─ Center
│  ├─ Border
│  ├─ Top
│  ├─ Cost
│  └─ TargetLineOrigin
└─ StateRoot
   ├─ DisabledOverlay
   ├─ LockOverlay
   └─ CooldownOverlay
```

원칙:

- 원화와 프레임 효과를 분리한다.
- 텍스트에는 강한 Distortion/Bloom을 적용하지 않는다.
- 카드 선택 Outline은 프레임 바깥쪽으로 확장한다.
- 카드 획득 Reveal은 별도 Presentation 프리팹에서 수행하고 목록 카드 프리팹을 과적재하지 않는다.
- 카드 풀 반환 시 모든 상태 Overlay와 Shader 값 초기화.

---

<a id="section-22"></a>
# 22. UI 보상 아이콘 비행

## 22.1 연출 구조

코인·보석·경험치·재료 획득은 다음 흐름을 사용한다.

```text
획득 위치에서 아이콘 생성
→ 0~100 ms Scatter
→ 100~300 ms 짧은 정지/회전
→ 250~750 ms HUD 목표로 곡선 이동
→ 목표 직전 Scale Down
→ 도착 순간 HUD Pulse + 숫자 증가 + 작은 Spark
→ 풀 반환
```

여러 개를 보여 줄 때 실제 보상 개수만큼 생성하지 않는다.

| 실제 획득량 | 표시 아이콘 권장 수 |
|---:|---:|
| 1 | 1 |
| 2~5 | 2~4 |
| 6~20 | 4~7 |
| 21~100 | 6~10 |
| 101 이상 | 8~14 |

Low 품질에서는 상한을 5~8개로 낮춘다.

## 22.2 베지어 이동 컴포넌트

파일명: `RewardFlyIcon.cs`

```csharp
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RewardFlyIcon : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Canvas canvas;
    [SerializeField] private AnimationCurve scaleCurve =
        AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0.2f);
    [SerializeField] private AnimationCurve progressCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private RectTransform movementRoot;
    private RectTransform target;
    private Camera uiCamera;

    private Vector2 p0;
    private Vector2 p1;
    private Vector2 p2;
    private Vector2 p3;
    private float duration;
    private float elapsed;
    private bool playing;

    private Action<RewardFlyIcon> releaseAction;
    private Action arrivedAction;

    private void Awake()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
    }

    public void Play(
        RectTransform root,
        RectTransform targetTransform,
        Vector2 startLocalPosition,
        float travelDuration,
        float arcHeight,
        float horizontalBend,
        Action<RewardFlyIcon> onRelease,
        Action onArrived = null)
    {
        movementRoot = root;
        target = targetTransform;
        duration = Mathf.Max(0.05f, travelDuration);
        releaseAction = onRelease;
        arrivedAction = onArrived;
        elapsed = 0f;
        playing = true;

        uiCamera = canvas != null
            && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        rectTransform.SetParent(movementRoot, false);
        rectTransform.anchoredPosition = startLocalPosition;
        rectTransform.localScale = Vector3.one;
        gameObject.SetActive(true);

        p0 = startLocalPosition;
        p3 = GetTargetLocalPosition();

        float direction = horizontalBend;
        p1 = p0 + new Vector2(direction, arcHeight);
        p2 = p3 + new Vector2(-direction * 0.35f, arcHeight * 0.25f);
    }

    private void Update()
    {
        if (!playing)
            return;

        elapsed += Time.unscaledDeltaTime;
        float rawT = Mathf.Clamp01(elapsed / duration);
        float t = progressCurve.Evaluate(rawT);

        // HUD가 움직이거나 Safe Area가 변해도 마지막 목표를 추적한다.
        p3 = GetTargetLocalPosition();

        rectTransform.anchoredPosition = CubicBezier(p0, p1, p2, p3, t);

        float scale = Mathf.Max(0f, scaleCurve.Evaluate(rawT));
        rectTransform.localScale = Vector3.one * scale;

        if (rawT >= 1f)
            Complete();
    }

    public void Cancel()
    {
        if (!playing)
            return;

        playing = false;
        arrivedAction = null;
        Release();
    }

    private void Complete()
    {
        if (!playing)
            return;

        playing = false;
        arrivedAction?.Invoke();
        arrivedAction = null;
        Release();
    }

    private void Release()
    {
        Action<RewardFlyIcon> callback = releaseAction;
        releaseAction = null;
        callback?.Invoke(this);
    }

    private Vector2 GetTargetLocalPosition()
    {
        if (target == null || movementRoot == null)
            return p3;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            target.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            movementRoot,
            screenPoint,
            uiCamera,
            out Vector2 localPoint);

        return localPoint;
    }

    private static Vector2 CubicBezier(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d,
        float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * oneMinusT * a
             + 3f * oneMinusT * oneMinusT * t * b
             + 3f * oneMinusT * t * t * c
             + t * t * t * d;
    }
}
```

## 22.3 보상 카운터 타이밍

세 가지 정책 중 하나를 프로젝트 전체에서 통일한다.

### 정책 A: 서버/게임 상태는 즉시, UI 숫자만 지연

- 실제 재화는 획득 즉시 반영.
- HUD 숫자는 아이콘 도착에 맞춰 따라감.
- 화면 전환이나 앱 종료에도 데이터가 안전.
- 가장 권장.

### 정책 B: 아이콘마다 분할 증가

- 총량을 표시 아이콘 수로 나누어 도착마다 증가.
- 마지막 아이콘에서 반올림 잔여량 처리.
- 큰 보상 체감이 좋음.
- 숫자 업데이트 소리도 너무 많이 울리지 않게 제한.

### 정책 C: 마지막 도착 시 한 번에 증가

- 구현이 가장 단순.
- 큰 수치가 한 번에 튀어 만족감이 좋을 수 있음.
- 이동 중 HUD와 실제 값이 일시적으로 다르게 보일 수 있음.

## 22.4 보상 아이콘 Scatter

동일 위치에서 겹치지 않게 한다.

```csharp
public static Vector2 MakeScatterOffset(int index, int count, float radius)
{
    if (count <= 1)
        return Vector2.zero;

    float angle = (index / (float)count) * Mathf.PI * 2f;
    float ring = radius * (0.65f + 0.35f * ((index % 3) / 2f));
    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
}
```

완전 무작위보다 균등 각도 + 작은 Jitter가 읽기 쉽다.

## 22.5 HUD 도착 Pulse

도착 시 다음을 120~220 ms 안에 수행한다.

- 아이콘 Scale 1.0 → 1.15~1.28 → 1.0.
- 작은 Ring 1개.
- 2~6 Spark.
- 숫자 색 밝아짐.
- 짧은 Click/Chime.
- 진동은 여러 아이콘마다가 아니라 첫 도착 또는 마지막 도착 한 번.

---

<a id="section-23"></a>
# 23. 카드·보상·전투 VFX 정밀 레시피

아래 값은 출발점이다. 모든 Size 값은 프로젝트의 Pixels Per Unit과 카드 크기에 맞춰 비율로 환산한다.

## 23.1 일반 카드 획득

| 레이어 | 설정 |
|---|---|
| Core Flash | 1 Particle, 80~120 ms, Additive, Scale 0.8→1.3, Alpha 0.8→0 |
| Border Trace | 카드 테두리 Mask, 300~450 ms, Alpha Blend |
| Spark | 8~14개, Lifetime 0.25~0.5 s, 작은 크기 |
| Shine | 대각선 Band, 450~650 ms |
| Card Motion | Scale 0.82→1.06→1.0, 280~420 ms |
| Sound | 짧은 종/종이 넘김, 1 Layer |
| Low 변경 | Spark 5~8개, Border Trace 유지, Bloom 없음 |

타임라인:

```text
0 ms      카드 등장 시작
40 ms     Core Flash
80 ms     Border Trace 시작
120 ms    Spark Burst
180 ms    카드 Overshoot
250 ms    Shine 시작
500 ms    안정화
```

## 23.2 희귀 카드 획득

추가:

- 희귀도 색 Ring 1~2개.
- Spark 12~24개.
- 프레임 Glyph 2~4개.
- Shine 강도 증가.
- 0.1~0.2초 짧은 사운드 레이어 추가.

Low:

- Ring 1개.
- Glyph 제거.
- Spark 10~14개.

## 23.3 영웅 카드 획득

| 단계 | 시간 | 내용 |
|---|---:|---|
| Anticipation | 0~250 ms | 배경 Dim, 카드 실루엣, 저음 상승 |
| Reveal | 250~420 ms | 강한 Core Flash, 프레임 색 노출 |
| Expansion | 420~800 ms | Ring 2개, Ribbon 2~4개, Spark 24~48개 |
| Showcase | 800~1500 ms | Foil 이동, 카드 천천히 흔들림 |
| Settle | 1500~1900 ms | UI 버튼과 설명 표시 |

Low:

- 배경 Dim 유지.
- Ribbon 대신 곡선 Sprite 1개.
- Spark 12~22개.
- Distortion/Light2D 제거.

## 23.4 전설 카드 획득

전설 연출은 화려함보다 **긴장 → 정지 → 공개 → 확인**의 리듬이 중요하다.

### 4단계 구조

1. **잠금 단계**
   - 입력을 일시 제한.
   - 배경 60~75% Dim.
   - 주변 소리 Duck.
   - 카드 Back 또는 실루엣.

2. **축적 단계**
   - 0.5~1.2초.
   - 중심으로 모이는 작은 입자.
   - 프레임 라인이 순차 점등.
   - 저주파 Rumble.

3. **공개 단계**
   - 80~120 ms White/Gold Flash.
   - 카드 Flip 또는 Dissolve Reveal.
   - Ring 2~3개.
   - Spark 36~90개.
   - High에서만 국소 Distortion와 2D Light Pulse.

4. **감상 단계**
   - 1.0~2.0초.
   - Foil/Holographic 이동.
   - 이름·희귀도·효과 문구 순차 표시.
   - 사용자가 눌러 닫기 전까지 장식 파티클은 매우 낮은 Rate로 Loop.

### 절대 금지

- 화면 전체 Flash를 150 ms 이상 유지.
- 텍스트 등장 전에 너무 많은 파티클로 카드를 가림.
- 연속 뽑기에서 매 카드마다 긴 강제 연출.
- Skip 버튼 없이 2초 이상 입력 잠금.
- 저사양에서도 고해상도 화면 Distortion 강제.

### 10회 뽑기 처리

- 최고 희귀도 카드에만 전체 연출.
- 나머지는 150~350 ms 짧은 Reveal.
- 카드 그룹 배치 후 최고 희귀 카드가 한 번 더 Pulse.
- Skip 시 결과 목록은 즉시 정확히 표시.

## 23.5 카드 선택

| 요소 | 값 |
|---|---|
| Lift | 카드 높이의 2~5% |
| Scale | 1.00→1.03~1.06 |
| Outline | 1~3 px 상당, Pulse 1~2 Hz 이하 |
| Border Spark | 2~6개, 선택 시작 1회 |
| Duration | 90~160 ms |
| Loop | Outline/Glow만 약하게 |

선택 상태는 배경색만으로 구분하지 않는다. 위치·외곽선·아이콘을 함께 사용한다.

## 23.6 카드 드래그

- 원위치에 Ghost 30~50% Alpha.
- 드래그 카드는 Sorting 최상위.
- 목표 가능 영역은 부드러운 Pulse.
- 목표 불가 영역은 채도를 낮추고 금지 아이콘.
- 드래그 경로에 Trail을 쓴다면 1개, 짧은 Lifetime 0.12~0.25초.
- 손가락 아래 카드가 완전히 가려지지 않게 Y Offset.
- 취소 복귀는 120~220 ms.

## 23.7 카드 사용

```text
0~80 ms      카드 압축/뒤로 당김
80~180 ms    목표 방향 이동 시작
120 ms       Launch Spark
180~320 ms   타겟 Impact
220~420 ms   카드 소멸/덱 이동
```

- 핵심 판정 프레임과 Impact Flash를 맞춘다.
- 투사체가 있으면 카드 자체 이동과 중복하지 않는다.
- 스킬 종류에 따라 색보다 형태를 먼저 다르게 만든다.

## 23.8 일반 물리 타격

| 레이어 | 설정 |
|---|---|
| Impact Slash | 1~2 Sprite, 80~160 ms, Alpha Blend/Additive |
| Debris | 3~8 Particle, Gravity 약하게 |
| Flash | 대상 Shader HitFlash 60~100 ms |
| Shake | 30~80 ms, 아주 작게 |
| Sound | 공격 종류별 Transient |

Low에서는 Debris 2~4개, Shake 유지, 추가 Glow 제거.

## 23.9 화염 타격

- Core: 노랑/백색, 짧고 밝게.
- Mid: 주황 Flame Flipbook.
- Outer: 적색/갈색 Smoke.
- Ember: 4~12개 위로 상승.
- Burn 지속 상태는 작은 Ember 1~3개/초.
- Smoke가 UI 텍스트를 오래 가리지 않게 0.3~0.8초.
- Low에서 Noise Off, Smoke 개수 절반.

## 23.10 냉기 타격

- 청백색 Impact Star.
- 결정 조각 4~10개.
- 얇은 Frost Overlay가 가장자리에서 안쪽으로 150~350 ms.
- Freeze 완료 시 작은 Crack line.
- 해제 시 조각이 바깥으로 떨어짐.
- 투명 블루 Sprite 여러 겹으로 화면을 덮지 않는다.

## 23.11 번개 타격

- Bolt는 1~3갈래.
- 40~100 ms 강한 노출.
- 1~2회 작은 Flicker.
- 대상 Flash 60 ms.
- Ground Arc는 High에만.
- 실시간 LineRenderer 세그먼트는 필요한 수만 유지.
- 매 프레임 새 배열 생성 금지.

## 23.12 독 상태

- 녹색만 쓰지 말고 Bubble/Drop 형태 사용.
- 작은 Bubble 1~3개/초.
- 카드 프레임에 유기적 흐름 Mask.
- Tick마다 작은 Pop.
- 텍스트와 아이콘으로 상태 명시.
- 저사양에서 Noise 대신 스크롤 텍스처.

## 23.13 출혈 상태

- 짧은 적색 Slash mark.
- 아래로 떨어지는 2~5 Drop.
- 지속 상태는 프레임의 Pulse가 아니라 아이콘 강조.
- 과도한 사실적 피 표현은 등급·지역 정책을 고려.

## 23.14 회복

- 아래에서 위로 이동하는 Plus/Glyph.
- 녹색 또는 프로젝트 회복색.
- 중심 Light Ray 1개.
- 숫자는 VFX보다 먼저 읽히게.
- 다중 회복은 각 대상마다 작은 연출, 전체 화면 Flash 금지.

## 23.15 보호막

- 120~220 ms에 Shield Arc 생성.
- 피격 순간 Ripple 1회.
- 남은 보호막은 얇은 Loop 테두리 또는 아이콘.
- 파괴 시 균열 → 조각 4~12개 → Fade.
- 방어막 Alpha가 카드 원화를 가리지 않게 0.08~0.22 범위부터 시작.

## 23.16 버프

- 위로 향하는 Arrow/Glyph.
- 따뜻한/밝은 색.
- 프레임 Pulse 1회.
- 수치 변화 텍스트.
- 지속 Loop는 아이콘에 집중.

## 23.17 디버프

- 아래로 향하는 Arrow/Glyph.
- 색뿐 아니라 하강 모션.
- 카드 채도 약간 감소.
- 120~240 ms의 짧은 Dark vignette를 카드 내부에만.

## 23.18 카드 강화

```text
재료 카드/아이콘 흡수
→ 중심 축적
→ 프레임 Line 점등
→ 등급/수치 상승 순간 Burst
→ 새 프레임 안정화
```

- 흡수 아이콘은 실제 재료 수만큼 만들지 않는다.
- 3~7개 대표 입자로 요약.
- 강화 성공과 실패의 형태·색·사운드를 명확히 다르게.
- 성공은 위/밖으로 확장, 실패는 안/아래로 축소.

## 23.19 카드 합성

- 두 카드가 중심으로 이동.
- 중간에 회전/압축.
- Silhouette를 잠시 감춤.
- 합성 카드가 밖으로 팽창.
- 재료 카드 삭제 판정은 연출보다 먼저 안전하게 완료.
- Skip 시 결과 상태가 즉시 일관돼야 한다.

## 23.20 카드 파괴

세 가지 스타일:

### Ash Dissolve

- Noise Dissolve 아래→위 또는 가장자리→중심.
- Edge Emission.
- 작은 Ash Particle 8~24개.

### Shatter

- 4~12 조각 Sprite.
- 짧은 바깥 방향 속도.
- 실제 동적 Mesh 분할보다 미리 제작한 조각 프리팹 권장.

### Void Collapse

- 카드 Scale 축소.
- 중심으로 흐르는 UV.
- 작은 Ring 수축.
- High에서만 국소 Distortion.

## 23.21 상자 열기

| 단계 | 시간 | 내용 |
|---|---:|---|
| Idle | 반복 | 약한 빛 누출, 1~3 Spark/초 |
| Tap | 0~120 ms | 압축, Click |
| Charge | 120~600 ms | 틈새 밝아짐, 흔들림 |
| Open | 600~850 ms | 뚜껑, Core Flash, Ring |
| Reward | 800~1500 ms | 아이템/카드 등장, Confetti |
| Settle | 이후 | 결과 UI |

Low에서 실시간 Light와 Distortion 제거. 틈새 Glow Sprite로 대체.

## 23.22 업적 완료

- 배지 Scale 0.6→1.12→1.0.
- Ring 1개.
- Confetti 8~20개.
- 제목 Shine.
- 화면 가장자리에 2초 이상 붙는 알림은 Loop Particle 금지.

## 23.23 스테이지 클리어

- 승리 텍스트 전에 200~400 ms 공간 확보.
- 큰 Burst는 1회.
- 이후 작은 Confetti는 1~2초.
- 결과 수치가 나타날 때 파티클 밀도를 낮춘다.
- Low에서 Confetti 12~24개, High 30~60개.

---

---

<a id="section-24"></a>
# 24. Shader Graph 제작 표준

## 24.1 하나의 거대 Master Graph를 만들지 않는다

모든 기능을 하나의 Shader Graph에 넣고 Boolean으로 켜고 끄는 방식은 처음에는 편해 보이지만 다음 문제가 생긴다.

- Keyword 조합이 늘어 Shader Variant가 폭증한다.
- 동적 Branch 때문에 비활성 기능도 비용이 남을 수 있다.
- 카드, UI, Particle의 Blend와 Stencil 요구가 서로 다르다.
- 그래프가 커져 컴파일·열기·수정 시간이 길어진다.
- 한 기능 수정이 모든 VFX를 깨뜨릴 수 있다.

권장 Shader Family:

```text
Shaders/VFX/
├─ Card/
│  ├─ SG_Card_Unlit.shadergraph
│  ├─ SG_Card_Lit.shadergraph
│  ├─ SG_Card_FoilOverlay.shadergraph
│  ├─ SG_Card_DissolveOverlay.shadergraph
│  └─ SG_Card_StatusOverlay.shadergraph
├─ Particle/
│  ├─ SG_Particle_Additive.shadergraph
│  ├─ SG_Particle_Alpha.shadergraph
│  └─ SG_Particle_Masked.shadergraph
├─ UI/
│  ├─ SG_UI_Shine.shadergraph
│  ├─ SG_UI_RadialPulse.shadergraph
│  └─ SG_UI_Dissolve.shadergraph
├─ Fullscreen/
│  ├─ SG_Fullscreen_Flash.shadergraph
│  └─ SG_Fullscreen_Vignette.shadergraph
└─ SubGraphs/
   ├─ SDF_RoundedRectangle.shadersubgraph
   ├─ SDF_Ring.shadersubgraph
   ├─ UV_Rotate.shadersubgraph
   ├─ UV_Polar.shadersubgraph
   ├─ Mask_DissolveEdge.shadersubgraph
   ├─ Color_Desaturate.shadersubgraph
   └─ Color_SafeAdditive.shadersubgraph
```

## 24.2 Graph별 역할

| Graph | Target/Sub Target | Blend | 용도 |
|---|---|---|---|
| Card Unlit | Universal / Sprite Unlit | Alpha | 조명 영향 없는 기본 카드 |
| Card Lit | Universal / Sprite Lit | Alpha | Light2D와 Normal Map 반응 카드 |
| Foil Overlay | Sprite Unlit 또는 UI | Additive/Alpha | 희귀도 홀로그램 레이어 |
| Particle Additive | Universal Unlit | Additive | Spark, Magic, Glow |
| Particle Alpha | Universal Unlit | Alpha/Premultiply | Smoke, Cloud, Ghost |
| Particle Masked | Universal Unlit | Alpha Clip | 날카로운 Glyph/Shard |
| UI Shine | Universal / UI Target | Alpha/Additive | 버튼·카드 UI Shine |
| Fullscreen | Fullscreen Shader Graph | 프로젝트별 | 매우 제한된 화면 효과 |

UI Target과 Sprite Target을 같은 Graph에서 억지로 해결하지 않는다. Stencil, Canvas clipping, SpriteRenderer batch 요구가 다르기 때문이다.

## 24.3 공통 Property 명명

| Property | 타입 | 기본값 | 설명 |
|---|---|---:|---|
| `_BaseMap` | Texture2D | White | 주 텍스처 |
| `_BaseTint` | Color | White | 기본 색 |
| `_Opacity` | Float | 1 | 전체 알파 |
| `_MaskMap` | Texture2D | White | Packed mask |
| `_NoiseMap` | Texture2D | Gray | Dissolve/Distortion Noise |
| `_EffectColor` | HDR Color | White | 효과 색 |
| `_EffectIntensity` | Float | 1 | 효과 강도 |
| `_Progress` | Range 0~1 | 0 | 범용 진행 값 |
| `_Dissolve` | Range 0~1 | 0 | Dissolve 진행 |
| `_DissolveWidth` | Range | 0.05 | Edge 폭 |
| `_DissolveEdgeColor` | HDR Color | White | Edge 색 |
| `_ShineProgress` | Float | -1 | Shine 위치 |
| `_ShineWidth` | Range | 0.12 | Shine 폭 |
| `_ShineSoftness` | Range | 0.05 | Shine Feather |
| `_Selection` | Range 0~1 | 0 | 선택 강조 |
| `_HitFlash` | Range 0~1 | 0 | 피격 Flash |
| `_GrayAmount` | Range 0~1 | 0 | Grayscale |
| `_FoilStrength` | Range | 0 | Foil 강도 |
| `_Rarity` | Range 0~1 | 0 | 희귀도 정규화 값 |
| `_Seed` | Float | 0 | 카드별 패턴 Offset |
| `_TimeScale` | Float | 1 | Shader 애니메이션 배율 |

Property 이름은 코드의 `Shader.PropertyToID`와 정확히 맞춘다.

## 24.4 Packed Mask 규격

카드 전용 Mask Texture를 다음처럼 패킹한다.

| 채널 | 의미 |
|---|---|
| R | 카드 프레임/테두리 |
| G | Foil이 적용될 원화 영역 |
| B | Emission 또는 희귀도 문양 |
| A | Dissolve 방향/보호 영역 또는 별도 Opacity |

프로젝트에 따라 A를 다른 용도로 쓰되, 같은 Shader Family에서는 의미를 고정한다.

장점:

- 네 장의 마스크를 한 텍스처로 줄임.
- Texture Sample 수 감소.
- 카드 아트와 효과 영역을 아트팀이 명확히 통제.

주의:

- 채널별 압축 Artifact 확인.
- Mask Texture는 일반적으로 sRGB를 끈다.
- 알파가 중요한 경우 압축 포맷을 실제 Android에서 확인.
- Mask가 카드별로 모두 다르면 Atlas/메모리 비용을 함께 계산.

## 24.5 UV Rotate Sub Graph

입력:

- `UV: Vector2`
- `Center: Vector2 = (0.5, 0.5)`
- `AngleRadians: Float`

계산:

```text
P = UV - Center
S = sin(Angle)
C = cos(Angle)
Rotated.x = P.x * C - P.y * S
Rotated.y = P.x * S + P.y * C
Output = Rotated + Center
```

Shader Graph 노드:

1. Subtract `UV - Center`.
2. Sine와 Cosine에 Angle 입력.
3. Split P.
4. Multiply/Add 조합으로 2×2 회전.
5. Combine.
6. Add Center.

`Rotate` 노드가 있더라도 Sub Graph로 감싸 그래프 전체의 각도 단위와 중심 규칙을 통일한다.

## 24.6 카드 Shine — 노드 단위

목표: 카드 위를 대각선 밝은 띠가 한 번 지나간다.

입력:

- `UV`
- `_ShineProgress`: -0.3~1.3
- `_ShineWidth`: 0.05~0.3
- `_ShineSoftness`: 0.01~0.2
- `_ShineAngle`: Radians 또는 Degree 통일
- `_EffectColor`
- 카드 Shape/Frame Mask

계산 개념:

```text
RUV = Rotate(UV, Center, Angle)
Distance = abs(RUV.x - ShineProgress)
Band = 1 - smoothstep(Width, Width + Softness, Distance)
Band *= ShapeMask
FinalRGB = BaseRGB + EffectColor.rgb * Band * EffectColor.a
```

권장:

- Shine는 프레임/원화 Mask에만 적용하고 투명 영역에는 적용하지 않는다.
- `ShineProgress`가 범위 밖일 때 Band가 0이 되게 한다.
- 한 카드에서 계속 Loop하지 않는다. 획득·선택·강화 순간에만.
- 카드 리스트에서 모든 카드가 같은 타이밍으로 Shine하지 않게 `_Seed` Offset.
- Low에서 Texture Noise로 Shine 표면을 깨지 않는다.

변형:

- 두 개의 Band를 약간 떨어뜨리면 프리즘 느낌.
- `Screen Position` 또는 카드 Tilt 값을 더하면 반응형 Foil.
- 노이즈를 곱할 때 한 번의 Sample로 충분히 표현.

## 24.7 Dissolve — 노드 단위

입력:

- `_NoiseMap`
- `_Dissolve`
- `_DissolveWidth`
- `_DissolveEdgeColor`
- Direction Gradient 선택

계산:

```text
Noise = Sample(NoiseMap, UV * Tiling + Offset).r
Field = lerp(Noise, Noise * 0.7 + DirectionGradient * 0.3, DirectionAmount)
Visible = step(Dissolve, Field)
EdgeA = smoothstep(Dissolve, Dissolve + Width, Field)
EdgeB = smoothstep(Dissolve + Width, Dissolve + Width * 2, Field)
Edge = saturate(EdgeA - EdgeB)
Alpha = BaseAlpha * Visible
RGB = BaseRGB + EdgeColor.rgb * Edge * EdgeColor.a
```

Alpha Clip을 사용할 때:

- Threshold 주변이 날카롭고 Overdraw를 일부 줄일 수 있음.
- 작은 입자/카드 가장자리의 aliasing 확인.
- MSAA가 약한 모바일에서 거친 테두리가 보일 수 있음.

Alpha Blend를 사용할 때:

- 부드럽지만 투명 영역의 Overdraw가 남음.
- 카드 전체 Fade와 섞기 쉬움.

Edge 폭은 해상도와 카드 크기에 따라 체감이 달라지므로 실제 해상도에서 조정한다.

## 24.8 홀로그램/Foil — 저비용 방식

2D 카드에서 실제 PBR 간섭막을 재현할 필요는 없다. 다음 조합이면 충분하다.

```text
PatternUV = UV * Tiling
Phase = PatternUV.x * A + PatternUV.y * B
Phase += _Seed
Phase += CardTiltX * TiltInfluence
Rainbow = CosinePalette(Phase)
FoilMask = MaskMap.g
Shimmer = smoothstep(... scrolling noise or band ...)
FoilRGB = Rainbow * FoilMask * Shimmer * _FoilStrength
Final = Base + FoilRGB
```

Cosine Palette 예:

```text
Color = A + B * cos(2π * (C * t + D))
```

모바일 규칙:

- Sin/Cos 여러 번보다 작은 Gradient Texture 1회 Sample이 더 예측 가능할 수 있다.
- Low는 3색 Gradient Texture + UV Scroll.
- Medium은 Gradient + 1 Noise Sample.
- High는 Tilt 반응 + 두 Mask 조합.
- 화면 좌표만 사용하면 카드가 움직이지 않아도 패턴이 화면에 고정되어 어색할 수 있다.
- 카드 Local UV와 Tilt 입력을 중심으로 하고 Screen Position은 약하게만 섞는다.

## 24.9 카드 Outline

우선순위:

1. 별도 Frame/Outline Sprite.
2. 사전 제작 Mask에서 Border 채널 사용.
3. SDF 기반 Outline.
4. Texture 주변 다중 샘플.

주변 샘플 방식:

```text
A0 = Alpha(UV)
A1 = Alpha(UV + (texel.x, 0))
A2 = Alpha(UV + (-texel.x, 0))
A3 = Alpha(UV + (0, texel.y))
A4 = Alpha(UV + (0, -texel.y))
Outline = max(A1,A2,A3,A4) - A0
```

- 4 Tap은 저렴하지만 대각선이 약함.
- 8 Tap은 더 매끄럽지만 Sample 수 증가.
- 카드가 이미 사각 프레임이면 Texture Sample Outline보다 Mask/Sprite가 훨씬 효율적.

## 24.10 Rounded Rectangle SDF

중심 기준 좌표:

```text
P = abs(UV - 0.5) - HalfSize + Radius
D = length(max(P, 0)) + min(max(P.x, P.y), 0) - Radius
```

활용:

- 카드 선택 외곽선.
- UI Highlight.
- Masked Shine.
- Tutorial 구멍.

Ring:

```text
Outer = 1 - smoothstep(0, Feather, D)
Inner = 1 - smoothstep(0, Feather, D + Thickness)
Ring = saturate(Outer - Inner)
```

Aspect Ratio가 다른 Rect에서 왜곡되지 않도록 UV에 Rect 크기 비율을 반영한다.

## 24.11 Radial Pulse/Ring

```text
Centered = UV - 0.5
Distance = length(Centered)
RingDistance = abs(Distance - Radius)
Ring = 1 - smoothstep(Width, Width + Softness, RingDistance)
```

- 시작 Radius 0.05, 종료 0.7~1.2.
- Alpha는 초기 1 → 종료 0.
- 카드 프레임 뒤에 배치.
- 큰 화면 전체 Ring 여러 장 중첩 금지.

## 24.12 Hit Flash

가장 단순하고 읽기 좋은 방식:

```text
Luma = dot(BaseRGB, float3(0.299, 0.587, 0.114))
FlashColor = lerp(BaseRGB, _EffectColor.rgb, _HitFlash)
```

변형:

- 강한 타격: White → EffectColor → Base.
- 독: 색 Flash보다 짧은 밝기 + 독 형태 Overlay.
- 방어: 청색/백색 Flash + Ripple.

`_HitFlash`를 1로 오래 유지하지 않는다. 보통 50~120 ms.

## 24.13 Grayscale/Disabled

```text
Luma = dot(BaseRGB, float3(0.299, 0.587, 0.114))
Gray = float3(Luma, Luma, Luma)
Result = lerp(BaseRGB, Gray, _GrayAmount)
```

비활성 카드는 Grayscale만으로 끝내지 않는다.

- Alpha 약간 감소.
- Lock/Cost 부족 아이콘.
- 텍스트 명도 조정.
- 상호작용 불가 Cursor/Touch 피드백.

## 24.14 상태 이상 Overlay

하나의 Graph에 모든 상태를 넣기보다 공통 Overlay Graph에 상태별 Texture/Color/Scroll 속도를 전달한다.

| 상태 | 패턴 | 이동 |
|---|---|---|
| Burn | 아래→위 Flame/Ember | 위로 |
| Freeze | 가장자리 Frost/Crack | 가장자리→중심 |
| Poison | Bubble/Drop | 천천히 위로 |
| Shock | Zigzag/Bolt | 짧은 Flicker |
| Curse | Rune/Smoke | 회전/안쪽 |
| Silence | Broken waveform/Glyph | 거의 정적 |

카드 본문 텍스트와 수치 영역은 Mask에서 제외한다.

## 24.15 Particle Shader Graph 공통 입력

ParticleSystem Renderer의 Custom Vertex Streams를 통해 전달할 값 예:

```text
POSITION
COLOR
TEXCOORD0.xy       Base UV
TEXCOORD0.zw       Flipbook blend 또는 보조 값
TEXCOORD1          Custom1
TEXCOORD2          Custom2
```

실제 Stream 목록은 Shader와 ParticleSystem Renderer 설정을 일치시킨다. NovaShader가 경고와 `Fix Now`를 제공하는 경우 이를 활용한다.

Custom Data 예:

- `Custom1.x`: Dissolve 진행.
- `Custom1.y`: Emission 강도.
- `Custom1.z`: UV 회전 Offset.
- `Custom1.w`: Distortion 강도.
- `Custom2.x`: 개별 Seed.
- `Custom2.y`: Edge Width.

하나의 ParticleSystem에서 Particle마다 다른 값을 전달할 때 유용하다.

## 24.16 Alpha, Premultiply, Additive

### Straight Alpha

```text
Src = SrcAlpha
Dst = OneMinusSrcAlpha
```

- 일반 UI/Smoke.
- 텍스처 가장자리 색 Bleeding에 주의.

### Premultiplied Alpha

```text
Src = One
Dst = OneMinusSrcAlpha
```

- 부드러운 Glow/Smoke 가장자리.
- 출력 RGB를 Alpha와 일관되게 준비.

### Additive

```text
Src = One 또는 SrcAlpha
Dst = One
```

- 빛/Spark.
- 검은 배경에서 예쁘다고 남용하면 밝은 배경에서 사라짐.
- 카드 원화를 태울 정도로 HDR 강도를 높이지 않는다.

## 24.17 Precision

- UV, Color, Mask, 일반 연산은 `Half`를 기본으로 검토.
- 큰 World Position, 깊은 누적, 매우 정밀한 SDF는 `Float` 필요성을 확인.
- Half/Float 변경 전후 실제 Android GPU에서 Artifact 확인.
- 그래프의 모든 노드를 무조건 Half로 바꾸는 자동 규칙은 금지.

## 24.18 Branch와 Keyword

우선순위:

1. 다른 Material/Graph로 분리.
2. 빌드 시 고정되는 Static Keyword.
3. 꼭 필요한 소수의 Local Keyword.
4. 동적 Branch는 마지막 수단.

금지:

- `_USE_NOISE`, `_USE_DISSOLVE`, `_USE_DISTORTION`, `_USE_FLOW`, `_USE_FOIL`, `_USE_LIGHT`, `_USE_MASK`, `_USE_RIM`을 모두 한 Graph에 조합.
- 수백 개 Material이 서로 다른 Keyword 조합을 갖게 함.
- 사용하지 않는 변형을 빌드에 전부 포함.

## 24.19 Shader Variant 관리

1. Android Development Build로 실제 플레이 경로를 순회한다.
2. 주요 VFX를 한 번씩 재생하는 `VFX_Gallery` 씬을 만든다.
3. 첫 재생 Hitch를 측정한다.
4. 필요한 Variant만 Warm-up 후보로 모은다.
5. 항상 포함 Shader 목록을 최소화한다.
6. 빌드 로그에서 Variant 수와 Stripping 결과를 확인한다.
7. 새 Keyword를 추가한 PR은 Variant 증가량을 기록한다.
8. UIEffect/NovaShader의 제공 최적화·Variant 도구를 검토한다.

모든 Material 변형을 무작정 ShaderVariantCollection에 넣으면 앱 시작 시간과 메모리가 증가할 수 있다.

## 24.20 2D Renderer와 Renderer Feature

- URP 2D Renderer는 일반 Forward Renderer 예제와 구조가 다를 수 있다.
- 문서가 `UniversalRendererData` 또는 `ForwardRendererData`에 Feature를 넣으라고 해도 현재 2D Renderer에서 동일하게 작동한다고 가정하지 않는다.
- Distortion, Blit, Opaque Texture 샘플링은 별도 검증 씬에서 확인한다.
- Renderer Feature 하나 때문에 카메라 중간 Texture나 추가 Pass가 생기는지 Frame Debugger로 확인한다.
- 카드 몇 장의 왜곡을 위해 전체 화면 Color Copy를 활성화하지 않는다.
- 가능한 경우 카드 내부 UV 왜곡으로 대체한다.

## 24.21 NovaShader 사용 범위

NovaShader를 우선 검토할 효과:

- Flipbook 화염/연기.
- Flow Map 에너지/액체.
- Dissolve Particle.
- Animated Tint.
- Emission/Spark.
- Custom Data 기반 Particle별 변화.
- Soft Particle 또는 Depth Fade가 실제로 필요한 월드 효과.

직접 Shader Graph가 더 나은 경우:

- 카드 프레임 전용 Mask.
- UI Stencil/Canvas clipping 중심 효과.
- 프로젝트 고유의 Foil/희귀도 표현.
- 간단한 한두 Sample Sprite Overlay.

NovaShader의 모든 기능을 한 Material에서 켜는 것이 목표가 아니다. 필요한 기능만 활성화하고 최적화 Material/Shader 생성 기능을 사용한다.

---

<a id="section-25"></a>
# 25. Particle System 제작 표준

## 25.1 효과 레이어 템플릿

```text
VFX_<Category>_<Name>_<Size>
├─ PS_Core
├─ PS_Shape
├─ PS_Detail
├─ PS_Spark
├─ PS_Smoke
├─ PS_Trail
├─ SR_Ring
├─ SR_Glow
├─ Light2D_Pulse
└─ VfxTierGate_*
```

모든 레이어를 반드시 넣는 것이 아니다. 보통 3~6개면 충분하다.

### 레이어 의미

- `Core`: 최초 50~150 ms의 핵심 밝기.
- `Shape`: 공격/보상의 의미를 전달하는 주 형태.
- `Detail`: 조각, Rune, 작은 보조 모양.
- `Spark`: 방향과 에너지.
- `Smoke`: 잔향과 부피.
- `Trail`: 이동 경로.
- `Ring`: 공간 확장/도착 강조.
- `Glow`: 값싼 가짜 광.
- `Light2D`: 특별한 순간만.

## 25.2 Main Module 기본

| 항목 | 일반 One-shot 출발값 |
|---|---|
| Duration | 0.2~1.2 s |
| Looping | Off |
| Start Lifetime | 0.15~0.8 s |
| Start Speed | 효과 크기에 맞게 0~5 |
| Start Size | Sprite 기준 0.05~1.0 |
| Start Rotation | Random 0~360° |
| Simulation Space | Local 또는 World를 의도대로 고정 |
| Scaling Mode | Hierarchy/Local 중 프리팹 규칙 통일 |
| Play On Awake | 풀 시스템 정책에 따라 On 가능, Service가 Stop/Clear 후 재생 |
| Max Particles | 실제 Burst의 1.2~2배 정도 |
| Stop Action | 풀 시스템이 직접 감지하면 None |
| Culling Mode | Loop/일시 효과 특성에 맞게 검증 |

`Max Particles=1000` 같은 기본값을 그대로 두지 않는다. 실제 최대치를 명시한다.

## 25.3 Emission

One-shot:

- Rate over Time보다 Burst 중심.
- Burst 1~2회.
- 1회 Burst count가 품질 등급에 따라 명확.

Loop:

- Rate over Time을 최소화.
- 화면에 보이는 개수 기준으로 계산.
- 카드 Aura는 1~6 particles/sec부터 시작.

동일 프레임 Burst가 많은 경우 CPU와 Overdraw가 동시에 튈 수 있다. 여러 연출을 10~40 ms 분산하는 것도 고려한다.

## 25.4 Shape

| 효과 | Shape 추천 |
|---|---|
| 폭발 | Circle/Sphere 작은 반경 |
| Slash Spark | Edge/Line 또는 좁은 Cone |
| Aura | Circle |
| 획득 집중 | Circle 바깥→안쪽, 속도 반전/애니메이션 |
| 비/눈 | Box |
| 카드 테두리 | 직접 Texture/Path 또는 4개 Edge emitter |
| HUD 도착 | Point/Circle 작은 반경 |

Mesh Shape는 편리하지만 복잡한 Mesh와 Skinned Mesh는 모바일 비용을 측정한다.

## 25.5 Velocity over Lifetime

- 기본 이동 방향을 Main Start Speed와 분담.
- 곡선이 너무 복잡하면 디자이너 유지보수가 어려움.
- 보상 Spark는 초기 Burst + 약한 위쪽 가속.
- Smoke는 느린 위쪽 속도 + 약한 랜덤.
- 카드 Border Particle은 Path를 흉내내려 여러 시스템을 쓰기보다 Shader Trace를 우선.

## 25.6 Limit Velocity over Lifetime

- Smoke/Ember의 속도를 부드럽게 정리할 때 유용.
- Dampen을 너무 높이면 모든 입자가 같은 속도로 보임.
- 성능 문제의 첫 번째 원인은 아니지만 불필요한 모듈은 끈다.

## 25.7 Inherit Velocity

투사체/카드 이동에서 자식 Particle이 이동 속도를 이어받아야 할 때 사용한다.

- 초기 이동감을 주는 `Initial` 모드.
- 지속적으로 따라가는 `Current` 모드는 의도 확인.
- UI 보상 아이콘에 붙은 Spark는 부모 이동과 화면 좌표 변환을 확인.

## 25.8 Force over Lifetime

- 바람, 중력 비슷한 곡선.
- 작은 Particle의 방향성 강화.
- 매 Particle 물리 Collision 대신 값싼 Force로 충분한 경우가 많다.

## 25.9 Color over Lifetime

일반 밝은 입자:

```text
0.00  Alpha 0
0.05  Alpha 1
0.55  Alpha 0.8
1.00  Alpha 0
```

불꽃:

```text
White/Yellow → Orange → Red/Brown → Transparent
```

Smoke:

```text
Dark Gray low alpha → Gray → Transparent
```

- 알파가 1인 큰 Smoke를 겹치지 않는다.
- Additive 입자는 Alpha와 HDR 색 강도를 함께 관리.

## 25.10 Size over Lifetime

Impact Flash:

```text
0.00  0.2
0.10  1.0
1.00  1.2
```

Smoke:

```text
0.00  0.4
1.00  1.3
```

Spark:

```text
0.00  1.0
0.60  0.8
1.00  0.0
```

X/Y Separate Axes로 Slash를 늘릴 수 있지만 텍스처 형태와 Stretch Renderer 중 더 적합한 쪽을 선택한다.

## 25.11 Rotation over Lifetime

- Smoke/Glow의 반복감을 줄이는 소량 회전.
- 빠른 회전은 작은 Sprite에서 Flicker 유발.
- 회전이 필요 없는 Spark에 습관적으로 넣지 않는다.

## 25.12 Noise

Noise는 비싸고 시각적 영향도 크므로 레이어별로 제한한다.

Low:

- 기본 Off.
- Smoke 핵심 레이어 하나에만 약하게 허용.

Medium:

- 1~2개 레이어.
- 낮은 Frequency.
- Scroll Speed 최소.

High:

- 필요한 레이어에만.
- 품질을 올리기 위해 Strength만 키우지 않는다.

대체:

- 미리 구운 Flipbook.
- UV Scroll Noise.
- Start Velocity Random.
- 두 개의 Smoke 시스템을 다른 방향으로.

## 25.13 Collision

기본 Off.

허용 후보:

- 바닥에 튀는 큰 조각 2~8개.
- 중요한 보스 잔해.
- 플레이어가 충돌 반응을 실제로 인지할 수 있는 효과.

대체:

- 미리 정한 포물선.
- Lifetime 후반 Y 속도 감소.
- 바닥 위치에서 Secondary Burst를 별도로 재생.

Collision 품질, Radius Scale, Layer Mask를 최소화한다.

## 25.14 Trigger

Trigger는 게임플레이 판정에 쓰지 않는다.

- Particle은 시각 표현이다.
- 데미지/획득 판정은 게임 시스템에서 처리.
- Trigger를 쓰더라도 장식적 반응에 제한.

## 25.15 Sub Emitters

허용:

- 큰 입자 1개가 폭발할 때 Spark 4~8개.
- Shield 조각이 사라질 때 작은 Dust.

주의:

- Birth/Collision/Death를 여러 단계 연결하면 수량이 기하급수적으로 늘 수 있음.
- 총 최악 수량을 계산.
- 2단계 이상 연결 금지에 가깝게 운영.
- Low에서는 Sub Emitter 자식을 끈다.

## 25.16 Texture Sheet Animation

- 4×4 = 16 frames: 일반 모바일 효과의 우선값.
- 8×8 = 64 frames: 큰 보스/전설 효과에 제한.
- FPS는 Lifetime과 Frame 수로 결정.
- Loop가 아닌 폭발은 Whole Sheet 1회.
- Frame over Time 곡선으로 끝 프레임 Hold 가능.
- Frame Blending은 품질 개선과 비용을 실제 기기에서 비교.

예:

```text
16 frames / 0.4 sec = 40 fps 상당
16 frames / 0.8 sec = 20 fps 상당
```

너무 빠르면 디테일을 볼 수 없고 Texture 비용만 낭비한다.

## 25.17 Lights Module

Particle Lights는 기본 금지에 가깝다.

- 하나의 큰 핵심 Particle이 Light를 만들게 하지 말고, 별도의 Light2D Pulse를 제어하는 방식 선호.
- `Ratio`를 매우 낮게.
- Range와 영향 Layer를 제한.
- Low에서는 제거.

## 25.18 Trails

- 핵심 Projectile, Slash, Ribbon에 사용.
- 카드 Border 장식에 여러 Trail 금지.
- Lifetime 0.08~0.35초부터 시작.
- Width Curve의 끝을 0으로.
- Min Vertex Distance를 너무 작게 설정하지 않는다.
- Trail Material을 명시.
- 풀 반환 시 반드시 Clear.

## 25.19 Custom Data

다음 경우에 사용:

- Particle마다 Dissolve 시작점이 다름.
- 한 시스템에서 여러 희귀도 색 변형.
- Ribbon의 폭/강도 변화.
- Shader 애니메이션 Phase 분산.

Custom Data를 사용했다면 프리팹 Inspector에 채널 의미를 주석 컴포넌트나 문서로 기록한다.

## 25.20 Renderer

일반 2D:

- Billboard 또는 Stretched Billboard.
- Sorting Layer/Order 명시.
- Cast Shadows Off.
- Receive Shadows Off.
- Motion Vectors Off 또는 파이프라인 기본에서 불필요 기능 비활성.
- Material이 Null이 아닌지 검사.
- Mesh Particle은 정점 수를 확인.

Stretched Billboard:

- Spark/Projectile Trail에 유용.
- Camera Scale과 속도 Scale을 조정.
- 너무 길면 큰 투명 Quad가 되어 Overdraw 증가.

## 25.21 Simulation Space

### Local

- 카드와 함께 이동하는 Aura.
- UI 요소에 붙은 장식.
- 부모 Scale/회전 영향 확인.

### World

- 카드가 이동해도 뒤에 남는 Spark.
- 투사체 Trail 잔상.
- 전장 폭발.

### Custom

- 특정 전장 Root를 기준으로 해야 할 때.
- 씬 이동/부모 Scale 문제를 피할 수 있음.

풀에서 다른 부모로 옮길 때 Simulation Space가 의도대로 유지되는지 반드시 확인한다.

## 25.22 Stop/Clear 재사용 체크

재생 전:

```text
StopEmittingAndClear
TrailRenderer.Clear
Transform reset
Material/Property reset
Random seed set
Animator Rebind
GameObject Active
ParticleSystem.Play
```

반환 시:

```text
Stop/Clear
Trail Clear
Loop Handle clear
Callbacks clear
Parent = pool root
Position/Rotation/Scale reset
GameObject inactive
```

---

<a id="section-26"></a>
# 26. VFX 텍스처·Flipbook 제작 파이프라인

## 26.1 소스 제작 원칙

- 색이 아니라 형태와 Alpha부터 읽히게 만든다.
- 흑백 Mask 소스를 보존한다.
- 색상은 Shader Tint로 바꿀 수 있게 회색/백색 중심으로 제작.
- Glow는 텍스처 안에 너무 크게 구워 넣지 않는다.
- 가장자리 Padding을 확보해 Atlas Bleeding 방지.
- 원본 PSD/Krita/Aseprite/Blender/Houdini 파일과 Export PNG를 분리.

## 26.2 권장 소스 폴더

```text
ArtSource/VFX/
├─ Particles/
├─ Flipbooks/
├─ Masks/
├─ Noise/
├─ Card/
├─ UI/
└─ Exports/

Assets/Game/VFX/Textures/
├─ Particle/
├─ Flipbook/
├─ Mask/
├─ Noise/
├─ Card/
└─ UI/
```

Unity 프로젝트에 대형 원본 작업 파일을 무조건 넣지 않는다. 팀 정책에 따라 별도 LFS/아트 저장소를 사용한다.

## 26.3 텍스처 유형별 권장

| 유형 | 크기 출발점 | sRGB | Mipmap | Wrap |
|---|---:|---|---|---|
| 작은 Spark | 64~128 | On | 상황별 | Clamp |
| Smoke | 128~256 | On | 월드 On 검토 | Clamp |
| Noise | 128~256 | Off | Off/On 검토 | Repeat |
| Mask | 128~512 | Off | 대개 Off | Clamp |
| Card Foil Pattern | 256~512 | 색이면 On | Off | Repeat |
| 4×4 Flipbook | 512~1024 | On | 월드 On 검토 | Clamp |
| UI Shine Mask | 128~256 | Off | Off | Clamp |
| Fullscreen Noise | 256~512 | Off | Off | Repeat |

UI는 화면상 크기가 고정이고 픽셀 정렬이 중요해 Mipmap이 필요 없는 경우가 많다. 월드 Particle은 축소 시 shimmering 완화에 Mipmap이 도움이 될 수 있다.

## 26.4 Alpha 가장자리

Straight Alpha 텍스처는 투명 픽셀 RGB가 검정이면 필터링 시 검은 테두리가 생길 수 있다.

해결:

- 투명 영역 RGB를 가장자리 색으로 확장.
- 아트 Export의 Alpha Bleed/Color Dilate 사용.
- Premultiplied Alpha Material 검토.
- Sprite Atlas Padding 확보.
- Android 압축 후 가장자리 재검사.

## 26.5 Flipbook 제작

### 4×4 표준

- 16 Frame.
- 폭발, Smoke Puff, Magic Impact.
- 512×512 또는 1024×1024.
- 각 셀에 2~8 px 여백.
- 모든 Frame의 중심이 흔들리지 않게 Anchor 고정.

### 8×8 제한 사용

- 64 Frame.
- 큰 화면 점유 효과.
- 보스/전설 연출.
- 메모리와 Sampling 비용 때문에 동시 다수 사용 금지.

### Frame 정리

- 거의 같은 Frame을 유지하려고 무조건 64장을 쓰지 않는다.
- 시작/Impact/Decay 형태 변화가 명확해야 한다.
- 마지막 Frame Alpha가 완전히 0인지 확인.
- Additive 텍스처의 배경이 진짜 검정인지 확인.

## 26.6 Noise 제작

Noise 종류:

- Cloud Noise: Smoke/Dissolve.
- Cellular/Voronoi: Magic/Crystal.
- Directional Flow: Energy/Water.
- Grain: Holographic breakup.

동일 Noise Texture를 여러 이펙트가 공유하도록 라이브러리화한다.

```text
T_Noise_Cloud_01
T_Noise_Cell_01
T_Noise_Flow_Arc_01
T_Noise_Grain_01
```

Noise마다 새 Texture를 만들면 메모리와 유지보수 비용이 빠르게 증가한다.

## 26.7 Android 압축 전략

기본 방향:

- AAB Texture Compression Targeting을 검토.
- ASTC 지원 기기에는 ASTC 변형.
- 폭넓은 호환용 ETC2 변형.
- Alpha가 중요한 VFX Texture는 압축 Artifact를 실제 기기에서 비교.
- 작은 Mask/Noise는 압축 블록 때문에 오히려 품질이 크게 깨질 수 있음.

ASTC Block 출발점:

| 자산 | 출발점 |
|---|---|
| 카드 원화 | ASTC 6×6 또는 품질 중요 시 4×4 |
| 일반 Particle | ASTC 6×6~8×8 |
| Smoke/Soft | ASTC 6×6 |
| Mask/SDF | ASTC 4×4~6×6 또는 비압축 검토 |
| Noise | ASTC 6×6~8×8, 패턴 Artifact 확인 |
| UI 아이콘 | ASTC 4×4~6×6 |

숫자는 절대 규칙이 아니다. APK/AAB 크기, 메모리, 실제 화질을 함께 본다.

## 26.8 Atlas

Atlas에 묶기 좋은 것:

- 작은 Spark/Glyph.
- 동일 Material을 쓰는 UI VFX.
- 같은 라이프사이클의 카드 희귀도 아이콘.

묶지 않거나 신중할 것:

- Repeat Wrap이 필요한 Noise.
- 큰 Flipbook.
- 서로 다른 압축/해상도 요구.
- 항상 같이 로드되지 않는 대형 테마 VFX.

## 26.9 메모리 계산

비압축 RGBA32 대략:

```text
Width × Height × 4 bytes
```

예:

```text
1024 × 1024 × 4 = 4 MB
```

Mipmaps가 있으면 대략 33%가 추가될 수 있다. 압축 포맷은 블록 크기에 따라 다르다. Import Inspector의 예상 크기만 믿지 말고 Android Player의 메모리 캡처를 확인한다.

---

<a id="section-27"></a>
# 27. 에디터 자동 검증 도구

## 27.1 목적

사람이 매번 놓치기 쉬운 항목을 자동으로 찾는다.

- Max Particles 과다.
- Loop 실수.
- Noise/Collision/Lights/Trails 사용.
- Shadow On.
- Material 누락.
- Sorting Layer 누락.
- 대형 VFX Texture.
- UI Texture의 Mipmap.
- Git 패키지 태그 미고정.
- 프리팹에 VfxInstance 누락.

자동 도구는 무조건 수정하지 않고 먼저 보고한다. VFX마다 예외가 있기 때문이다.

## 27.2 선택한 VFX 프리팹 검사

다음 파일은 `Assets/Game/VFX/Editor/VfxPrefabAudit.cs`에 둔다.

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VfxPrefabAudit
{
    private const int WarningMaxParticles = 128;
    private const int CriticalMaxParticles = 512;

    [MenuItem("Tools/VFX/Audit Selected Prefabs")]
    private static void AuditSelected()
    {
        Object[] selected = Selection.objects;
        int prefabCount = 0;
        int warningCount = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null
                || PrefabUtility.GetPrefabAssetType(prefab)
                    == PrefabAssetType.NotAPrefab)
            {
                continue;
            }

            prefabCount++;
            warningCount += AuditPrefab(prefab, path);
        }

        Debug.Log(
            $"[VFX Audit] Prefabs: {prefabCount}, warnings: {warningCount}");
    }

    [MenuItem("Tools/VFX/Audit All Under Assets/Game/VFX")]
    private static void AuditAll()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { "Assets/Game/VFX" });

        int warningCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                warningCount += AuditPrefab(prefab, path);
        }

        Debug.Log(
            $"[VFX Audit] All prefabs: {guids.Length}, warnings: {warningCount}");
    }

    private static int AuditPrefab(GameObject prefab, string path)
    {
        int warnings = 0;
        ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);

        if (systems.Length > 0 && prefab.GetComponent<VfxInstance>() == null)
        {
            Warn(prefab, path, "Root에 VfxInstance가 없습니다.");
            warnings++;
        }

        for (int i = 0; i < systems.Length; i++)
            warnings += AuditParticle(prefab, systems[i], path);

        return warnings;
    }

    private static int AuditParticle(
        GameObject prefab,
        ParticleSystem ps,
        string path)
    {
        int warnings = 0;
        ParticleSystem.MainModule main = ps.main;

        if (main.maxParticles > CriticalMaxParticles)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} maxParticles={main.maxParticles} (매우 큼)");
            warnings++;
        }
        else if (main.maxParticles > WarningMaxParticles)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} maxParticles={main.maxParticles}");
            warnings++;
        }

        if (main.loop)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Looping 활성. 지속형인지 확인.");
            warnings++;
        }

        ParticleSystem.NoiseModule noise = ps.noise;
        if (noise.enabled)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Noise 활성. Low 변형 확인.");
            warnings++;
        }

        ParticleSystem.CollisionModule collision = ps.collision;
        if (collision.enabled)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Collision 활성.");
            warnings++;
        }

        ParticleSystem.TrailModule trails = ps.trails;
        if (trails.enabled)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Trails 활성. 풀 반환 Clear 확인.");
            warnings++;
        }

        ParticleSystem.LightsModule lights = ps.lights;
        if (lights.enabled)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Particle Lights 활성.");
            warnings++;
        }

        ParticleSystem.SubEmittersModule subEmitters = ps.subEmitters;
        if (subEmitters.enabled && subEmitters.subEmittersCount > 0)
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Sub Emitters={subEmitters.subEmittersCount}");
            warnings++;
        }

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return warnings;

        if (renderer.sharedMaterial == null)
        {
            Warn(prefab, path, $"{GetPath(ps.transform)} Material 누락.");
            warnings++;
        }

        if (renderer.shadowCastingMode != ShadowCastingMode.Off)
        {
            Warn(prefab, path, $"{GetPath(ps.transform)} Cast Shadows가 켜져 있음.");
            warnings++;
        }

        if (renderer.receiveShadows)
        {
            Warn(prefab, path, $"{GetPath(ps.transform)} Receive Shadows가 켜져 있음.");
            warnings++;
        }

        if (string.IsNullOrEmpty(renderer.sortingLayerName)
            || renderer.sortingLayerName == "Default")
        {
            Warn(
                prefab,
                path,
                $"{GetPath(ps.transform)} Sorting Layer가 Default. 의도 확인.");
            warnings++;
        }

        if (trails.enabled && renderer.trailMaterial == null)
        {
            Warn(prefab, path, $"{GetPath(ps.transform)} Trail Material 누락.");
            warnings++;
        }

        return warnings;
    }

    private static string GetPath(Transform target)
    {
        List<string> parts = new();
        Transform current = target;

        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void Warn(Object context, string assetPath, string message)
    {
        Debug.LogWarning($"[VFX Audit] {assetPath}: {message}", context);
    }
}
#endif
```

프로젝트 기준에 맞춰 임계값과 예외 Attribute를 추가한다.

## 27.3 Texture 검사

파일명: `VfxTextureAudit.cs`, `Editor` 폴더에 둔다.

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class VfxTextureAudit
{
    [MenuItem("Tools/VFX/Audit Selected Textures")]
    private static void AuditSelected()
    {
        Object[] selected = Selection.objects;
        int count = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (importer == null || texture == null)
                continue;

            count++;
            Audit(texture, importer, path);
        }

        Debug.Log($"[VFX Texture Audit] Checked {count} textures.");
    }

    private static void Audit(
        Texture2D texture,
        TextureImporter importer,
        string path)
    {
        bool looksLikeMask = path.Contains("_Mask") || path.Contains("/Mask/");
        bool looksLikeNoise = path.Contains("_Noise") || path.Contains("/Noise/");
        bool looksLikeUi = path.Contains("/UI/");
        bool looksLikeFlipbook = path.Contains("_Flipbook")
            || path.Contains("/Flipbook/");

        if ((looksLikeMask || looksLikeNoise) && importer.sRGBTexture)
        {
            Debug.LogWarning(
                $"[VFX Texture] {path}: Mask/Noise로 보이지만 sRGB가 켜져 있습니다.",
                texture);
        }

        if (looksLikeUi && importer.mipmapEnabled)
        {
            Debug.LogWarning(
                $"[VFX Texture] {path}: UI 텍스처 Mipmap 활성. 필요성 확인.",
                texture);
        }

        if (looksLikeFlipbook
            && (texture.width > 2048 || texture.height > 2048))
        {
            Debug.LogWarning(
                $"[VFX Texture] {path}: Flipbook이 2048보다 큽니다.",
                texture);
        }

        if (importer.wrapMode == TextureWrapMode.Repeat && !looksLikeNoise)
        {
            Debug.LogWarning(
                $"[VFX Texture] {path}: Repeat Wrap. Atlas/가장자리 확인.",
                texture);
        }

        TextureImporterPlatformSettings android =
            importer.GetPlatformTextureSettings("Android");

        if (!android.overridden)
        {
            Debug.LogWarning(
                $"[VFX Texture] {path}: Android Override가 없습니다. "
                + "프로젝트 전역 설정으로 충분한지 확인.",
                texture);
        }
    }
}
#endif
```

## 27.4 CI 검증 아이디어

Build 전에 다음을 자동 검사한다.

- `VfxDefinition.stableId` 중복.
- 모든 Definition에 Low 프리팹 존재.
- 프리팹의 Missing Script.
- Particle Renderer Material 누락.
- Shader Graph 컴파일 오류.
- Git UPM URL에 태그가 없음.
- Android Target API가 정책보다 낮음.
- ARM64 비활성.
- 개발용 Debug Material 포함.
- VFX Gallery 씬의 모든 Definition 재생 성공.

CI에서 모든 경고를 실패로 만들기보다 `Error / Warning / Info` 등급을 둔다.

---

<a id="section-28"></a>
# 28. 프로파일링과 성능 승인

## 28.1 Editor 수치로 승인하지 않는다

Editor에는 다음 오차가 있다.

- Editor UI와 Scene/Game View 비용.
- Mono/개발 환경 차이.
- PC GPU와 모바일 타일 기반 GPU 차이.
- Shader 컴파일/캐시 차이.
- 마우스/키보드와 Touch 입력 차이.

최종 승인은 Android IL2CPP ARM64 실제 Player에서 한다.

## 28.2 테스트 빌드 종류

### 기능 검증 빌드

- Development Build On.
- Script Debugging 필요 시만.
- Autoconnect Profiler.
- OpenGLES3.
- 품질 Low/Medium/High 전환 UI.
- VFX Debug Overlay 포함.

### 성능 검증 빌드

- Development Build On 또는 Profiling을 위한 최소 설정.
- Deep Profiling Off.
- Script Debugging Off.
- 실제 릴리스와 같은 IL2CPP/ARM64.
- Managed Stripping/Shader Stripping도 릴리스에 가깝게.

### 최종 릴리스 후보

- Development Off.
- 로그 최소화.
- AAB.
- Google Play Internal Testing.
- Play Asset/Texture Compression 설정 포함.

## 28.3 VFX Gallery 씬

반드시 만든다.

```text
VFX_Gallery
├─ QualityControls
├─ BackgroundControls
│  ├─ Dark
│  ├─ Mid
│  └─ Bright
├─ CameraControls
├─ SpawnControls
├─ CardShowcase
├─ UIShowcase
├─ CombatShowcase
├─ RewardShowcase
└─ StressTest
```

기능:

- 모든 `VfxDefinition` 검색/재생.
- 1회/10회/100회 Burst.
- Low/Medium/High 전환.
- 30/60 FPS 전환.
- 검은/회색/흰 배경 전환.
- 해상도/Aspect preset.
- Time Scale 0/0.25/1/2.
- Pause/Resume.
- Active Particle/Pool 수 표시.
- Draw Call, Batches, Triangles, Texture Memory 기록.

## 28.4 저사양 Stress Scene

최악의 실제 플레이를 재현한다.

- 화면에 카드 최대 개수.
- 적/아군 최대 개수.
- 동시에 가능한 상태 이상 모두.
- 보상 아이콘 최대 표시.
- 일반 공격 Burst를 1~2초 반복.
- UI 팝업/카운터 갱신 동시.
- 배경 Parallax/애니메이션 포함.
- 실제 해상도와 렌더 스케일.

인위적으로 모든 이펙트를 무한 재생하는 테스트와 실제 최악 플레이 테스트를 둘 다 둔다.

## 28.5 측정 순서

1. VFX 전부 Off 기준 Frame Time 기록.
2. Core VFX만 On.
3. Particle 추가.
4. UI Particle 추가.
5. Bloom/Post Processing 추가.
6. Distortion/Renderer Feature 추가.
7. Light2D 추가.
8. 각 단계의 CPU/GPU/메모리 차이 기록.

이렇게 해야 “VFX가 느리다”가 아니라 어느 기능이 비용을 만들었는지 알 수 있다.

## 28.6 Unity Profiler

확인 모듈:

- CPU Usage.
- GPU Usage가 기기/API에서 가능할 경우.
- Rendering.
- Memory.
- UI/UI Details.
- Audio.

VFX 관련 CPU 항목:

- ParticleSystem.Update.
- ParticleSystem.BeginUpdateAll.
- Canvas.BuildBatch.
- Graphic.Rebuild.
- Animator.Update.
- Script Update의 VFX 서비스.
- Instantiate/Destroy.
- GC.Alloc.

검수:

- 일반 플레이 중 VFX 때문에 매 프레임 GC Alloc이 생기지 않는가.
- Burst 순간 Main Thread Spike가 있는가.
- UI Particle이 Canvas 전체 Rebuild를 유발하는가.
- 카드 Shader Property 갱신이 모든 카드에 매 프레임 실행되는가.

## 28.7 URP Rendering Debugger

활용:

- CPU/GPU/Present 병목 비율.
- Rendering 단계별 CPU/GPU 시간.
- Material/Lighting Debug.
- Overdraw/복잡도 관련 Debug View가 제공될 경우 활용.

목적:

- FPS가 낮은 이유가 GPU인지 CPU인지 구분.
- Post Processing 또는 특정 Pass 비용 확인.
- Light2D/Renderer Feature 추가 전후 비교.

## 28.8 Frame Debugger

확인:

- 같은 Material이 왜 여러 Draw Call로 쪼개졌는가.
- UI 마스크/Stencil 때문에 Pass가 늘었는가.
- Renderer Feature가 Fullscreen Copy를 추가했는가.
- 불필요한 Camera Stack이 있는가.
- 카드마다 Material 인스턴스가 생겼는가.
- Sorting 변경으로 Batch가 깨졌는가.

Frame Debugger는 GPU 시간 자체보다 렌더 순서와 Draw 구조를 이해하는 데 사용한다.

## 28.9 Overdraw 검사

특히 확인:

- 화면 전체 반투명 Vignette 여러 장.
- 카드 한 장 위에 Glow/Status/Foil/Selection/Hit Overlay가 모두 중첩.
- 큰 Smoke Texture의 대부분이 투명.
- Stretched Billboard가 화면을 길게 덮음.
- UI Particle이 Mask 밖에서도 렌더됨.
- Bloom을 위해 큰 Additive Sprite를 겹침.

개선 순서:

1. 투명 Quad 크기를 실제 형태에 가깝게 자름.
2. 레이어 수 감소.
3. 같은 효과를 Shader 한 Pass에서 결합.
4. 화면 밖 Culling.
5. Alpha Clip이 적합한 날카로운 자산에만 적용.
6. 효과 지속시간 단축.

## 28.10 Memory Profiler

확인:

- VFX Texture 중복.
- Material 인스턴스 수.
- Shader Variant 메모리.
- Particle 프리팹 풀 크기.
- 비활성 프리팹도 참조 때문에 메모리에 상주하는지.
- Addressables Group별 VFX 중복 의존성.
- 카드 원화/Mask Atlas가 동시에 너무 많이 로드되는지.

풀은 CPU 할당을 줄이지만 너무 크게 만들면 메모리를 낭비한다. 실제 동시 수 + 작은 여유만 유지한다.

## 28.11 Android GPU Inspector/플랫폼 도구

Unity Profiler만으로 원인이 안 보이면 Android GPU Inspector 같은 플랫폼 도구를 사용한다.

확인 후보:

- GPU Counter.
- Fragment/Vertex 부하.
- Texture Sampling.
- Bandwidth.
- Render Pass.
- Thermal/전력 경향.

특정 제조사 도구 결과를 모든 Android 기기에 일반화하지 않는다.

## 28.12 열/배터리 Soak Test

최소 시나리오:

1. 기기를 실온에서 시작.
2. 화면 밝기를 고정.
3. 동일 네트워크/배터리 조건을 기록.
4. 전투 Stress Scene을 15~30분 반복.
5. 1분, 5분, 10분, 20분의 FPS/Frame Time 기록.
6. Thermal Throttling 이후 품질 강등 동작 확인.
7. 앱 Pause/Resume 후 상태 확인.

첫 30초만 빠르고 10분 후 크게 느려지는 설정은 승인하지 않는다.

## 28.13 승인 기준 예시

### Tier L / 30 FPS 모드

- P50 Frame Time: 30 ms 이하 목표.
- P95 Frame Time: 33.3~38 ms 안쪽 목표.
- VFX Burst 때문에 반복적인 50 ms 이상 Spike가 없어야 함.
- 일반 플레이 중 VFX 경로 GC Alloc 0 B/frame 목표.
- 전설/보스 연출의 단발 Spike는 별도 기준으로 기록하되 입력이 끊기지 않게.

### Tier M / 60 FPS 모드

- P50: 15~16.7 ms 안쪽.
- P95: 18~22 ms 안쪽 목표.
- 전투 지속 중 안정 60에 가깝게.
- 발열 후 자동 품질 강등이 지나치게 늦지 않게.

### Tier H

- High VFX가 추가되어도 60 FPS 목표 유지.
- 90/120 FPS는 옵션이며 모든 기기 기본값으로 두지 않는다.

숫자는 프로젝트 장르/기기/게임 로직에 맞춰 조정하되 PR과 릴리스마다 같은 방식으로 비교한다.

## 28.14 성능 기록 템플릿

```markdown
### VFX Performance Record

- Build commit:
- Unity:
- URP Renderer:
- Device model:
- Android version:
- Graphics API:
- Resolution:
- Render Scale:
- VFX Tier:
- Target FPS:
- Test scene:
- Test duration:
- P50 frame time:
- P95 frame time:
- Worst spike:
- Main-thread ms:
- GPU ms:
- Draw calls/batches:
- Active particles peak:
- VFX pool active peak:
- GC Alloc/frame:
- Total memory:
- Texture memory:
- Thermal state/notes:
- Screenshot/capture link:
- Pass/Fail:
```

---

<a id="section-29"></a>
# 29. Android 출시 설정과 기기 전략

## 29.1 “가장 많이 쓰는 최저 기기”의 의미

전 세계 Android 시장에는 단 하나의 대표 최저 기기가 없다.

지역, 장르, 과금 성향, 스토어 노출, 설치 용량에 따라 사용자 분포가 달라진다. 그러므로 다음 3단계로 접근한다.

1. **출시 전 가정**: 3 GB RAM 저가형, 30 FPS 안정 목표.
2. **Soft Launch 데이터**: Google Play Console의 Reach and devices, Android Vitals, 실제 세션 성능 확인.
3. **출시 후 조정**: 설치/매출 기여가 낮고 QA 비용이 높은 기기군만 최소 사양에서 제외.

이 문서의 실무 최저 기준:

```text
Android 9 / API 28 이상 권장
RAM 3 GB급
OpenGL ES 3.x
720p~1080p급 디스플레이
30 FPS 안정 모드
Low VFX Tier
```

Unity 6.3 자체 Android 지원 최저 API보다 높은 API 28을 택하는 이유는 테스트 행렬과 오래된 드라이버 문제를 줄이기 위한 프로젝트 정책이다. 실제 사업 요구가 있으면 API 25~27을 별도 QA 후 지원할 수 있다.

## 29.2 출시 테스트 기기군

브랜드 하나에 치우치지 않는다.

### Tier L — 필수

- RAM 3 GB급.
- 저가형 Mali/PowerVR/Adreno 계열 중 최소 2종.
- 720p와 1080p 각 1종.
- Android 9~12 중 오래된 OS 1종.
- OpenGLES3.
- 30 FPS Low.

### Tier M — 필수

- RAM 4~6 GB.
- 중급 Mali/Adreno 각 1종 이상.
- Android 13~15.
- OpenGLES3와 Vulkan 비교.
- 60 FPS Medium.

### Tier H — 권장

- RAM 8 GB 이상.
- 고주사율 90/120 Hz.
- Android 15~16.
- Vulkan.
- 60 FPS High와 고주사율 옵션.

### 제조사 분산

- Samsung 계열.
- Xiaomi/Redmi/Poco 계열.
- Oppo/Realme/Vivo 계열 중 목표 시장 비중에 맞게.
- Google Pixel 또는 표준 Android 검증 기기.

모델 이름은 출시 지역과 시점에 맞춰 매 분기 갱신한다.

## 29.3 Player Settings 기준

```text
Build System: Gradle
Build App Bundle: On for Play release
Scripting Backend: IL2CPP
Target Architectures: ARM64
Minimum API Level: 28 권장
Target API Level: 36 또는 설치 SDK의 정책 충족 최신
Internet Access: 필요한 경우만
Write Permission: Internal
Managed Stripping: 기능 테스트 후 Medium 이상 검토
Strip Engine Code: 릴리스 검증 후 On 검토
Optimized Frame Pacing: On
```

- 2026년 8월 31일 이후 Google Play 신규 앱·업데이트는 API 36 타깃 요구를 기준으로 준비한다.
- Play 정책은 변경될 수 있으므로 릴리스 직전에 공식 요구사항을 재확인한다.
- 16 KB Memory Page Size 관련 네이티브 플러그인 호환성을 확인한다.
- GitHub에서 가져온 오래된 `.so` 플러그인이 있으면 페이지 정렬과 ABI를 점검한다.

## 29.4 Graphics API 전략

### 기본 권장

```text
1. OpenGLES3
2. Vulkan — 검증된 기기군 또는 옵션/원격 설정
```

이 선택은 2D 카드 게임의 드라이버 안정성과 넓은 단말 범위를 우선한 보수적 시작점이다. Vulkan이 더 적합한 프로젝트는 순서를 바꿀 수 있다.

Vulkan 우선 전환 조건:

- 목표 단말에서 Vulkan Crash율이 낮음.
- Shader/Renderer Feature가 모두 정상.
- Frame Time 또는 전력에서 의미 있는 개선.
- UI Mask, Particle, Post Processing, 앱 Resume 테스트 통과.
- Play Console 기기 분포상 Vulkan 1.1+ 비중이 충분.

API별 빌드를 반드시 비교할 항목:

- 첫 Shader 표시 Hitch.
- UI Stencil/Mask.
- Sprite Atlas 색/Alpha.
- Light2D Normal.
- Bloom.
- NovaShader Distortion.
- 앱 Background/Resume.
- 기기 회전.
- 화면 녹화/오버레이 상황.

## 29.5 Frame Pacing

- Android Player Settings의 Optimized Frame Pacing을 켠다.
- `Application.targetFrameRate`를 30 또는 60으로 명시한다.
- 90/120 FPS는 사용자가 선택하거나 검증된 고성능 기기에서만.
- 표시 주사율과 목표 FPS 조합이 어색하지 않은지 실제 기기에서 확인.
- 순간 최대 FPS보다 안정적인 Frame Time을 우선한다.

간단한 설정 코드:

```csharp
using UnityEngine;

public static class FrameRatePolicy
{
    public static void Apply30Fps()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
    }

    public static void Apply60Fps()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
}
```

앱 Resume, 품질 설정 변경, Android Game Mode 변화 후 목표값이 유지되는지 확인한다.

## 29.6 Render Scale

권장 시작:

| Tier | Render Scale |
|---|---:|
| Low | 0.75~0.9 |
| Medium | 0.9~1.0 |
| High | 1.0 |

주의:

- Screen Space Overlay UI는 월드 Render Scale과 다르게 선명할 수 있다.
- Sprite Pixel Art는 Scale 변화로 흔들림/Blurring이 생길 수 있다.
- 카드 원화가 핵심인 게임은 0.75까지 낮추기 전에 가독성 검증.
- GPU 병목일 때만 의미가 크며 CPU 병목에는 효과가 제한적.

## 29.7 Texture Compression Targeting

AAB에서 기기별 Texture Compression Targeting을 사용하면 ASTC와 ETC2 변형을 배포할 수 있다.

검수:

- 각 변형의 다운로드 크기.
- 설치 후 Texture 메모리.
- Alpha/Gradient Banding.
- 카드 글자나 아이콘이 Texture에 구워진 경우 가독성.
- 오래된 기기 fallback.

VFX 전용 대형 Flipbook 때문에 App Bundle이 크게 늘지 않게 한다.

## 29.8 Low Memory 대응

```csharp
using UnityEngine;

public sealed class LowMemoryResponder : MonoBehaviour
{
    private void OnEnable()
    {
        Application.lowMemory += HandleLowMemory;
    }

    private void OnDisable()
    {
        Application.lowMemory -= HandleLowMemory;
    }

    private static void HandleLowMemory()
    {
        VfxQualityRuntime.SetTier(VfxQualityTier.Low);

        // 프로젝트의 Addressables/캐시 서비스에 정리 요청을 전달한다.
        // 현재 화면에서 필요 없는 VFX 테마와 대형 Flipbook을 해제한다.
    }
}
```

금지:

- Low Memory 이벤트에서 무조건 `Resources.UnloadUnusedAssets`를 즉시 호출해 전투를 멈추게 함.
- 풀 오브젝트를 모두 파괴해 다음 전투에서 큰 Instantiate Spike를 만듦.

대신 메뉴/전환 구간에서 점진적으로 정리한다.

## 29.9 Safe Area와 다양한 화면비

카드 획득·보상 이동 VFX는 다음을 검증한다.

- 16:9.
- 18:9.
- 19.5:9.
- 20:9 이상.
- 노치.
- Punch-hole.
- Navigation bar.
- 태블릿 4:3.
- Foldable의 넓은 화면.

HUD 목표 위치는 하드코딩 픽셀이 아니라 RectTransform/Safe Area를 기준으로 계산한다.

## 29.10 앱 Pause/Resume

다음 버그를 테스트한다.

- Resume 후 Particle이 한꺼번에 폭발.
- `unscaledDeltaTime`에 큰 값이 들어와 보상 아이콘이 즉시 도착.
- Animator가 잘못된 중간 상태.
- 풀 Instance의 Callback이 두 번 호출.
- Audio/Haptic이 늦게 재생.
- Bloom/Renderer Feature가 검게 표시.

Resume 첫 프레임 Delta를 Clamp하는 방식을 UI Tween에 검토한다.

```csharp
float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
```

게임플레이 시간 계산에는 별도 정책을 사용한다.

---

<a id="section-30"></a>
# 30. 타이밍·모션·카메라 연출

## 30.1 타이밍 원칙

좋은 VFX는 파티클 수보다 타이밍이 좋다.

기본 4단계:

1. **Anticipation**: 무슨 일이 일어날지 예고.
2. **Action**: 판정과 핵심 형태.
3. **Impact**: Flash, Shake, Sound, 숫자.
4. **Recovery**: Smoke, 잔광, 안정화.

일반 타격:

```text
Anticipation 0~80 ms
Action       80~150 ms
Impact       120~220 ms
Recovery     200~600 ms
```

카드 획득:

```text
Anticipation 0~200 ms
Reveal       150~450 ms
Celebration  300~900 ms
Settle       700~1500 ms
```

## 30.2 Easing

| 상황 | Easing |
|---|---|
| 카드 등장 | Ease Out Back 또는 작은 Overshoot |
| HUD로 이동 | Ease In 또는 Ease In Out |
| Impact Ring | 빠른 Ease Out |
| 사라짐 | Ease In |
| 선택 Lift | Ease Out Cubic |
| 취소 복귀 | Ease In Out Quad/Cubic |
| 전설 Reveal | 구간별 Custom Curve |

모든 요소에 Bounce를 쓰지 않는다. 보상/귀여운 UI에는 적합하지만 공격 Impact는 Sharp curve가 더 좋다.

## 30.3 화면 Shake

규칙:

- Position, Rotation, FOV/Orthographic Size 중 필요한 축만.
- UI HUD 전체를 무조건 흔들지 않는다.
- 연속 공격은 Shake를 누적하지 않고 Envelope를 합성.
- Reduced Motion에서 0~30%로 감소.
- 보스 연출도 플레이어 조준/드래그를 방해하지 않게.

권장 강도 상대값:

| 사건 | 강도 | 시간 |
|---|---:|---:|
| 버튼 확정 | 0 | 0 |
| 일반 타격 | 0.05~0.12 | 40~80 ms |
| 치명타 | 0.15~0.3 | 80~160 ms |
| 큰 폭발 | 0.25~0.5 | 120~260 ms |
| 보스 필살 | 0.4~0.8 | 180~400 ms |

실제 단위는 카메라 크기와 Cinemachine 구현에 맞춘다.

## 30.4 Hit Stop

- 일반 타격 0~30 ms.
- 치명타 30~70 ms.
- 매우 큰 공격 60~120 ms.
- UI와 입력까지 멈출지 분리.
- Particle이 Scaled Time을 쓸지 Unscaled Time을 쓸지 의도적으로 결정.
- 온라인/턴제 판정과 시간 정지를 분리.

카드 게임에서는 긴 Hit Stop보다 짧은 Target Pause + Sound transient가 더 자연스러운 경우가 많다.

## 30.5 Slow Motion

- 궁극기/보스 피니시처럼 드문 순간.
- UI 입력과 네트워크 타이머는 Unscaled.
- Particle Simulation Speed가 기대대로 보이는지 확인.
- Slow Motion 종료 시 Audio pitch 정책.
- Reduced Motion에서 단축/비활성.

## 30.6 Layered Timing

모든 레이어를 같은 프레임에 시작하지 않는다.

예시:

```text
0 ms    Core Flash
20 ms   Main Shape
50 ms   Spark
80 ms   Ring
120 ms  Smoke
180 ms  Secondary Glyph
```

20~80 ms 간격만으로도 깊이가 생기고 CPU/GPU Burst가 분산된다.

## 30.7 카드 Flip

2D 카드 Flip 방식:

1. Y축 Scale 1→0.
2. 중간 프레임에 앞/뒤 Sprite 교체.
3. Scale 0→1.
4. 약한 Shine/Ring.

실제 3D Rotation을 쓸 경우 Perspective, Sorting, Backface, UI clipping을 확인한다.

추천 시간:

- 일반 뒤집기: 180~300 ms.
- 희귀 Reveal: 350~650 ms.
- 전설 Reveal: Anticipation 포함 700~1400 ms.

## 30.8 카드 Tilt

드래그/호버 시:

- X/Y Tilt 최대 3~8°.
- Foil 방향에 Tilt 값을 전달.
- 손가락 위치를 Low-pass filter.
- 릴리스 시 100~220 ms 복귀.
- Reduced Motion에서 Tilt Off 또는 1~2°.

모바일 터치에서는 카드 내부 좌표가 급격히 변하므로 매 프레임 즉시 따라가지 않는다.

---

<a id="section-31"></a>
# 31. 오디오·햅틱과 VFX 동기화

## 31.1 VFX만으로 완성하려 하지 않는다

만족감은 다음 조합이다.

```text
Visual shape
+ Motion timing
+ Sound transient
+ Pitch/layer
+ Haptic
+ Number/UI feedback
```

파티클을 두 배 늘리기 전에 Sound와 Timing을 개선한다.

## 31.2 사운드 레이어

일반 Impact:

- Attack/body transient.
- Material layer.
- 작은 low-end 또는 tail.

카드 획득:

- Reveal whoosh.
- Rarity chime.
- UI settle click.

전설:

- Anticipation rise.
- 순간 silence/duck.
- Reveal impact.
- 희귀도 motif.
- 감상 loop는 매우 약하게.

## 31.3 동시 재생 제한

- Spark 하나마다 Sound를 재생하지 않는다.
- 보상 아이콘 10개 도착 시 10개의 같은 Click 대신 간격 제한/Pitch sequence.
- 동일 SFX는 30~80 ms Cooldown.
- 연타는 첫 소리와 마지막 소리를 강조.

## 31.4 햅틱

권장:

| 사건 | 햅틱 |
|---|---|
| 일반 버튼 | 매우 약함 또는 없음 |
| 구매 확정 | 짧은 Light |
| 카드 사용 | Light/Medium |
| 치명타 | Medium |
| 전설 획득 | 단계별 1~2회, 과도하지 않게 |
| 보상 아이콘 | 첫 또는 마지막 도착 1회 |
| 오류 | 짧은 경고 패턴 |

- 사용자 설정에서 Off 가능.
- 기기별 햅틱 품질 차이 고려.
- 연속 진동 금지.
- 배터리 절약 모드/Reduced Motion과 연계 가능.

## 31.5 동기화 기준

- 판정 시각과 가장 강한 Sound transient를 맞춘다.
- Audio latency를 실제 기기에서 확인.
- 애니메이션 Event보다 코드의 판정 이벤트를 기준으로 하는 것이 안전할 수 있다.
- Skip/빠른 진행 시 Sound가 뒤늦게 남지 않게 중지/전환.

---

<a id="section-32"></a>
# 32. 접근성·가독성·광과민성

## 32.1 Reduced Motion 설정

옵션 예:

```text
Motion Effects: Full / Reduced / Minimal
Screen Shake: 100% / 50% / Off
Flashes: Full / Reduced
Particles: High / Standard / Low
Haptics: On / Off
```

Reduced:

- Shake 30~50%.
- 카드 Tilt 절반.
- 큰 Zoom/Pan 단축.
- 보상 아이콘 곡선 이동 거리 축소.
- 반복 Pulse 속도 감소.

Minimal:

- Shake Off.
- Tilt Off.
- 전체 화면 Flash를 작은 카드 내부 Flash로 대체.
- 장식 Particle 최소.
- 중요한 정보는 아이콘/텍스트로 유지.

## 32.2 Flash

- 빠른 전환의 전체 화면 밝은 Flash를 반복하지 않는다.
- 번개 효과는 화면 일부와 짧은 시간에 제한.
- 전설 연출의 White Flash는 Reduced Flash에서 Alpha/면적 감소.
- 밝기 변화 사이에 충분한 간격.
- Debug Stress에서도 실사용보다 빠르게 Flash를 반복하지 않는다.

## 32.3 색각 접근성

희귀도/상태는 다음을 함께 사용한다.

- 색.
- 프레임 형태.
- 별/Gem 개수.
- Glyph.
- Particle 패턴.
- 사운드 motif.
- 텍스트 라벨.

예:

| 희귀도 | 형태 |
|---|---|
| Common | 단순 사각 프레임, Spark 적음 |
| Rare | 이중 모서리, 별 1개 |
| Epic | 각진/장식 프레임, 별 2개 |
| Legendary | 고유 Crown/Glyph, 별 3개, 긴 Reveal |

## 32.4 텍스트 가독성

- VFX가 텍스트 위를 장시간 지나지 않는다.
- 카드 설명 영역을 Foil Mask에서 제외.
- Additive Glow가 흰 텍스트와 합쳐지지 않게.
- Damage Number와 Particle 색 대비.
- Localization으로 텍스트가 길어져도 VFX Anchor가 겹치지 않게.

## 32.5 정보 우선순위

동시에 여러 이벤트가 발생하면:

1. 위험/실패 경고.
2. 플레이어 입력 결과.
3. 큰 보상/희귀 획득.
4. 일반 전투.
5. 장식.

낮은 우선순위 VFX를 줄여 높은 우선순위 정보가 보이게 한다.

---

<a id="section-33"></a>
# 33. 테스트와 QA 체크리스트

## 33.1 기능 테스트

- [ ] 효과가 올바른 이벤트에서 정확히 한 번 재생된다.
- [ ] Pause/Resume 후 중복 재생되지 않는다.
- [ ] Time Scale 0에서 UI VFX 정책이 맞다.
- [ ] 카드 풀 반환 후 이전 상태가 남지 않는다.
- [ ] Scene 전환 시 Callback/Coroutine이 남지 않는다.
- [ ] 빠른 연타/Skip에서도 결과 상태가 정확하다.
- [ ] 보상 값은 연출 취소와 무관하게 저장된다.
- [ ] Target이 파괴된 보상 아이콘이 안전하게 반환된다.
- [ ] 앱 Background/Resume에서 큰 Delta로 튀지 않는다.
- [ ] 화면 회전/해상도 변경 시 HUD 목적지를 재계산한다.

## 33.2 렌더링 테스트

- [ ] 검은 배경에서 보인다.
- [ ] 흰 배경에서 보인다.
- [ ] 복잡한 전장 배경에서 읽힌다.
- [ ] 카드 원화와 텍스트를 가리지 않는다.
- [ ] Sorting Layer가 의도대로다.
- [ ] UI Mask/RectMask2D가 정상이다.
- [ ] Sprite Atlas에서 Bleeding이 없다.
- [ ] OpenGLES3와 Vulkan 결과가 허용 범위 내 같다.
- [ ] Low/Medium/High 전환 시 Missing Material이 없다.
- [ ] Bloom Off에서도 의미가 유지된다.
- [ ] HDR Off 대체 표현이 있다.

## 33.3 성능 테스트

- [ ] 실제 Tier L 기기에서 30 FPS 목표.
- [ ] Tier M에서 60 FPS 목표.
- [ ] Burst 순간 반복 Spike 기록.
- [ ] 전투 중 GC Alloc 0 B/frame 목표.
- [ ] Pool 고갈 시 안전하게 생략/대체.
- [ ] Active Particle Peak가 예산 이내.
- [ ] Overdraw Debug에서 큰 겹침 없음.
- [ ] Shader Variant 첫 재생 Hitch 허용 범위.
- [ ] 15~30분 Thermal Soak 통과.
- [ ] Low Memory 이벤트 대응.
- [ ] 대형 Flipbook 메모리 확인.

## 33.4 입력 테스트

- [ ] Particle의 Raycast Target이 꺼져 있다.
- [ ] UIEffect Overlay가 버튼 입력을 막지 않는다.
- [ ] 카드 Drag 중 VFX가 Pointer event를 가로채지 않는다.
- [ ] 획득 연출 Skip 버튼이 항상 접근 가능.
- [ ] 빠른 탭으로 중복 결제/보상 요청이 일어나지 않는다.

## 33.5 접근성 테스트

- [ ] Reduced Motion.
- [ ] Screen Shake Off.
- [ ] Flash Reduced.
- [ ] Haptic Off.
- [ ] 색각 시뮬레이션에서도 희귀도/상태 구분.
- [ ] 작은 화면에서도 텍스트 가독성.
- [ ] 화면 밝기 낮음/높음에서 핵심 효과 구분.

## 33.6 회귀 테스트

Unity/URP/패키지 업데이트 때:

- [ ] 모든 Shader Graph 컴파일.
- [ ] Pink Material 없음.
- [ ] 2D Light/Normal.
- [ ] UI Mask/Stencil.
- [ ] ParticleEffectForUGUI Trail.
- [ ] UIEffect Dissolve/Blur/Transition.
- [ ] NovaShader Custom Vertex Streams.
- [ ] Android OpenGLES3/Vulkan.
- [ ] Build size와 Shader Variant 증가량.
- [ ] Memory leak.
- [ ] Pool 반환.

## 33.7 PR 완료 정의

VFX PR은 다음이 있어야 완료다.

```text
1. Before/After 영상 또는 GIF
2. Low/Medium/High 영상
3. Tier L 실기기 캡처
4. VFX Gallery 등록
5. Definition/Prefab/Material/Texture 명명 규칙 통과
6. Audit 경고 설명 또는 수정
7. 성능 수치
8. 접근성 옵션 동작
9. 라이선스 기록
10. 변경 문서
```

---

<a id="section-34"></a>
# 34. AI 에이전트 작업 프로토콜

이 문서를 읽은 AI 에이전트는 Unity VFX 요청을 받을 때 다음 절차를 따른다.

## 34.1 요청 분석

입력이 부족해도 작업을 멈추지 않는다. 다음 기본값으로 가정하고 가정을 명시한다.

```text
Project: 2D URP / Android
Unity: 6000.3 LTS 최신 검증 패치
Renderer: 2D Renderer
Low target: 3 GB RAM, 30 FPS
Medium target: 4~6 GB, 60 FPS
Coordinates: 요청 문맥에 따라 World 또는 UI
Pooling: Required
Quality variants: Low/Medium/High
```

먼저 한 문장으로 의미를 정의한다.

예:

```text
“전설 카드 획득이 일반 보상과 즉시 구별되고, 사용자가 카드 원화와 이름을 감상할 수 있게 한다.”
```

## 34.2 산출물 순서

1. 게임플레이 의미.
2. 화면 공간과 Anchor.
3. 0~끝까지 Timeline.
4. 레이어 목록.
5. Particle/Shader/Texture 요구.
6. Low/Medium/High 차이.
7. Prefab Hierarchy.
8. Material/Property 목록.
9. Pooling/Lifetime.
10. 구현 코드.
11. 프로파일 절차.
12. 완료 체크리스트.

## 34.3 새 패키지 설치 판단

새 패키지를 설치하기 전:

- Unity 6000.3 호환성.
- 최근 Release/Commit.
- License.
- Open Issues.
- URP 2D Renderer 지원 여부.
- Android/OpenGLES3/Vulkan.
- IL2CPP/AOT.
- 기존 패키지와 기능 중복.
- Git URL Tag 고정 가능.
- 제거/업데이트 경로.

단순 Shine 하나를 위해 대형 패키지를 추가하지 않는다.

## 34.4 구현 선택 트리

```text
효과가 단순 Sprite 애니메이션인가?
├─ Yes → Animator 또는 코드 Tween + Sprite
└─ No
   ↓
Particle System 모듈과 기존 Material로 가능한가?
├─ Yes → Particle System
└─ No
   ↓
NovaShader 기능으로 가능한가?
├─ Yes → NovaShader Material
└─ No
   ↓
카드/UI 전용인가?
├─ Yes → UIEffect 또는 전용 Shader Graph
└─ No
   ↓
새 Shader Graph/Sub Graph 작성
   ↓
Renderer Feature/Fullscreen pass는 최후에 검토
```

## 34.5 AI가 작성하는 Shader Graph 설명 형식

Shader Graph `.shadergraph` 파일을 직접 안정적으로 생성하기 어려운 환경에서는 다음을 제공한다.

```markdown
### Graph Settings
- Target:
- Surface:
- Blend:
- Alpha Clip:
- Precision:

### Properties
| Reference | Type | Default |

### Nodes
1. Node A 생성
2. A.out → B.in 연결
3. ...

### Outputs
- Base Color:
- Alpha:
- Emission:

### Material Defaults
...

### Validation
...
```

노드 이름과 연결을 생략하고 “예쁜 홀로그램을 만든다”로 끝내지 않는다.

## 34.6 AI가 작성하는 Particle 레시피 형식

```markdown
### PS_Core
- Main:
- Emission:
- Shape:
- Color over Lifetime:
- Size over Lifetime:
- Renderer:
- Material:
- Sorting:
- Quality:

### PS_Spark
...
```

모든 모듈 값을 숫자 범위로 제시한다.

## 34.7 AI의 금지 행동

- 확인하지 않은 최신 버전 번호를 만들어내지 않는다.
- `main` 브랜치를 프로덕션 manifest에 넣지 않는다.
- Mobile을 이유로 시각 품질을 무조건 제거하지 않는다.
- High 효과만 만들고 “나중에 최적화”하지 않는다.
- 모든 카드마다 Material Clone.
- 매 재생 `Instantiate/Destroy`.
- Shader Graph의 Screenshot 없이 구조를 알 수 없다고 작업을 포기하지 않는다.
- 실제 판정을 Particle Collision에 넣지 않는다.
- 외부 Asset의 License를 무시하지 않는다.
- 사용자에게 실제 기기 검증 없이 “최적화 완료”라고 단정하지 않는다.

## 34.8 AI 요청 예시

### 카드 획득

```text
이 SKILL.md 규칙을 따라 Rare, Epic, Legendary 카드 획득 이펙트를 만들어라.
각각 Timeline, Prefab hierarchy, Particle module 값, Shader Graph 속성,
Low/Medium/High 변형, 풀링 Definition, Android Tier L 검수 기준을 작성하라.
```

### 전투

```text
화염 카드가 적을 타격하고 Burn 3턴을 부여하는 VFX를 설계하라.
Impact와 지속 상태가 구분되어야 하며 Low에서는 Noise/Light/Distortion을 사용하지 마라.
```

### Shader Graph

```text
SpriteRenderer 카드용 Foil Overlay Shader Graph를 설계하라.
MaterialPropertyBlock으로 _FoilStrength, _Seed, _ShineProgress를 제어하고
노드 연결을 순서대로 작성하라. Texture sample은 3회 이하를 목표로 하라.
```

### 최적화

```text
선택한 VFX 프리팹을 모바일 기준으로 감사하라.
Overdraw, maxParticles, Noise, Collision, Trail, Material instance,
UI Canvas rebuild, Shader variants를 심각도별로 보고하고 수정안을 작성하라.
```

---

<a id="section-35"></a>
# 35. 개발용 VFX 디버그 도구

## 35.1 Debug Overlay에 표시할 값

Development Build에서만 다음을 표시한다.

```text
VFX Tier
Target FPS / measured FPS
CPU/GPU frame time 가능 범위
Global active VFX
Active ParticleSystem 수
Estimated active particle count
Pool misses
Dropped decorative VFX
VFX material count
UI particle count
Current graphics API
Render scale
Thermal warning
```

릴리스 사용자 화면에는 노출하지 않는다.

## 35.2 간단한 Debug HUD

파일명: `VfxDebugHud.cs`

```csharp
using UnityEngine;

public sealed class VfxDebugHud : MonoBehaviour
{
    [SerializeField] private VfxService service;
    [SerializeField] private KeyCode cycleTierKey = KeyCode.F8;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private float smoothedDelta = 1f / 60f;

    private void Update()
    {
        smoothedDelta = Mathf.Lerp(
            smoothedDelta,
            Time.unscaledDeltaTime,
            0.08f);

        if (Input.GetKeyDown(cycleTierKey))
            CycleTier();
    }

    private void OnGUI()
    {
        float fps = 1f / Mathf.Max(0.0001f, smoothedDelta);
        string graphicsApi = SystemInfo.graphicsDeviceType.ToString();
        int active = service != null ? service.GlobalActiveCount : -1;

        GUI.Box(new Rect(8, 8, 310, 130), string.Empty);
        GUI.Label(new Rect(18, 16, 290, 24), $"VFX Tier: {VfxQualityRuntime.Tier}");
        GUI.Label(new Rect(18, 40, 290, 24), $"FPS: {fps:0.0} / Target {Application.targetFrameRate}");
        GUI.Label(new Rect(18, 64, 290, 24), $"Active VFX: {active}");
        GUI.Label(new Rect(18, 88, 290, 24), $"Graphics API: {graphicsApi}");
        GUI.Label(new Rect(18, 112, 290, 24), $"Press {cycleTierKey} to cycle tier");
    }

    private static void CycleTier()
    {
        VfxQualityTier next = VfxQualityRuntime.Tier switch
        {
            VfxQualityTier.Low => VfxQualityTier.Medium,
            VfxQualityTier.Medium => VfxQualityTier.High,
            _ => VfxQualityTier.Low
        };

        VfxQualityRuntime.SetTier(next);
    }
#endif
}
```

프로덕션에서는 새 Input System Action과 전용 Debug Menu에 연결한다.

## 35.3 VFX 이벤트 로그

문제 재현을 위해 Development Build에서 Ring Buffer를 둔다.

기록:

```text
Timestamp
Stable VFX ID
Position/space
Quality tier
Spawn success/drop reason
Pool active/capacity
Lifetime
Caller event ID
```

매 이벤트를 파일/네트워크에 즉시 쓰지 않는다. 최근 100~500개를 메모리에 보관하고 사용자가 Bug Report를 만들 때 내보낸다.

## 35.4 Gallery 자동 순회

모든 Definition을 다음 조건으로 자동 재생한다.

1. Low.
2. Medium.
3. High.
4. 검은 배경.
5. 흰 배경.
6. 16:9.
7. 20:9.
8. Time Scale 0/1.
9. 10회 연속.

Screenshot 비교 또는 사람이 빠르게 회귀 검수할 수 있게 한다.

---

<a id="section-36"></a>
# 36. 문제 해결 사전

## 36.1 Particle이 보이지 않는다

확인 순서:

1. GameObject Active.
2. ParticleSystem이 Play 상태.
3. Main의 Start Color Alpha.
4. Color over Lifetime Alpha.
5. Material/Shader.
6. Sorting Layer/Order.
7. Camera Culling Mask.
8. Renderer Enabled.
9. Simulation Space/Scale.
10. Shape 위치.
11. Material의 Blend/Alpha Clip.
12. Shader Graph Property Reference.
13. Particle Vertex Streams.
14. UI Particle 컴포넌트 Refresh.
15. Mask/RectMask2D 범위.

## 36.2 Editor에서는 보이는데 Android에서 분홍색

가능 원인:

- Shader가 빌드에서 Strip.
- URP Target/Sub Target 불일치.
- Shader Graph 컴파일 오류.
- 지원되지 않는 Shader Model/기능.
- 패키지 버전 호환 문제.
- Material이 Editor 전용 Shader 참조.

조치:

- Android 빌드 로그 확인.
- Shader Variant/Stripping 로그.
- VFX Gallery에서 실제 Material 사용.
- 문제 Shader를 무조건 Always Included에 넣기 전 원인 확인.
- OpenGLES3/Vulkan 각각 테스트.

## 36.3 검은/흰 사각 테두리가 보인다

원인:

- 투명 픽셀 RGB Bleeding.
- 잘못된 Blend.
- Premultiply 불일치.
- Atlas Padding 부족.
- 압축 Artifact.

조치:

- Alpha Dilate.
- Material Blend 확인.
- Texture Import Alpha 설정.
- Atlas Padding.
- ASTC Block 크기 개선.

## 36.4 Additive 효과가 흰 배경에서 사라진다

Additive는 이미 밝은 배경에 더할 여지가 적다.

대체:

- 어두운 외곽 Shape를 Alpha Blend로 추가.
- 흰색이 아닌 고채도 중간 밝기 Color.
- Outline/실루엣 강화.
- 잠깐의 Dark Backplate.
- Alpha Blend Core + Additive Glow의 두 레이어.

## 36.5 UI Particle이 마스크 밖에 보인다

확인:

- ParticleEffectForUGUI가 올바른 Canvas/Mask 아래에 있는가.
- Mask와 RectMask2D 지원 설정.
- Particle Material이 UI clipping을 지원하는가.
- UIParticle Renderer/Material replacement가 적용됐는가.
- Nested Canvas가 정렬을 분리하는가.
- Trail Material도 마스크를 지원하는가.

## 36.6 카드별 Shader 값이 모두 같이 바뀐다

원인:

- Shared Material 값을 직접 변경.
- uGUI Image에서 공용 Material 사용.
- MaterialPropertyBlock 미적용.

SpriteRenderer:

- MaterialPropertyBlock 사용.

uGUI:

- UIEffect parameter.
- 상태별 Shared Material.
- 표시 카드 수만큼 Material Pool.
- Custom vertex data 구조.

## 36.7 `renderer.material` 때문에 Material이 늘어난다

- `renderer.material`은 인스턴스를 만들 수 있다.
- 조회만 하려면 `sharedMaterial`을 검토.
- 카드별 값은 MPB.
- 생성한 Material은 명시적으로 Destroy하고 Pool/Cache.
- Memory Profiler에서 Material 이름 뒤 `(Instance)` 수를 확인.

## 36.8 첫 재생만 끊긴다

가능 원인:

- Shader Variant 첫 컴파일/생성.
- Particle 프리팹 첫 Instantiate.
- Texture/Addressable 첫 로드.
- Animator Controller 첫 초기화.
- Audio Clip Decompress.

조치:

- 로딩 화면에서 풀 Prewarm.
- 필요한 Asset 사전 로드.
- VFX Gallery 경로로 주요 Variant Warm-up 검토.
- 첫 전투 직전 분산 준비.
- 모든 것을 앱 시작 한 프레임에 준비하지 않는다.

## 36.9 풀링 후 Trail이 과거 위치에서 길게 연결된다

- 반환 시 `TrailRenderer.Clear()`.
- Particle Trail도 Stop/Clear.
- 위치 변경 전에 Trail emitting Off.
- 위치 적용 후 다음 프레임부터 emitting.
- World Simulation Particle 잔여 확인.

## 36.10 Particle이 풀로 돌아오지 않는다

확인:

- Looping On.
- Sub Emitter가 계속 살아 있음.
- Lifetime이 매우 김.
- Animator-only인데 Hard Lifetime이 0.
- Time Scale 0인데 Scaled Time으로 수명 계산.
- Callback 누락.
- 비활성화된 상태에서 Update가 멈춤.

Hard Lifetime 안전망을 둔다.

## 36.11 풀로 너무 빨리 돌아온다

- 최초 프레임 `IsAlive` 검사 유예.
- 자식 ParticleSystem 캐시 누락.
- Delayed Burst가 아직 시작 전인데 IsAlive false.
- Animator-only 프리팹.
- ParticleSystem이 `Play On Awake Off`이고 Service가 Play하지 않음.

Delayed Burst가 긴 프리팹은 Hard Lifetime 또는 명시적인 완료 신호를 사용한다.

## 36.12 카드 이동 시 Particle이 뒤틀린다

- Local/World Simulation Space.
- 부모의 비균일 Scale.
- Canvas Scale Factor.
- UIParticle의 Position/Scale mode.
- Stretched Billboard 속도.
- 이동 중 Sorting.

카드 Root에 비균일 Scale 애니메이션을 하고 Particle 자식도 같이 Scale되는 구조를 피하고, VFX 전용 보정 Root를 둔다.

## 36.13 Distortion이 2D Renderer에서 작동하지 않는다

- 해당 Renderer Feature가 일반 Forward Renderer용인지 확인.
- Opaque Texture/Camera Color Copy 필요 여부.
- 2D Renderer Data에 Feature를 넣을 수 있는지.
- Render Pass Event.
- Android Graphics API.

대체:

- 카드/입자 UV 자체 Distortion.
- 작은 사전 제작 Wobble Sprite.
- 전체 화면 샘플링 제거.

## 36.14 Bloom이 카드 전체를 번지게 한다

- Threshold가 너무 낮음.
- 카드 원화/텍스트에 HDR 값.
- UI와 World가 같은 Volume.
- Bloom Dirt/강도 과다.

조치:

- Effect 전용 HDR 영역만.
- 카메라/레이어 구조 재검토.
- 가짜 Glow Sprite.
- Low/Medium에서 Bloom Off.

## 36.15 Shader Graph가 너무 느리다

검사:

- Texture Sample 수.
- Noise 노드 중복.
- Branch/Keyword.
- Screen Color/Depth Sample.
- 8 Tap Outline.
- Sin/Cos 반복.
- Full precision 남용.
- 큰 화면 면적과 Overdraw.

Sub Graph가 같은 계산을 여러 번 수행하지 않는지 Generated Shader/Profiler로 확인한다.

## 36.16 UIEffect Blur가 느리다

- Blur 반경.
- 대상 Rect 크기.
- 여러 겹.
- 실시간 갱신.
- 카드 리스트 전체 적용.

대체:

- 사전 Blur Sprite.
- 배경 Dim.
- 작은 영역만 Blur.
- 팝업 진입 중 한 번만.

## 36.17 숫자 카운터가 VFX와 어긋난다

- 실제 데이터와 표시값 분리.
- 아이콘 도착 Callback 누락.
- Skip 처리.
- 화면 전환 중 Tween 취소.
- 여러 보상 Queue 순서.

서버/실제 값은 즉시 확정하고 표시값만 애니메이션한다.

## 36.18 Android에서 색이 다르다

- Color Space.
- HDR 지원/Format.
- Texture Compression.
- 기기 Display 색 모드.
- Bloom/Tone Mapping.
- sRGB Flag.

한 기기 결과를 절대 색 기준으로 삼지 말고 여러 제조사에서 허용 범위를 정한다.

## 36.19 Vulkan에서만 깨진다

- Shader precision/undefined behavior.
- Renderer Feature.
- Native plugin.
- Driver 특정 문제.
- Graphics Jobs/Multithreaded Rendering.

OpenGLES3와 비교하고 Unity 패치 Release Notes 및 기기별 Crash를 확인한다. 특정 기기군에 API fallback을 제공할 수 있게 설계한다.

## 36.20 패키지 업데이트 후 컴파일 실패

1. `manifest.json`과 `packages-lock.json` Diff.
2. Unity Editor 호환 범위.
3. 패키지 Tag가 실제 Release인지.
4. API Breaking Change.
5. Sample/Editor 코드 충돌.
6. Library 삭제는 마지막 수단.
7. 깨끗한 브랜치에서 재현.
8. 이전 Tag로 즉시 Rollback 가능하게.

---

<a id="section-37"></a>
# 37. 구현 로드맵

## Phase 0 — 기준선

- Unity `6000.3.20f1` 별도 브랜치 검증.
- Android API 36 빌드.
- Tier L/M/H 테스트 기기 확보.
- 현재 Frame Time/메모리 기준선 기록.
- `VFX_Gallery` 씬 생성.

완료 조건:

- 기존 게임이 새 Editor에서 동일하게 동작.
- OpenGLES3 ARM64 빌드 통과.
- Vulkan 비교 빌드 통과 또는 알려진 문제 기록.

## Phase 1 — 기반 시스템

- `VfxDefinition`.
- `VfxService`.
- Pooling.
- Quality Tier.
- Debug HUD.
- Sorting Layer/Folder/Naming 규칙.
- Audit 도구.

완료 조건:

- 일반 VFX 100회 연속 재생에 반복 GC Alloc 없음.
- 풀 고갈이 안전하게 처리.
- Low/Medium/High 전환.

## Phase 2 — 공통 Shader 라이브러리

- Card Unlit/Lit.
- Foil Overlay.
- Dissolve.
- Shine.
- Selection Outline.
- Status Overlay.
- Particle Alpha/Additive/Masked.
- 공통 Sub Graph.

완료 조건:

- 카드별 Material Clone 없이 SpriteRenderer 상태 변경.
- Android에서 모든 Graph 정상.
- Variant 수 기록.

## Phase 3 — UI VFX

- ParticleEffectForUGUI 도입.
- UIEffect 도입.
- 버튼/탭/팝업.
- Reward Fly Icon.
- HUD Pulse.
- Mask/Sorting 규칙.

완료 조건:

- 별도 Camera/RenderTexture 없이 카드 UI 파티클.
- 리스트/스크롤에서 누수 없음.
- Canvas Rebuild 측정.

## Phase 4 — 카드 핵심 이벤트

우선순위:

1. 선택.
2. 드래그.
3. 사용.
4. 뽑기/드로우.
5. 획득 Common/Rare/Epic/Legendary.
6. 강화.
7. 합성.
8. 파괴.
9. 상태 이상.

완료 조건:

- 희귀도 색 없이도 강도 구분.
- Skip 지원.
- Low 변형 완비.

## Phase 5 — 전투 VFX

- 물리 Hit.
- Crit.
- Fire/Ice/Lightning/Poison.
- Heal/Shield/Buff/Debuff.
- Death/Spawn.
- Boss warning.

완료 조건:

- 게임플레이 판정과 완전히 분리.
- 동시 최악 상황 예산 통과.

## Phase 6 — 최적화/적응형 품질

- Thermal/Frame time Governor.
- Screen visibility culling.
- Global VFX limit.
- Shader warm-up.
- Texture compression variants.
- Memory unload 정책.

완료 조건:

- Tier L 30 FPS Soak.
- Tier M 60 FPS Soak.
- P90/P99 또는 P50/P95 기록.

## Phase 7 — 접근성/출시

- Reduced Motion.
- Reduced Flash.
- Shake/Haptic 옵션.
- 색각 검수.
- Play Console Reach and devices 분석.
- Third-party notices.
- CI audit.

완료 조건:

- QA 체크리스트 전체 통과.
- 릴리스 후보 성능 기록 첨부.

---

<a id="section-38"></a>
# 38. 패키지·라이선스·업데이트 운영

## 38.1 외부 패키지 기록

`THIRD_PARTY_NOTICES.md` 또는 프로젝트의 라이선스 목록에 기록한다.

```markdown
## NovaShader
- Repository: https://github.com/CyberAgentGameEntertainment/NovaShader
- Version/tag: 3.6.0
- Purpose: Particle shader
- License: 저장소 LICENSE 재확인
- Modified: Yes/No

## ParticleEffectForUGUI
- Repository: https://github.com/mob-sakai/ParticleEffectForUGUI
- Version/tag: 4.13.3
- Purpose: UI particle renderer
- License: 저장소 LICENSE 재확인
- Modified: Yes/No

## UIEffect
- Repository: https://github.com/mob-sakai/UIEffect
- Version/tag: 5.11.5
- Purpose: UI visual effects
- License: 저장소 LICENSE 재확인
- Modified: Yes/No
```

실제 라이선스 이름과 고지 요구는 설치 시점의 `LICENSE` 원문을 기준으로 한다.

## 38.2 업데이트 절차

1. Release Notes 읽기.
2. 현재 Tag와 새 Tag Diff.
3. Unity 6000.3/URP 호환성.
4. 별도 Branch.
5. 하나의 패키지만 업데이트.
6. VFX Gallery 회귀.
7. Android OpenGLES3/Vulkan.
8. Memory/Shader Variant 비교.
9. 패키지 Lockfile 커밋.
10. Rollback Tag 기록.

## 38.3 금지

- Release Notes 없이 자동 최신화.
- 여러 VFX 패키지를 한 PR에 동시에 업데이트.
- Git Tag가 아닌 Branch 고정.
- License 파일 삭제.
- Package 내부를 직접 수정하고 Patch 기록을 남기지 않음.
- 출처 불명 Shader 코드를 복사.

## 38.4 패키지 수정이 필요할 때

선택:

1. Upstream Issue/PR.
2. Git Fork + 고정 Commit/Tag.
3. Embedded Package + 변경 기록.
4. 프로젝트 Wrapper로 우회.

가능하면 Package 원본을 직접 수정하지 않고 Extension/Wrapper를 사용한다.

---

<a id="section-39"></a>
# 39. 참고 자료와 검증 기준일

> 마지막 온라인 검증일: **2026-07-27**. 정책·버전·패키지 Release는 변경될 수 있으므로 Unity 또는 스토어 배포 직전에 재확인한다.

## 39.1 Unity/Android 공식 자료

- Unity 6000.3.20f1 Release Notes:  
  https://unity.com/releases/editor/whats-new/6000.3.20f1
- Unity 6.3 Android 요구사항과 호환성:  
  https://docs.unity3d.com/6000.3/Documentation/Manual/android-requirements-and-compatibility.html
- URP Rendering Debugger:  
  https://docs.unity3d.com/6000.3/Documentation/Manual/urp/features/rendering-debugger-reference.html
- Android Distribution Dashboard:  
  https://developer.android.com/about/dashboards
- Google Play Target API 요구사항:  
  https://developer.android.com/google/play/requirements/target-sdk
- Android Slow Sessions/Frame Pacing:  
  https://developer.android.com/games/optimize/vitals/slow-session
- Android Frame Rate 측정:  
  https://developer.android.com/games/optimize/framerate
- Android 전력 최적화:  
  https://developer.android.com/games/optimize/power

## 39.2 VFX 패키지

- NovaShader:  
  https://github.com/CyberAgentGameEntertainment/NovaShader
- NovaShader Releases:  
  https://github.com/CyberAgentGameEntertainment/NovaShader/releases
- ParticleEffectForUGUI:  
  https://github.com/mob-sakai/ParticleEffectForUGUI
- ParticleEffectForUGUI Releases:  
  https://github.com/mob-sakai/ParticleEffectForUGUI/releases
- UIEffect:  
  https://github.com/mob-sakai/UIEffect
- UIEffect Releases:  
  https://github.com/mob-sakai/UIEffect/releases

## 39.3 학습용 저장소

- Unity URP Toon Lit Shader Example:  
  https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
- Unity Visual Effect Graph Samples:  
  https://github.com/Unity-Technologies/VisualEffectGraph-Samples
- Unity VFX Toolbox:  
  https://github.com/Unity-Technologies/VFXToolbox
- Boat Attack URP sample:  
  https://github.com/Unity-Technologies/BoatAttack
- Sprite Glow:  
  https://github.com/elringus/sprite-glow
- Awesome Unity Open Source:  
  https://github.com/baba-s/awesome-unity-open-source-on-github

## 39.4 AI/Editor 자동화 참고

- Coplay Unity MCP:  
  https://github.com/CoplayDev/unity-mcp
- Ivan Murzak Unity MCP:  
  https://github.com/IvanMurzak/Unity-MCP
- Unity AI ParticleSystem:  
  https://github.com/IvanMurzak/Unity-AI-ParticleSystem
- Unity Skills:  
  https://github.com/Besty0728/Unity-Skills

외부 저장소의 Star 수는 품질 보증이 아니다. 유지보수 상태, License, Issues, 실제 프로젝트 호환성을 함께 평가한다.

---

<a id="section-40"></a>
# 40. 용어집

| 용어 | 의미 |
|---|---|
| Alpha Blend | 투명도를 이용해 배경과 혼합하는 방식 |
| Additive | 색을 더해 빛처럼 보이게 하는 혼합 |
| AAB | Android App Bundle |
| ASTC | Android에서 널리 쓰이는 블록 기반 Texture 압축 |
| Batch | 여러 렌더 대상을 묶어 처리하는 단위 |
| Bloom | 밝은 픽셀 주변을 번지게 하는 후처리 |
| Burst | 특정 시각에 여러 Particle을 한 번에 방출 |
| Canvas Rebuild | uGUI Geometry/Batch를 다시 만드는 작업 |
| Custom Data | Particle에서 Shader로 전달하는 사용자 값 |
| Dissolve | Noise/Mask Threshold로 사라지거나 나타나는 효과 |
| Draw Call | GPU에 렌더 명령을 제출하는 호출 |
| ETC2 | OpenGL ES 3 계열의 표준 Texture 압축 |
| Flipbook | 여러 Animation Frame을 한 Texture Sheet에 배치 |
| Foil | 카드 표면의 무지개/금속 반사형 연출 |
| Frame Pacing | Frame 표시 간격을 일정하게 유지하는 것 |
| GPU Instancing | 같은 Mesh/Material을 여러 인스턴스로 효율 렌더링 |
| HDR | 1보다 큰 밝기 값을 표현하는 범위 |
| IL2CPP | C# IL을 C++로 변환하는 Unity Backend |
| Mask | 효과가 적용될 영역을 지정하는 Texture/Stencil |
| MaterialPropertyBlock | Material Clone 없이 Renderer별 속성을 전달하는 방식 |
| Noise | 불규칙한 패턴/움직임을 만드는 함수 또는 Texture |
| Overdraw | 같은 화면 픽셀을 여러 번 그리는 현상 |
| Pooling | Object를 파괴하지 않고 재사용하는 구조 |
| Premultiplied Alpha | RGB에 Alpha가 미리 곱해진 혼합 표현 |
| Renderer Feature | URP 렌더 과정에 Pass를 추가하는 기능 |
| SDF | 도형 경계까지의 거리를 저장/계산하는 표현 |
| Shader Variant | Keyword/설정 조합으로 생성되는 Shader 변형 |
| Soft Particle | Depth를 사용해 교차 경계를 부드럽게 하는 Particle |
| SRP Batcher | SRP에서 Material 데이터를 효율 처리하는 Batching |
| Stencil | 픽셀 단위 렌더 허용 영역을 제어하는 버퍼 |
| Sub Emitter | Particle 사건에서 다른 ParticleSystem을 방출 |
| Trail | 움직임 뒤에 남는 띠/선 |
| URP | Universal Render Pipeline |
| VFX | Visual Effects |
| VFX Tier | Low/Medium/High 시각 품질 단계 |

---

<a id="section-41"></a>
# 41. 빠른 치트시트

## 41.1 이펙트 하나를 만들 때

```text
의미 정의
→ World/UI 선택
→ Timeline 작성
→ Core/Shape/Detail 레이어 분해
→ 기존 Material/Texture 확인
→ Low부터 제작
→ Medium/High 추가
→ 풀링 Definition
→ Gallery 등록
→ 실제 Android 프로파일
→ 접근성 검수
```

## 41.2 카드 획득

```text
Common:   Flash + Border + 5~12 Spark
Rare:     + Ring + Shine + 10~24 Spark
Epic:     + Anticipation + Ribbon/Glyph + 20~48 Spark
Legendary:+ Dim + Charge + Reveal + Foil + 36~90 Spark
Low:      Distortion/Light/Bloom 제거, 형태와 타이밍 유지
```

## 41.3 모바일에서 먼저 끌 것

```text
Distortion
Particle Lights
Noise
Collision
Secondary Sub Emitters
Extra Trails
Bloom
Large transparent overlays
```

## 41.4 절대 남길 것

```text
게임플레이 핵심 형태
Impact 타이밍
경고 영역
상태 아이콘/텍스트
희귀도 형태 차이
사용자 입력 피드백
```

## 41.5 Shader Graph 최적화

```text
Texture Samples 줄이기
중복 Noise 계산 합치기
거대 Master Graph 피하기
동적 Branch 피하기
Mask 채널 패킹
Half precision 검토
Screen Color/Depth 최소화
Variant 수 기록
```

## 41.6 Particle 최적화

```text
Max Particles 명시
Burst 수 계산
Lifetime 단축
큰 투명 Quad 줄이기
Noise/Collision/Light 제한
Trail Clear
화면 밖 중지
Pool 사용
```

---

<a id="section-42"></a>
# 42. 최종 마스터 체크리스트

## 프로젝트

- [ ] Unity 6000.3 최신 검증 패치 Branch 테스트.
- [ ] URP 2D Renderer Asset 백업.
- [ ] API 36 Target.
- [ ] Minimum API 정책 확정.
- [ ] ARM64/IL2CPP.
- [ ] OpenGLES3/Vulkan 기기 행렬.
- [ ] Optimized Frame Pacing.
- [ ] Low/Medium/High Quality.

## 패키지

- [ ] NovaShader Tag 고정.
- [ ] ParticleEffectForUGUI 안정 Tag 고정.
- [ ] UIEffect 안정 Tag 고정.
- [ ] License 기록.
- [ ] `packages-lock.json` 커밋.
- [ ] Preview 패키지 사용 사유 기록.

## 아키텍처

- [ ] `VfxDefinition`.
- [ ] `VfxService`.
- [ ] Object Pool.
- [ ] Global/per-effect 동시 제한.
- [ ] 중요도/Drop 정책.
- [ ] Quality Gate.
- [ ] 화면 밖 Culling.
- [ ] Resume/Low Memory 처리.

## 카드

- [ ] 선택.
- [ ] 드래그.
- [ ] 타겟 가능/불가.
- [ ] 사용.
- [ ] Draw/Flip.
- [ ] 피격.
- [ ] 회복.
- [ ] Shield.
- [ ] 상태 이상.
- [ ] 강화.
- [ ] 합성.
- [ ] 파괴.
- [ ] 획득 Common/Rare/Epic/Legendary.
- [ ] Skip/10회 뽑기.

## Shader

- [ ] Card Unlit/Lit.
- [ ] Foil Overlay.
- [ ] Shine.
- [ ] Dissolve.
- [ ] Outline/Selection.
- [ ] Status Overlay.
- [ ] Particle Alpha/Additive/Masked.
- [ ] Packed Mask 규격.
- [ ] MPB Property 이름.
- [ ] Variant/Warm-up.

## Particle

- [ ] Max Particles.
- [ ] Loop 확인.
- [ ] Lifetime.
- [ ] Noise Low Off.
- [ ] Collision 최소.
- [ ] Lights 최소.
- [ ] Trail Clear.
- [ ] Sub Emitter 총량.
- [ ] Sorting.
- [ ] Material.
- [ ] Custom Vertex Streams.

## UI

- [ ] 별도 Camera/RT 최소화.
- [ ] Canvas Mask.
- [ ] RectMask2D.
- [ ] Raycast 차단 없음.
- [ ] Canvas Rebuild 측정.
- [ ] Reward Fly pooling.
- [ ] HUD 도착 Pulse.
- [ ] Safe Area.
- [ ] 화면비.

## 성능

- [ ] Tier L 30 FPS.
- [ ] Tier M 60 FPS.
- [ ] Frame Time percentile.
- [ ] Burst Spike.
- [ ] GC Alloc.
- [ ] Overdraw.
- [ ] Draw Call/Batch.
- [ ] Texture Memory.
- [ ] Shader Memory.
- [ ] Pool Memory.
- [ ] Thermal Soak.
- [ ] Low Memory.

## 접근성

- [ ] Reduced Motion.
- [ ] Shake Off.
- [ ] Reduced Flash.
- [ ] Haptic Off.
- [ ] 색각 구분.
- [ ] 텍스트 가독성.
- [ ] Skip.

## 배포

- [ ] AAB.
- [ ] Texture Compression Targeting.
- [ ] 16 KB page native plugin 확인.
- [ ] API 정책 재확인.
- [ ] Internal Testing.
- [ ] Android Vitals.
- [ ] Reach and devices.
- [ ] Third-party notice.
- [ ] 성능 기록 첨부.

---

# 문서 종료 원칙

이 문서의 목표는 “Particle을 많이 뿌리는 게임”이 아니다.

좋은 모바일 2D VFX는 다음을 동시에 만족한다.

1. 플레이어가 결과를 즉시 이해한다.
2. 카드와 보상의 가치가 분명히 느껴진다.
3. 낮은 사양에서도 핵심 형태와 타이밍이 유지된다.
4. 반복 재생에도 Frame Time이 안정적이다.
5. 코드와 Shader, Texture가 재사용 가능하다.
6. 접근성 옵션에서도 게임 정보가 손실되지 않는다.
7. 실제 Android 기기 데이터로 계속 개선된다.

**먼저 의미를 만들고, 그다음 형태를 만들고, 마지막에 빛과 입자를 더한다.**
