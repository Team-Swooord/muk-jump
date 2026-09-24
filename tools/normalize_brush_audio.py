#!/usr/bin/env python3
"""기존 붓 녹음의 파형·길이를 유지한 채 +12.04 dB 파생 WAV를 만든다. 원본은 보존한다."""
import array
import io
from pathlib import Path
import subprocess
import tempfile
import wave

root = Path(__file__).resolve().parents[1]
folder = root / "Assets/Resources/MukJump/Audio/SFX"
source = folder / "SFX_Brush_Community.mp3"
target = folder / "SFX_Brush_Community_Balanced.wav"
with tempfile.TemporaryDirectory(prefix="mukjump-brush-") as temporary:
    decoded = Path(temporary) / "brush.wav"
    subprocess.run(["/usr/bin/afconvert", "-f", "WAVE", "-d", "LEI16@44100", "-c", "1",
                    str(source), str(decoded)], check=True)
    with wave.open(str(decoded), "rb") as reader:
        assert reader.getnchannels() == 1 and reader.getsampwidth() == 2
        samples = array.array("h", reader.readframes(reader.getnframes()))
    peak = max(map(abs, samples))
    assert 0 < peak * 4 < 32767, "원본 변경으로 4배 증폭의 여유가 사라졌습니다. 먼저 음량을 재측정하세요."
    amplified = array.array("h", (value * 4 for value in samples))
    output = io.BytesIO()
    with wave.open(output, "wb") as writer:
        writer.setparams((1, 2, 44100, len(amplified), "NONE", "not compressed"))
        writer.writeframes(amplified.tobytes())
    content = output.getvalue()
    if not target.exists() or target.read_bytes() != content:
        target.write_bytes(content)
    print(f"{target.name}: {len(samples)} samples, peak {peak / 32768:.6f} → {peak * 4 / 32768:.6f}")
