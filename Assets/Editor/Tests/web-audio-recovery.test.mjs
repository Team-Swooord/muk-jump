import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = readFileSync(new URL('../../Plugins/WebGL/MukJumpWebAudio.jslib', import.meta.url), 'utf8');
function fixture() {
  const events = new Map();
  function target(prefix) { return {
    addEventListener(name, fn, capture) { assert.equal(capture, true); events.set(prefix + name, fn); },
    removeEventListener(name, fn) { assert.equal(events.get(prefix + name), fn); events.delete(prefix + name); },
  }; }
  let resumes = 0;
  const audio = { state: 'suspended', resume() { resumes++; return Promise.resolve(); } };
  const context = { window: target('w:'), document: { ...target('d:'), hidden: false },
    WEBAudio: { audioContext: audio }, Module: { deinitializers: [] },
    LibraryManager: { library: {} }, mergeInto: Object.assign };
  vm.runInNewContext(source, context);
  return { ...context, events, audio, get resumes() { return resumes; },
    allow: context.LibraryManager.library.MukJumpWebAudioSetRecoveryAllowed };
}
test('첫 활성화에서 재개를 요청하고 touchend·pointerup 때 다시 요청할 수 있다', () => {
  const f = fixture(); f.allow(1); assert.equal(f.resumes, 1);
  f.events.get('w:touchend')(); f.events.get('w:pointerup')();
  assert.equal(f.resumes, 3);
  f.audio.state = 'running'; f.events.get('w:touchend')();
  assert.equal(f.resumes, 3);
});
test('화면 복귀 interrupted 상태도 처리하고 숨김·광고 중에는 재개하지 않는다', () => {
  const f = fixture(); f.allow(0); f.audio.state = 'interrupted';
  for (const name of ['w:focus', 'w:pageshow', 'w:touchend', 'd:visibilitychange']) f.events.get(name)();
  assert.equal(f.resumes, 0);
  f.document.hidden = true; f.allow(1); f.events.get('w:touchend')();
  assert.equal(f.resumes, 0);
  f.document.hidden = false; f.events.get('d:visibilitychange')();
  assert.equal(f.resumes, 1);
  f.allow(0); f.events.get('w:touchend')(); assert.equal(f.resumes, 1);
  f.allow(1); assert.equal(f.resumes, 2);
});
test('거절·예외 후 첫 터치를 막지 않으며 닫힌 context를 교체하지 않는다', async () => {
  const f = fixture(); f.audio.resume = () => Promise.reject(Error('activation required'));
  f.allow(1); await Promise.resolve();
  f.audio.resume = () => { throw Error('interrupted'); }; f.events.get('w:touchend')();
  let count = 0; f.audio.resume = () => { count++; return Promise.resolve(); };
  f.events.get('w:touchend')(); assert.equal(count, 1);
  f.audio.state = 'closed'; f.events.get('w:touchend')(); assert.equal(count, 1);
  assert.equal(f.WEBAudio.audioContext, f.audio);
});
test('복귀 반복에 리스너를 중복 설치하지 않고 종료 시 모두 제거한다', () => {
  const f = fixture(); f.allow(1); const count = f.events.size;
  for (let i = 0; i < 5; i++) { f.allow(0); f.allow(1); }
  assert.equal(f.events.size, count); assert.equal(f.Module.deinitializers.length, 1);
  f.Module.deinitializers[0](); assert.equal(f.events.size, 0);
  assert.equal(f.Module.mukjumpAudioRecovery, undefined);
});
