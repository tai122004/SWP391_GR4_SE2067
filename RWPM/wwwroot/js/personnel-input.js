// Loaded only by personnel/store forms. Passwords and attendance forms are excluded.
(function () {
    const names = new Set(['Username', 'FullName', 'Email', 'PhoneNumber', 'EmployeeCode', 'CitizenId',
        'ResignReason', 'StoreCode', 'StoreName', 'Address', 'Phone']);
    const fields = Array.from(document.querySelectorAll('form input, form textarea'))
        .filter(el => names.has(el.name) && el.type !== 'password');
    function normalize(el) { el.value = el.value.trim(); }
    fields.forEach(el => {
        el.addEventListener('blur', () => normalize(el), true);
        if (window.jQuery && jQuery.validator && jQuery(el).closest('form').data('validator'))
            jQuery(el).rules('add', { normalizer: value => value.trim() });
    });
    new Set(fields.map(el => el.form)).forEach(form => {
        if (form) form.addEventListener('submit', () => fields.filter(el => el.form === form).forEach(normalize), true);
    });
})();
