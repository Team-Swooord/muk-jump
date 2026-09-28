mergeInto(LibraryManager.library, {
    MukJumpTossPremiumReadOrders: function (targetPtr, requestPtr) {
        var target = UTF8ToString(targetPtr), request = UTF8ToString(requestPtr);
        var ended = false;
        function finish(ok, orders) {
            if (ended) return;
            ended = true;
            clearTimeout(timer);
            SendMessage(target, 'ReceivePremiumOrders', JSON.stringify({ request: request, ok: ok, orders: orders || [] }));
        }
        var timer = setTimeout(function () { finish(false); }, 20000);
        (async function () {
            try {
                var iap = window.AppsInToss && window.AppsInToss.IAP;
                if (!iap || typeof iap.getCompletedOrRefundedOrders !== 'function' ||
                    typeof iap.getPendingOrders !== 'function') throw new Error('unsupported');
                var orders = [], key, seen = new Set();
                for (var page = 0; page < 100; page++) {
                    var response = await iap.getCompletedOrRefundedOrders(key ? { key: key } : undefined);
                    if (ended) return;
                    if (!response || !Array.isArray(response.orders) || typeof response.hasNext !== 'boolean')
                        throw new Error('invalid order page');
                    for (var order of response.orders) {
                        if (!order || typeof order.orderId !== 'string' || !order.orderId ||
                            typeof order.sku !== 'string' || !order.sku ||
                            (order.status !== 'COMPLETED' && order.status !== 'REFUNDED')) throw new Error('invalid order');
                        orders.push({ orderId: order.orderId, sku: order.sku, status: order.status });
                    }
                    if (!response.hasNext) break;
                    key = response.nextKey;
                    if (typeof key !== 'string' || !key || seen.has(key) || page === 99) throw new Error('invalid cursor');
                    seen.add(key);
                }
                var pending = await iap.getPendingOrders();
                if (ended) return;
                if (!pending || !Array.isArray(pending.orders)) throw new Error('invalid pending orders');
                for (var order of pending.orders) {
                    if (!order || typeof order.orderId !== 'string' || !order.orderId ||
                        typeof order.sku !== 'string' || !order.sku) throw new Error('invalid pending order');
                    orders.push({ orderId: order.orderId, sku: order.sku, status: 'PENDING' });
                }
                finish(true, orders);
            } catch (_) { finish(false); }
        })();
    }
});
