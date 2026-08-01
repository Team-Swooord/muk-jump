# 영구 성장 먹나무 배경 v3 생성 기록

- 생성 도구: OpenAI ImageGen
- 입력 이미지: `pg_tree_background_v2.png`
- 생성 원본: `pg_tree_background_v3_chroma.png`
- 게임용 투명본:
  `Assets/Resources/MukJump/UI/PermanentGrowth/pg_tree_background_v3.png`

## 편집 프롬프트

```text
Edit the supplied permanent-growth tree into a clean Unity 2D mobile UI
background sprite. Preserve the same single ancient Korean ink-painting tree,
its twisting trunk, dry-brush texture, black and charcoal palette, casual-game
simplicity, and portrait composition. Repaint every root and branch tip so it
ends naturally inside the canvas. Leave generous, visibly empty padding on all
four sides; no trunk, root, branch, or ink wash may touch or be cropped by an
edge. Remove every pale rectangular patch, clipped image boundary, background
paper, halo, shadow, mist, text, number, icon, node, flower, fruit, circle,
seal, red, gold, animal, or scenery.

Use one perfectly flat solid #00ff00 chroma-key background behind the tree,
with no gradient, texture, lighting, or green reflected into the tree. Keep the
whole silhouette centered and fully visible at 1024x1536. The result must remain
a single connected, organically asymmetric Korean sumi-e tree suitable for
placing circular upgrade nodes on top.
```

## 후처리와 검수

ImageGen 결과에 `remove_chroma_key.py`의 border 자동 키 추정, soft matte,
despill을 적용했다. 최종 파일은 1024×1536 RGBA이고 비투명 픽셀 경계는
`x=52..955`, `y=65..1460`이다. 최외곽 네 변은 전부 alpha 0이며 녹색 작업색과
옅은 사각 배경이 남지 않았는지 자동 검사했다. 런타임은 이 이미지를
`preserveAspect=true`로 표시한다.
