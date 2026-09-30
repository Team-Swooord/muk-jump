import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { LIVE, decodeLz4, readWebData, inspectResources, inspectPremiumResources, verifyAdFields, verifyNunit, verifyAudit, verifyAnalyticsPayload, checklistRows } from '../verify_toss_release.mjs';

// 아래 전면 ID와 SKU는 메모리 직렬화 fixture에만 사용하며 LIVE/Unity 설정에 넣지 않는다.
const fixtureAds = { ...LIVE, interstitial: 'ait.v2.live.0123456789abcdef' };
test('운영 광고 세 ID를 대조하고 빈 전면 설정을 거부한다', () => {
  verifyAdFields({ ...fixtureAds }, fixtureAds);
  for (const invalid of [
    { ...fixtureAds, rewarded: 'ait-ad-test-rewarded-id' },
    { ...fixtureAds, banner: LIVE.rewarded },
    { ...fixtureAds, rewarded: LIVE.banner, banner: LIVE.rewarded },
    { ...fixtureAds, interstitial: 'ait.v2.live.other' },
    { ...fixtureAds, interstitial: '' },
    { ...fixtureAds, banner: '' },
  ]) assert.throws(() => verifyAdFields(invalid, fixtureAds));
  assert.throws(() => verifyAdFields({ ...fixtureAds, interstitial: '' }, { ...fixtureAds, interstitial: '' }));
  assert.throws(() => verifyAdFields({ ...fixtureAds, interstitial: LIVE.banner }, { ...fixtureAds, interstitial: LIVE.banner }));
});

test('LZ4 literal과 겹치는 match를 정확히 복원한다', () => {
  assert.equal(decodeLz4(Buffer.from([0x30, 97, 98, 99]), 3).toString(), 'abc');
  assert.equal(decodeLz4(Buffer.from([0x10, 97, 1, 0]), 5).toString(), 'aaaaa');
});

test('잘린 LZ4와 비정상 길이를 거부한다', () => {
  for (const [src, n] of [[[], -1], [[], 2 ** 30], [[0xf0], 4], [[0x30, 97], 3], [[0x10, 97, 0, 0], 5], [[0x10, 97, 9, 0], 5], [[0x30, 97, 98, 99], 2]])
    assert.throws(() => decodeLz4(Buffer.from(src), n));
});

test('알 수 없는 WebData 형식은 성공으로 간주하지 않는다', () => {
  assert.throws(() => readWebData(Buffer.alloc(32)));
  const bytes = Buffer.alloc(24); bytes.write('UnityWebData1.0\0'); bytes.writeUInt32LE(40, 16);
  assert.throws(() => readWebData(bytes));
});

const checklist = readFileSync(new URL('../../docs/store/apps-in-toss-game-checklist.md', import.meta.url), 'utf8');
test('60항목을 빠짐없이 다루고 실기기는 자동 통과 처리하지 않는다', () => {
  const rows = checklistRows(checklist, true);
  assert.equal(rows.length, 60);
  for (const id of [1, 2, 6, 14, 15, 16, 24, 33, 35, 45, 47, 49, 51, 52, 53, 54])
    assert.equal(rows[id - 1].status, 'device_or_manual_evidence_required');
  for (let id = 36; id <= 44; id++) assert.equal(rows[id - 1].status, 'device_or_manual_evidence_required');
  assert.equal(rows[2].status, 'code_verified');
  assert(checklistRows(checklist, false).every(r => ['blocked', 'not_applicable'].includes(r.status)));
});

test('체크리스트의 중복/누락을 거부한다', () => {
  assert.throws(() => checklistRows(checklist.replace('| 47 |', '| 46 |'), true));
  assert.throws(() => checklistRows(checklist.replace(/^\| 47 \|.*\n/m, ''), true));
});

