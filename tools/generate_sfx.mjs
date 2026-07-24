import fs from "node:fs";
import path from "node:path";

const sampleRate = 44100;
const outputDir = path.resolve("Assets/Resources/MukJump/Audio/SFX");
fs.mkdirSync(outputDir, { recursive: true });

let seed = 0x4d554b;
function random() {
  seed = (seed * 1664525 + 1013904223) >>> 0;
  return seed / 0xffffffff * 2 - 1;
}

function writeWav(name, duration, generator) {
  const count = Math.ceil(duration * sampleRate);
  const pcm = Buffer.alloc(count * 2);
  let phase = 0;
  let filtered = 0;
  for (let i = 0; i < count; i++) {
    const t = i / sampleRate;
    const result = generator(t, i / (count - 1), phase, filtered);
    phase = result.phase ?? phase;
    filtered = result.filtered ?? filtered;
    const value = Math.max(-1, Math.min(1, result.value));
    pcm.writeInt16LE(Math.round(value * 32767), i * 2);
  }

  const header = Buffer.alloc(44);
  header.write("RIFF", 0);
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write("WAVE", 8);
  header.write("fmt ", 12);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(1, 22);
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(sampleRate * 2, 28);
  header.writeUInt16LE(2, 32);
  header.writeUInt16LE(16, 34);
  header.write("data", 36);
  header.writeUInt32LE(pcm.length, 40);
  fs.writeFileSync(path.join(outputDir, name), Buffer.concat([header, pcm]));
}

writeWav("SFX_Brush_Draw_Loop.wav", 1.6, (t, p, phase, filtered) => {
  const grain = random();
  filtered += (grain - filtered) * 0.16;
  const pulse = 0.76 + Math.sin(t * Math.PI * 2 * 3.75) * 0.11;
  const bristle = Math.sin(t * Math.PI * 2 * 31) * 0.1;
  const edgeFade = Math.min(1, p / 0.04, (1 - p) / 0.04);
  return { value: (filtered * 0.72 + bristle) * pulse * edgeFade * 0.68, filtered };
});

writeWav("SFX_Brush_Transition.wav", 1.15, (t, p, phase, filtered) => {
  filtered += (random() - filtered) * 0.12;
  const sweep = Math.sin(t * Math.PI * 2 * (18 + p * 16)) * 0.12;
  const envelope = Math.sin(Math.PI * p) ** 0.45;
  return { value: (filtered * 0.82 + sweep) * envelope * 0.8, filtered };
});

writeWav("SFX_Wall_Hit.wav", 0.18, (t, p, phase, filtered) => {
  const frequency = 135 - p * 65;
  phase += frequency / sampleRate * Math.PI * 2;
  filtered += (random() - filtered) * 0.34;
  const envelope = Math.exp(-p * 7.5);
  return { value: (Math.sin(phase) * 0.76 + filtered * 0.38) * envelope * 0.9, phase, filtered };
});

writeWav("SFX_Character_Death.wav", 0.38, (t, p, phase) => {
  const frequency = 1280 * (1 - p) ** 2 + 145;
  phase += frequency / sampleRate * Math.PI * 2;
  const envelope = Math.sin(Math.PI * Math.min(1, p / 0.1)) * Math.exp(-p * 3.2);
  const squeak = Math.sin(phase) * 0.78 + Math.sin(phase * 2.03) * 0.18;
  return { value: squeak * envelope * 0.92, phase };
});

writeWav("SFX_Game_Over.wav", 0.78, (t, p, phase) => {
  const frequency = p < 0.46 ? 294 : p < 0.72 ? 220 : 147;
  phase += frequency / sampleRate * Math.PI * 2;
  const notePosition = p < 0.46 ? p / 0.46 : p < 0.72 ? (p - 0.46) / 0.26 : (p - 0.72) / 0.28;
  const envelope = Math.sin(Math.PI * Math.min(1, notePosition)) * (1 - p * 0.35);
  return { value: (Math.sin(phase) * 0.72 + Math.sin(phase * 0.5) * 0.16) * envelope * 0.72, phase };
});

for (const file of fs.readdirSync(outputDir).filter((name) => name.endsWith(".wav")))
  console.log(path.join(outputDir, file));
