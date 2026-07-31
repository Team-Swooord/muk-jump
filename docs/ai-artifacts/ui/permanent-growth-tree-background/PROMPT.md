# 영구 성장 큰 먹나무 배경 생성 기록

- 생성 방식: OpenAI 기본 이미지 생성 + 로컬 chroma-key 제거
- 참고 이미지 1: `Assets/Resources/MukJump/UI/PermanentGrowth/pg_tree_trunk.png`
- 참고 이미지 2: `docs/ai-artifacts/ui/permanent_growth_tree_concept_v1.png`
- 생성 원본: `pg_tree_background_v2_chroma.png`
- 게임용 투명본:
  `Assets/Resources/MukJump/UI/PermanentGrowth/pg_tree_background_v2.png`

## 최종 프롬프트

```text
Use case: stylized-concept
Asset type: Unity 2D mobile permanent-growth tree background sprite,
portrait orientation
Input images: Image 1 is the exact ink-brush texture and old gnarled
trunk style to preserve; Image 2 is layout reference only for a large
tree carrying upgrade nodes. Ignore all UI, text, numbers, icons,
panels, red marks, and fruits in Image 2.

Create one complete giant ancient Korean ink-painting tree that fills a
tall portrait canvas. It must read immediately as a single connected
background tree, not a flowchart: heavy rooted base at bottom center,
one broad twisting trunk, then many natural thick limbs splitting
repeatedly toward upper-left, upper-center, upper-right, and both side
directions. Include enough broad branch surfaces and smaller forks for
about 39 circular fruit nodes to be overlaid later. Keep open negative
gaps between limbs so node labels remain readable.

Use restrained Korean sumi-e / watercolor ink on hanji, matching the
old trunk: casual-game simplicity, a strong readable silhouette, black
and charcoal gray only, dry-brush edges, moderate texture, not
photorealistic and not overly detailed. Keep the whole tree, roots, and
all branch tips visible with generous padding. The tree should feel
organically asymmetric while remaining balanced overall.

Use a perfectly flat solid #00ff00 chroma-key background with no
gradient, texture, floor, shadow, mist, or lighting variation. Do not
use #00ff00 in the tree. No text, numbers, UI, fruit, flowers, glowing
nodes, circles, red, gold, seals, Japanese motifs, people, animals,
scenery, cast shadow, or watermark.
```

## 후처리

`remove_chroma_key.py`의 border 자동 키 추정, soft matte, despill을
사용했다. 최종 PNG는 1024×1536 RGBA이며 네 모서리와 나무 사이 여백이
투명하다. 빨강과 금색은 포함하지 않고 검정·회색 먹색만 유지했다.
