(function () {
    'use strict';
    const form = document.getElementById('changePasswordForm');
    if (!form) return;

    const summary = document.getElementById('changePasswordFormError');
    const submit = form.querySelector('button[type="submit"]');

    function clearErrors() {
        summary.textContent = '';
        summary.classList.add('d-none');
        form.querySelectorAll('[data-valmsg-for]').forEach(span => { span.textContent = ''; });
        form.querySelectorAll('input.is-invalid').forEach(input => input.classList.remove('is-invalid'));
    }

    function showError(message) {
        summary.textContent = message;
        summary.classList.remove('d-none');
    }

    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        clearErrors();
        submit.disabled = true;
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const result = await response.json();
            if (response.ok && result.success) {
                window.location.assign(result.redirectUrl || '/');
                return;
            }

            let firstInvalid = null;
            Object.entries(result.errors || {}).forEach(([field, messages]) => {
                const input = Array.from(form.querySelectorAll('input')).find(el => el.name === field);
                const span = Array.from(form.querySelectorAll('[data-valmsg-for]')).find(el => el.dataset.valmsgFor === field);
                if (input) { input.classList.add('is-invalid'); firstInvalid ||= input; }
                if (span) span.textContent = (messages || []).join(' ');
                else if (messages?.length) showError(messages.join(' '));
            });
            if (result.message) showError(result.message);
            if (firstInvalid) firstInvalid.focus();
        } catch (error) {
            showError(document.documentElement.lang === 'vi'
                ? 'Không gửi được yêu cầu. Vui lòng thử lại.'
                : 'Unable to submit the request. Please try again.');
        } finally {
            submit.disabled = false;
        }
    });
})();
