import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Assets/Plugins/WebGL/MukJumpTossPremium.jslib', import.meta.url), 'utf8');
const supported = fn => Object.assign(fn, { isSupported: () => true });
const api = (overrides = {}) => ({
  getCompletedOrRefundedOrders: supported(async () => ({ hasNext: false, orders: [] })),
  getPendingOrders: supported(async () => ({ orders: [] })),
  completeProductGrant: supported(async () => true), ...overrides,
});
async function run(iap) {
  const messages = [], timers = [];
  const context = { LibraryManager: { library: {} }, mergeInto: Object.assign,
    UTF8ToString: x => x, window: { AppsInToss: { IAP: iap } },
    setTimeout: fn => (timers.push(fn), 1), clearTimeout: () => {},
    SendMessage: (target, method, value) => messages.push({ target, method, ...JSON.parse(value) }) };
  vm.runInNewContext(source, context);
  context.LibraryManager.library.MukJumpTossPremiumReadOrders('runtime', 'request-1');
  for (let i = 0; i < 20; i++) await Promise.resolve();
  return { messages, timeout: () => timers[0]() };
}
const order = (id, status = 'COMPLETED') => ({ orderId: id, sku: 'premium', status });
test('one no-argument SDK call preserves completed, refunded and pending states', async () => {
  let calls = 0;
  const { messages } = await run(api({ getCompletedOrRefundedOrders: supported(async (...args) => {
    calls++; assert.equal(args.length, 0);
    return { hasNext: false, orders: [order('new'), order('old', 'REFUNDED')] };
  }), getPendingOrders: supported(async () => ({ orders: [order('pending')] })) }));
  assert.equal(calls, 1); assert.equal(messages.length, 1); assert.equal(messages[0].ok, true);
  assert.equal(messages[0].historyComplete, true);
  assert.deepEqual(messages[0].orders.map(o => o.status), ['COMPLETED', 'REFUNDED', 'PENDING']);
});
test('hasNext fails closed without fake pagination, pending call or partial entitlement', async () => {
  let calls = 0, pending = 0;
  const { messages } = await run(api({ getCompletedOrRefundedOrders: supported(async () => {
    calls++; return { hasNext: true, nextKey: 'ignored-by-sdk', orders: [order('owned')] };
  }), getPendingOrders: supported(async () => { pending++; return { orders: [] }; }) }));
  assert.equal(calls, 1); assert.equal(pending, 0);
  assert.equal(messages[0].ok, false); assert.equal(messages[0].historyComplete, false);
  assert.deepEqual(messages[0].orders, []); assert.equal(messages[0].code, 'INCOMPLETE_ORDER_HISTORY');
});
for (const [name, page] of Object.entries({ missing: undefined, badOrders: {}, missingHasNext: { orders: [] },
  badState: { hasNext: false, orders: [order('one', 'UNKNOWN')] } })) {
  test('rejects '+name+' without partial success', async () => {
    const { messages } = await run(api({ getCompletedOrRefundedOrders: supported(async () => page) }));
    assert.equal(messages.length, 1); assert.equal(messages[0].ok, false); assert.deepEqual(messages[0].orders, []);
  });
}
test('pending lookup failure preserves SDK error code, not partial purchase', async () => {
  const { messages } = await run(api({ getCompletedOrRefundedOrders: supported(async () => ({ hasNext: false, orders: [order('owned')] })),
    getPendingOrders: supported(async () => { throw Object.assign(Error('offline'), { code: 'NETWORK_ERROR' }); }) }));
  assert.equal(messages[0].ok, false); assert.equal(messages[0].code, 'NETWORK_ERROR'); assert.deepEqual(messages[0].orders, []);
});
test('malformed pending fails closed', async () => {
  const { messages } = await run(api({ getPendingOrders: supported(async () => ({ orders: [{}] })) }));
  assert.equal(messages[0].ok, false); assert.deepEqual(messages[0].orders, []);
});
for (const mode of ['missing', 'no-support-check', 'unsupported', 'throws']) {
  test('unsupported API '+mode+' requires app update', async () => {
    const iap = api();
    if (mode === 'missing') delete iap.completeProductGrant;
    else if (mode === 'no-support-check') delete iap.completeProductGrant.isSupported;
    else iap.completeProductGrant.isSupported = () => { if (mode === 'throws') throw Error('old app'); return false; };
    const { messages } = await run(iap);
    assert.equal(messages[0].ok, false); assert.equal(messages[0].code, 'UNSUPPORTED_APP_VERSION');
  });
}
test('timeout is terminal and late success is ignored', async () => {
  let resolve;
  const result = await run(api({ getCompletedOrRefundedOrders: supported(() => new Promise(r => resolve = r)) }));
  result.timeout(); resolve({ hasNext: false, orders: [order('late')] });
  await Promise.resolve(); await Promise.resolve(); result.timeout();
  assert.equal(result.messages.length, 1); assert.equal(result.messages[0].ok, false); assert.equal(result.messages[0].code, 'TIMEOUT');
});

