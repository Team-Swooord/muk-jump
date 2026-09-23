#!/usr/bin/env node
// 실제 .ait와 테스트 결과를 검사한다. 에디터 성공을 실기기 성공으로 승격하지 않는다.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { brotliDecompressSync } from 'node:zlib';
import { parseArgs } from 'node:util';
import { resolveCliEntry } from '../Assets/Editor/ait-patch-cli.mjs';

export const LIVE = Object.freeze({ rewarded: 'ait.v2.live.7edb523366544df1', interstitial: '', banner: 'ait.v2.live.5d95763582324c1c' });
const LIMIT = 512 * 1024 * 1024;
const NATIVE = ['MukJumpBackendSettings', 'MukJumpGoogleAdsSettings', 'MukJumpAnalyticsSettings', 'TheBackendSettings', 'TheBackendGoogleSettingsForAndroid', 'TheBackendGoogleSettingsForIOS', 'GoogleMobileAdsSettings'];
const NA = new Set([20, 22, 23, 30, 31, 36, 37, 38, 39, 40, 41, 42, 43, 44, 55, 56, 57, 58, 59, 60]);
// 코드/산출물 검사만으로 확인 가능한 항목. 나머지는 실제 토스 앱 증거가 필요하다.
const CODE_ONLY = new Set([3, 12, 13, 17, 18, 19, 25, 48, 50]);
const SUITES = ['AppsInTossRelease', 'AppsInTossExit', 'AppsInTossAnalytics', 'FirebaseAnalytics', 'TossPopupLayout', 'ApplicationPauseBoundary', 'MonetizationPolicy', 'MobileUiLayout', 'GameplayBannerLayout', 'GameplayHudView', 'PauseMenuView', 'SettingsScroll', 'PermanentGrowthProfile', 'ScoreManagerRecord', 'FirstRunTutorial', 'StartupBrandSplash'];
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const align = n => Math.ceil(n / 4) * 4;

function span(bytes, offset, length) {
  assert(Number.isSafeInteger(offset) && Number.isSafeInteger(length) && offset >= 0 && length >= 0 && offset + length <= bytes.length, '압축 데이터 범위가 잘못됐습니다');
  return bytes.subarray(offset, offset + length);
}

export function decodeLz4(src, size) {
  assert(Number.isInteger(size) && size >= 0 && size <= LIMIT, '비정상 압축 해제 크기');
  const out = Buffer.alloc(size); let i = 0, j = 0;
  const byte = () => { assert(i < src.length, '잘린 LZ4'); return src[i++]; };
  const length = initial => { let n = initial; if (n === 15) { let x; do { x = byte(); n += x; } while (x === 255); } return n; };
  while (i < src.length) {
    const token = byte(), literals = length(token >>> 4);
    assert(j + literals <= size, 'LZ4 literal 출력 범위');
    span(src, i, literals).copy(out, j); i += literals; j += literals;
    if (i === src.length) break;
    const distance = byte() | (byte() << 8), matches = length(token & 15) + 4;
    assert(distance > 0 && distance <= j && j + matches <= size, 'LZ4 match 범위');
    for (let k = 0; k < matches; k++) out[j + k] = out[j - distance + k];
    j += matches;
  }
  assert.equal(j, size, 'LZ4 크기 불일치'); return out;
}

export function readWebData(bytes) {
  assert.equal(bytes.subarray(0, 16).toString(), 'UnityWebData1.0\0');
  const end = bytes.readUInt32LE(16); assert(end >= 20 && end <= bytes.length);
  const files = new Map(); let p = 20;
  while (p < end) {
    assert(p + 12 <= end); const offset = bytes.readUInt32LE(p), length = bytes.readUInt32LE(p + 4), n = bytes.readUInt32LE(p + 8); p += 12;
    assert(p + n <= end); const name = span(bytes, p, n).toString(); p += n;
    assert(!files.has(name) && offset >= end, '중복/잘못된 WebData 항목'); files.set(name, span(bytes, offset, length));
  }
  return files;
}

