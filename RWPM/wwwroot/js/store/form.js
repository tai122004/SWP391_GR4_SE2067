/*
 * Form chi nhánh dùng chung cho Views/Store/Create.cshtml và Views/Store/Edit.cshtml:
 * bản đồ Leaflet chọn vị trí, bán kính chấm công, tìm địa chỉ / reverse geocode.
 * Nạp sau leaflet.js. Không có hàm nào được gọi từ markup.
 * Config: window.RWPM.storeForm = {
 *   urls: { reverseGeocode, searchAddress },
 *   i18n: { mapStreets, mapSatellite, mapOsm, location, emptySearch, searching, searchBtn,
 *           serverError, notFound ("{0}" = từ khóa), searchError ("{0}" = lỗi) }
 * }
 */
(function (cfg) {
    'use strict';

    const i18n = cfg.i18n;

    function storeEsc(t) { return String(t == null ? '' : t).replace(/[&<>"']/g, function (c) { return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[c]; }); }
    document.addEventListener("DOMContentLoaded", function () {
        var defaultLat = 21.025142;
        var defaultLng = 105.553973;

        var latInput = document.getElementById("storeLat");
        var lngInput = document.getElementById("storeLng");
        var mainAddressInput = document.getElementById("storeAddress");
        var minDistInput = document.getElementById("storeMinDistance");
        var radiusInput = document.getElementById("storeRadius");
        var searchInput = document.getElementById("txtAddressSearch");
        var btnSearch = document.getElementById("btnSearchAddress");
        var suggestionsList = document.getElementById("searchSuggestionsList");

        function parseCoord(val, defaultVal) {
            if (val === null || val === undefined || val === '') return defaultVal;
            if (typeof val === 'number') return val;
            var str = String(val).replace(',', '.').trim();
            var num = parseFloat(str);
            return isNaN(num) ? defaultVal : num;
        }

        function syncAddressText(text) {
            if (!text) return;
            var cleanText = text.trim();
            if (cleanText.length > 250) cleanText = cleanText.substring(0, 250);
            if (mainAddressInput) {
                mainAddressInput.value = cleanText;
            }
            if (searchInput && searchInput.value !== cleanText) {
                searchInput.value = cleanText;
            }
        }

        // Real-time 2-way typing sync between Address input and Search input
        if (searchInput) {
            searchInput.addEventListener("input", function () {
                if (mainAddressInput) mainAddressInput.value = this.value;
            });
        }
        if (mainAddressInput) {
            mainAddressInput.addEventListener("input", function () {
                if (searchInput) searchInput.value = this.value;
            });
            if (mainAddressInput.value && searchInput) {
                searchInput.value = mainAddressInput.value;
            }
        }

        var initialLat = parseCoord(latInput ? latInput.value : null, defaultLat);
        var initialLng = parseCoord(lngInput ? lngInput.value : null, defaultLng);
        var initialMinDist = parseInt(minDistInput.value) || 0;
        var initialRadius = parseInt(radiusInput.value) || 100;

        var hasSelectedLocation = !!(latInput && lngInput && latInput.value && lngInput.value);
        var locationPrompt = document.getElementById("storeLocationPrompt");
        if (locationPrompt) locationPrompt.classList.toggle("d-none", hasSelectedLocation);

        // Google Maps Tile Layers
        var googleStreets = L.tileLayer('https://{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', {
            maxZoom: 20,
            subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
            attribution: '&copy; Google Maps'
        });

        var googleSat = L.tileLayer('https://{s}.google.com/vt/lyrs=s,h&x={x}&y={y}&z={z}', {
            maxZoom: 20,
            subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
            attribution: '&copy; Google Maps Satellite'
        });

        var osmLayer = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap'
        });

        var map = L.map('storeMap', {
            center: [initialLat, initialLng],
            zoom: 15,
            layers: [googleStreets]
        });

        var baseMaps = {};
        baseMaps[i18n.mapStreets] = googleStreets;
        baseMaps[i18n.mapSatellite] = googleSat;
        baseMaps[i18n.mapOsm] = osmLayer;

        L.control.layers(baseMaps).addTo(map);

        setTimeout(function () {
            map.invalidateSize();
        }, 300);

        window.addEventListener('resize', function () {
            map.invalidateSize();
        });

        var marker = hasSelectedLocation ? L.marker([initialLat, initialLng], { draggable: true }).addTo(map) : null;

        var minCircle = L.circle([initialLat, initialLng], {
            color: '#B42318',
            dashArray: '6, 6',
            fillColor: '#B42318',
            fillOpacity: 0.15,
            radius: initialMinDist
        });

        var outerCircle = L.circle([initialLat, initialLng], {
            color: '#1554BB',
            fillColor: '#1554BB',
            fillOpacity: 0.1,
            radius: initialRadius
        });

        if (hasSelectedLocation) {
            minCircle.addTo(map);
            outerCircle.addTo(map);
        }

        function attachMarkerDrag() {
            marker.on('dragend', function (e) {
                var position = marker.getLatLng();
                updateMap(position.lat, position.lng);
                reverseGeocode(position.lat, position.lng);
            });
        }
        if (marker) attachMarkerDrag();

        function updateMap(lat, lng) {
            lat = parseCoord(lat, NaN);
            lng = parseCoord(lng, NaN);
            if (!Number.isFinite(lat) || !Number.isFinite(lng) || lat < -90 || lat > 90 || lng < -180 || lng > 180) return;

            if (!marker) {
                marker = L.marker([lat, lng], { draggable: true }).addTo(map);
                minCircle.addTo(map);
                outerCircle.addTo(map);
                attachMarkerDrag();
            }
            marker.setLatLng([lat, lng]);
            minCircle.setLatLng([lat, lng]);
            outerCircle.setLatLng([lat, lng]);
            map.panTo([lat, lng]);

            if (latInput) latInput.value = lat.toFixed(6);
            if (lngInput) lngInput.value = lng.toFixed(6);
            if (locationPrompt) locationPrompt.classList.add("d-none");
        }

        function reverseGeocode(lat, lng) {
            var url = cfg.urls.reverseGeocode + "?lat=" + lat + "&lng=" + lng;
            fetch(url)
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    if (data && data.address) {
                        syncAddressText(data.address);
                    }
                })
                .catch(function () {});
        }

        function updateRadius() {
            var minD = parseInt(minDistInput.value) || 0;
            var maxD = parseInt(radiusInput.value) || 100;

            minCircle.setRadius(minD);
            outerCircle.setRadius(maxD);
        }

        map.on('click', function (e) {
            updateMap(e.latlng.lat, e.latlng.lng);
            reverseGeocode(e.latlng.lat, e.latlng.lng);
        });

        minDistInput.addEventListener('input', updateRadius);
        radiusInput.addEventListener('input', updateRadius);

        // Quick location buttons
        document.querySelectorAll(".btn-quick-loc").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var lat = parseFloat(this.getAttribute("data-lat"));
                var lng = parseFloat(this.getAttribute("data-lng"));
                map.setView([lat, lng], 16);
            });
        });

        function renderSuggestions(data) {
            if (!suggestionsList) return;
            suggestionsList.innerHTML = "";

            if (!data || data.length === 0) {
                suggestionsList.classList.add("d-none");
                return;
            }

            data.forEach(function (item) {
                var a = document.createElement("a");
                a.href = "#";
                a.className = "list-group-item list-group-item-action py-2 px-3 small";
                var addrText = item.display_name || item.address || i18n.location;
                a.innerHTML = '<i class="bi bi-geo-alt text-muted me-2"></i>' + storeEsc(addrText);
                a.addEventListener("click", function (e) {
                    e.preventDefault();
                    var lat = parseCoord(item.lat, NaN);
                    var lon = parseCoord(item.lon || item.lng, NaN);
                    updateMap(lat, lon);
                    map.setZoom(16);
                    suggestionsList.classList.add("d-none");
                    syncAddressText(addrText);
                });
                suggestionsList.appendChild(a);
            });

            suggestionsList.classList.remove("d-none");
        }

        document.addEventListener("click", function (e) {
            if (suggestionsList && !e.target.closest("#txtAddressSearch") && !e.target.closest("#searchSuggestionsList")) {
                suggestionsList.classList.add("d-none");
            }
        });

        function executeAddressSearch(queryText) {
            var query = queryText || (searchInput ? searchInput.value.trim() : '');
            if (!query) {
                AppToast.warning(i18n.emptySearch);
                return;
            }

            if (btnSearch) {
                btnSearch.disabled = true;
                btnSearch.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> ' + storeEsc(i18n.searching);
            }

            var url = cfg.urls.searchAddress + "?query=" + encodeURIComponent(query);
            fetch(url)
                .then(function (res) {
                    if (!res.ok) throw new Error(i18n.serverError + res.status + ")");
                    return res.json();
                })
                .then(function (data) {
                    if (btnSearch) {
                        btnSearch.disabled = false;
                        btnSearch.innerHTML = '<i class="bi bi-geo-alt me-1"></i> ' + storeEsc(i18n.searchBtn);
                    }
                    if (data && data.length > 0) {
                        renderSuggestions(data);
                    } else {
                        if (suggestionsList) suggestionsList.classList.add("d-none");
                        AppToast.warning(i18n.notFound.replace('{0}', query));
                    }
                })
                .catch(function (err) {
                    if (btnSearch) {
                        btnSearch.disabled = false;
                        btnSearch.innerHTML = '<i class="bi bi-geo-alt me-1"></i> ' + storeEsc(i18n.searchBtn);
                    }
                    if (suggestionsList) suggestionsList.classList.add("d-none");
                    AppToast.error(i18n.searchError.replace('{0}', err.message));
                });
        }

        if (btnSearch && searchInput) {
            btnSearch.addEventListener("click", function () { executeAddressSearch(); });
            searchInput.addEventListener("keypress", function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    executeAddressSearch();
                }
            });
        }
    });
})(window.RWPM.storeForm);
