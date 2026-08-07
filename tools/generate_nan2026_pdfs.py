#!/usr/bin/env python3
"""NAN 2026 제출용 PDF 3종을 재현 가능하게 생성한다.

실행:
  /Users/seungyeoning/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3.12 \
    tools/generate_nan2026_pdfs.py

문서의 상세 원문은 docs/submission/*.md에 보존한다. 이 스크립트는 제출용 편집 디자인과
요약 문구를 담당하며, 실제 게임 스크린샷과 저장소의 프로젝트 자산만 사용한다.
"""

from __future__ import annotations

from io import BytesIO
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image, ImageEnhance, ImageOps
from reportlab.lib.colors import Color, HexColor
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "pdf"
ASSET = ROOT / "docs" / "submission" / "assets"

W, H = A4
M = 46

INK = HexColor("#1C1B1A")
INK_SOFT = HexColor("#34312D")
PAPER = HexColor("#F3EEDF")
PAPER_2 = HexColor("#E7DFCC")
RED = HexColor("#AE1C3C")
MUTED = HexColor("#6B655B")
LIGHT_MUTED = HexColor("#BEB6A7")
WHITE = HexColor("#FBF8F0")
GOLD = HexColor("#A48347")

FONT_PATH = Path("/System/Library/Fonts/Supplemental/AppleGothic.ttf")
FONT = "MukBody"

GAMEPLAY = ASSET / "gameplay_phone.png"
TUTORIAL = ASSET / "tutorial_phone.png"
GAME_OVER = ASSET / "game_over_phone.png"
LOGO = ROOT / "Assets" / "Art" / "UI" / "muk_logo.png"
PLAYER = ROOT / "Assets" / "Art" / "Character" / "Player" / "character_muk_bangul_v3.png"
MAP_0 = ROOT / "Assets" / "Art" / "Background" / "Maps" / "map_00_quiet_mountain.png"
TREE = ROOT / "Assets" / "Resources" / "MukJump" / "UI" / "PermanentGrowth" / "pg_tree_background_v3.png"
GAUGE_TRACK = ROOT / "Assets" / "Art" / "UI" / "muk_gauge_track.png"
GAUGE_FILL = ROOT / "Assets" / "Art" / "UI" / "muk_gauge_fill.png"
ITEMS = [
    (ROOT / "Assets" / "Art" / "UI" / "ink_drop.png", "먹물방울", "먹떼 50m 상승"),
    (ROOT / "Assets" / "Art" / "UI" / "golden_brush.png", "황금 붓", "초과 획 퇴출 보류"),
    (ROOT / "Assets" / "Art" / "UI" / "ink_shield.png", "먹 방어막", "피해·추락 1회 방어"),
    (ROOT / "Assets" / "Art" / "UI" / "ink_clone.png", "먹분신", "옆에 새 생존자 생성"),
]
DRAGON = ROOT / "Assets" / "Resources" / "MukJump" / "Obstacles" / "child_ink_dragon_4frame_v3.png"
HAETAE = ROOT / "Assets" / "Resources" / "MukJump" / "Obstacles" / "child_ink_haetae_4frame_v2.png"

_image_cache: dict[tuple, ImageReader] = {}


def register_fonts() -> None:
    if not FONT_PATH.exists():
        raise FileNotFoundError(f"Korean font not found: {FONT_PATH}")
    pdfmetrics.registerFont(TTFont(FONT, str(FONT_PATH)))


def rgb_tuple(color: Color) -> tuple[int, int, int]:
    return tuple(round(channel * 255) for channel in (color.red, color.green, color.blue))


