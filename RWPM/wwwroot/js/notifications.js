// =======================================================
// Thông báo trong ứng dụng (chuông trên thanh top)
// - Kết nối SignalR "/hub/notifications" (tự kết nối lại)
// - Sự kiện "Notification_New" { id, unreadCount } -> tải lại danh sách (render theo ngôn ngữ người xem) + toast
// - Sự kiện "Notification_Count" { unreadCount } -> đồng bộ badge giữa các tab
// - Dự phòng: nếu SignalR lỗi -> hỏi số chưa đọc mỗi 60 giây
// - Trang Notification/Index: phần tử [data-notif-open] -> đánh dấu đã đọc rồi chuyển trang
// =======================================================
(function (window, document) {
    'use strict';

    var root = document.getElementById('appNotif');
    if (!root) return;

    var cfg = {
        latestUrl: root.getAttribute('data-latest-url'),
        countUrl: root.getAttribute('data-count-url'),
        markReadUrl: root.getAttribute('data-mark-read-url'),
        markAllUrl: root.getAttribute('data-mark-all-url'),
        hubUrl: root.getAttribute('data-hub-url') || '/hub/notifications'
    };
    var i18n = {};
    try { i18n = JSON.parse(root.getAttribute('data-i18n') || '{}'); } catch (e) { i18n = {}; }
    function t(key, fallback) { return i18n[key] || fallback || key; }
    function fmt(pattern, value) { return String(pattern).replace('{0}', value); }

    var badge = root.querySelector('[data-notif-badge]');
    var list = root.querySelector('[data-notif-list]');
    var toggle = root.querySelector('[data-bs-toggle="dropdown"]');
    var POLL_MS = 60000;
    var pollTimer = null;
    var loadedAt = Date.now();
    var lastItems = [];

    function token() {
        var input = root.querySelector('input[name="__RequestVerificationToken"]') ||
            document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function getJson(url) {
        return fetch(url, {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            cache: 'no-store'
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    function postJson(url, data) {
        var body = new URLSearchParams();
        Object.keys(data || {}).forEach(function (k) { body.append(k, data[k]); });
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': token(),
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
            },
            body: body.toString()
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    function relative(ageSeconds, fallback) {
        var s = Math.max(0, Math.floor(ageSeconds));
        if (s < 60) return t('justNow', 'just now');
        if (s < 3600) return fmt(t('minutesAgo', '{0} min ago'), Math.floor(s / 60));
        if (s < 86400) return fmt(t('hoursAgo', '{0} h ago'), Math.floor(s / 3600));
        if (s < 7 * 86400) return fmt(t('daysAgo', '{0} days ago'), Math.floor(s / 86400));
        return fallback || '';
    }

    function setBadge(count) {
        if (!badge) return;
        var n = parseInt(count, 10) || 0;
        badge.textContent = n > 99 ? '99+' : String(n);
        badge.classList.toggle('d-none', n <= 0);
    }

    function el(tag, className, text) {
        var e = document.createElement(tag);
        if (className) e.className = className;
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }

    function renderList(items) {
        if (!list) return;
        list.innerHTML = '';
        if (!items || !items.length) {
            list.appendChild(el('div', 'text-center text-muted small py-4', t('empty', 'No notifications')));
            return;
        }
        items.forEach(function (item) {
            var a = el('a', 'app-notif-item' + (item.isRead ? '' : ' is-unread'));
            a.href = item.url || '#';
            a.setAttribute('data-notif-open', item.id);
            if (item.url) a.setAttribute('data-notif-url', item.url);

            var color = item.color || 'primary';
            var iconWrap = el('span', 'app-notif-icon bg-' + color + ' bg-opacity-10 text-' + color);
            iconWrap.appendChild(el('i', 'bi ' + (item.icon || 'bi-bell')));
            a.appendChild(iconWrap);

            var body = el('div', 'flex-grow-1 min-w-0');
            body.appendChild(el('div', 'app-notif-title', item.title));
            body.appendChild(el('div', 'app-notif-msg', item.message));
            var time = el('div', 'app-notif-time', relative(item.ageSeconds, item.createdDateStr));
            time.setAttribute('data-notif-age', item.ageSeconds);
            time.title = item.createdDateStr || '';
            body.appendChild(time);
            a.appendChild(body);

            if (!item.isRead) a.appendChild(el('span', 'app-notif-dot'));
            list.appendChild(a);
        });
    }

    function loadLatest() {
        return getJson(cfg.latestUrl).then(function (res) {
            loadedAt = Date.now();
            lastItems = (res && res.items) || [];
            renderList(lastItems);
            setBadge(res ? res.unreadCount : 0);
            return lastItems;
        }).catch(function () {
            if (list && !lastItems.length) {
                list.innerHTML = '';
                list.appendChild(el('div', 'text-center text-danger small py-4', t('loadError', 'Could not load notifications.')));
            }
            return [];
        });
    }

    function refreshCount() {
        return getJson(cfg.countUrl).then(function (res) {
            if (res && res.success) setBadge(res.count);
        }).catch(function () { /* bỏ qua: sẽ thử lại ở lần sau */ });
    }

    // Cập nhật lại "x phút trước" mỗi phút (dropdown + trang danh sách)
    function tickRelativeTimes() {
        var elapsed = (Date.now() - loadedAt) / 1000;
        document.querySelectorAll('#appNotif [data-notif-age]').forEach(function (node) {
            var base = parseFloat(node.getAttribute('data-notif-age')) || 0;
            var text = relative(base + elapsed, node.title);
            if (text) node.textContent = text;
        });
    }
    var pageLoadedAt = Date.now();
    function tickPageTimes() {
        var elapsed = (Date.now() - pageLoadedAt) / 1000;
        document.querySelectorAll('.app-main [data-notif-age]').forEach(function (node) {
            var base = parseFloat(node.getAttribute('data-notif-age')) || 0;
            var text = relative(base + elapsed, '');
            if (text) node.textContent = text;
        });
    }
    setInterval(function () { tickRelativeTimes(); tickPageTimes(); }, 60000);

    // ---------- Mở thông báo: đánh dấu đã đọc rồi chuyển trang ----------
    function openNotification(id, url) {
        return postJson(cfg.markReadUrl, { id: id }).then(function (res) {
            if (res) setBadge(res.unreadCount);
            var target = (res && res.url) || url;
            if (target) window.location.href = target;
            else loadLatest();
        }).catch(function () {
            if (url) window.location.href = url;
        });
    }

    document.addEventListener('click', function (e) {
        var link = e.target.closest ? e.target.closest('[data-notif-open]') : null;
        if (!link) return;
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.button === 1) return; // mở tab mới: giữ hành vi mặc định
        e.preventDefault();
        openNotification(link.getAttribute('data-notif-open'), link.getAttribute('data-notif-url'));
    });

    var markAllBtn = root.querySelector('[data-notif-mark-all]');
    if (markAllBtn) {
        markAllBtn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            markAllBtn.disabled = true;
            postJson(cfg.markAllUrl, {}).then(function (res) {
                setBadge(0);
                return loadLatest().then(function () {
                    if (res && res.message && window.AppToast) window.AppToast.success(res.message);
                });
            }).catch(function () {
                if (window.AppToast) window.AppToast.error(t('loadError', 'Could not load notifications.'));
            }).then(function () { markAllBtn.disabled = false; });
        });
    }

    // Tải lại danh sách mỗi khi mở dropdown
    if (toggle) {
        toggle.addEventListener('show.bs.dropdown', function () { loadLatest(); });
    }

    // ---------- Realtime (SignalR) + dự phòng polling ----------
    function startPolling() {
        if (pollTimer) return;
        pollTimer = setInterval(refreshCount, POLL_MS);
    }
    function stopPolling() {
        if (!pollTimer) return;
        clearInterval(pollTimer);
        pollTimer = null;
    }

    function showToastFor(id) {
        if (!window.AppToast) return;
        var item = null;
        for (var i = 0; i < lastItems.length; i++) {
            if (String(lastItems[i].id) === String(id)) { item = lastItems[i]; break; }
        }
        var text = item ? (item.title + '\n' + item.message) : t('newNotification', 'New notification');
        var toastEl = window.AppToast.info(text);
        if (toastEl && item && toastEl.addEventListener) {
            toastEl.style.cursor = 'pointer';
            toastEl.addEventListener('click', function (e) {
                if (e.target.closest && e.target.closest('.btn-close')) return;
                openNotification(item.id, item.url);
            });
        }
    }

    function connect() {
        if (!window.signalR || !window.signalR.HubConnectionBuilder) {
            startPolling();
            return;
        }
        var connection = new window.signalR.HubConnectionBuilder()
            .withUrl(cfg.hubUrl)
            .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
            .build();

        connection.on('Notification_New', function (payload) {
            if (payload && payload.unreadCount !== undefined) setBadge(payload.unreadCount);
            loadLatest().then(function () { showToastFor(payload ? payload.id : null); });
        });
        connection.on('Notification_Count', function (payload) {
            if (payload && payload.unreadCount !== undefined) setBadge(payload.unreadCount);
        });

        connection.onreconnecting(function () { startPolling(); });
        connection.onreconnected(function () { stopPolling(); refreshCount(); });
        connection.onclose(function () {
            // Hết lượt tự kết nối lại: chuyển sang polling và thử kết nối lại định kỳ
            startPolling();
            setTimeout(function () { start(); }, POLL_MS);
        });

        function start() {
            return connection.start().then(function () {
                stopPolling();
            }).catch(function () {
                startPolling();
                setTimeout(start, POLL_MS);
            });
        }
        start();
    }

    // Khởi tạo
    refreshCount();
    connect();
})(window, document);
