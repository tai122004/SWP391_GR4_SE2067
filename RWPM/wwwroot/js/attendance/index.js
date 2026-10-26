/*
 * Chấm công — Views/Attendance/Index.cshtml
 * Chấm vào/ra kèm GPS + ảnh chụp từ camera, ẩn/hiện camera & danh sách nhân viên khi đổi tab,
 * phân trang phía client (10 dòng/trang) cho các bảng lịch sử.
 * Config: window.RWPM.attendanceIndex = {
 *   urls: { checkIn, checkOut },
 *   i18n: { photoRequired, gpsLoading, gpsDenied, gpsUnknown, gpsNotSupported, camError, processing,
 *           locateFailed, employeeList, today, totalRecordBar, paginationPrevious, paginationNext }
 * }
 */
(function ($, cfg) {
    'use strict';

    var attI18n = cfg.i18n;

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.getElementById("attendanceForm");
        var btnCheckIn = document.getElementById("btnCheckIn");
        var btnCheckOut = document.getElementById("btnCheckOut");
        var latInput = document.getElementById("userLatitude");
        var lngInput = document.getElementById("userLongitude");
        var gpsStatus = document.getElementById("gpsStatus");

        function submitWithGPS(actionName, btnElement) {
            if (!form) return;

            if (!photoInput.files || photoInput.files.length === 0) {
                AppToast.warning(attI18n.photoRequired);
                return;
            }

            var targetUrl = actionName === "CheckIn" ? cfg.urls.checkIn : cfg.urls.checkOut;

            // Ngăn chặn click đúp
            if (btnElement) {
                btnElement.disabled = true;
                var originalText = btnElement.innerHTML;
                btnElement.innerHTML = '<i class="spinner-border spinner-border-sm me-1"></i> ' + attI18n.processing;
            }

            if (gpsStatus) {
                gpsStatus.className = "alert alert-info small py-1 mb-2";
                gpsStatus.innerHTML = '<i class="bi bi-geo-alt spinner-grow spinner-grow-sm me-1"></i> ';
                gpsStatus.appendChild(document.createTextNode(attI18n.gpsLoading));
                gpsStatus.classList.remove("d-none");
            }

            function handleGpsSuccess(position) {
                if (latInput) latInput.value = position.coords.latitude;
                if (lngInput) lngInput.value = position.coords.longitude;
                form.action = targetUrl;
                form.submit();
            }

            function handleGpsFailure(error) {
                if (gpsStatus) gpsStatus.classList.add("d-none");
                if (btnElement) {
                    btnElement.disabled = false;
                    btnElement.innerHTML = originalText;
                }

                var errorMsg = "";
                if (error && error.code === 1) { // PERMISSION_DENIED
                    errorMsg = attI18n.gpsDenied;
                } else if (error) {
                    var template = attI18n.gpsUnknown;
                    errorMsg = template.replace("{0}", error.message || attI18n.locateFailed);
                } else {
                    errorMsg = attI18n.gpsNotSupported;
                }
                AppToast.error(errorMsg);
            }

            if (navigator.geolocation) {
                navigator.geolocation.getCurrentPosition(
                    handleGpsSuccess,
                    handleGpsFailure,
                    {
                        enableHighAccuracy: true,
                        timeout: 10000,
                        maximumAge: 0
                    }
                );
            } else {
                handleGpsFailure(null);
            }
        }

        if (btnCheckIn && !btnCheckIn.disabled) {
            btnCheckIn.addEventListener("click", function () {
                submitWithGPS("CheckIn", this);
            });
        }

        if (btnCheckOut && !btnCheckOut.disabled) {
            btnCheckOut.addEventListener("click", function () {
                submitWithGPS("CheckOut", this);
            });
        }

        // --- CAMERA WEBRTC LOGIC ---
        var video = document.getElementById('camera-stream');
        var preview = document.getElementById('camera-preview');
        var canvas = document.getElementById('camera-canvas');
        var btnCapture = document.getElementById('btnCapture');
        var btnRetake = document.getElementById('btnRetake');
        var photoInput = document.getElementById('photoInput');
        var cameraLoading = document.getElementById('camera-loading');
        var personalClockInArea = document.getElementById('personalClockInArea');
        var cameraStream = null;

        function startCamera() {
            if (!video || cameraStream) return;
            // Nếu đã chụp ảnh và đang xem trước thì không bật lại video
            if (preview && preview.style.display === 'block') {
                return;
            }
            if (cameraLoading) cameraLoading.style.display = 'block';

            navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' } })
                .then(function (stream) {
                    cameraStream = stream;
                    video.srcObject = stream;
                    if (cameraLoading) cameraLoading.style.display = 'none';
                })
                .catch(function (err) {
                    console.error("Lỗi Camera:", err);
                    if (cameraLoading) {
                        cameraLoading.textContent = attI18n.camError;
                    }
                });
        }

        function stopCamera() {
            if (cameraStream) {
                cameraStream.getTracks().forEach(function (track) {
                    track.stop();
                });
                cameraStream = null;
            }
            if (video) {
                video.srcObject = null;
            }
        }

        if (video) {
            // Chỉ khởi động camera khi khu vực chấm công cá nhân đang hiển thị
            var isAreaHidden = personalClockInArea && personalClockInArea.classList.contains('d-none');
            if (!isAreaHidden) {
                startCamera();
            }

            if (btnCapture) {
                btnCapture.addEventListener('click', function () {
                    var context = canvas.getContext('2d');
                    canvas.width = video.videoWidth;
                    canvas.height = video.videoHeight;
                    context.drawImage(video, 0, 0, canvas.width, canvas.height);

                    var dataUrl = canvas.toDataURL('image/jpeg');
                    preview.src = dataUrl;

                    video.style.display = 'none';
                    preview.style.display = 'block';
                    btnCapture.style.display = 'none';
                    btnRetake.style.display = 'block';

                    fetch(dataUrl)
                        .then(res => res.blob())
                        .then(blob => {
                            var file = new File([blob], "capture.jpg", { type: "image/jpeg" });
                            var dataTransfer = new DataTransfer();
                            dataTransfer.items.add(file);
                            photoInput.files = dataTransfer.files;
                        });
                });
            }

            if (btnRetake) {
                btnRetake.addEventListener('click', function () {
                    video.style.display = 'block';
                    preview.style.display = 'none';
                    btnCapture.style.display = 'block';
                    btnRetake.style.display = 'none';
                    photoInput.value = "";
                    startCamera();
                });
            }
        }

        // Xử lý ẩn/hiện camera & danh sách nhân viên khi Quản lý & HR chuyển đổi tab
        var empTabBtn = document.getElementById("emp-history-tab");
        var myTabBtn = document.getElementById("my-history-tab");
        var managerEmpList = document.getElementById("managerEmpListContainer");
        var leftPanelHeader = document.getElementById("leftPanelHeader");

        if (empTabBtn) {
            empTabBtn.addEventListener("shown.bs.tab", function () {
                if (personalClockInArea) personalClockInArea.classList.add("d-none");
                if (managerEmpList) managerEmpList.classList.remove("d-none");
                if (leftPanelHeader) leftPanelHeader.textContent = attI18n.employeeList;
                stopCamera();
            });
        }
        if (myTabBtn) {
            myTabBtn.addEventListener("shown.bs.tab", function () {
                if (personalClockInArea) personalClockInArea.classList.remove("d-none");
                if (managerEmpList) managerEmpList.classList.add("d-none");
                if (leftPanelHeader) leftPanelHeader.textContent = attI18n.today;
                startCamera();
            });
        }

        // --- PHÂN TRANG CHO TẤT CẢ CÁC BẢNG LỊCH SỬ (10 DÒNG / TRANG) ---
        var totalRecordBarTemplate = attI18n.totalRecordBar;
        var previousAriaLabel = attI18n.paginationPrevious;
        var nextAriaLabel = attI18n.paginationNext;

        function initTablePagination(tableId, paginationContainerId, pageSize) {
            if (!pageSize) pageSize = 10;
            var table = document.getElementById(tableId);
            var container = document.getElementById(paginationContainerId);
            if (!table || !container) return;

            var tbody = table.querySelector('tbody');
            if (!tbody) return;

            // Lấy tất cả các hàng dữ liệu thực tế (bỏ qua hàng "không có dữ liệu" với td có colspan)
            var allRows = Array.from(tbody.querySelectorAll('tr')).filter(function (tr) {
                return !tr.querySelector('td[colspan]');
            });

            var totalRecords = allRows.length;
            if (totalRecords === 0) {
                container.innerHTML = '';
                container.classList.add('d-none');
                return;
            }

            container.classList.remove('d-none');
            var totalPages = Math.ceil(totalRecords / pageSize);
            var currentPage = 1;

            function renderPage(page) {
                currentPage = page;
                var startIndex = (page - 1) * pageSize;
                var endIndex = Math.min(startIndex + pageSize, totalRecords);

                // Ẩn/hiện các hàng tương ứng với trang
                allRows.forEach(function (row, index) {
                    if (index >= startIndex && index < endIndex) {
                        row.style.display = '';
                    } else {
                        row.style.display = 'none';
                    }
                });

                // Tạo text thông tin bản ghi
                var startDisplay = startIndex + 1;
                var endDisplay = endIndex;
                var infoText = totalRecordBarTemplate
                    .replace('{0}', startDisplay)
                    .replace('{1}', endDisplay)
                    .replace('{2}', totalRecords);

                var paginationHtml = `
                    <div class="text-muted small">
                        ${infoText}
                    </div>
                    <nav class="table-pagination">
                        <ul class="pagination pagination-sm mb-0">
                            <li class="page-item ${currentPage <= 1 ? 'disabled' : ''}">
                                <a class="page-link" href="javascript:void(0)" data-page="${currentPage - 1}" aria-label="${previousAriaLabel}">
                                    <span aria-hidden="true">&laquo;</span>
                                </a>
                            </li>
                `;

                // Thuật toán hiển thị các số trang (tối đa 5 nút, có dấu ... nếu quá dài)
                var maxButtons = 5;
                var startPage = Math.max(1, currentPage - 2);
                var endPage = Math.min(totalPages, startPage + maxButtons - 1);
                if (endPage - startPage + 1 < maxButtons) {
                    startPage = Math.max(1, endPage - maxButtons + 1);
                }

                if (startPage > 1) {
                    paginationHtml += `<li class="page-item"><a class="page-link" href="javascript:void(0)" data-page="1">1</a></li>`;
                    if (startPage > 2) {
                        paginationHtml += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
                    }
                }

                for (var p = startPage; p <= endPage; p++) {
                    paginationHtml += `
                        <li class="page-item ${p === currentPage ? 'active' : ''}">
                            <a class="page-link" href="javascript:void(0)" data-page="${p}">${p}</a>
                        </li>
                    `;
                }

                if (endPage < totalPages) {
                    if (endPage < totalPages - 1) {
                        paginationHtml += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
                    }
                    paginationHtml += `<li class="page-item"><a class="page-link" href="javascript:void(0)" data-page="${totalPages}">${totalPages}</a></li>`;
                }

                paginationHtml += `
                            <li class="page-item ${currentPage >= totalPages ? 'disabled' : ''}">
                                <a class="page-link" href="javascript:void(0)" data-page="${currentPage + 1}" aria-label="${nextAriaLabel}">
                                    <span aria-hidden="true">&raquo;</span>
                                </a>
                            </li>
                        </ul>
                    </nav>
                `;

                container.innerHTML = paginationHtml;

                // Gán sự kiện click cho các link phân trang
                var pageLinks = container.querySelectorAll('a.page-link');
                pageLinks.forEach(function (link) {
                    link.addEventListener('click', function (e) {
                        e.preventDefault();
                        var targetPage = parseInt(this.getAttribute('data-page'));
                        if (!isNaN(targetPage) && targetPage >= 1 && targetPage <= totalPages && targetPage !== currentPage) {
                            renderPage(targetPage);
                        }
                    });
                });
            }

            renderPage(1);
        }

        // Khởi tạo phân trang (10 dòng/trang) cho tất cả các bảng
        initTablePagination("myAttendanceTable", "myAttendancePagination", 10);
        initTablePagination("empAttendanceTable", "empAttendancePagination", 10);
        initTablePagination("employeeSelfAttendanceTable", "employeeSelfAttendancePagination", 10);
    });
})(jQuery, window.RWPM.attendanceIndex);