def prepared_image(
    path: Path,
    size: tuple[int, int],
    *,
    mode: str = "cover",
    focal: tuple[float, float] = (0.5, 0.5),
    tint: Color | None = None,
    contrast: float = 1.0,
    quality: int = 88,
) -> ImageReader:
    """PDF 박스에 맞춘 압축 이미지를 메모리에서 생성한다."""
    key = (str(path), size, mode, focal, str(tint), contrast, quality)
    if key in _image_cache:
        return _image_cache[key]

    with Image.open(path) as source:
        image = source.convert("RGBA")
        if tint is not None:
            alpha = image.getchannel("A")
            solid = Image.new("RGBA", image.size, (*rgb_tuple(tint), 255))
            solid.putalpha(alpha)
            image = solid
        if contrast != 1.0:
            base = image.convert("RGB")
            base = ImageEnhance.Contrast(base).enhance(contrast)
            base.putalpha(image.getchannel("A"))
            image = base

        if mode == "cover":
            image = ImageOps.fit(
                image,
                size,
                method=Image.Resampling.LANCZOS,
                centering=focal,
            )
        else:
            image.thumbnail(size, Image.Resampling.LANCZOS)
            background = Image.new("RGBA", size, (255, 255, 255, 0))
            background.alpha_composite(
                image,
                ((size[0] - image.width) // 2, (size[1] - image.height) // 2),
            )
            image = background

        stream = BytesIO()
        if image.getchannel("A").getextrema() == (255, 255):
            image.convert("RGB").save(stream, format="JPEG", quality=quality, optimize=True)
        else:
            image.save(stream, format="PNG", optimize=True)
        stream.seek(0)
        reader = ImageReader(stream)
        reader._muk_stream = stream  # ImageReader 수명 동안 BytesIO 유지
        _image_cache[key] = reader
        return reader


def draw_image_cover(
    c: canvas.Canvas,
    path: Path,
    x: float,
    y: float,
    width: float,
    height: float,
    *,
    focal: tuple[float, float] = (0.5, 0.5),
    opacity: float = 1.0,
    contrast: float = 1.0,
) -> None:
    reader = prepared_image(
        path,
        (max(8, round(width * 2)), max(8, round(height * 2))),
        mode="cover",
        focal=focal,
        contrast=contrast,
    )
    c.saveState()
    if opacity < 1:
        c.setFillAlpha(opacity)
    c.drawImage(reader, x, y, width=width, height=height, mask="auto")
    c.restoreState()


def draw_image_contain(
    c: canvas.Canvas,
    path: Path,
    x: float,
    y: float,
    width: float,
    height: float,
    *,
    tint: Color | None = None,
    opacity: float = 1.0,
) -> None:
    reader = prepared_image(
        path,
        (max(8, round(width * 2)), max(8, round(height * 2))),
        mode="contain",
        tint=tint,
    )
    c.saveState()
    if opacity < 1:
        c.setFillAlpha(opacity)
    c.drawImage(reader, x, y, width=width, height=height, mask="auto")
    c.restoreState()


def split_token(token: str, font: str, size: float, max_width: float) -> list[str]:
    chunks: list[str] = []
    current = ""
    for ch in token:
        candidate = current + ch
        if current and pdfmetrics.stringWidth(candidate, font, size) > max_width:
            chunks.append(current)
            current = ch
        else:
            current = candidate
    if current:
        chunks.append(current)
    return chunks


def wrap_text(text: str, font: str, size: float, max_width: float) -> list[str]:
    lines: list[str] = []
    for paragraph in text.split("\n"):
        if not paragraph:
            lines.append("")
            continue
        words = paragraph.split(" ")
        current = ""
        for word in words:
            pieces = (
                split_token(word, font, size, max_width)
                if pdfmetrics.stringWidth(word, font, size) > max_width
                else [word]
            )
            for piece in pieces:
                candidate = piece if not current else f"{current} {piece}"
                if current and pdfmetrics.stringWidth(candidate, font, size) > max_width:
                    lines.append(current)
                    current = piece
                else:
                    current = candidate
        if current:
            lines.append(current)
    return lines


def draw_text(
    c: canvas.Canvas,
    text: str,
    x: float,
    y: float,
    width: float,
    *,
    size: float = 11,
    leading: float | None = None,
    color: Color = INK,
    align: str = "left",
    max_lines: int | None = None,
) -> float:
    leading = leading or size * 1.48
    lines = wrap_text(text, FONT, size, width)
    if max_lines is not None:
        lines = lines[:max_lines]
    c.saveState()
    c.setFillColor(color)
    c.setFont(FONT, size)
    cursor = y
    for line in lines:
        if align == "center":
            c.drawCentredString(x + width / 2, cursor, line)
        elif align == "right":
            c.drawRightString(x + width, cursor, line)
        else:
            c.drawString(x, cursor, line)
        cursor -= leading
    c.restoreState()
    return cursor


def draw_title(
    c: canvas.Canvas,
    title: str,
    x: float,
    y: float,
    width: float,
    *,
    dark: bool,
    size: float = 31,
) -> float:
    return draw_text(
        c,
        title,
        x,
        y,
        width,
        size=size,
        leading=size * 1.18,
        color=WHITE if dark else INK,
    )


def draw_kicker(c: canvas.Canvas, text: str, x: float, y: float, *, dark: bool) -> None:
    c.setFillColor(RED)
    c.circle(x + 4, y + 3, 3.2, fill=1, stroke=0)
    c.setFillColor(LIGHT_MUTED if dark else MUTED)
    c.setFont(FONT, 8.5)
    c.drawString(x + 15, y, text)


def draw_rule(c: canvas.Canvas, x: float, y: float, width: float, *, dark: bool, red: bool = False) -> None:
    c.setStrokeColor(RED if red else (Color(1, 1, 1, 0.24) if dark else Color(0.11, 0.105, 0.1, 0.2)))
    c.setLineWidth(0.8)
    c.line(x, y, x + width, y)


def draw_footer(c: canvas.Canvas, label: str, page: int, total: int, *, dark: bool) -> None:
    c.setFillColor(LIGHT_MUTED if dark else MUTED)
    c.setFont(FONT, 7.6)
    c.drawString(M, 24, f"최연소밴드 · {label}")
    c.drawRightString(W - M, 24, f"{page} / {total}")


def page_base(c: canvas.Canvas, *, dark: bool, label: str, page: int, total: int) -> None:
    c.setFillColor(INK if dark else PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    if not dark:
        draw_image_cover(c, MAP_0, 0, 0, W, H, focal=(0.5, 0.6), opacity=0.055)
    draw_footer(c, label, page, total, dark=dark)


def draw_phone(c: canvas.Canvas, path: Path, x: float, y: float, width: float, height: float) -> None:
    c.setFillColor(WHITE)
    c.roundRect(x - 3, y - 3, width + 6, height + 6, 18, fill=1, stroke=0)
    draw_image_cover(c, path, x, y, width, height, focal=(0.5, 0.5), contrast=1.03)


def draw_step(c: canvas.Canvas, number: str, title: str, body: str, x: float, y: float, width: float) -> None:
    c.setFillColor(RED)
    c.setFont(FONT, 25)
    c.drawString(x, y, number)
    c.setFillColor(INK)
    c.setFont(FONT, 15)
    c.drawString(x + 40, y + 2, title)
    draw_text(c, body, x + 40, y - 24, width - 40, size=9.6, leading=14.5, color=MUTED)


def draw_metric(c: canvas.Canvas, value: str, label: str, x: float, y: float, *, dark: bool) -> None:
    c.setFillColor(WHITE if dark else INK)
    c.setFont(FONT, 25)
    c.drawString(x, y, value)
    c.setFillColor(LIGHT_MUTED if dark else MUTED)
    c.setFont(FONT, 8.5)
    c.drawString(x, y - 18, label)


def draw_cover_meta(c: canvas.Canvas, x: float, y: float, *, dark: bool) -> None:
    draw_text(
        c,
        "최연소밴드 · 김승연 / 최성빈\nNHN NAN 2026 · 2026.08",
        x,
        y,
        250,
        size=8.8,
        leading=14,
        color=LIGHT_MUTED if dark else MUTED,
    )


def setup_doc(c: canvas.Canvas, title: str, subject: str) -> None:
    c.setTitle(title)
    c.setAuthor("최연소밴드 — 김승연, 최성빈")
    c.setSubject(subject)
    c.setCreator("최연소밴드 · reproducible ReportLab submission generator")


def build_game_pdf(path: Path) -> None:
    total = 6
    label = "게임 소개 및 설명"
    c = canvas.Canvas(str(path), pagesize=A4, pageCompression=1)
    setup_doc(c, "먹점프 — 게임 소개 및 설명", "NAN 2026 게임 개요·플레이 방법·실행 방법")

    # 1 — 표지
    page_base(c, dark=True, label=label, page=1, total=total)
    draw_phone(c, GAMEPLAY, 330, 58, 222, 724)
    draw_kicker(c, "NAN 2026 GAME INTRODUCTION", M, 750, dark=True)
    draw_image_contain(c, LOGO, 42, 565, 235, 145, tint=WHITE)
    draw_text(
        c,
        "캐릭터를 움직이지 않는다.\n길을 그린다.",
        M,
        515,
        240,
        size=23,
        leading=33,
        color=WHITE,
    )
    draw_rule(c, M, 415, 210, dark=True, red=True)
    draw_text(
        c,
        "선을 그려 발판을 만들고,\n먹방울을 지키며 오르는\n수묵 클라이밍.",
        M,
        380,
        230,
        size=11.5,
        leading=18,
        color=LIGHT_MUTED,
    )
    draw_cover_meta(c, M, 112, dark=True)
    c.showPage()

    # 2 — 조작
    page_base(c, dark=False, label=label, page=2, total=total)
    draw_kicker(c, "그리는 것이 곧 조작", M, 785, dark=False)
    draw_title(c, "캐릭터 대신 길을 움직인다", M, 742, W - 2 * M, dark=False, size=30)
    draw_text(
        c,
        "먹방울은 약 1초마다 스스로 뛴다. 플레이어는 점프 버튼 대신 다음 착지점을 그린다.",
        M,
        690,
        W - 2 * M,
        size=11.3,
        leading=17,
        color=MUTED,
    )
    draw_image_cover(c, GAMEPLAY, M, 365, W - 2 * M, 285, focal=(0.5, 0.58), contrast=1.05)
    draw_step(c, "01", "본다", "먹방울의 궤적과 다음 위험을 읽는다.", M, 300, 150)
    draw_step(c, "02", "긋는다", "손가락을 끌고 떼어 수묵 발판을 만든다.", 220, 300, 155)
    draw_step(c, "03", "오른다", "기울기와 길이가 다음 점프를 바꾼다.", 395, 300, 155)
    draw_rule(c, M, 194, W - 2 * M, dark=False, red=True)
    draw_text(
        c,
        "짧고 평평한 획은 안전하다. 길고 기운 획은 더 멀리 보내지만 착지가 어렵다.",
        M,
        162,
        W - 2 * M,
        size=13,
        leading=19,
        color=INK,
        align="center",
    )
    c.showPage()

    # 3 — 먹 예산
    page_base(c, dark=True, label=label, page=3, total=total)
    draw_kicker(c, "먹은 자원이고 시간이다", M, 785, dark=True)
    draw_title(c, "처음 쓸 수 있는 먹은 4.8m", M, 740, 380, dark=True, size=30)
    draw_text(
        c,
        "하단 게이지는 지금부터 더 그릴 수 있는 남은 용량이다.\n선이 마르면 그 길이만큼 다시 찬다.",
        M,
        682,
        410,
        size=11.2,
        leading=17.5,
        color=LIGHT_MUTED,
    )
    c.setFillColor(PAPER_2)
    c.roundRect(M, 470, W - 2 * M, 120, 20, fill=1, stroke=0)
    draw_image_contain(c, GAUGE_TRACK, M + 20, 502, W - 2 * M - 40, 55, tint=INK)
    draw_image_contain(c, GAUGE_FILL, M + 20, 502, (W - 2 * M - 40) * 0.62, 55, tint=RED)
    c.setFillColor(INK)
    c.setFont(FONT, 9)
    c.drawRightString(W - M - 22, 482, "남은 먹 62%")
    statements = [
        ("3.4초", "선명하게 유지"),
        ("1.1초", "처음 그린 쪽부터 건조"),
        ("동시에", "붓자국과 충돌 판정 제거"),
    ]
    for index, (value, text_value) in enumerate(statements):
        x = M + index * 168
        draw_metric(c, value, text_value, x, 390, dark=True)
    draw_rule(c, M, 324, W - 2 * M, dark=True, red=True)
    draw_text(
        c,
        "새 선이 용량을 넘기면 가장 오래된 획부터 사라진다. 황금 붓은 8초 동안 용량 초과 퇴출만 미루며 자연 건조는 계속된다.",
        M,
        290,
        W - 2 * M,
        size=12,
        leading=19,
        color=WHITE,
    )
    draw_text(
        c,
        "많이 그리는 기술보다, 지금 남겨야 할 한 획을 고르는 판단이 중요하다.",
        M,
        178,
        W - 2 * M,
        size=15.5,
        leading=23,
        color=RED,
    )
    c.showPage()

    # 4 — 먹떼
    page_base(c, dark=False, label=label, page=4, total=total)
    draw_kicker(c, "개별 체력 · 공동 생존", M, 785, dark=False)
    draw_title(c, "한 마리가 아니라 먹떼를 살린다", M, 742, W - 2 * M, dark=False, size=29)
    draw_image_cover(c, GAMEPLAY, M, 342, 284, 340, focal=(0.5, 0.48), contrast=1.06)
    draw_text(
        c,
        "첫 12m에는 먹분신이 반드시 나온다. 새 분신은 아이템을 먹은 개체 바로 옆에서 태어나며, 한 마리만 살아 있어도 도전은 끝나지 않는다.",
        355,
        650,
        195,
        size=10.7,
        leading=17,
        color=INK,
    )
    draw_rule(c, 355, 548, 195, dark=False)
    draw_text(
        c,
        "체력은 각 캐릭터 머리 위에 따로 표시된다. 추락하면 방어막 또는 체력 한 칸을 쓰고, 살아 있으면 35m를 복귀 상승한다.",
        355,
        515,
        195,
        size=10.7,
        leading=17,
        color=MUTED,
    )
    draw_rule(c, M, 292, W - 2 * M, dark=False, red=True)
    draw_text(c, "카메라는 가장 높은 생존자를 따라간다.", M, 250, W - 2 * M, size=18, color=INK)
    draw_text(
        c,
        "선두가 죽으면 남은 먹떼가 다시 보이도록 한 번 재구도한다. 장애물과 맵 진행만 하위 중앙값을 사용해 혼자 튀어 오른 분신이 난도를 앞당기지 않게 했다.",
        M,
        210,
        W - 2 * M,
        size=10.5,
        leading=16.5,
        color=MUTED,
    )
    c.showPage()

    # 5 — 변수
    page_base(c, dark=True, label=label, page=5, total=total)
    draw_kicker(c, "한 판을 흔드는 것들", M, 785, dark=True)
    draw_title(c, "도구는 네 개, 위험은 고도와 함께 늘어난다", M, 742, W - 2 * M, dark=True, size=27)
    c.setFillColor(PAPER_2)
    c.rect(0, 465, W, 185, fill=1, stroke=0)
    slot_w = W / 4
    for index, (icon, name, effect) in enumerate(ITEMS):
        x = index * slot_w
        draw_image_contain(c, icon, x + 22, 520, slot_w - 44, 95)
        c.setFillColor(INK)
        c.setFont(FONT, 10.5)
        c.drawCentredString(x + slot_w / 2, 497, name)
        c.setFillColor(MUTED)
        c.setFont(FONT, 7.7)
        c.drawCentredString(x + slot_w / 2, 480, effect)
    draw_text(
        c,
        "30m까지는 공격 장애물이 없다. 이후 먹가시와 낙묵석, 어린 동양 용이 등장하고 320m부터 벽을 따라 내려오는 먹해태가 합류한다.",
        M,
        410,
        W - 2 * M,
        size=11.2,
        leading=18,
        color=WHITE,
    )
    draw_image_contain(c, DRAGON, 46, 215, 300, 150, tint=RED, opacity=0.92)
    draw_image_contain(c, HAETAE, 370, 220, 150, 150, tint=RED, opacity=0.92)
    draw_rule(c, M, 185, W - 2 * M, dark=True)
    draw_text(
        c,
        "산길 → 바람 능선 → 먹비 계곡 → 검은 절벽 → 수묵 우주",
        M,
        150,
        W - 2 * M,
        size=13.5,
        leading=20,
        color=LIGHT_MUTED,
        align="center",
    )
    c.showPage()

    # 6 — 성장·실행
    page_base(c, dark=False, label=label, page=6, total=total)
    draw_kicker(c, "판 밖에서 정하는 빌드", M, 785, dark=False)
    draw_title(c, "세 계보, 각 한 줄기만", M, 742, 320, dark=False, size=30)
    draw_text(
        c,
        "먹빛으로 산 노드는 남고, 실제 도전에는 생존·도약·먹 운용에서 고른 한 갈래씩만 적용된다.",
        M,
        690,
        330,
        size=10.8,
        leading=17,
        color=MUTED,
    )
    draw_image_contain(c, TREE, 5, 230, 355, 445, opacity=0.34)
    for x, y, color in [(105, 336, RED), (178, 410, INK_SOFT), (248, 322, GOLD), (155, 520, RED), (270, 520, INK_SOFT)]:
        c.setFillColor(color)
        c.circle(x, y, 10, fill=1, stroke=0)
    draw_text(c, "생존", 380, 620, 160, size=15, color=RED)
    draw_text(c, "본체 1회 부활 · 50m 상승", 380, 592, 165, size=9.4, color=MUTED)
    draw_text(c, "도약", 380, 535, 160, size=15, color=INK)
    draw_text(c, "매 자동점프 2단 도약", 380, 507, 165, size=9.4, color=MUTED)
    draw_text(c, "먹 운용", 380, 450, 160, size=15, color=GOLD)
    draw_text(c, "현재 최대 먹 용량 2배", 380, 422, 165, size=9.4, color=MUTED)
    draw_rule(c, M, 205, W - 2 * M, dark=False, red=True)
    draw_text(c, "실행", M, 170, 100, size=17, color=INK)
    draw_text(c, "Android 7.1(API 25) 이상 ARM64 기기에서 APK 설치 후 실행", 128, 171, 415, size=9.2, color=MUTED)
    draw_text(c, "Unity", M, 132, 100, size=17, color=INK)
    draw_text(c, "Unity 6000.3.10f1 → Assets/Scenes/Main.unity → Play", 128, 133, 415, size=9.2, color=MUTED)
    c.setFillColor(INK)
    c.rect(0, 45, W, 52, fill=1, stroke=0)
    draw_text(
        c,
        "로그인 없음 · API 키 없음 · 네트워크 없이 플레이 가능",
        M,
        64,
        W - 2 * M,
        size=10.5,
        color=WHITE,
        align="center",
    )
    c.save()


def build_ai_pdf(path: Path) -> None:
    total = 6
    label = "AI 활용 기술"
    c = canvas.Canvas(str(path), pagesize=A4, pageCompression=1)
    setup_doc(c, "먹점프 — AI 활용 기술", "NAN 2026 AI 도구·프롬프트·검증·권리 기록")

    # 1 — 표지
    page_base(c, dark=False, label=label, page=1, total=total)
    draw_kicker(c, "NAN 2026 AI UTILIZATION", M, 785, dark=False)
    draw_title(c, "먹점프를 만든\n19일의 AI 활용 기록", M, 733, W - 2 * M, dark=False, size=34)
    draw_text(
        c,
        "기획부터 코드·아트·사운드·QA·문서까지.\nAI가 낸 초안과 사람이 남긴 결정을 함께 기록했다.",
        M,
        632,
        W - 2 * M,
        size=12,
        leading=19,
        color=MUTED,
    )
    phone_w = 142
    draw_phone(c, TUTORIAL, 49, 135, phone_w, 430)
    draw_phone(c, GAMEPLAY, 226, 105, phone_w, 460)
    draw_phone(c, GAME_OVER, 403, 135, phone_w, 430)
    draw_text(
        c,
        "300개 이상 기능 단위 커밋 · 200건 이상 AI 활용 기록",
        M,
        76,
        W - 2 * M,
        size=9.5,
        color=MUTED,
        align="center",
    )
    c.showPage()

    # 2 — 역할
    page_base(c, dark=True, label=label, page=2, total=total)
    draw_kicker(c, "작업 방식", M, 785, dark=True)
    draw_title(c, "AI가 만들고, 사람이 결정했다", M, 742, W - 2 * M, dark=True, size=30)
    stages = [
        ("문제", "팀이 감각과 제약을 적는다"),
        ("대안", "AI가 코드·수치·이미지 초안을 낸다"),
        ("선택", "사람이 버릴 것과 남길 것을 고른다"),
        ("검증", "Unity와 실기기에서 다시 깨뜨려 본다"),
    ]
    y = 615
    for index, (title, body) in enumerate(stages):
        c.setFillColor(RED if index == 2 else PAPER_2)
        c.circle(82, y + 5, 12, fill=1, stroke=0)
        if index < len(stages) - 1:
            c.setStrokeColor(Color(1, 1, 1, 0.3))
            c.setLineWidth(1)
            c.line(82, y - 7, 82, y - 115)
        draw_text(c, title, 115, y + 10, 90, size=17, color=WHITE)
        draw_text(c, body, 215, y + 8, 320, size=10.8, leading=17, color=LIGHT_MUTED)
        y -= 130
    c.setFillColor(PAPER_2)
    c.rect(0, 48, W, 80, fill=1, stroke=0)
    draw_text(
        c,
        "게임 안의 원격 AI는 없다. 제출 빌드는 API 키와 네트워크 없이 동작한다.",
        M,
        82,
        W - 2 * M,
        size=12,
        color=INK,
        align="center",
    )
    c.showPage()

    # 3 — 먹 규칙 사례
    page_base(c, dark=False, label=label, page=3, total=total)
    draw_kicker(c, "사례 1 · 규칙 재설계", M, 785, dark=False)
    draw_title(c, "먹 게이지의 뜻부터 다시 정했다", M, 742, W - 2 * M, dark=False, size=29)
    draw_text(
        c,
        "처음에는 시간이 지나면 차는 에너지 바였다. 발판이 쌓이고 벽타기가 생기자, 팀은 게이지를 화면에 남길 수 있는 먹선 예산으로 바꿨다.",
        M,
        685,
        W - 2 * M,
        size=10.8,
        leading=17,
        color=MUTED,
    )
    c.setFillColor(INK)
    c.rect(0, 390, W, 210, fill=1, stroke=0)
    draw_image_contain(c, GAUGE_TRACK, M, 480, W - 2 * M, 64, tint=PAPER_2)
    draw_image_contain(c, GAUGE_FILL, M, 480, (W - 2 * M) * 0.56, 64, tint=RED)
    draw_text(c, "새 획", M, 438, 100, size=12, color=WHITE)
    draw_text(c, "→", 160, 438, 30, size=15, color=RED, align="center")
    draw_text(c, "오래된 획부터 건조", 208, 438, 180, size=12, color=WHITE)
    draw_text(c, "→", 402, 438, 30, size=15, color=RED, align="center")
    draw_text(c, "용량 반환", 445, 438, 100, size=12, color=WHITE)
    draw_text(c, "AI가 비교한 것", M, 330, 190, size=15, color=INK)
    draw_text(
        c,
        "총량 제한·자연 건조·FIFO 소멸·할인 계산·연속 획 경계",
        M,
        295,
        220,
        size=10,
        leading=16,
        color=MUTED,
    )
    draw_text(c, "팀이 확정한 것", 335, 330, 190, size=15, color=INK)
    draw_text(
        c,
        "기본 4.8m, 시각과 충돌 동시 제거, 사라진 길이만큼 UI 회복",
        335,
        295,
        215,
        size=10,
        leading=16,
        color=MUTED,
    )
    draw_rule(c, M, 180, W - 2 * M, dark=False, red=True)
    draw_text(
        c,
        "규칙 하나를 바꾼 뒤 물리·게이지·성장 수치·회귀 테스트까지 같은 결정으로 맞췄다.",
        M,
        145,
        W - 2 * M,
        size=13,
        leading=19,
        color=INK,
        align="center",
    )
    c.showPage()

    # 4 — 아트 사례
    page_base(c, dark=True, label=label, page=4, total=total)
    draw_kicker(c, "사례 2 · 아트 제작", M, 785, dark=True)
    draw_title(c, "생성 이미지는 완성본이 아니었다", M, 742, W - 2 * M, dark=True, size=29)
    draw_text(
        c,
        "초안을 여러 장 만든 뒤 스타일에 맞는 결과만 골랐다. 색을 줄이고 외곽선 두께·피사체 크기·알파·기준점을 다시 맞춰 Unity 스프라이트로 만들었다.",
        M,
        684,
        W - 2 * M,
        size=10.8,
        leading=17,
        color=LIGHT_MUTED,
    )
    c.setFillColor(PAPER_2)
    c.rect(0, 350, W, 255, fill=1, stroke=0)
    draw_image_contain(c, PLAYER, 35, 380, 150, 190)
    for index, (icon, _, _) in enumerate(ITEMS):
        draw_image_contain(c, icon, 190 + index * 92, 440, 82, 105)
    draw_image_contain(c, DRAGON, 205, 362, 240, 95)
    draw_image_contain(c, HAETAE, 450, 365, 110, 100)
    draw_text(c, "프롬프트", M, 294, 115, size=15, color=WHITE)
    draw_text(
        c,
        "검정·한지색 / 굵은 먹 외곽선 / 단순하고 귀엽게 / 텍스트와 3D 없음",
        150,
        294,
        395,
        size=10,
        leading=15.5,
        color=LIGHT_MUTED,
    )
    draw_rule(c, M, 220, W - 2 * M, dark=True)
    draw_text(c, "사람의 작업", M, 184, 115, size=15, color=WHITE)
    draw_text(
        c,
        "채택과 폐기 / 크로마 제거 / 투명화 / 프레임 정렬 / Unity 기준점 보정",
        150,
        184,
        395,
        size=10,
        leading=15.5,
        color=LIGHT_MUTED,
    )
    c.showPage()

    # 5 — QA 사례
    page_base(c, dark=False, label=label, page=5, total=total)
    draw_kicker(c, "사례 3 · 실패 경로 감사", M, 785, dark=False)
    draw_title(c, "정상 저장보다 망가지는 순서를 먼저 봤다", M, 742, W - 2 * M, dark=False, size=28)
    draw_text(
        c,
        "먹빛·39개 노드·누적 거리·정산 이력은 한 번 잃으면 되돌리기 어렵다. 구현 대화와 검수 대화를 나누고, 재현 가능한 실패만 테스트로 옮겼다.",
        M,
        684,
        W - 2 * M,
        size=10.8,
        leading=17,
        color=MUTED,
    )
    failures = [
        "손상 JSON이 정상 백업을 덮는 문제",
        "저장 중단 뒤 진행도가 이전 세대로 돌아가는 문제",
        "복구 버튼 연속 입력이 정상 저장까지 지우는 문제",
        "최고 기록과 먹빛 중 하나만 저장되는 문제",
    ]
    y = 570
    for index, text_value in enumerate(failures):
        c.setFillColor(RED if index == 3 else INK)
        c.circle(M + 7, y + 4, 5, fill=1, stroke=0)
        draw_text(c, text_value, M + 28, y + 8, W - 2 * M - 28, size=13, color=INK)
        y -= 82
    c.setFillColor(INK)
    c.rect(M, 168, W - 2 * M, 92, fill=1, stroke=0)
    draw_text(
        c,
        "손상·미래 버전 저장은 자동 초기화하지 않는다.\n원본을 보존한 읽기 전용 복구 화면에서만 교체한다.",
        M + 25,
        218,
        W - 2 * M - 50,
        size=11.5,
        leading=18,
        color=WHITE,
        align="center",
    )
    c.showPage()

    # 6 — 투명성
    page_base(c, dark=True, label=label, page=6, total=total)
    draw_kicker(c, "검증 · 권리 · 실행 독립성", M, 785, dark=True)
    draw_title(c, "제출 빌드는 AI API 없이 완전히 실행된다", M, 738, W - 2 * M, dark=True, size=28)
    draw_rule(c, M, 665, W - 2 * M, dark=True, red=True)
    draw_metric(c, "AI", "대안·초안·경계조건 탐색", M, 602, dark=True)
    draw_metric(c, "사람", "기획 결정·선별·밸런스·최종 승인", 250, 602, dark=True)
    draw_metric(c, "게임", "원격 AI 호출 없음", 455, 602, dark=True)
    draw_text(c, "검증", M, 505, 100, size=16, color=WHITE)
    draw_text(
        c,
        "Runtime·Editor 정적 컴파일 / Unity 회귀 테스트 / iPhone 실기기 / Android 세로 해상도 프리셋 / 씬 빌더 재현",
        140,
        505,
        405,
        size=9.7,
        leading=15.5,
        color=LIGHT_MUTED,
    )
    draw_rule(c, M, 415, W - 2 * M, dark=True)
    draw_text(c, "도구", M, 377, 100, size=16, color=WHITE)
    draw_text(
        c,
        "Claude·Claude Code / OpenAI Codex / ChatGPT·ImageGen / Suno / Pillow·AVFoundation / Unity Test Framework·Bee·Roslyn",
        140,
        377,
        405,
        size=9.7,
        leading=15.5,
        color=LIGHT_MUTED,
    )
    draw_rule(c, M, 280, W - 2 * M, dark=True)
    draw_text(c, "권리 기록", M, 242, 100, size=16, color=WHITE)
    draw_text(
        c,
        "Suno Pro 생성곡, Freesound CC0, Pixabay Content License, Unity Companion License와 폰트·Pngtree 재배포 확인 항목을 docs/ai-usage-log.md에 링크와 함께 기록했다.",
        140,
        242,
        405,
        size=9.7,
        leading=15.5,
        color=LIGHT_MUTED,
    )
    c.setFillColor(PAPER_2)
    c.rect(0, 46, W, 86, fill=1, stroke=0)
    draw_text(
        c,
        "잘된 답뿐 아니라 폐기한 기능과 사람의 수정 이유까지 남기는 것이 이 프로젝트의 AI 활용 증빙이다.",
        M,
        87,
        W - 2 * M,
        size=11,
        color=INK,
        align="center",
    )
    c.save()


def build_team_pdf(path: Path) -> None:
    total = 4
    label = "팀 소개 및 역할"
    c = canvas.Canvas(str(path), pagesize=A4, pageCompression=1)
    setup_doc(c, "최연소밴드 — 팀 소개 및 역할", "NAN 2026 2인 팀 역할·협업 방식")

    # 1 — 표지
    page_base(c, dark=False, label=label, page=1, total=total)
    draw_image_cover(c, TUTORIAL, 0, 0, W / 2, H, focal=(0.5, 0.5), opacity=0.78)
    draw_image_cover(c, GAMEPLAY, W / 2, 0, W / 2, H, focal=(0.5, 0.52), opacity=0.78)
    c.setFillColor(Color(PAPER.red, PAPER.green, PAPER.blue, alpha=0.94))
    c.rect(0, 300, W, 280, fill=1, stroke=0)
    draw_kicker(c, "NAN 2026 TEAM", M, 535, dark=False)
    draw_title(c, "최연소밴드", M, 482, W - 2 * M, dark=False, size=42)
    draw_text(
        c,
        "UI·아트와 게임·기획을 나눠 책임지고,\n매 통합마다 서로의 결과를 직접 플레이한 2인 팀.",
        M,
        418,
        W - 2 * M,
        size=13,
        leading=21,
        color=MUTED,
    )
    draw_cover_meta(c, M, 330, dark=False)
    c.showPage()

    # 2 — 역할
    page_base(c, dark=True, label=label, page=2, total=total)
    draw_kicker(c, "누가 무엇을 결정했는가", M, 785, dark=True)
    draw_title(c, "화면과 규칙, 두 책임", M, 742, W - 2 * M, dark=True, size=31)
    c.setStrokeColor(Color(1, 1, 1, 0.24))
    c.setLineWidth(1)
    c.line(W / 2, 145, W / 2, 655)
    draw_text(c, "김승연", M, 626, 200, size=24, color=WHITE)
    draw_text(c, "UI·아트", M, 587, 200, size=13, color=RED)
    draw_text(
        c,
        "로비·튜토리얼·옵션·결과창·먹나무\n\n모바일 Safe Area와 터치 영역\n\n수묵 아트 방향과 생성 결과 선별\n\n투명화·크기·기준점 보정\n\n실기기 UI 검수와 제출 화면",
        M,
        538,
        210,
        size=10.2,
        leading=17,
        color=LIGHT_MUTED,
    )
    draw_image_contain(c, PLAYER, 87, 150, 150, 165, tint=PAPER_2, opacity=0.92)
    draw_text(c, "최성빈", W / 2 + 38, 626, 210, size=24, color=WHITE)
    draw_text(c, "게임·기획", W / 2 + 38, 587, 210, size=13, color=RED)
    draw_text(
        c,
        "자동 점프와 드로잉 발판 물리\n\n먹·분신·체력·카메라 규칙\n\n아이템·장애물·날씨·맵\n\n39노드 성장과 보상 밸런스\n\n저장 복구·회귀 테스트·기술 검수",
        W / 2 + 38,
        538,
        220,
        size=10.2,
        leading=17,
        color=LIGHT_MUTED,
    )
    draw_image_contain(c, DRAGON, W / 2 + 20, 168, 245, 130, tint=PAPER_2, opacity=0.92)
    c.showPage()

    # 3 — 함께 고친 장면
    page_base(c, dark=False, label=label, page=3, total=total)
    draw_kicker(c, "역할이 만난 지점", M, 785, dark=False)
    draw_title(c, "실기기에서 튜토리얼이 깨졌다", M, 742, W - 2 * M, dark=False, size=29)
    draw_phone(c, TUTORIAL, M, 220, 220, 475)
    draw_text(
        c,
        "김승연은 다섯 장의 정보량과 정렬을 다시 잡았다. 최성빈은 팝업이 열려 있는 동안 물리·스폰·날씨·기록 시간과 그리기 입력이 모두 멈추도록 상태를 고쳤다.",
        315,
        660,
        230,
        size=10.7,
        leading=17,
        color=INK,
    )
    draw_rule(c, 315, 532, 230, dark=False, red=True)
    draw_text(
        c,
        "같은 iPhone 화면을 다시 보며 버튼 위치와 마지막 시작 시점을 확인했다. 화면 문제와 게임 상태 문제를 따로 넘기지 않고 한 장면으로 끝냈다.",
        315,
        495,
        230,
        size=10.7,
        leading=17,
        color=MUTED,
    )
    draw_text(c, "UI에서 보이지 않는 규칙은 설명을 바꾸고, 설명하기 어려운 규칙은 다시 단순하게 만들었다.", 315, 335, 230, size=13, leading=20, color=RED)
    c.showPage()

    # 4 — 개발 흐름과 책임
    page_base(c, dark=True, label=label, page=4, total=total)
    draw_kicker(c, "19일의 흐름", M, 785, dark=True)
    draw_title(c, "나눠 맡고, 함께 끝냈다", M, 742, W - 2 * M, dark=True, size=31)
    timeline = [
        ("7.20", "첫 프로토타입", "자동 점프와 그린 발판"),
        ("7.24", "먹떼와 아이템", "분신·사망 자국·소리"),
        ("7.30", "모바일 화면", "로비·튜토리얼·먹나무"),
        ("8.03", "main 통합", "씬 빌더와 작업 규칙 정리"),
        ("8.07", "제출 마감", "실기기 검수·문서·영상"),
    ]
    y = 625
    for index, (date, title, body) in enumerate(timeline):
        c.setFillColor(RED if index == len(timeline) - 1 else PAPER_2)
        c.circle(88, y + 5, 7, fill=1, stroke=0)
        if index < len(timeline) - 1:
            c.setStrokeColor(Color(1, 1, 1, 0.27))
            c.line(88, y - 4, 88, y - 83)
        draw_text(c, date, 112, y + 10, 70, size=12, color=WHITE)
        draw_text(c, title, 192, y + 10, 145, size=13, color=WHITE)
        draw_text(c, body, 350, y + 9, 195, size=9.3, color=LIGHT_MUTED)
        y -= 92
    draw_rule(c, M, 175, W - 2 * M, dark=True, red=True)
    draw_text(
        c,
        "UI·아트는 김승연, 게임·기획은 최성빈이 주 책임을 맡았다. 통합·우선순위·실기기·제출은 공동 책임이다.",
        M,
        140,
        W - 2 * M,
        size=11.2,
        leading=18,
        color=WHITE,
        align="center",
    )
    draw_text(
        c,
        "저장소 커밋은 대표 계정에 집중돼 있어 개인 기여도 지표로 사용하지 않았다.",
        M,
        88,
        W - 2 * M,
        size=8.6,
        color=LIGHT_MUTED,
        align="center",
    )
    c.save()


def main() -> None:
    register_fonts()
    OUT.mkdir(parents=True, exist_ok=True)
    required = [GAMEPLAY, TUTORIAL, GAME_OVER, LOGO, PLAYER, MAP_0, TREE, GAUGE_TRACK, GAUGE_FILL, DRAGON, HAETAE]
    required.extend(icon for icon, _, _ in ITEMS)
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise FileNotFoundError("Missing submission assets:\n" + "\n".join(missing))

    build_game_pdf(OUT / "NAN2026_MukJump_Game_Introduction.pdf")
    build_ai_pdf(OUT / "NAN2026_MukJump_AI_Utilization.pdf")
    build_team_pdf(OUT / "NAN2026_MukJump_Team_Roles.pdf")
    print(f"Generated NAN 2026 PDFs in {OUT}")


if __name__ == "__main__":
    main()