export function readUnityFs(bytes) {
  let p = 0;
  const cstring = () => { const end = bytes.indexOf(0, p); assert(end >= p); const value = span(bytes, p, end - p).toString(); p = end + 1; return value; };
  assert.equal(cstring(), 'UnityFS'); const version = bytes.readUInt32BE(p); p += 4;
  assert(version >= 6 && version <= 8, '미검증 UnityFS 버전'); cstring(); cstring();
  const total = Number(bytes.readBigUInt64BE(p)); p += 8; assert.equal(total, bytes.length);
  const compressed = bytes.readUInt32BE(p), raw = bytes.readUInt32BE(p + 4), flags = bytes.readUInt32BE(p + 8); p += 12;
  if (version >= 7) p = Math.ceil(p / 16) * 16;
  const decode = (part, size, mode) => {
    if (mode === 0) { assert.equal(part.length, size); return part; }
    assert(mode === 2 || mode === 3, '미지원 UnityFS 압축'); return decodeLz4(part, size);
  };
  const infoAt = flags & 128 ? bytes.length - compressed : p;
  const info = decode(span(bytes, infoAt, compressed), raw, flags & 63);
  let q = 16; const count = info.readUInt32BE(q); q += 4; const blocks = []; let unpackedSize = 0;
  assert(count <= 100000);
  for (let n = 0; n < count; n++) {
    span(info, q, 10); const block = { raw: info.readUInt32BE(q), compressed: info.readUInt32BE(q + 4), flags: info.readUInt16BE(q + 8) }; q += 10;
    unpackedSize += block.raw; assert(unpackedSize <= LIMIT); blocks.push(block);
  }
  const entries = [], number = info.readUInt32BE(q); q += 4; assert(number <= 100000);
  for (let n = 0; n < number; n++) {
    span(info, q, 20); const offset = Number(info.readBigUInt64BE(q)), size = Number(info.readBigUInt64BE(q + 8)); q += 20;
    const end = info.indexOf(0, q); assert(end >= q); const name = span(info, q, end - q).toString(); q = end + 1; entries.push({ offset, size, name });
  }
  let offset = flags & 128 ? p : p + compressed; if (flags & 512) offset = Math.ceil(offset / 16) * 16;
  const data = Buffer.concat(blocks.map(b => { const part = decode(span(bytes, offset, b.compressed), b.raw, b.flags & 63); offset += b.compressed; return part; }));
  const result = new Map(); for (const e of entries) { assert(!result.has(e.name)); result.set(e.name, span(data, e.offset, e.size)); } return result;
}

export function verifyAdFields(fields) { assert.deepEqual(fields, LIVE, '먹점프 운영 광고 ID가 다르거나 테스트/전면 광고가 섞였습니다'); }

export function inspectResources(bytes) {
  assert(Buffer.isBuffer(bytes), 'resources.assets 없음');
  const name = 'AppsInTossAdSettings', at = bytes.indexOf(name); assert(at >= 4 && bytes.readUInt32LE(at - 4) === name.length, '직렬화 광고 설정 없음');
  let p = align(at + name.length);
  const string = () => { const n = bytes.readUInt32LE(p); p += 4; assert(n <= 256); const value = span(bytes, p, n).toString(); p = align(p + n); return value; };
  const fields = { rewarded: string(), interstitial: string(), banner: string() }; verifyAdFields(fields);
  for (const token of [...NATIVE, 'ait-ad-test-rewarded-id', 'ait-ad-test-banner-id']) assert(!bytes.includes(Buffer.from(token)), `Resources에 금지 설정 포함: ${token}`);
  return fields;
}

