import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = readFileSync(new URL('../../Plugins/WebGL/MukJumpTossAds.jslib', import.meta.url), 'utf8');
function supported(sdk) {
  const context = { window: { AppsInToss: sdk }, LibraryManager: { library: {} }, mergeInto: Object.assign };
  vm.runInNewContext(source, context);
  return context.LibraryManager.library.MukJumpTossFullScreenAdsSupported();
}
const api = supported => Object.assign(() => {}, { isSupported: () => supported });
test('브라우저·구 토스·지원 여부 예외에는 광고를 요청하지 않는다', () => {
  assert.equal(supported(undefined), 0);
  assert.equal(supported({}), 0);
  assert.equal(supported({ loadFullScreenAd: api(true), showFullScreenAd: api(false) }), 0);
  assert.equal(supported({ loadFullScreenAd: Object.assign(() => {}, { isSupported() { throw Error('not ready'); } }), showFullScreenAd: api(true) }), 0);
});
test('두 공식 광고 API가 모두 지원되는 환경만 허용한다', () => {
  assert.equal(supported({ loadFullScreenAd: api(true), showFullScreenAd: api(true) }), 1);
});
