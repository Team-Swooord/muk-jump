import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Assets/Plugins/WebGL/MukJumpTossPremium.jslib', import.meta.url), 'utf8');
async function run(iap) {
  const messages = [], timers = [];
  const context = { LibraryManager: { library: {} }, mergeInto: Object.assign,
    UTF8ToString: x => x, window: { AppsInToss: { IAP: iap } },
    setTimeout: fn => (timers.push(fn), 1), clearTimeout: () => {},
    SendMessage: (target, method, value) => messages.push({ target, method, ...JSON.parse(value) }) };
  vm.runInNewContext(source, context);
  context.LibraryManager.library.MukJumpTossPremiumReadOrders('runtime', 'request-1');
  for (let i = 0; i < 110; i++) await Promise.resolve();
  return { messages, timeout: () => timers[0]() };
}
const order = (id, status = 'COMPLETED') => ({ orderId: id, sku: 'premium', status });
const pending = async () => ({ orders: [] });
test('reads all pages, preserves refund and pending source states', async () => {
  const keys = [];
  const result = await run({ getCompletedOrRefundedOrders: async value => {
    keys.push(value?.key);
    return value?.key ? { hasNext: false, orders: [order('old', 'REFUNDED')] }
      : { hasNext: true, nextKey: 'second', orders: [order('new')] };
  }, getPendingOrders: async () => ({ orders: [{ orderId: 'pending', sku: 'premium' }] }) });
  assert.deepEqual(keys, [undefined, 'second']);
  assert.equal(result.messages.length, 1); assert.equal(result.messages[0].ok, true);
  assert.deepEqual(result.messages[0].orders.map(o => o.status), ['COMPLETED', 'REFUNDED', 'PENDING']);
});
for (const [name, page] of Object.entries({ missing: undefined, badOrders: {}, missingCursor: { hasNext: true, orders: [] },
  badState: { hasNext: false, orders: [order('one', 'UNKNOWN')] } })) {
  test(`rejects ${name} without partial success`, async () => {
    const { messages } = await run({ getCompletedOrRefundedOrders: async () => page, getPendingOrders: pending });
    assert.equal(messages.length, 1); assert.equal(messages[0].ok, false);
  });
}
test('rejects repeated cursor', async () => {
  const { messages } = await run({ getCompletedOrRefundedOrders: async () => ({ hasNext: true, nextKey: 'same', orders: [] }), getPendingOrders: pending });
  assert.equal(messages[0].ok, false);
});
test('pending lookup failure does not masquerade as no purchase', async () => {
  const { messages } = await run({ getCompletedOrRefundedOrders: async () => ({ hasNext: false, orders: [order('owned')] }),
    getPendingOrders: async () => { throw Error('offline'); } });
  assert.equal(messages[0].ok, false); assert.deepEqual(messages[0].orders, []);
});
test('unsupported API fails closed', async () => { assert.equal((await run({})).messages[0].ok, false); });
test('timeout is terminal and late success is ignored', async () => {
  let resolve;
  const result = await run({ getCompletedOrRefundedOrders: () => new Promise(r => resolve = r), getPendingOrders: pending });
  result.timeout(); resolve({ hasNext: false, orders: [order('late')] });
  await Promise.resolve(); await Promise.resolve(); result.timeout();
  assert.equal(result.messages.length, 1); assert.equal(result.messages[0].ok, false);
});