function attributes(tag) { return Object.fromEntries([...tag.matchAll(/([\w-]+)="([^"]*)"/g)].map(m => [m[1], m[2]])); }
export function verifyNunit(xml, notBefore) {
  const run = attributes(xml.match(/<test-run\b[^>]*>/)?.[0] ?? '');
  assert.equal(run.result, 'Passed', 'Unity 테스트 미완료/실패');
  const total = Number(run.total), passed = Number(run.passed);
  // 시작/닫힘 회귀에 토스 전환 지표와 네이티브 분석 동의 경계 검사를 더한다.
  assert(total >= 594 && passed === total && Number(run.failed) === 0 && Number(run.skipped) === 0 && Number(run.inconclusive) === 0, '테스트 누락/실패/건너뜀');
  const time = Date.parse(run['start-time']?.replace(' ', 'T'));
  assert(Number.isFinite(notBefore) && time >= notBefore - 1000, '이전 실행의 테스트 결과입니다');
  const cases = [...xml.matchAll(/<test-case\b[^>]*>/g)].map(m => attributes(m[0]));
  assert.equal(cases.length, total, '실제 실행 개수 불일치'); assert(cases.every(c => c.result === 'Passed'));
  for (const suite of SUITES) assert(cases.some(c => c.fullname?.includes(suite + 'Tests.')), `${suite} 검사 없음`);
  for (const name of ['TossStartupDoesNotRequireOptionalGameCenterProfile',
    'TossIdentityFailureCanCloseWithoutUnlockingUnverifiedRecords(False)',
    'TossIdentityFailureCanCloseWithoutUnlockingUnverifiedRecords(True)',
    'HapticDiagnostic', 'TossRewardOutcomeKeepsEarnedRewardWhenDisposedBeforeDismissal'])
    assert(cases.some(c => c.fullname?.includes(name)), `${name} 회귀 검사 없음`);
  return { total, passed, failed: 0, skipped: 0, startedAt: run['start-time'], suites: SUITES };
}

export function checklistRows(markdown, automatedPassed) {
  const rows = [...markdown.matchAll(/^\| (\d+) \| ([^|]+) \| ([^\n]+) \|$/gm)].map(m => ({ id: Number(m[1]), title: m[2].trim(), evidence: m[3].trim() }));
  assert.deepEqual(rows.map(r => r.id), Array.from({ length: 60 }, (_, n) => n + 1), '공식 60항목 누락/중복/순서 변경');
  return rows.map(r => ({ ...r, status: NA.has(r.id) ? 'not_applicable' : !automatedPassed ? 'blocked' : CODE_ONLY.has(r.id) ? 'code_verified' : 'device_or_manual_evidence_required' }));
}

export function verifyAudit(audit, project) {
  assert.equal(audit.projectPath, project); assert.equal(audit.target, 'WebGL');
  assert(audit.sceneSourceCurrent && audit.readyForValidation && !audit.playing && !audit.compiling && !audit.updating && audit.playerSafetyIssues.length === 0);
  assert.equal(audit.missingScriptsInLoadedScenes, 0);
  assert(audit.loadedScenes.some(scene => scene.path === 'Assets/Scenes/Main.unity' && !scene.isDirty && scene.rootCount > 0 && scene.missingScripts === 0), '실제 Main 씬이 로드되지 않았거나 미저장/누락 스크립트가 있습니다');
}

function unity(project, cli, command, args = []) {
  const response = JSON.parse(execFileSync(cli, ['command', command, ...args, '--project-path', project, '--format', 'json'], { encoding: 'utf8', timeout: 45000, maxBuffer: 4 * 1024 * 1024 }));
  assert(response.success, `${command} 명령 실패`); let result = response.data.result;
  if (typeof result === 'string') result = JSON.parse(result);
  if (command === 'eval') { assert(result.success && result.diagnostics.length === 0); result = result.result; }
  return result;
}

export function verifyAnalyticsPayload(webData) {
  const initializers = JSON.parse(webData.get('RuntimeInitializeOnLoads.json')?.toString() ?? '{}');
  assert(initializers.root?.some(entry => entry.assemblyName === 'Assembly-CSharp' &&
    entry.nameSpace === 'MukJump.Core' && entry.className === 'AppsInTossAnalyticsRuntime' &&
    entry.methodName === 'Bootstrap' && entry.loadTypes === 1), '번들에 토스 분석 시작 연결이 없습니다');
  const metadata = webData.get('Il2CppData/Metadata/global-metadata.dat');
  const events = ['mukjump_screen_view', 'mukjump_run_started', 'mukjump_first_stroke',
    'mukjump_run_completed', 'mukjump_tutorial_completed', 'mukjump_growth_completed',
    'mukjump_rewarded_ad_flow', 'mukjump_rewarded_ad_outcome'];
  for (const name of events) assert(metadata?.includes(Buffer.from(name)), `번들에 지표 이벤트 없음: ${name}`);
  const parameters = ['duration_seconds', 'stroke_count'];
  for (const name of parameters) assert(metadata?.includes(Buffer.from(name)), `번들에 지표 매개변수 없음: ${name}`);
  return { runtimeBootstrap: true, events, parameters };
}

export async function verifyArtifact(project, file) {
  const require = createRequire(resolveCliEntry(resolve(project, 'ait-build')));
  const { AITReader } = require('@apps-in-toss/ait-format');
  const bytes = readFileSync(file), reader = AITReader.fromBuffer(bytes), metadata = reader.toAppJson()._metadata;
  assert.equal(reader.appName, 'mukjump'); assert.equal(metadata.runtimeVersion, '0.84.0'); assert.equal(metadata.sdkVersion, '3.5.0'); assert.equal(reader.permissions.length, 0);
  const names = reader.listEntries(); assert.equal(new Set(names).size, names.length);
  const files = new Map(); let webBytes = 0;
  for (const name of names) {
    assert(!name.split('/').includes('..') && !name.startsWith('/'));
    const value = Buffer.from(await reader.readEntry(name)); files.set(name, value);
    if (name.startsWith('sources/')) {
      webBytes += value.length;
      assert(!/google-services(?:-desktop)?\.json|GoogleService-Info\.plist|__capture|qa-bridge/i.test(name), `금지된 배포 파일: ${name}`);
      if (/\.(html|js|mjs|css|json)$/.test(name)) assert(!/%(?:AIT|UNITY)_[A-Z0-9_]+%/.test(value.toString()), `미치환 토큰: ${name}`);
    }
  }
  assert(webBytes > 0 && webBytes <= 100 * 1024 * 1024);
  const html = files.get('sources/index.html')?.toString() ?? '';
  assert(html.includes('100dvh') && html.includes('#EAE3D2'));
  assert(html.includes("if ('false' !== 'true') return"), '디버그 콘솔이 꺼졌는지 확인할 수 없습니다');
  const bridge = files.get('sources/assets/index.js')?.toString() ?? '';
  assert(html.includes('./assets/index.js') && /window\.AppsInToss\s*=/.test(bridge), 'SDK 3.5 웹 브리지 연결이 없습니다');
  for (const api of ['loadFullScreenAd', 'showFullScreenAd', 'TossAds']) assert(bridge.includes(api), `광고 API 누락: ${api}`);
  assert(!names.some(n => /devtools|devconsole|vconsole/i.test(n)), '출시 번들에 개발 도구가 포함됐습니다');
  const dataNames = names.filter(n => /^sources\/Build\/[^/]+\.data\.unityweb$/.test(n)); assert.equal(dataNames.length, 1);
  const webData = readWebData(brotliDecompressSync(files.get(dataNames[0]), { maxOutputLength: LIMIT }));
  const analytics = verifyAnalyticsPayload(webData);
  const assets = readUnityFs(webData.get('data.unity3d')); const ads = inspectResources(assets.get('resources.assets'));
  return { path: file, bytes: bytes.length, sha256: sha(bytes), webBytes, deploymentId: reader.deploymentId, runtimeVersion: metadata.runtimeVersion, sdkVersion: metadata.sdkVersion, ads, analytics, production: true, nativeSettingsIncluded: false };
}

async function main() {
  const { values } = parseArgs({ options: { project: { type: 'string', default: process.cwd() }, ait: { type: 'string', default: 'ait-build/mukjump.ait' }, out: { type: 'string', default: 'output/qa/toss-release-verification.json' }, 'not-before': { type: 'string' }, 'unity-cli': { type: 'string', default: '/Users/seungyeoning/.unity/bin/unity' }, 'require-device': { type: 'boolean', default: false } } });
  const project = resolve(values.project), out = resolve(project, values.out);
  const report = { checkedAt: new Date().toISOString(), automatedPassed: false, releaseReady: false, errors: [] };
  try {
    const xml = readFileSync(resolve(project, 'Temp/MukJumpEditModeResults.xml'), 'utf8');
    report.tests = verifyNunit(xml, Date.parse(values['not-before']));
    // Unity를 닫아 Temp가 정리돼도 이번 검사 원문과 해시를 보존한다.
    mkdirSync(dirname(out), { recursive: true });
    report.tests.evidencePath = resolve(dirname(out), 'unity-tests.xml');
    report.tests.sha256 = sha(Buffer.from(xml));
    writeFileSync(report.tests.evidencePath, xml);
    report.compile = unity(project, values['unity-cli'], 'recompile_status');
    assert(['completed', 'up_to_date'].includes(report.compile.status) && report.compile.failed === false && report.compile.errors.length === 0);
    report.audit = unity(project, values['unity-cli'], 'mukjump_audit');
    verifyAudit(report.audit, project);
    report.releaseSettings = unity(project, values['unity-cli'], 'eval', ['return new { issues = MukJump.EditorTools.MukJumpAppsInTossSetup.CollectReleaseIssues(), payloadIssues = MukJump.EditorTools.MukJumpAppsInTossSetup.CollectBuiltPayloadIssues("ait-build/dist/web") };']);
    assert.equal(report.releaseSettings.issues.length, 0); assert.equal(report.releaseSettings.payloadIssues.length, 0);
    report.artifact = await verifyArtifact(project, resolve(project, values.ait));
    report.automatedPassed = true;
  } catch (error) { report.errors.push(error.message); }
  report.checklist = checklistRows(readFileSync(resolve(project, 'docs/store/apps-in-toss-game-checklist.md'), 'utf8'), report.automatedPassed);
  report.remaining = report.checklist.filter(r => r.status === 'device_or_manual_evidence_required' || r.status === 'blocked').map(r => r.id);
  report.partialInternalEvidence = {
    simulatorVerifiedPortion: [9, 11, 28],
    internalPolicyReviewedPortion: [27, 46],
    note: '각 행의 일부 내부 증거만 표시한다. 토스 호스트·실광고·실기기 미확인 조건은 remaining에 유지한다.'
  };
  // 이 도구는 OS 무음·실제 광고·기기 재접속 증거를 만들 수 없다. 출시 승인으로 사용 금지.
  report.releaseReady = report.automatedPassed && report.remaining.length === 0;
  mkdirSync(dirname(out), { recursive: true }); writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({ report: out, automatedPassed: report.automatedPassed, releaseReady: report.releaseReady, remaining: report.remaining, errors: report.errors, artifact: report.artifact }, null, 2));
  if (!report.automatedPassed) process.exitCode = 1;
  else if (values['require-device'] && !report.releaseReady) process.exitCode = 2;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) await main();