test('이전·실패·건너뛴 결과 또는 일부 검사만으로 성공 처리하지 않는다', () => {
  for (const xml of [
    '<test-run result="Failed" total="504" passed="503" failed="1" skipped="0" inconclusive="0"/>',
    '<test-run result="Passed" total="1" passed="1" failed="0" skipped="0" inconclusive="0"/>',
    '<test-run result="Passed" total="504" passed="504" failed="0" skipped="0" inconclusive="0" start-time="2026-09-22 00:00:00Z"/>',
  ]) assert.throws(() => verifyNunit(xml, Date.parse('2026-09-23T00:00:00Z')));
});

test('실제 검사 목록이 모두 있을 때만 최신 결과를 인정한다', () => {
  const suites = ['AppsInTossRelease', 'AppsInTossExit', 'AppsInTossAnalytics', 'FirebaseAnalytics', 'TossPopupLayout', 'ApplicationPauseBoundary', 'MonetizationPolicy', 'MobileUiLayout', 'GameplayBannerLayout', 'GameplayHudView', 'PauseMenuView', 'SettingsScroll', 'PermanentGrowthProfile', 'ScoreManagerRecord', 'FirstRunTutorial', 'StartupBrandSplash'];
  const header = '<test-run result="Passed" total="594" passed="594" failed="0" skipped="0" inconclusive="0" start-time="2026-09-23 00:00:00Z">';
  const cases = Array.from({ length: 589 }, (_, i) => `<test-case fullname="${suites[i % suites.length]}Tests.Case${i}" result="Passed"/>`).join('') +
    ['AppsInTossReleaseTests.TossStartupDoesNotRequireOptionalGameCenterProfile',
      'SettingsScrollTests.TossIdentityFailureCanCloseWithoutUnlockingUnverifiedRecords(False)',
      'SettingsScrollTests.TossIdentityFailureCanCloseWithoutUnlockingUnverifiedRecords(True)',
      'MobileFeedbackPolishTests.HapticDiagnosticShowsSettingGateAndDuplicateLanding',
      'MonetizationPolicyTests.TossRewardOutcomeKeepsEarnedRewardWhenDisposedBeforeDismissal']
      .map(name => `<test-case fullname="${name}" result="Passed"/>`).join('');
  const xml = header + cases + '</test-run>', since = Date.parse('2026-09-22T23:59:59Z');
  assert.equal(verifyNunit(xml, since).passed, 594);
  assert.throws(() => verifyNunit(xml.replaceAll('TossStartupDoesNotRequireOptionalGameCenterProfile', 'UnrelatedTest'), since));
  assert.throws(() => verifyNunit(xml.replaceAll('TossIdentityFailureCanCloseWithoutUnlockingUnverifiedRecords(True)', 'UnrelatedTest'), since));
  assert.throws(() => verifyNunit(xml.replaceAll('ScoreManagerRecordTests', 'ScoreManagerWrongTests'), since));
  assert.throws(() => verifyNunit(xml.replaceAll('AppsInTossAnalyticsTests', 'UnrelatedTests'), since));
  assert.throws(() => verifyNunit(xml.replaceAll('FirebaseAnalyticsTests', 'UnrelatedTests'), since));
  assert.throws(() => verifyNunit(xml, Date.parse('2026-09-24T00:00:00Z')));
});

test('직렬화된 운영 설정을 읽고 네이티브/테스트 설정 혼입을 거부한다', () => {
  const string = value => { const b = Buffer.alloc(Math.ceil((4 + value.length) / 4) * 4); b.writeUInt32LE(value.length); b.write(value, 4); return b; };
  const bytes = Buffer.concat(['AppsInTossAdSettings', fixtureAds.rewarded, fixtureAds.interstitial, fixtureAds.banner].map(string));
  assert.deepEqual(inspectResources(bytes, fixtureAds), fixtureAds);
  assert.throws(() => inspectResources(Buffer.concat([bytes, Buffer.from('GoogleMobileAdsSettings')]), fixtureAds));
  assert.throws(() => inspectResources(Buffer.concat([bytes, Buffer.from('ait-ad-test-banner-id')]), fixtureAds));
  assert.throws(() => inspectResources(Buffer.concat([bytes, Buffer.from('ait-ad-test-interstitial-id')]), fixtureAds));
});

