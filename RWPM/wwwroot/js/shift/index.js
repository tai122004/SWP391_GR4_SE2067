// Views/Shift/Index.cshtml — bật/tắt trạng thái hoạt động của ca (nút .status-toggle).
// Cấu hình: window.RWPM.shiftIndex = { urls: { updateActiveStatus }, i18n: { statusUpdateFailed } }
(function (cfg) {
    'use strict';
    document.querySelectorAll('.status-toggle').forEach(function (button) {
        button.addEventListener('click', async function () {
            const oldValue = button.dataset.active === 'true';
            const newValue = !oldValue;
            button.disabled = true;
            try {
                const response = await fetch(cfg.urls.updateActiveStatus + '/' + button.dataset.id, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({ shiftId: Number(button.dataset.id), isActive: newValue })
                });
                if (!response.ok) {
                    const text = (await response.text() || '').trim();
                    throw new Error(text && text.charAt(0) !== '{' && text.charAt(0) !== '<' ? text : '');
                }

                button.dataset.active = String(newValue);
                button.setAttribute('aria-pressed', String(newValue));
                const activeLabel = button.dataset.activeLabel;
                const inactiveLabel = button.dataset.inactiveLabel;
                button.title = newValue ? activeLabel : inactiveLabel;
                button.querySelector('.status-dot').classList.toggle('is-active', newValue);
                button.querySelector('.status-dot').classList.toggle('is-inactive', !newValue);
                button.querySelector('span:nth-child(2)').textContent = newValue ? activeLabel : inactiveLabel;
            } catch (error) {
                if (typeof AppToast !== 'undefined') AppToast.error(error.message || cfg.i18n.statusUpdateFailed);
            } finally {
                button.disabled = false;
            }
        });
    });
})(window.RWPM.shiftIndex);
