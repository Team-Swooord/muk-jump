import { readFileSync, readdirSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';

const sdk = readdirSync('Library/PackageCache').find(p => p.startsWith('im.toss.apps-in-toss-unity-sdk@'));
const original = readFileSync(resolve('Library/PackageCache', sdk, 'WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js'), 'utf8');
const replacement = readFileSync(new URL('../mukjump-playerprefs-prerun.txt', import.meta.url), 'utf8').trimEnd();
const patched = original.replace(/    function buildPreRunArray\(existing\) \{[\s\S]*?        return arr;\r?\n    \}/, replacement);

function boot(source, reverse) {
  const warnings = [];
  const window = { __AIT_PLAYERPREFS: { enabled: true }, AppsInToss: { Storage: {
    getItem: () => new Promise(() => {}), setItem: () => { throw Error('must not write'); },
  } }, addEventListener() {}, sessionStorage: { getItem: () => null } };
  const context = vm.createContext({ window, document: { addEventListener() {} },
    console: { log() {}, warn: value => warnings.push(value) },
    setTimeout: () => 1, clearTimeout() {}, Uint8Array });
  vm.runInContext(source, context);
  const calls = [];
  const module = { preRun: [function (m) { assert.equal(m, module); calls.push('existing'); }] };
  window.__AIT_PP.configure(module);
  const idbfs = {};
  for (const name of ['syncfs', 'getLocalSet', 'loadLocalEntry', 'storeLocalEntry', 'removeLocalEntry'])
    idbfs[name] = () => {};
  const originalSync = idbfs.syncfs;
  module.preRun.push(function (m) { m.__unityIdbfsMount = { mount: { type: idbfs, mountpoint: '/idbfs' } }; calls.push('mount'); });
  module.preRun.push(function () { calls.push('data'); });
  const callbacks = [...module.preRun];
  if (reverse) callbacks.reverse();
  for (const cb of callbacks) cb(module);
  return { captured: window.__AIT_PP.captured, hooked: idbfs.syncfs !== originalSync, calls, warnings };
}

test('기존 SDK는 Unity 6000.5의 FIFO preRun에서 저장소 연결을 건너뛴다', () => {
  const result = boot(original, false);
  assert.equal(result.captured, false);
  assert.match(result.warnings.join('\n'), /마운트가 트랩 설치보다 먼저/);
});
for (const reverse of [false, true]) test(`초기화 순서 ${reverse ? 'LIFO' : 'FIFO'}에서도 기존 콜백 보존·마운트 전에 연결`, () => {
  const result = boot(patched, reverse);
  assert.equal(result.captured, true);
  assert.equal(result.hooked, true);
  assert.deepEqual(result.calls, reverse ? ['data', 'mount', 'existing'] : ['existing', 'mount', 'data']);
  assert.equal(result.warnings.length, 0);
});

const gate = readFileSync(new URL('../../Plugins/WebGL/MukJumpTossPersistence.jslib', import.meta.url), 'utf8');
function ready(status, captured = true) {
  const context = { window: { __AIT_PP: { captured, status: () => status } }, LibraryManager: { library: {} }, mergeInto: Object.assign };
  vm.runInNewContext(gate, context);
  return context.LibraryManager.library.MukJumpTossStorageReady();
}
for (const status of [{ mode: 'pending' }, { mode: 'vanilla' }, { mode: 'foreign' },
  { mode: 'ait', disabled: true }, { mode: 'ait', foreign: true }])
  test(`저장소 ${JSON.stringify(status)} 실행은 빈 기록을 정상값으로 확정하지 않는다`, () => assert.equal(ready(status), 0));
test('실제 토스 저장소 복원 경로만 정상 판정한다', () => {
  assert.equal(ready({ mode: 'ait' }), 1);
  assert.equal(ready({ mode: 'ait' }, false), 0);
});

// SDK의 실제 복원·저장 코드를 메모리 IDBFS와 격리된 토스 Storage로 실행한다.
// 사용자 계정이나 실제 PlayerPrefs는 읽거나 수정하지 않는다.
async function storageSession(remote, initial = {}, options = {}) {
  const entries = new Map(Object.entries(initial));
  const timers = new Set();
  const events = {};
  let writes = 0;
  const window = { __AIT_PLAYERPREFS: { enabled: true, isProduction: true },
    AppsInToss: { Storage: {
      async getItem(key) { if (options.readFailure) throw Error('injected read'); return remote.get(key) ?? null; },
      async setItem(key, value) { if (options.writeFailure) throw Error('injected write'); writes++; remote.set(key, value); },
    } }, addEventListener(name, callback) { events[name] = callback; },
    sessionStorage: { getItem: () => null, setItem() {}, removeItem() {} } };
  const context = vm.createContext({ window, document: { visibilityState: 'visible', addEventListener(name, callback) { events[name] = callback; } },
    console: { log() {}, warn() {} }, Uint8Array, btoa, atob,
    setTimeout(fn, ms) { const timer = setTimeout(() => { timers.delete(timer); fn(); }, ms); timers.add(timer); return timer; },
    clearTimeout(timer) { clearTimeout(timer); timers.delete(timer); } });
  vm.runInContext(patched, context);
  const idbfs = {
    syncfs(_mount, _populate, cb) { cb(null); },
    getLocalSet(_mount, cb) { cb(null, { entries: Object.fromEntries(entries) }); },
    loadLocalEntry(path, cb) { cb(null, entries.get(path)); },
    storeLocalEntry(path, value, cb) { entries.set(path, value); cb(null); },
    removeLocalEntry(path, cb) { entries.delete(path); cb(null); },
  };
  const mount = { type: idbfs, mountpoint: '/idbfs' };
  const module = {};
  window.__AIT_PP.configure(module);
  module.preRun.push(m => { m.__unityIdbfsMount = { mount }; });
  for (const cb of module.preRun) cb(module);
  await new Promise(resolve => idbfs.syncfs(mount, true, resolve));
  return { entries, status: () => window.__AIT_PP.status(), writes: () => writes,
    persist: () => new Promise(resolve => idbfs.syncfs(mount, false, resolve)),
    flush: () => events.pagehide(),
    close() { for (const timer of timers) clearTimeout(timer); } };
}
const prefPath = '/idbfs/existing-origin/PlayerPrefs';
function localPrefs(text) {
  return { '/idbfs/existing-origin': { mode: 16877, timestamp: 1 },
    [prefPath]: { mode: 33188, timestamp: 1, contents: new TextEncoder().encode(text) } };
}
const storedText = session => new TextDecoder().decode(session.entries.get(prefPath)?.contents);

test('기존 로컬 기록을 Storage에 승격하고 로컬이 빈 재실행에서도 바이트 그대로 복원한다', async () => {
  const remote = new Map();
  const payload = 'fixture-owner-A|best=137|seconds=-1|skin=8ball|currency=19|sound=off';
  const first = await storageSession(remote, localPrefs(payload));
  try { assert.equal(first.status().mode, 'ait'); await first.persist(); assert.ok(first.writes() > 0); }
  finally { first.close(); }
  const second = await storageSession(remote);
  try { assert.equal(second.status().mode, 'ait'); assert.equal(storedText(second), payload); assert.ok(second.status().restoredBytes > 0); }
  finally { second.close(); }
});

test('Storage 읽기 실패와 빈 로컬은 기존 원격 기록을 덮어쓰지 않으며 다음 재실행에서 복원한다', async () => {
  const remote = new Map();
  const first = await storageSession(remote, localPrefs('fixture-owner-A|best=137'));
  await first.persist(); first.close();
  const previous = [...remote.entries()];
  const failed = await storageSession(remote, {}, { readFailure: true });
  try { assert.equal(failed.status().mode, 'vanilla'); await failed.persist(); failed.flush(); await new Promise(r => setTimeout(r, 5)); assert.equal(failed.writes(), 0); assert.deepEqual([...remote.entries()], previous); }
  finally { failed.close(); }
  const retry = await storageSession(remote);
  try { assert.equal(storedText(retry), 'fixture-owner-A|best=137'); }
  finally { retry.close(); }
});

test('화면 이탈 flush는 최신 파일만 저장하며 별도 사용자의 Storage와 섞지 않는다', async () => {
  const remoteA = new Map(), remoteB = new Map();
  const a = await storageSession(remoteA, localPrefs('fixture-owner-A|best=137'));
  const b = await storageSession(remoteB, localPrefs('fixture-owner-B|best=22'));
  try {
    await a.persist(); await b.persist();
    a.entries.set(prefPath, localPrefs('fixture-owner-A|best=180|sound=off')[prefPath]);
    a.flush(); await new Promise(r => setTimeout(r, 10));
  } finally { a.close(); b.close(); }
  const nextA = await storageSession(remoteA), nextB = await storageSession(remoteB);
  try { assert.equal(storedText(nextA), 'fixture-owner-A|best=180|sound=off'); assert.equal(storedText(nextB), 'fixture-owner-B|best=22'); }
  finally { nextA.close(); nextB.close(); }
});
