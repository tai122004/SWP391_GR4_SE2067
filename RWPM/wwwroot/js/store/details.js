/*
 * Bản đồ vị trí chi nhánh (Views/Store/Details.cshtml). Chỉ được nạp khi chi nhánh có tọa độ, sau leaflet.js.
 * Config: window.RWPM.storeDetails = {
 *   lat, lng, radius,            // số
 *   storeName, address,          // chuỗi cho popup
 *   i18n: { mapGoogle, mapOsm }  // tên lớp bản đồ
 * }
 */
(function (cfg) {
    'use strict';

    document.addEventListener("DOMContentLoaded", function () {
        var lat = cfg.lat;
        var lng = cfg.lng;
        var radius = cfg.radius;

        var mapContainer = document.getElementById('detailsMap');
        if (!mapContainer) return;

        var map = L.map('detailsMap', {
            center: [lat, lng],
            zoom: 16,
            zoomControl: true
        });

        // Google Streets Layer for high reliability
        var googleStreets = L.tileLayer('https://{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', {
            maxZoom: 20,
            subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
            attribution: '&copy; Google Maps'
        });

        // OpenStreetMap Layer as fallback
        var osmLayer = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap'
        });

        // Default layer
        googleStreets.addTo(map);

        // Add Layer Control
        var layers = {};
        layers[cfg.i18n.mapGoogle] = googleStreets;
        layers[cfg.i18n.mapOsm] = osmLayer;
        L.control.layers(layers).addTo(map);

        // Marker Pin
        var popup = document.createElement('div');
        var popupName = document.createElement('strong');
        popupName.textContent = cfg.storeName;
        popup.appendChild(popupName);
        popup.appendChild(document.createElement('br'));
        popup.appendChild(document.createTextNode(cfg.address));
        L.marker([lat, lng]).addTo(map).bindPopup(popup).openPopup();

        // Circle Radius
        L.circle([lat, lng], {
            color: '#1554BB',
            fillColor: '#1554BB',
            fillOpacity: 0.2,
            radius: radius
        }).addTo(map);

        // Ensure map tiles load correctly after layout calculation
        setTimeout(function () {
            map.invalidateSize();
        }, 300);

        // Recalculate tile sizes when switching to Info tab
        var infoTabBtn = document.getElementById('info-tab');
        if (infoTabBtn) {
            infoTabBtn.addEventListener('click', function () {
                setTimeout(function () {
                    map.invalidateSize();
                }, 200);
            });
        }
    });
})(window.RWPM.storeDetails);