test('최종 패키지 상품 SKU의 누락·불일치·잘린 직렬화를 차단한다', () => {
  const string = value => { const b = Buffer.alloc(Math.ceil((4 + value.length) / 4) * 4); b.writeUInt32LE(value.length); b.write(value, 4); return b; };
  const bytes = Buffer.concat(['AppsInTossPremiumSettings', 'fixture-sku'].map(string));
  assert.deepEqual(inspectPremiumResources(bytes, 'fixture-sku'), { sku: 'fixture-sku', serializedSettingsVerified: true, realPurchaseVerified: false });
  for (const sku of [undefined, '', ' fixture-sku', 'different-sku']) assert.throws(() => inspectPremiumResources(bytes, sku));
  assert.throws(() => inspectPremiumResources(Buffer.alloc(10), 'fixture-sku'));
  assert.throws(() => inspectPremiumResources(bytes.subarray(0, bytes.length - 5), 'fixture-sku'));
  assert.throws(() => inspectPremiumResources(Buffer.concat(['AppsInTossPremiumSettings', ''].map(string)), 'fixture-sku'));
});

test('빈 씬 또는 다른 프로젝트의 감사를 최종 검증으로 인정하지 않는다', () => {
  const project = '/project/mukjump';
  const audit = { projectPath: project, target: 'WebGL', sceneSourceCurrent: true, readyForValidation: true, playing: false, compiling: false, updating: false, playerSafetyIssues: [], missingScriptsInLoadedScenes: 0, loadedScenes: [{ path: 'Assets/Scenes/Main.unity', isDirty: false, rootCount: 1, missingScripts: 0 }] };
  verifyAudit(audit, project);
  for (const changed of [{ loadedScenes: [] }, { projectPath: '/project/other' }, { target: 'iOS' }, { missingScriptsInLoadedScenes: 1 }, { playing: true }, { playerSafetyIssues: ['enabled'] }, { loadedScenes: [{ ...audit.loadedScenes[0], isDirty: true }] }])
    assert.throws(() => verifyAudit({ ...audit, ...changed }, project));
});

test('분석 시작 연결 또는 완료 이벤트가 없는 이전 번들은 거부한다', () => {
  const entry = { assemblyName: 'Assembly-CSharp', nameSpace: 'MukJump.Core', className: 'AppsInTossAnalyticsRuntime', methodName: 'Bootstrap', loadTypes: 1 };
  const events = ['mukjump_screen_view', 'mukjump_run_started', 'mukjump_first_stroke', 'mukjump_run_completed', 'mukjump_tutorial_completed', 'mukjump_growth_completed', 'mukjump_rewarded_ad_flow', 'mukjump_rewarded_ad_outcome'];
  const parameters = ['duration_seconds', 'stroke_count'];
  const files = new Map([
    ['RuntimeInitializeOnLoads.json', Buffer.from(JSON.stringify({ root: [entry] }))],
    ['Il2CppData/Metadata/global-metadata.dat', Buffer.from([...events, ...parameters].join('\0'))],
  ]);
  assert.deepEqual(verifyAnalyticsPayload(files), { runtimeBootstrap: true, events, parameters });
  assert.throws(() => verifyAnalyticsPayload(new Map()));
  const missingEvent = new Map(files);
  missingEvent.set('Il2CppData/Metadata/global-metadata.dat', Buffer.from([...events.filter(e => e !== 'mukjump_run_completed'), ...parameters].join('\0')));
  assert.throws(() => verifyAnalyticsPayload(missingEvent));
  const missingParameter = new Map(files);
  missingParameter.set('Il2CppData/Metadata/global-metadata.dat', Buffer.from([...events, 'duration_seconds'].join('\0')));
  assert.throws(() => verifyAnalyticsPayload(missingParameter));
  const missingBootstrap = new Map(files);
  missingBootstrap.set('RuntimeInitializeOnLoads.json', Buffer.from(JSON.stringify({ root: [{ ...entry, methodName: 'Unused' }] })));
  assert.throws(() => verifyAnalyticsPayload(missingBootstrap));
});
