import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
const settle = () => new Promise(setImmediate);

const source = readFileSync(process.env.MUKJUMP_WEB_AUDIO_SOURCE || new URL('../../Plugins/WebGL/MukJumpWebAudio.jslib', import.meta.url), 'utf8');
function fixture() {
  const events = new Map();
  const timers = new Map(); let timerId = 0;
  function target(prefix) { return {
    addEventListener(name, fn, capture) { if (prefix !== 'a:') assert.equal(capture, true); events.set(prefix + name, fn); },
    removeEventListener(name, fn) { assert.equal(events.get(prefix + name), fn); events.delete(prefix + name); },
  }; }
  let resumes = 0, suspends = 0, foregroundSignals = 0;
  const audio = { ...target('a:'), state: 'suspended', currentTime: 0,
    resume() { resumes++; return Promise.resolve(); },
    suspend() { suspends++; this.state = 'suspended'; return Promise.resolve(); } };
  const context = { window: target('w:'), document: { ...target('d:'), hidden: false },
    WEBAudio: { audioContext: audio }, Module: { deinitializers: [] },
    LibraryManager: { library: {} }, mergeInto: Object.assign,
    setTimeout(fn) { timers.set(++timerId, fn); return timerId; }, clearTimeout(id) { timers.delete(id); },
    SendMessage(object, method, value) { assert.equal(object, 'MobileApplicationLifecycle'); assert.equal(method, 'OnWebForegroundInteraction'); assert.equal(value, 'visible'); foregroundSignals++; } };
  vm.runInNewContext(source, context);
  return { ...context, events, audio, get resumes() { return resumes; },
    get suspends() { return suspends; }, get foregroundSignals() { return foregroundSignals; }, timers,
    tick() { const pending = [...timers.values()]; timers.clear(); pending.forEach(fn => fn()); },
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
test('광고 뒤 running으로 보이지만 멈춘 시계는 같은 context를 한 번 순환해 복구한다', async () => {
  const f = fixture(); f.allow(0); f.audio.state = 'running'; f.allow(1);
  f.events.get('w:touchend')({type:'touchend',isTrusted:true});
  f.tick(); await settle();
  assert.equal(f.suspends, 1); assert.equal(f.resumes, 1);
  assert.equal(f.WEBAudio.audioContext, f.audio);
});
test('실제 전경 터치는 오래된 Unity focus 차단을 재평가하지만 숨김·합성 이벤트는 무시한다', () => {
  const f = fixture(); f.allow(0);
  f.events.get('w:touchend')({type:'touchend',isTrusted:true});
  assert.equal(f.foregroundSignals, 1); assert.equal(f.resumes, 0);
  f.events.get('w:touchend')({type:'touchend',isTrusted:false});
  f.document.hidden = true; f.events.get('w:touchend')({type:'touchend',isTrusted:true});
  assert.equal(f.foregroundSignals, 1);
});
test('정상 시계·광고 재진입·숨김·종료에는 복구 순환이 소리를 임의로 켜지 않는다', async () => {
  for (const mode of ['healthy', 'ad', 'hidden', 'destroy']) {
    const f = fixture(); f.audio.state = 'running'; f.allow(1);
    if (mode === 'healthy') f.audio.currentTime = 1;
    if (mode === 'ad') f.allow(0);
    if (mode === 'hidden') f.document.hidden = true;
    if (mode === 'destroy') f.Module.deinitializers[0]();
    f.tick(); await Promise.resolve();
    assert.equal(f.suspends, 0, mode); assert.equal(f.resumes, 0, mode);
  }
});
test('늦은 suspend 완료 뒤 새 광고나 백그라운드로 바뀌면 resume하지 않는다', async () => {
  for (const mode of ['ad','hidden','destroy']) {
    const f=fixture(); let finish;
    f.audio.state='running'; f.audio.suspend=()=>new Promise(resolve=>{finish=resolve;});
    f.allow(1); f.tick();
    if(mode==='ad') f.allow(0);
    if(mode==='hidden') f.document.hidden=true;
    if(mode==='destroy') f.Module.deinitializers[0]();
    finish(); await settle();
    assert.equal(f.resumes,0,mode);
  }
});
test('context 순환 실패나 고착에 무한 재시도하지 않으며 다음 실제 입력은 다시 시도한다', async () => {
  const f=fixture(); f.audio.state='running'; f.allow(1); f.tick();
  await settle();
  f.tick(); await Promise.resolve(); assert.equal(f.suspends,1);
  f.audio.state='running'; f.audio.currentTime=1; f.events.get('w:touchend')();
  f.audio.currentTime=2; f.tick(); assert.equal(f.suspends,1);
  f.audio.state='interrupted'; f.audio.suspend=()=>Promise.reject(Error('interrupted'));
  f.events.get('w:touchend')(); f.tick();
  await settle();
  f.Module.deinitializers[0](); assert.equal(f.timers.size,0);
});
