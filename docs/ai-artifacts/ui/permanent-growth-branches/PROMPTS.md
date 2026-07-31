# 영구 성장 모듈형 나뭇가지 생성 기록

## 공통 참조

- `docs/ai-artifacts/ui/permanent_growth_tree_concept_v1.png`
- `Assets/Resources/MukJump/UI/PermanentGrowth/pg_tree_trunk.png`
- 먼저 생성한 `pg_branch_piece_01.png`를 02~06의 스타일 잠금 참조로 재사용

## 공통 프롬프트

> 먹점프의 모바일 영구 성장 나무에 붙일 독립형 모듈 나뭇가지 스프라이트를
> 생성한다. 첨부한 먹나무 줄기와 정확히 같은 한국 수묵 수채화 붓결, 종이에
> 스며든 검정·회색 먹 농담, 거칠고 자연스러운 가장자리를 유지한다. 하나의
> 나뭇가지만 화면 중앙에 두고 시작 단면은 왼쪽, 가늘어지는 끝은 오른쪽을 향한다.
> 잎, 꽃, 열매, 성장 노드, 아이콘, 글자, 테두리, 그림자, 광택, 3D 표현은 넣지
> 않는다. 배경은 후처리용 완전 균일한 `#00FF00` 단색이며 가지와 접촉 그림자나
> 초록 반사는 없어야 한다.

## 형태별 추가 지시

1. `pg_branch_piece_01`: 완만하게 위로 휘는 굵은 기본 가지. 캔버스 폭 84%,
   높이 34% 이내.
2. `pg_branch_piece_02`: 아래로 내려갔다 다시 오르는 S자 가지.
3. `pg_branch_piece_03`: 짧고 단단하며 거의 직선인 연결 가지.
4. `pg_branch_piece_04`: 하나의 굵은 밑동에서 위·아래 두 끝으로 갈라지는 Y자
   분기 가지.
5. `pg_branch_piece_05`: 긴 주가지 중간에서 위쪽으로 짧은 곁가지 하나가
   솟는 형태.
6. `pg_branch_piece_06`: 성장 계보 끝에 붙이는 짧고 가는 종결 가지.

## 후처리

OpenAI 기본 이미지 생성으로 만든 원본은 이 폴더의
`pg_branch_piece_01_chroma.png`부터 `06_chroma.png`까지 보존했다. 프로젝트용
RGBA는 `remove_chroma_key.py --auto-key border --soft-matte
--transparent-threshold 12 --opaque-threshold 220 --despill`로 녹색 배경을
제거해 `Assets/Resources/MukJump/UI/PermanentGrowth/`에 저장했다.
