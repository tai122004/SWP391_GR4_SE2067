document.querySelectorAll('.shift-store-picker').forEach(function (picker) {
    const toggle = picker.querySelector('[data-bs-toggle="dropdown"]');
    const search = picker.querySelector('.store-search');
    const options = Array.from(picker.querySelectorAll('.store-option'));
    const details = picker.querySelector('.store-selection-details');
    const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
    function update() {
        const names = options.filter(option => option.querySelector('input').checked)
            .map(option => option.querySelector('label').textContent.trim());
        toggle.textContent = names.length === 1 ? names[0] : names.length
            ? picker.dataset.selectedFormat.replace('{0}', names.length) : picker.dataset.placeholder;
        details.hidden = names.length < 2;
        picker.querySelector('.store-selection-names').textContent = names.join(', ');
    }
    search.addEventListener('input', function () {
        const query = normalize(search.value);
        options.forEach(option => { option.hidden = !normalize(option.querySelector('label').textContent).includes(query); });
        picker.querySelector('.store-no-results').hidden = options.some(option => !option.hidden);
    });
    picker.querySelector('.store-select-all').addEventListener('click', function () {
        options.forEach(option => { option.querySelector('input').checked = true; });
        update();
    });
    picker.querySelector('.store-clear-all').addEventListener('click', function () {
        options.forEach(option => { option.querySelector('input').checked = false; });
        update();
    });
    options.forEach(option => option.querySelector('input').addEventListener('change', update));
    picker.addEventListener('shown.bs.dropdown', function () { search.focus(); });
    picker.closest('form').addEventListener('reset', function () { setTimeout(update, 0); });
    update();
});
