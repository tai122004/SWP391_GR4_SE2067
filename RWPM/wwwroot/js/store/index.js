/*
 * Trang danh sách chi nhánh (Views/Store/Index.cshtml): bật/tắt trạng thái hoạt động.
 * Config: window.RWPM.storeIndex = {
 *   urls: { updateActiveStatus },
 *   i18n: { active, inactive, updated, failed }
 * }
 */
(function ($, cfg) {
    'use strict';

    const i18n = cfg.i18n;

    $(document).ready(function () {
        $('.active-toggle').change(function () {
            var checkbox = $(this);
            var storeId = checkbox.data('id');
            var isActive = checkbox.is(':checked');

            $.ajax({
                url: cfg.urls.updateActiveStatus + '/' + storeId,
                type: 'POST',
                contentType: 'application/json',
                headers: {
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                },
                data: JSON.stringify({
                    storeId: storeId,
                    isActive: isActive
                }),
                success: function () {
                    var choice = checkbox.closest('.status-choice');
                    choice.find('.status-dot').toggleClass('is-active', isActive).toggleClass('is-inactive', !isActive);
                    choice.find('.status-choice-text').text(isActive ? i18n.active : i18n.inactive);
                    AppToast.success(i18n.updated);
                },
                error: function (xhr) {
                    checkbox.prop('checked', !isActive);
                    AppToast.error(i18n.failed + ' ' + (xhr.responseText || ''));
                }
            });
        });
    });
})(jQuery, window.RWPM.storeIndex);
