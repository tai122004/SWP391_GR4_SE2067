/*
 * Chi tiết đánh giá hiệu suất — Views/Performance/Detail.cshtml (dùng cho cả Detail và MyDetail)
 * Bộ đếm ký tự cho ô nhận xét (#reviewComment -> #commentCount). Không cần config.
 */
(function () {
    'use strict';

    var comment = document.getElementById('reviewComment');
    var counter = document.getElementById('commentCount');
    if (comment && counter) {
        comment.addEventListener('input', function () { counter.textContent = comment.value.length; });
    }
})();
