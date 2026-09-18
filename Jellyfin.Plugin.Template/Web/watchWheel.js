(function () {
    'use strict';

    var TWO_PI = Math.PI * 2;

    function value(item, name) {
        if (!item) return undefined;
        return item[name] !== undefined
            ? item[name] : item[name.charAt(0).toLowerCase() + name.slice(1)];
    }

    function isSeries(item) {
        return String(value(item, 'Type') || '').toLowerCase() === 'series';
    }

    function idOf(item) { return String(value(item, 'Id') || ''); }
    function nameOf(item) { return String(value(item, 'Name') || 'Unknown'); }

    function playbackTarget(item) {
        var id = isSeries(item) ? value(item, 'NextEpisodeId') : idOf(item);
        if (!id) return null;
        var ticks = Number(value(item, 'PlaybackPositionTicks'));
        return {
            id: String(id),
            ticks: Number.isFinite(ticks) && ticks > 0 ? Math.floor(ticks) : 0
        };
    }

    function episodeCode(item) {
        var season = value(item, 'NextSeasonNumber');
        var episode = value(item, 'NextEpisodeNumber');
        return season == null || episode == null ? ''
            : 'S' + String(season).padStart(2, '0')
                + 'E' + String(episode).padStart(2, '0');
    }

    function timeLabel(ticks) {
        var seconds = Math.floor(ticks / 10000000);
        var minutes = Math.floor(seconds / 60);
        return minutes + ':' + String(seconds % 60).padStart(2, '0');
    }

    function normalizedUserId(id) {
        return String(id || '').replace(/-/g, '').toLowerCase();
    }

    async function sendSessionPlayback(target) {
        var api = window.ApiClient;
        if (!api || typeof api.deviceId !== 'function'
            || typeof api.getCurrentUserId !== 'function'
            || typeof api.getJSON !== 'function' || typeof api.ajax !== 'function') {
            throw new Error('The authenticated Jellyfin session API is unavailable.');
        }
        var deviceId = api.deviceId();
        var userId = api.getCurrentUserId();
        if (!deviceId || !userId) {
            throw new Error('Cannot identify this browser and its signed-in user. Reload Jellyfin.');
        }
        var query = new URLSearchParams();
        query.set('DeviceId', deviceId);
        var sessions = await api.getJSON(api.getUrl('Sessions?' + query.toString()));
        if (!Array.isArray(sessions)) {
            throw new Error('Jellyfin returned an unexpected session list.');
        }
        // Never fall back to an arbitrary session or another playback device.
        var matches = sessions.filter(function (session) {
            return value(session, 'DeviceId') === deviceId
                && normalizedUserId(value(session, 'UserId')) === normalizedUserId(userId);
        });
        if (matches.length !== 1) {
            throw new Error(matches.length ? 'Multiple matching browser sessions were found. Reload Jellyfin.'
                : 'This browser session was not found. Reload Jellyfin and try again.');
        }
        var session = matches[0];
        if (!value(session, 'Id')) {
            throw new Error('This browser session has no session identifier.');
        }
        if (value(session, 'SupportsRemoteControl') !== true) {
            throw new Error('This browser session is not accepting playback commands. Reload Jellyfin and try again.');
        }
        var command = new URLSearchParams();
        command.set('ItemIds', target.id);
        command.set('PlayCommand', 'PlayNow');
        command.set('StartPositionTicks', String(target.ticks));
        try {
            await api.ajax({
                type: 'POST',
                url: api.getUrl('Sessions/' + encodeURIComponent(value(session, 'Id'))
                    + '/Playing?' + command.toString())
            });
        } catch (error) {
            var status = error && (error.status || error.statusCode);
            throw new Error('Jellyfin rejected the playback command'
                + (status ? ' (HTTP ' + status + ')' : '') + '.');
        }
    }

    function createApp(page) {
        var state = {
            items: [], pool: [], removed: new Set(), history: [], winner: null, rotation: 0,
            spinning: false, loading: false, playing: false, request: 0
        };
        function byId(id) { return page.querySelector('#' + id); }
        var canvas = byId('watchWheelCanvas');
        var context = canvas.getContext('2d');
        function message(text) { byId('wwMessage').textContent = text || ''; }
        function count() { byId('candidateCount').textContent = String(eligibleItems().length); }
        function busy() { return state.spinning || state.loading || state.playing; }
        var filterIds = ['wwType', 'wwGenre', 'wwDecade', 'wwWatchStatus'];
        var storageKey = preferenceKey();
        var savedPreferences = {};

        function preferenceKey() {
            var api = window.ApiClient;
            var server = api && typeof api.serverId === 'function' && api.serverId();
            var user = api && typeof api.getCurrentUserId === 'function' && api.getCurrentUserId();
            return server && user ? 'watchwheel:v1:' + encodeURIComponent(server)
                + ':' + encodeURIComponent(normalizedUserId(user)) : null;
        }

        function storageNotice(text) { byId('wwStorageNote').textContent = text; }
        function readSaved() {
            if (!storageKey) {
                storageNotice('Settings and recent picks are available for this visit only.');
                return;
            }
            try {
                var saved = JSON.parse(window.localStorage.getItem(storageKey) || '{}');
                if (!saved || typeof saved !== 'object' || Array.isArray(saved)) throw new Error('Invalid saved data');
                savedPreferences = saved.preferences && typeof saved.preferences === 'object'
                    ? saved.preferences : {};
                state.history = Array.isArray(saved.history) ? saved.history.filter(function (entry) {
                    return entry && typeof entry.id === 'string' && entry.id
                        && typeof entry.name === 'string' && typeof entry.pickedAt === 'string'
                        && Number.isFinite(Date.parse(entry.pickedAt));
                }).slice(0, 10).map(function (entry) {
                    return {
                        id: entry.id, name: entry.name, type: entry.type === 'Series' ? 'Series' : 'Movie',
                        episodeId: typeof entry.episodeId === 'string' ? entry.episodeId : '',
                        episode: typeof entry.episode === 'string' ? entry.episode : '', pickedAt: entry.pickedAt
                    };
                }) : [];
                storageNotice('Saved for your Jellyfin account in this browser. Recent picks are not watched history.');
            } catch (error) {
                storageNotice('Saved settings could not be read. You can keep using the wheel.');
            }
        }

        function savePreferences() {
            var preferences = { avoidRecent: byId('wwAvoidRecent').checked };
            filterIds.forEach(function (id) { preferences[id] = byId(id).value; });
            savedPreferences = preferences;
            if (!storageKey || storageKey !== preferenceKey()) return;
            try {
                window.localStorage.setItem(storageKey, JSON.stringify({
                    preferences: preferences, history: state.history
                }));
                storageNotice('Saved for your Jellyfin account in this browser. Recent picks are not watched history.');
            } catch (error) {
                storageNotice('This browser could not save your settings. Changes last for this visit only.');
            }
        }

        function restorePreferences() {
            filterIds.forEach(function (id) {
                var select = byId(id);
                var saved = savedPreferences[id];
                if (typeof saved === 'string' && Array.from(select.options).some(function (option) {
                    return option.value === saved;
                })) select.value = saved;
            });
            byId('wwAvoidRecent').checked = savedPreferences.avoidRecent === true;
        }

        function eligibleItems() {
            var recent = new Set(byId('wwAvoidRecent').checked
                ? state.history.map(function (entry) { return entry.id; }) : []);
            return state.pool.filter(function (item) {
                return !state.removed.has(idOf(item)) && !recent.has(idOf(item));
            });
        }

        function poolMessage() {
            if (eligibleItems().length) return 'Wheel ready.';
            var remaining = state.pool.filter(function (item) { return !state.removed.has(idOf(item)); });
            return remaining.length && byId('wwAvoidRecent').checked
                ? 'All matching titles are recent picks. Turn off Avoid recent picks or clear history.'
                : 'No choices remain. Adjust your filters or refresh the wheel.';
        }

        function renderHistory() {
            var list = byId('wwHistoryList');
            list.textContent = '';
            byId('wwHistoryEmpty').classList.toggle('hidden', state.history.length > 0);
            state.history.forEach(function (entry) {
                var row = document.createElement('li'); row.className = 'wwHistoryRow';
                var info = document.createElement('div'); info.className = 'wwHistoryInfo';
                var title = document.createElement('strong'); title.textContent = entry.name;
                var detail = document.createElement('div'); detail.className = 'winnerMeta';
                detail.textContent = [entry.type, entry.episode, new Date(entry.pickedAt).toLocaleString()]
                    .filter(Boolean).join(' • ');
                info.appendChild(title); info.appendChild(detail); row.appendChild(info);
                function addButton(label, id) {
                    var button = document.createElement('button');
                    button.type = 'button'; button.className = 'raised emby-button'; button.textContent = label;
                    button.addEventListener('click', function () { if (!busy()) openDetails(id); });
                    row.appendChild(button);
                }
                addButton(entry.type === 'Series' ? 'Series Details' : 'Movie Details', entry.id);
                if (entry.type === 'Series' && entry.episodeId) addButton('Episode Details', entry.episodeId);
                list.appendChild(row);
            });
        }

        function rememberWinner(item) {
            state.history.unshift({
                id: idOf(item), name: nameOf(item), type: isSeries(item) ? 'Series' : 'Movie',
                episodeId: isSeries(item) ? String(value(item, 'NextEpisodeId') || '') : '',
                episode: isSeries(item) ? [episodeCode(item), value(item, 'NextEpisodeName')].filter(Boolean).join(' • ') : '',
                pickedAt: new Date().toISOString()
            });
            state.history = state.history.slice(0, 10);
            savePreferences(); renderHistory(); count();
        }

        function refreshLocalPool() {
            state.items = eligibleItems(); state.rotation = 0;
            count(); drawWheel(); syncButtons(); message(poolMessage());
        }

        function clearHistory() {
            if (busy()) return;
            state.history = []; savePreferences(); renderHistory(); refreshLocalPool();
        }

        function syncButtons() {
            var locked = busy();
            var available = eligibleItems().length;
            byId('wwSpin').disabled = locked || !available;
            byId('wwApplyFilters').disabled = locked;
            byId('wwAgain').disabled = locked || !available;
            byId('wwRemove').disabled = locked || !state.winner;
            byId('wwPlay').disabled = locked || !playbackTarget(state.winner);
            byId('wwOpen').disabled = locked || !state.winner;
            byId('wwEpisode').disabled = locked || !state.winner;
            byId('wwClearHistory').disabled = locked || !state.history.length;
            byId('wwAvoidRecent').disabled = locked;
            filterIds.forEach(function (id) { byId(id).disabled = locked; });
        }
        function hideWinner() {
            state.winner = null;
            byId('winnerCard').classList.add('hidden');
        }

        function posterUrl(id) {
            if (typeof window.ApiClient.getImageUrl === 'function') {
                return window.ApiClient.getImageUrl(id, { type: 'Primary', maxWidth: 500, quality: 90 });
            }
            return window.ApiClient.getUrl('Items/' + encodeURIComponent(id) + '/Images/Primary');
        }

        function showWinner(item) {
            state.winner = item;
            byId('winnerTitle').textContent = nameOf(item);
            var parts = [value(item, 'Type'), value(item, 'Year')].filter(Boolean);
            var rating = value(item, 'CommunityRating');
            if (rating != null && Number.isFinite(Number(rating))) parts.push('★ ' + Number(rating).toFixed(1));
            var remaining = value(item, 'RemainingEpisodes');
            if (isSeries(item) && remaining != null) {
                parts.push(remaining + (Number(remaining) === 1 ? ' episode remaining' : ' episodes remaining'));
            }
            byId('winnerMeta').textContent = parts.join(' • ');
            var target = playbackTarget(item);
            var episode = [episodeCode(item), value(item, 'NextEpisodeName')].filter(Boolean).join(' • ');
            byId('winnerEpisode').textContent = isSeries(item)
                ? (target ? 'Next: ' + (episode || 'Next unwatched episode') : 'No next episode available. Refresh the wheel.') : '';
            byId('winnerResume').textContent = target && target.ticks > 0
                ? 'Resume at ' + timeLabel(target.ticks) : '';
            var action = target && target.ticks > 0 ? 'Resume' : 'Play';
            var playLabel = action;
            if (isSeries(item)) {
                playLabel = target
                    ? action + ' ' + (episodeCode(item) || 'Next Episode')
                    : 'No Episode Available';
            }
            byId('wwPlay').textContent = playLabel;
            byId('wwPlay').title = isSeries(item) && target
                ? action + ': ' + (episode || 'Next unwatched episode')
                : playLabel;
            byId('wwOpen').textContent = isSeries(item) ? 'Series Details' : 'Movie Details';
            byId('wwEpisode').classList.toggle('hidden', !isSeries(item) || !target);
            var genres = byId('winnerGenres');
            genres.textContent = '';
            (value(item, 'Genres') || []).forEach(function (genre) {
                var tag = document.createElement('span');
                tag.className = 'winnerGenre';
                tag.textContent = genre;
                genres.appendChild(tag);
            });
            byId('winnerOverview').textContent = value(item, 'Overview') || 'No overview available.';
            var poster = byId('winnerPoster');
            poster.style.display = 'block';
            poster.onerror = function () { poster.style.display = 'none'; };
            poster.alt = nameOf(item);
            // Artwork and series details deliberately retain the series identifier.
            poster.src = posterUrl(idOf(item));
            byId('winnerCard').classList.remove('hidden');
            syncButtons();
            byId('winnerCard').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }

        function openDetails(id) {
            if (!id) return;
            var serverId = typeof window.ApiClient.serverId === 'function' ? window.ApiClient.serverId() : '';
            window.location.hash = '#/details?id=' + encodeURIComponent(id)
                + (serverId ? '&serverId=' + encodeURIComponent(serverId) : '');
        }

        async function playWinner() {
            if (busy() || !state.winner) return;
            var target = playbackTarget(state.winner);
            if (!target) { message('No episode is available. Refresh the wheel.'); return; }
            state.playing = true;
            syncButtons();
            message(target.ticks > 0 ? 'Resuming playback...' : 'Starting playback...');
            try {
                await sendSessionPlayback(target);
                message('Playback requested.');
            } catch (error) {
                console.error('Watch Wheel playback failed:', error);
                message((error.message || 'Could not start playback here.') + ' Use '
                    + (isSeries(state.winner) ? 'Episode Details' : 'Movie Details')
                    + ' to play it in Jellyfin.');
            } finally {
                state.playing = false;
                syncButtons();
            }
        }

        async function loadFilters() {
            var result = await window.ApiClient.getJSON(window.ApiClient.getUrl('WatchWheel/Filters'));
            function populate(id, items, label, suffix) {
                var select = byId(id);
                select.textContent = '';
                var any = document.createElement('option');
                any.value = ''; any.textContent = label; select.appendChild(any);
                items.forEach(function (item) {
                    var option = document.createElement('option');
                    option.value = String(item); option.textContent = String(item) + suffix;
                    select.appendChild(option);
                });
            }
            populate('wwGenre', value(result, 'Genres') || [], 'Any Genre', '');
            populate('wwDecade', value(result, 'Decades') || [], 'Any Decade', 's');
        }

        async function loadCandidates() {
            if (busy()) return;
            savePreferences();
            state.removed.clear();
            var request = ++state.request;
            state.loading = true;
            hideWinner();
            syncButtons();
            message('Loading your Jellyfin library...');
            var params = new URLSearchParams();
            params.set('type', byId('wwType').value || 'both');
            if (byId('wwGenre').value) params.set('genre', byId('wwGenre').value);
            if (byId('wwDecade').value) params.set('decade', byId('wwDecade').value);
            var watchStatus = byId('wwWatchStatus').value || 'all';
            params.set('includeInProgress', watchStatus === 'not-started' ? 'false' : 'true');
            try {
                var result = await window.ApiClient.getJSON(window.ApiClient.getUrl('WatchWheel/Items?' + params));
                if (request !== state.request) return;
                state.pool = (value(result, 'Items') || []).filter(function (item) {
                    // Use the backend's episode-aware status, not the next episode's resume ticks.
                    if (watchStatus === 'in-progress') return value(item, 'IsInProgress') === true;
                    if (watchStatus === 'not-started') return value(item, 'IsInProgress') === false;
                    return true;
                });
                state.items = eligibleItems();
                state.rotation = 0;
                var statusLabel = watchStatus === 'in-progress' ? 'In progress'
                    : watchStatus === 'not-started' ? 'Not started' : 'All unwatched';
                message(state.items.length ? 'Wheel ready · ' + statusLabel + '.'
                    : poolMessage());
            } catch (error) {
                state.items = []; state.pool = [];
                console.error('Watch Wheel failed to load items:', error);
                message('Unable to load titles. Try Refresh Wheel again.');
            } finally {
                state.loading = false;
                count(); drawWheel(); syncButtons();
            }
        }

        function drawWheel() {
            var cx = canvas.width / 2, cy = canvas.height / 2;
            var radius = Math.min(cx, cy) - 12;
            context.clearRect(0, 0, canvas.width, canvas.height);
            if (!state.items.length) {
                context.beginPath(); context.arc(cx, cy, radius, 0, TWO_PI);
                context.fillStyle = 'rgba(255,255,255,0.06)'; context.fill();
                context.fillStyle = 'rgba(255,255,255,0.72)'; context.font = '600 28px sans-serif';
                context.textAlign = 'center'; context.textBaseline = 'middle';
                context.fillText('No choices', cx, cy);
                return;
            }
            var arc = TWO_PI / state.items.length;
            state.items.forEach(function (item, index) {
                var start = state.rotation + index * arc;
                context.beginPath(); context.moveTo(cx, cy);
                context.arc(cx, cy, radius, start, start + arc); context.closePath();
                context.fillStyle = 'hsl(' + ((index * 360 / state.items.length + 195) % 360) + ',64%,43%)';
                context.fill(); context.strokeStyle = 'rgba(255,255,255,0.22)'; context.lineWidth = 2; context.stroke();
                if (state.items.length > 60) return;
                var font = state.items.length <= 12 ? 19 : state.items.length <= 25 ? 15 : 11;
                var limit = state.items.length <= 20 ? 22 : 14;
                var title = nameOf(item);
                if (title.length > limit) title = title.slice(0, limit - 1) + '…';
                context.save(); context.translate(cx, cy); context.rotate(start + arc / 2);
                context.translate(radius * 0.68, 0); context.rotate(Math.PI / 2);
                context.fillStyle = '#fff'; context.font = '700 ' + font + 'px sans-serif';
                context.textAlign = 'center'; context.textBaseline = 'middle';
                context.shadowColor = 'rgba(0,0,0,0.75)'; context.shadowBlur = 4;
                context.fillText(title, 0, 0); context.restore();
            });
            context.beginPath(); context.arc(cx, cy, radius, 0, TWO_PI);
            context.strokeStyle = 'rgba(255,255,255,0.78)'; context.lineWidth = 7; context.stroke();
        }

        function spin() {
            if (busy()) return;
            state.items = eligibleItems();
            if (!state.items.length) { syncButtons(); message(poolMessage()); return; }
            hideWinner(); state.spinning = true; syncButtons(); message('Spinning...');
            var index = Math.floor(Math.random() * state.items.length);
            var arc = TWO_PI / state.items.length;
            var start = state.rotation;
            var desired = -Math.PI / 2 - (index * arc + arc / 2);
            var delta = ((desired - start) % TWO_PI + TWO_PI) % TWO_PI;
            var end = start + delta + TWO_PI * (6 + Math.floor(Math.random() * 3));
            var duration = 5000 + Math.random() * 1300;
            var started = performance.now();
            function animate(now) {
                if (!page.isConnected) { state.spinning = false; syncButtons(); return; }
                var progress = Math.min((now - started) / duration, 1);
                state.rotation = start + (end - start) * (1 - Math.pow(1 - progress, 5));
                drawWheel();
                if (progress < 1) { requestAnimationFrame(animate); return; }
                state.rotation = ((end % TWO_PI) + TWO_PI) % TWO_PI;
                state.spinning = false;
                var winner = state.items[index];
                rememberWinner(winner);
                message(eligibleItems().length ? 'The wheel has spoken.'
                    : 'The wheel has spoken. ' + poolMessage());
                showWinner(winner);
            }
            requestAnimationFrame(animate);
        }

        function removeWinner() {
            if (busy() || !state.winner) return;
            var id = idOf(state.winner);
            state.removed.add(id);
            hideWinner(); refreshLocalPool();
        }

        async function start() {
            readSaved(); renderHistory();
            filterIds.forEach(function (id) { byId(id).addEventListener('change', savePreferences); });
            byId('wwAvoidRecent').addEventListener('change', function () {
                if (busy()) return;
                savePreferences(); refreshLocalPool();
            });
            byId('wwClearHistory').addEventListener('click', clearHistory);
            byId('wwApplyFilters').addEventListener('click', loadCandidates);
            byId('wwSpin').addEventListener('click', spin);
            byId('wwAgain').addEventListener('click', spin);
            byId('wwRemove').addEventListener('click', removeWinner);
            byId('wwPlay').addEventListener('click', playWinner);
            byId('wwOpen').addEventListener('click', function () {
                if (!busy() && state.winner) openDetails(idOf(state.winner));
            });
            byId('wwEpisode').addEventListener('click', function () {
                var target = playbackTarget(state.winner);
                if (!busy() && target) openDetails(target.id);
            });
            state.loading = true; syncButtons(); drawWheel(); message('Loading filters...');
            try { await loadFilters(); }
            catch (error) { console.warn('Watch Wheel filters could not load:', error); }
            restorePreferences();
            state.loading = false;
            await loadCandidates();
        }
        return { start: start };
    }

    var initialized = new WeakSet();
    window.WatchWheelApp = {
        init: function (page) {
            if (!page || initialized.has(page)) return;
            initialized.add(page);
            createApp(page).start().catch(function (error) {
                console.error('Watch Wheel initialization failed:', error);
                var message = page.querySelector('#wwMessage');
                if (message) message.textContent = 'Watch Wheel could not initialize. Reload the page.';
            });
        }
    };
})();
