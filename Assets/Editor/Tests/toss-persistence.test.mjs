import { readFileSync, readdirSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';

const sdk = readdirSync('Library/PackageCache').find(p => p.startsWith('im.toss.apps-in-toss-unity-sdk@'));
const original = readFileSync(resolve('Library/PackageCache', sdk, 'WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js'), 'utf8');
const replacement = readFileSync(new URL('../mukjump-playerprefs-prerun.txt', import.meta.url), 'utf8').trimEnd();
const ordered = readFileSync(new URL('../mukjump-playerprefs-ordered.txt', import.meta.url), 'utf8').trimEnd();
const restore = readFileSync(new URL('../mukjump-playerprefs-restore.txt', import.meta.url), 'utf8').trimEnd();
const patched = process.env.MUKJUMP_PATCHED_PREFS ? readFileSync(process.env.MUKJUMP_PATCHED_PREFS, 'utf8') : original.replace(/    function buildPreRunArray\(existing\) \{[\s\S]*?        return arr;\r?\n    \}/, replacement)
  .replace('    function pushScoped(mount) {\n        return new Promise(function (resolve) {',
    '    function pushScoped(mount) {\n        return new Promise(function (resolve, reject) {')
  .replace("                resolve(false);\n            });\n        }).catch(function (e) {\n            recordError('push', e);\n            return false;",
    "                reject(e);\n            });\n        }).catch(function (e) {\n            recordError('push', e);\n            throw e;")
  .replace('    function status() {', ordered + '\n\n    function status() {')
  .replace('    var api = {', restore + '\n\n    var api = {\n        preparePrefsPath: mukjumpPreparePrefsPath,\n        confirmOwner: mukjumpConfirmOwner,');

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
  { mode: 'ait' }, { mode: 'ait', restoreReady: true, disabled: true }, { mode: 'ait', restoreReady: true, foreign: true }])
  test(`저장소 ${JSON.stringify(status)} 실행은 빈 기록을 정상값으로 확정하지 않는다`, () => assert.equal(ready(status), 0));
