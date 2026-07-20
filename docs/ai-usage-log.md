# AI 활용 내역 로그

> 제출물 4번(AI 활용 기술 문서)의 원본 자료. 개발 중 AI 도구를 사용할 때마다 즉시 기록한다.

## 외부 에셋 · 오픈소스 출처

| 항목 | 출처 | 라이선스 |
|---|---|---|
| 캐릭터/배경 아트 | 팀 자체 제작 (AI 보조 드로잉 후 수작업 검수) | 자체 저작물 |
| Unity 패키지 | Unity Technologies (URP, Input System 등 공식 패키지) | Unity Companion License |

---

### 2026-07-20 — 프로젝트 기획 및 아트 시안

- 사용 도구: Claude (기획 문서화), AI 이미지 보조 (배경 산수화 시안 v1~v3)
- 목적: 게임 컨셉 확정, 캐릭터 '먹방울이' 및 세로 산수화 배경 시안 제작
- 주요 프롬프트/지시: 수묵화 스타일 세로 산수화 배경, 하단 한지 여백(플레이 공간) 유지,
  숯검댕이를 먹빛으로 재해석한 캐릭터(다리만 있음, 눈만으로 표정 표현)
- 결과물: `Assets/Art/Background/background_ink_landscape.png`,
  `Assets/Art/Character/character_muk_bangul_v3.png`, `muk_jump_hackathon_final.pptx`
- 사람의 수정/검토 내용: v1(구름형) → v2(능선+붓나무) → v3(소나무형) 시안 비교 후 최종본 선정,
  팔레트(INK/PAPER/RED 낙관) 직접 확정

### 2026-07-20 — 코어 루프 스크립트 초기 구현

- 사용 도구: Claude Code (터미널)
- 목적: 자동 점프, 터치 스트로크 → 발판 생성, 게임 루프(추락/재도전/점수) 스크립트 작성
- 주요 프롬프트/지시: CLAUDE.md의 4~7절 설계(자동 점프 주기, 발판 각도·길이 → 궤적 반영,
  스트로크 캡처 → 스무딩 → EdgeCollider, AI 변환 + 폴백 구조)를 그대로 구현하도록 지시
- 결과물: `Assets/Scripts/{Core,Player,Drawing,AI}` 하위 스크립트, 씬 빌더 에디터 스크립트
  (feature/core-loop 브랜치 커밋 이력 참고)
- 사람의 수정/검토 내용: (Unity 에디터에서 플레이 테스트 후 물리 파라미터 튜닝 예정 — 추후 기록)
