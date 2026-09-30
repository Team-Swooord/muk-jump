mergeInto(LibraryManager.library, {
    MukJumpTossPremiumPurchase: function (targetPtr, requestPtr, skuPtr) {
        var target = UTF8ToString(targetPtr), request = UTF8ToString(requestPtr), sku = UTF8ToString(skuPtr);
        var sessions = window.__mukPremiumPurchases || (window.__mukPremiumPurchases = {});
        var session = { ended: false, cleanup: null, grants: {}, accepted: {}, sequence: 0 };
        sessions[request] = session;
        function release() {
            window.removeEventListener('pagehide', pagehide);
            for (var key in session.grants) session.grants[key](false);
            if (session.cleanup) { var cleanup = session.cleanup; session.cleanup = null; try { cleanup(); } catch (_) {} }
        }
        session.cancel = function () { session.ended = true; release(); delete sessions[request]; };
        function send(value) { value.request = request; SendMessage(target, 'ReceivePremiumPurchase', JSON.stringify(value)); }
        function finish(kind, code) {
            if (session.ended) return;
            session.ended = true; release(); delete sessions[request];
            send({ kind: kind, code: code || '' });
        }
        function pagehide() { finish('error', 'PAGE_HIDDEN'); }
        window.addEventListener('pagehide', pagehide);
        try {
            var iap = window.AppsInToss && window.AppsInToss.IAP;
            if (!iap || typeof iap.createOneTimePurchaseOrder !== 'function' ||
                typeof iap.createOneTimePurchaseOrder.isSupported !== 'function' ||
                iap.createOneTimePurchaseOrder.isSupported() !== true) {
                finish('error', 'UNSUPPORTED_APP_VERSION'); return;
            }
            var cleanup = iap.createOneTimePurchaseOrder({
                options: { sku: sku, processProductGrant: function (order) {
                    if (session.ended || !order || typeof order.orderId !== 'string' || !order.orderId)
                        return Promise.resolve(false);
                    return new Promise(function (resolve) {
                        var key = String(++session.sequence);
                        var timer = setTimeout(function () { if (session.grants[key]) session.grants[key](false); }, 15000);
                        session.grants[key] = function (allowed) {
                            if (!session.grants[key]) return;
                            if (allowed === true) session.accepted[order.orderId] = true;
                            delete session.grants[key]; clearTimeout(timer); resolve(allowed === true);
                        };
                        send({ kind: 'grant', orderId: order.orderId, grantId: key });
                    });
                } },
                onEvent: function (event) {
                    if (event && event.type === 'success' && event.data &&
                        typeof event.data.orderId === 'string' && event.data.orderId &&
                        session.accepted[event.data.orderId] === true) finish('success');
                    else finish('error', 'INVALID_RESPONSE');
                },
                onError: function (error) { finish('error', error && typeof error.code === 'string' && error.code || 'ERROR'); }
            });
            if (session.ended) { if (typeof cleanup === 'function') cleanup(); }
            else session.cleanup = typeof cleanup === 'function' ? cleanup : null;
        } catch (error) { finish('error', error && typeof error.code === 'string' && error.code || 'ERROR'); }
    },
    MukJumpTossPremiumGrantReply: function (requestPtr, grantPtr, allowed) {
        var sessions = window.__mukPremiumPurchases || {}, session = sessions[UTF8ToString(requestPtr)];
        var grant = session && !session.ended && session.grants[UTF8ToString(grantPtr)];
        if (grant) grant(allowed === 1);
    },
    MukJumpTossPremiumCancelPurchase: function (requestPtr) {
        var session = (window.__mukPremiumPurchases || {})[UTF8ToString(requestPtr)];
        if (session) session.cancel();
    },
    MukJumpTossPremiumReadOrders: function (targetPtr, requestPtr) {
        var target = UTF8ToString(targetPtr), request = UTF8ToString(requestPtr);
        var ended = false;
        function finish(ok, orders, code) {
            if (ended) return;
            ended = true;
            clearTimeout(timer);
            SendMessage(target, 'ReceivePremiumOrders', JSON.stringify({ request: request, ok: ok,
                historyComplete: ok, orders: orders || [], code: code || '' }));
        }
        var timer = setTimeout(function () { finish(false, [], 'TIMEOUT'); }, 20000);
        (async function () {
            try {
                var iap = window.AppsInToss && window.AppsInToss.IAP;
                var supported = ['getCompletedOrRefundedOrders', 'getPendingOrders', 'completeProductGrant'].every(function (name) {
                    try { return iap && typeof iap[name] === 'function' &&
                        typeof iap[name].isSupported === 'function' && iap[name].isSupported() === true; }
                    catch (_) { return false; }
                });
                if (!supported) { finish(false, [], 'UNSUPPORTED_APP_VERSION'); return; }
                // 설치 SDK는 인자를 받지 않고 첫 페이지(최대 50개)만 반환한다.
                // 다음 페이지가 있으면 부분 목록으로 미보유/환불을 확정하지 않는다.
                var response = await iap.getCompletedOrRefundedOrders();
                if (ended) return;
                if (!response || !Array.isArray(response.orders) || typeof response.hasNext !== 'boolean')
                    throw new Error('invalid order page');
                if (response.hasNext) { finish(false, [], 'INCOMPLETE_ORDER_HISTORY'); return; }
                var orders = [];
                for (var order of response.orders) {
                    if (!order || typeof order.orderId !== 'string' || !order.orderId ||
                        typeof order.sku !== 'string' || !order.sku ||
                        (order.status !== 'COMPLETED' && order.status !== 'REFUNDED')) throw new Error('invalid order');
                    orders.push({ orderId: order.orderId, sku: order.sku, status: order.status });
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
            } catch (error) { finish(false, [], error && error.code || 'ERROR'); }
        })();
    }
});