test('실제 토스 저장소 복원 경로만 정상 판정한다', () => {
  assert.equal(ready({ mode: 'ait', restoreReady: true }), 1);
  assert.equal(ready({ mode: 'ait', restoreReady: true }, false), 0);
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
  let fs;
  const idbfs = {
    syncfs(_mount, populate, cb) {
      if (populate && options.localReadFailure) { cb(Error('injected local read')); return; }
      if (!populate && options.scanDuringMirror) {
        idbfs.getLocalSet(_mount, () => {});
        // 실제 IDBFS는 비동기 원격 목록 조회 뒤에 로컬 바이트도 다시 읽는다.
        setImmediate(() => { for (const path of entries.keys()) idbfs.loadLocalEntry(path, () => {}); cb(null); });
      } else cb(null);
    },
    getLocalSet(_mount, cb) { if (options.scanDuringMirror && fs) for (const path of entries.keys()) fs.lookupPath(path); cb(null, { entries: Object.fromEntries(entries) }); },
    loadLocalEntry(path, cb) { if (options.scanDuringMirror && fs) fs.lookupPath(path); cb(null, entries.get(path)); },
    storeLocalEntry(path, value, cb) { if(options.storeFailure) { cb(Error("injected restore failure")); return; } entries.set(path, value); cb(null); },
    removeLocalEntry(path, cb) { entries.delete(path); cb(null); },
  };
  const mount = { type: idbfs, mountpoint: '/idbfs' };
  const module = {};
  window.__AIT_PP.configure(module);
  module.preRun.push(m => { m.__unityIdbfsMount = { mount }; });
  for (const cb of module.preRun) cb(module);
  await new Promise(resolve => idbfs.syncfs(mount, true, resolve));
  // 빌드에 연결되는 실제 jslib lookup 훅을 통해 Unity의 첫 PlayerPrefs 접근을 재현한다.
  fs = { lookupPath: path => entries.get(path) };
  const bridge = { window, FS: fs, LibraryManager: { library: {} }, mergeInto: Object.assign };
  vm.runInNewContext(gate, bridge);
  bridge.LibraryManager.library.$MukJumpTossStorageWatch();
  const currentPath = options.currentPath ?? prefPath;
  const loadedPrefs = fs.lookupPath(currentPath);
  if (options.authorizeOwner !== false) bridge.LibraryManager.library.MukJumpTossConfirmStorageOwner();
  await new Promise(resolve => setImmediate(resolve));
  return { entries, loadedPrefs, lookup: path => fs.lookupPath(path), confirmOwner: () => bridge.LibraryManager.library.MukJumpTossConfirmStorageOwner(), status: () => window.__AIT_PP.status(), writes: () => writes,
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


test('원격 복원 쓰기 실패는 정상 부팅·빈 스냅샷 덮어쓰기로 이어지지 않는다', async () => {
  const remote=await seededRemote('owner-A|best=137|skin=Gat|growth=19');
  const before=[...remote.entries()];
  const session=await storageSession(remote,{}, {storeFailure:true});
  try {
    assert.notEqual(session.status().mode,'ait');
    await session.persist(); session.flush(); await tick();
    assert.deepEqual([...remote.entries()],before);
  } finally {session.close();}
});

test('앱 경로가 바뀌어도 원격 기록을 현재 Unity 경로에서 읽거나 안전하게 시작을 막는다', async () => {
  const remote=await seededRemote('owner-A|best=137|skin=Gat|growth=19');
  const session=await storageSession(remote,{}, {currentPath:'/idbfs/new-origin/PlayerPrefs'});
  try {
    const current=session.entries.get('/idbfs/new-origin/PlayerPrefs');
    assert.equal(new TextDecoder().decode(current?.contents), 'owner-A|best=137|skin=Gat|growth=19');
    assert.equal(session.status().restoreReady, true);
    assert.equal(session.status().pathRemapped, true);
    assert.ok(session.loadedPrefs, 'Unity의 첫 읽기 전에 복원해야 한다');
  } finally {session.close();}
});

test('실제 읽기 경로와 소유자 확인 전에는 자동 flush·로컬 미러·원격을 보존한다', async () => {
  const remote = await seededRemote('owner-A|skin=Gat|wallet=19');
  const before = [...remote.entries()];
  const session = await storageSession(remote, {}, { authorizeOwner: false });
  try {
    session.entries.set(prefPath, localPrefs('owner-B|skin=Ink|wallet=0')[prefPath]);
    assert.ok(await session.persist()); session.flush(); await tick();
    assert.deepEqual([...remote.entries()], before);
    assert.equal(session.status().ownerReady, false);
  } finally { session.close(); }
});

test('경로 이관은 원본 바이트를 별도 보존하고 새 경로에서도 다시 복원한다', async () => {
  const payload = 'owner-A|best=151|time=83|skin=FoxMask|unlocked=Gat,FoxMask|wallet=27|growth=3';
  const remote = await seededRemote(payload);
  const original = JSON.parse(JSON.parse(remote.get('AITUnityFS_v1_manifest')).inline);
  const path = '/idbfs/new-release/PlayerPrefs';
  const first = await storageSession(remote, {}, { currentPath: path });
  try { assert.equal(await first.persist(), null); }
  finally { first.close(); }
  assert.deepEqual(JSON.parse(remote.get('MukJump.PlayerPrefs.beforePathMigration.v1')), original);
  const next = await storageSession(remote, {}, { currentPath: path });
  try { assert.equal(new TextDecoder().decode(next.loadedPrefs.contents), payload); }
  finally { next.close(); }
});

for (const damage of ['base64', 'mode', 'empty']) test(`손상된 원격 ${damage}는 정상 복원으로 처리하지 않는다`, async () => {
  const remote = await seededRemote('owner-A|wallet=19|skin=Gat');
  const manifest = JSON.parse(remote.get('AITUnityFS_v1_manifest'));
  const body = JSON.parse(manifest.inline);
  if (damage === 'mode') body.files[prefPath].m = 0;
  else body.files[prefPath].d = damage === 'empty' ? '' : '!invalid!';
  manifest.inline = JSON.stringify(body); remote.set('AITUnityFS_v1_manifest', JSON.stringify(manifest));
  const before = [...remote.entries()];
  const session = await storageSession(remote);
  try { assert.notEqual(session.status().mode, 'ait'); await session.persist(); session.flush(); await tick(); assert.deepEqual([...remote.entries()], before); }
  finally { session.close(); }
});

test('서로 다른 후보가 여럿이면 새 경로에 점수·시간·착용을 추측해 합치지 않는다', async () => {
  const remote = await seededRemote('owner-A|best=151|skin=Gat');
  const manifest = JSON.parse(remote.get('AITUnityFS_v1_manifest')), body = JSON.parse(manifest.inline);
  body.files['/idbfs/another-origin'] = body.files['/idbfs/existing-origin'];
  body.files['/idbfs/another-origin/PlayerPrefs'] = { ...body.files[prefPath], d: btoa('owner-A|best=200|skin=Ink') };
  manifest.inline = JSON.stringify(body); remote.set('AITUnityFS_v1_manifest', JSON.stringify(manifest));
  const before = [...remote.entries()];
  const session = await storageSession(remote, {}, { currentPath: '/idbfs/new-origin/PlayerPrefs' });
  try { assert.equal(session.status().restoreReady, false); assert.notEqual(session.status().mode, 'ait'); await session.persist(); assert.deepEqual([...remote.entries()], before); }
  finally { session.close(); }
});


test('연속 두 번의 경로 변경도 활성 원본 하나로 복원하고 옛 로컬 파일은 보존한다', async () => {
  const payload = 'owner-A|wallet=27|growth=3|best=151|skin=FoxMask';
  const remote = await seededRemote(payload);
  for (const hash of ['second-release', 'third-release']) {
    const path = '/idbfs/' + hash + '/PlayerPrefs';
    const session = await storageSession(remote, localPrefs('owner-A|obsolete-local'), { currentPath: path });
    try {
      assert.equal(new TextDecoder().decode(session.loadedPrefs.contents), payload);
      assert.ok(session.entries.has(prefPath));
      assert.equal(await session.persist(), null);
      const body = JSON.parse(JSON.parse(remote.get('AITUnityFS_v1_manifest')).inline);
      assert.deepEqual(Object.keys(body.files).filter(k => k.endsWith('/PlayerPrefs')), [path]);
    } finally { session.close(); }
  }
});

test('진행 중 파일을 수집하지 못하거나 빈 파일이 되면 기존 원격을 삭제하지 않는다', async () => {
  const remote = await seededRemote('owner-A|wallet=27|skin=Gat');
  const session = await storageSession(remote);
  const before = [...remote.entries()];
  try {
    session.entries.delete(prefPath);
    assert.ok(await session.persist());
    session.flush(); await tick(); assert.deepEqual([...remote.entries()], before);
  } finally { session.close(); }
});

test('소유자 저장 완료 상태는 실제 Storage 비동기 완료 전에는 성공이 아니다', async () => {
  let release;
  const barrier = new Promise(resolve => { release = resolve; });
  const remote = new Map();
  const session = await storageSession(remote, localPrefs('owner-A|wallet=27|skin=Gat'), { beforeWrite: () => barrier });
  try {
    assert.equal(session.status().ownerCommitState, 0);
    release(); await tick(); await tick();
    assert.equal(session.status().ownerCommitState, 1);
    assert.ok(remote.has('AITUnityFS_v1_manifest'));
  } finally { release(); session.close(); }
});

test('소유자 바인딩 메모리가 있어도 초기 실제 Storage 쓰기 실패는 시작 성공이 아니다', async () => {
  const session = await storageSession(new Map(), localPrefs('owner-A|wallet=27'), { writeFailure: true });
  try { assert.equal(session.status().ownerCommitState, -1); }
  finally { session.close(); }
});


test('로컬 읽기 실패와 원격 부재를 신규 계정의 빈 기록으로 확정하지 않는다', async () => {
  const remote = new Map();
  const session = await storageSession(remote, {}, { localReadFailure: true });
  try {
    assert.equal(session.status().restoreReady, false);
    assert.equal(session.status().disabled, true);
    assert.ok(await session.persist());
    assert.equal(remote.size, 0);
  } finally { session.close(); }
});


test('이전 소유자 저장의 늦은 callback은 새 저장 대기를 성공으로 바꾸지 않는다', async () => {
  const releases = [];
  const session = await storageSession(new Map(), localPrefs('owner-A|wallet=27'), {
    beforeWrite: () => new Promise(resolve => releases.push(resolve)),
  });
  try {
    session.entries.set(prefPath, localPrefs('owner-A|wallet=31')[prefPath]);
    assert.equal(session.confirmOwner(), 1);
    releases[0](); await tick(); await tick();
    assert.equal(session.status().ownerCommitState, 0);
    assert.equal(releases.length, 2);
    releases[1](); await tick(); await tick();
    assert.equal(session.status().ownerCommitState, 1);
  } finally { for (const release of releases) release(); session.close(); }
});


test('IDBFS 미러가 보존된 옛 경로를 조회해도 새 Unity 읽기로 오인하지 않는다', async () => {
  const payload = 'owner-A|best=79|skin=BondKnot';
  const remote = await seededRemote(payload);
  const session = await storageSession(remote, localPrefs(payload), {
    currentPath: '/idbfs/new-release/PlayerPrefs', scanDuringMirror: true,
  });
  try {
    assert.equal(new TextDecoder().decode(session.loadedPrefs.contents), payload);
    assert.equal(await session.persist(), null);
    assert.equal(session.status().restoreReady, true);
    assert.equal(session.status().ownerCommitState, 1);
    assert.ok(session.entries.has(prefPath));
    // 내부 미러 밖에서 진짜 다른 Unity 경로가 읽히면 기존 보호는 유지한다.
    session.lookup('/idbfs/another-engine/PlayerPrefs');
    assert.equal(session.status().restoreReady, false);
    assert.equal(session.status().disabled, true);
  } finally { session.close(); }
});
