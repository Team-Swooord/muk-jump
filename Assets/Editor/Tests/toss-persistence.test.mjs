import { readFileSync, readdirSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';

const sdk = readdirSync('Library/PackageCache').find(p => p.startsWith('im.toss.apps-in-toss-unity-sdk@'));
const original = readFileSync(resolve('Library/PackageCache', sdk, 'WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js'), 'utf8');
const replacement = readFileSync(new URL('../mukjump-playerprefs-prerun.txt', import.meta.url), 'utf8').trimEnd();
const ordered = readFileSync(new URL('../mukjump-playerprefs-ordered.txt', import.meta.url), 'utf8').trimEnd();
const patched = process.env.MUKJUMP_PATCHED_PREFS ? readFileSync(process.env.MUKJUMP_PATCHED_PREFS, 'utf8') : original.replace(/    function buildPreRunArray\(existing\) \{[\s\S]*?        return arr;\r?\n    \}/, replacement)
  .replace('    function pushScoped(mount) {\n        return new Promise(function (resolve) {',
    '    function pushScoped(mount) {\n        return new Promise(function (resolve, reject) {')
  .replace("                resolve(false);\n            });\n        }).catch(function (e) {\n            recordError('push', e);\n            return false;",
    "                reject(e);\n            });\n        }).catch(function (e) {\n            recordError('push', e);\n            throw e;")
  .replace('    function status() {', ordered + '\n\n    function status() {');

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
      async getItem(key) { if (options.beforeRead) await options.beforeRead();
        if (options.readFailure) throw Error('injected read'); return remote.get(key) ?? null; },
      async setItem(key, value) { if (options.writeFailure) throw Error('injected write');
        if (options.beforeWrite) await options.beforeWrite(key, value);
        writes++; remote.set(key, value); },
    } }, addEventListener(name, callback) { events[name] = callback; },
    sessionStorage: { getItem: () => null, setItem() {}, removeItem() {} } };
  const context = vm.createContext({ window, document: { visibilityState: 'visible', addEventListener(name, callback) { events[name] = callback; } },
    console: { log() {}, warn() {} }, Uint8Array, btoa, atob,
    setTimeout(fn, ms) { const timer = setTimeout(() => { timers.delete(timer); fn(); }, ms); timers.add(timer); return timer; },
    clearTimeout(timer) { clearTimeout(timer); timers.delete(timer); } });
  vm.runInContext(options.source ?? patched, context);
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
    populate: () => new Promise(resolve => idbfs.syncfs(mount, true, resolve)),
    flush: () => events.pagehide(),
    hide: () => { context.document.visibilityState = 'hidden'; events.visibilitychange(); },
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

const tick = () => new Promise(r => setImmediate(r));
async function seededRemote(text) {
  const remote = new Map();
  const seed = await storageSession(remote, localPrefs(text));
  await seed.persist(); seed.close(); return remote;
}

test('종료 flush와 일반 저장의 역순 완료가 마지막 착용을 되돌리지 않는다', async () => {
  const remote = await seededRemote('owner-A|skin=Ink|sound=off');
  const pending = [];
  const session = await storageSession(remote, {}, { beforeWrite: () => new Promise(resolve => pending.push(resolve)) });
  try {
    session.entries.set(prefPath, localPrefs('owner-A|skin=Gat|sound=off')[prefPath]);
    const first = session.persist(); await tick();
    session.entries.set(prefPath, localPrefs('owner-A|skin=FoxMask|sound=off')[prefPath]);
    session.flush(); session.hide(); await tick();
    // 이전 SDK에서는 B 완료 후 A 완료를 허용해 원격이 A로 되돌아간다.
    for (let i = pending.length - 1; i >= 1; i--) pending[i]();
    await tick(); pending[0](); await first; await tick();
    for (const finish of pending) finish(); await tick(); await tick();
  } finally { session.close(); }
  const next = await storageSession(remote);
  try { assert.equal(storedText(next), 'owner-A|skin=FoxMask|sound=off'); }
  finally { next.close(); }
});

test('착용 뒤 늦은 populate는 최초 스냅샷으로 새 선택을 덮지 않는다', async () => {
  const remote = await seededRemote('owner-A|skin=Gat');
  const session = await storageSession(remote);
  try {
    session.entries.set(prefPath, localPrefs('owner-A|skin=Ink')[prefPath]);
    await session.persist(); await session.populate();
    assert.equal(storedText(session), 'owner-A|skin=Ink');
  } finally { session.close(); }
});

test('Storage 저장 실패를 syncfs callback 성공으로 반환하지 않는다', async () => {
  const remote = await seededRemote('owner-A|skin=Gat');
  const session = await storageSession(remote, {}, { writeFailure: true });
  try {
    session.entries.set(prefPath, localPrefs('owner-A|skin=FoxMask')[prefPath]);
    const error = await session.persist();
    assert.ok(error, '실패 callback은 오류를 전달해야 한다');
  } finally { session.close(); }
});

test('한 번 실패한 저장도 다음 최신 선택과 명시적 벗기기를 재시도할 수 있다', async () => {
  const remote = await seededRemote('owner-A|skin=Gat|gold=0|growth=19|sound=off');
  const options = { writeFailure: true };
  const session = await storageSession(remote, {}, options);
  try {
    session.entries.set(prefPath, localPrefs('owner-A|skin=FoxMask|gold=0|growth=19|sound=off')[prefPath]);
    assert.ok(await session.persist());
    options.writeFailure = false;
    session.entries.set(prefPath, localPrefs('owner-A|skin=Ink|gold=0|growth=19|sound=off')[prefPath]);
    assert.equal(await session.persist(), null);
  } finally { session.close(); }
  const next = await storageSession(remote);
  try { assert.equal(storedText(next), 'owner-A|skin=Ink|gold=0|growth=19|sound=off'); }
  finally { next.close(); }
});

test('초기 읽기 지연 동안 빈 저장을 올리지 않고 복원 완료 뒤에만 부팅한다', async () => {
  const remote = await seededRemote('owner-A|skin=Gat|growth=19');
  const before = [...remote.entries()];
  let release;
  const wait = new Promise(r => { release = r; });
  let completed = false;
  const starting = storageSession(remote, {}, { beforeRead: () => wait }).then(value => { completed = true; return value; });
  await tick(); assert.equal(completed, false); assert.deepEqual([...remote.entries()], before);
  release(); const session = await starting;
  try { assert.equal(storedText(session), 'owner-A|skin=Gat|growth=19'); }
  finally { session.close(); }
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