function purchaseFixture(create) {
  const messages = [], timers = [];
  const listeners = new Map();
  const context = { LibraryManager: { library: {} }, mergeInto: Object.assign,
    UTF8ToString: x => x, window: { AppsInToss: { IAP: { createOneTimePurchaseOrder: create } },
      addEventListener: (name, fn) => listeners.set(name, fn), removeEventListener: name => listeners.delete(name) },
    setTimeout: fn => (timers.push(fn), timers.length), clearTimeout: () => {},
    SendMessage: (target, method, value) => messages.push({ target, method, ...JSON.parse(value) }) };
  vm.runInNewContext(source, context);
  const library = context.LibraryManager.library;
  library.MukJumpTossPremiumPurchase('runtime', 'purchase-1', 'premium');
  return { messages, timers, library, context, listeners };
}
for (const code of ['ITEM_ALREADY_OWNED', 'PAYMENT_PENDING', 'USER_CANCELED', 'UNSUPPORTED_APP_VERSION', 'NETWORK_ERROR']) {
  test('purchase preserves '+code+' and cleans synchronous error exactly once', () => {
    let cleaned = 0;
    const f = purchaseFixture(supported(({ onError }) => {
      onError({ code, message: 'private error text' }); return () => cleaned++;
    }));
    assert.equal(f.messages.length, 1); assert.equal(f.messages[0].code, code);
    assert.equal(f.messages[0].kind, 'error'); assert.equal(cleaned, 1);
    assert.equal(JSON.stringify(f.messages).includes('private error text'), false);
    f.library.MukJumpTossPremiumCancelPurchase('purchase-1'); assert.equal(cleaned, 1);
  });
}
test('purchase grant waits for Unity acknowledgement; success and late callbacks finish once', async () => {
  let options, cleaned = 0;
  const f = purchaseFixture(supported(value => { options = value; return () => cleaned++; }));
  assert.equal(options.options.sku, 'premium');
  const granted = options.options.processProductGrant({ orderId: 'order-1' });
  assert.equal(f.messages[0].kind, 'grant');
  f.library.MukJumpTossPremiumGrantReply('purchase-1', f.messages[0].grantId, 1);
  assert.equal(await granted, true);
  options.onEvent({ type: 'success', data: { orderId: 'order-1' } });
  options.onError({ code: 'LATE' });
  assert.equal(f.messages.length, 2); assert.equal(f.messages[1].kind, 'success'); assert.equal(cleaned, 1);
  f.timers[0]();
});
test('owner disposal denies outstanding grant and ignores late callbacks', async () => {
  let options, cleaned = 0;
  const f = purchaseFixture(supported(value => { options = value; return () => cleaned++; }));
  const granted = options.options.processProductGrant({ orderId: 'late-order' });
  f.library.MukJumpTossPremiumCancelPurchase('purchase-1');
  assert.equal(await granted, false); assert.equal(cleaned, 1);
  f.library.MukJumpTossPremiumGrantReply('purchase-1', f.messages[0].grantId, 1);
  options.onEvent({ type: 'success', data: { orderId: 'late-order' } });
  assert.equal(f.messages.length, 1);
});
test('purchase grant timeout denies and malformed success never grants', async () => {
  let options;
  const f = purchaseFixture(supported(value => { options = value; return () => {}; }));
  assert.equal(await options.options.processProductGrant({}), false);
  const granted = options.options.processProductGrant({ orderId: 'timeout' });
  f.timers[0](); assert.equal(await granted, false);
  options.onEvent({ type: 'success', data: {} });
  assert.equal(f.messages.at(-1).code, 'INVALID_RESPONSE');
});
test('missing purchase support fails closed; thrown errors preserve code', () => {
  assert.equal(purchaseFixture(() => {}).messages[0].code, 'UNSUPPORTED_APP_VERSION');
  assert.equal(purchaseFixture(supported(() => { throw { code: 'NETWORK_ERROR' }; })).messages[0].code, 'NETWORK_ERROR');
});
test('pagehide cancels once, denies outstanding grants and removes listener', async () => {
  let options, cleaned = 0;
  const f = purchaseFixture(supported(value => { options = value; return () => cleaned++; }));
  const granted = options.options.processProductGrant({ orderId: 'hidden' });
  f.listeners.get('pagehide')();
  assert.equal(await granted, false); assert.equal(cleaned, 1); assert.equal(f.listeners.size, 0);
  assert.equal(f.messages.at(-1).code, 'PAGE_HIDDEN');
});
test('success must match an acknowledged grant, not another order', async () => {
  let options;
  const f = purchaseFixture(supported(value => { options = value; return () => {}; }));
  const granted = options.options.processProductGrant({ orderId: 'one' });
  f.library.MukJumpTossPremiumGrantReply('purchase-1', f.messages[0].grantId, 1);
  assert.equal(await granted, true);
  options.onEvent({ type: 'success', data: { orderId: 'other' } });
  assert.equal(f.messages.at(-1).code, 'INVALID_RESPONSE');
});
