(function () {
    'use strict';

    var TWO_PI = Math.PI * 2;

    var state = {
        page: null,
        canvas: null,
        context: null,

        items: [],
        winner: null,

        rotation: 0,
        spinning: false,

        removedIds: new Set()
    };

    function getValue(object, pascalName, camelName) {
        if (!object) {
            return undefined;
        }

        if (object[pascalName] !== undefined) {
            return object[pascalName];
        }

        return object[camelName];
    }

    function idOf(item) {
        return String(
            getValue(item, 'Id', 'id') || ''
        );
    }

    function nameOf(item) {
        return (
            getValue(item, 'Name', 'name')
            || 'Unknown'
        );
    }

    function typeOf(item) {
        return (
            getValue(item, 'Type', 'type')
            || ''
        );
    }

    function yearOf(item) {
        return getValue(
            item,
            'Year',
            'year'
        );
    }

    function genresOf(item) {
        return (
            getValue(item, 'Genres', 'genres')
            || []
        );
    }

    function ratingOf(item) {
        return getValue(
            item,
            'CommunityRating',
            'communityRating'
        );
    }

    function overviewOf(item) {
        return (
            getValue(
                item,
                'Overview',
                'overview'
            )
            || 'No overview available.'
        );
    }

    function byId(id) {
        return state.page.querySelector(
            '#' + id
        );
    }

    function setMessage(message) {
        byId('wwMessage').textContent =
            message || '';
    }

    function setCount() {
        byId('candidateCount').textContent =
            String(state.items.length);
    }

    function getFiltersUrl() {
        return window.ApiClient.getUrl(
            'WatchWheel/Filters'
        );
    }

    function getItemsUrl() {
        var type =
            byId('wwType').value;

        var genre =
            byId('wwGenre').value;

        var decade =
            byId('wwDecade').value;

        var includeInProgress =
            byId('wwInProgress').checked;

        var params = new URLSearchParams();

        params.set(
            'type',
            type || 'both'
        );

        if (genre) {
            params.set(
                'genre',
                genre
            );
        }

        if (decade) {
            params.set(
                'decade',
                decade
            );
        }

        params.set(
            'includeInProgress',
            includeInProgress
                ? 'true'
                : 'false'
        );

        return window.ApiClient.getUrl(
            'WatchWheel/Items?'
            + params.toString()
        );
    }

    async function loadFilters() {
        setMessage(
            'Loading filters...'
        );

        var result =
            await window.ApiClient.getJSON(
                getFiltersUrl()
            );

        var genres =
            getValue(
                result,
                'Genres',
                'genres'
            ) || [];

        var decades =
            getValue(
                result,
                'Decades',
                'decades'
            ) || [];

        var genreSelect =
            byId('wwGenre');

        var decadeSelect =
            byId('wwDecade');

        genreSelect.innerHTML =
            '<option value="">Any Genre</option>';

        decadeSelect.innerHTML =
            '<option value="">Any Decade</option>';

        genres.forEach(function (genre) {
            var option =
                document.createElement(
                    'option'
                );

            option.value = genre;
            option.textContent = genre;

            genreSelect.appendChild(option);
        });

        decades.forEach(function (decade) {
            var option =
                document.createElement(
                    'option'
                );

            option.value =
                String(decade);

            option.textContent =
                String(decade) + 's';

            decadeSelect.appendChild(
                option
            );
        });

        setMessage('');
    }

    async function loadCandidates() {
        if (state.spinning) {
            return;
        }

        hideWinner();

        state.removedIds.clear();

        setMessage(
            'Loading your Jellyfin library...'
        );

        byId('wwSpin').disabled = true;

        try {
            var result =
                await window.ApiClient.getJSON(
                    getItemsUrl()
                );

            state.items =
                (
                    getValue(
                        result,
                        'Items',
                        'items'
                    )
                    || []
                ).filter(function (item) {
                    return !state.removedIds.has(
                        idOf(item)
                    );
                });

            setCount();

            state.rotation = 0;

            drawWheel();

            if (state.items.length === 0) {
                setMessage(
                    'No titles match these filters.'
                );
            } else {
                setMessage(
                    'Wheel ready.'
                );
            }

            byId('wwSpin').disabled =
                state.items.length === 0;
        } catch (error) {
            console.error(
                'Watch Wheel failed to load items:',
                error
            );

            setMessage(
                'Unable to load titles.'
            );
        }
    }

    function truncateTitle(title, maxLength) {
        if (title.length <= maxLength) {
            return title;
        }

        return (
            title.slice(
                0,
                maxLength - 1
            )
            + '…'
        );
    }

    function drawWheel() {
        var canvas =
            state.canvas;

        var context =
            state.context;

        var width =
            canvas.width;

        var height =
            canvas.height;

        var centerX =
            width / 2;

        var centerY =
            height / 2;

        var radius =
            Math.min(
                centerX,
                centerY
            ) - 12;

        context.clearRect(
            0,
            0,
            width,
            height
        );

        if (state.items.length === 0) {
            context.beginPath();

            context.arc(
                centerX,
                centerY,
                radius,
                0,
                TWO_PI
            );

            context.fillStyle =
                'rgba(255,255,255,0.06)';

            context.fill();

            context.fillStyle =
                'rgba(255,255,255,0.72)';

            context.font =
                '600 28px sans-serif';

            context.textAlign =
                'center';

            context.textBaseline =
                'middle';

            context.fillText(
                'No choices',
                centerX,
                centerY
            );

            return;
        }

        var arc =
            TWO_PI
            / state.items.length;

        state.items.forEach(
            function (item, index) {
                var start =
                    state.rotation
                    + index * arc;

                var end =
                    start + arc;

                var hue =
                    (
                        index
                        * 360
                        / state.items.length
                        + 195
                    ) % 360;

                context.beginPath();

                context.moveTo(
                    centerX,
                    centerY
                );

                context.arc(
                    centerX,
                    centerY,
                    radius,
                    start,
                    end
                );

                context.closePath();

                context.fillStyle =
                    'hsl('
                    + hue
                    + ', 64%, 43%)';

                context.fill();

                context.strokeStyle =
                    'rgba(255,255,255,0.22)';

                context.lineWidth = 2;

                context.stroke();

                // Don't render labels if
                // there are too many choices.
                if (state.items.length <= 60) {
                    drawSegmentLabel(
                        item,
                        index,
                        arc,
                        centerX,
                        centerY,
                        radius
                    );
                }
            }
        );

        context.beginPath();

        context.arc(
            centerX,
            centerY,
            radius,
            0,
            TWO_PI
        );

        context.strokeStyle =
            'rgba(255,255,255,0.78)';

        context.lineWidth = 7;

        context.stroke();
    }

    function drawSegmentLabel(
        item,
        index,
        arc,
        centerX,
        centerY,
        radius
    ) {
        var context =
            state.context;

        var angle =
            state.rotation
            + index * arc
            + arc / 2;

        var labelRadius =
            radius * 0.68;

        var fontSize;

        if (state.items.length <= 12) {
            fontSize = 19;
        } else if (state.items.length <= 25) {
            fontSize = 15;
        } else {
            fontSize = 11;
        }

        var maxTitleLength =
            state.items.length <= 20
                ? 22
                : 14;

        context.save();

        context.translate(
            centerX,
            centerY
        );

        context.rotate(angle);

        context.translate(
            labelRadius,
            0
        );

        context.rotate(
            Math.PI / 2
        );

        context.fillStyle =
            '#ffffff';

        context.font =
            '700 '
            + fontSize
            + 'px sans-serif';

        context.textAlign =
            'center';

        context.textBaseline =
            'middle';

        context.shadowColor =
            'rgba(0,0,0,0.75)';

        context.shadowBlur = 4;

        context.fillText(
            truncateTitle(
                nameOf(item),
                maxTitleLength
            ),
            0,
            0
        );

        context.restore();
    }

    function easeOutQuint(value) {
        return (
            1
            - Math.pow(
                1 - value,
                5
            )
        );
    }

    function spin() {
        if (
            state.spinning
            || state.items.length === 0
        ) {
            return;
        }

        hideWinner();

        state.spinning = true;

        byId('wwSpin').disabled = true;

        setMessage(
            'Spinning...'
        );

        var winnerIndex =
            Math.floor(
                Math.random()
                * state.items.length
            );

        var arc =
            TWO_PI
            / state.items.length;

        var normalizedCurrent =
            (
                state.rotation
                % TWO_PI
                + TWO_PI
            ) % TWO_PI;

        // Pointer is at the top:
        // -PI / 2.
        var desired =
            -Math.PI / 2
            - (
                winnerIndex * arc
                + arc / 2
            );

        var delta =
            (
                desired
                - normalizedCurrent
                + TWO_PI
            ) % TWO_PI;

        var extraRotations =
            6
            + Math.floor(
                Math.random() * 3
            );

        delta +=
            TWO_PI
            * extraRotations;

        var startRotation =
            state.rotation;

        var endRotation =
            state.rotation
            + delta;

        var duration =
            5000
            + Math.random() * 1300;

        var startTime =
            performance.now();

        function animate(now) {
            var elapsed =
                now - startTime;

            var progress =
                Math.min(
                    elapsed / duration,
                    1
                );

            var eased =
                easeOutQuint(progress);

            state.rotation =
                startRotation
                + (
                    endRotation
                    - startRotation
                ) * eased;

            drawWheel();

            if (progress < 1) {
                requestAnimationFrame(
                    animate
                );

                return;
            }

            state.rotation =
                endRotation;

            drawWheel();

            state.spinning = false;

            state.winner =
                state.items[winnerIndex];

            byId('wwSpin').disabled = false;

            setMessage(
                'The wheel has spoken.'
            );

            showWinner(
                state.winner
            );
        }

        requestAnimationFrame(
            animate
        );
    }

    function getPosterUrl(itemId) {
        try {
            if (
                typeof window.ApiClient
                    .getImageUrl === 'function'
            ) {
                return window.ApiClient
                    .getImageUrl(
                        itemId,
                        {
                            type: 'Primary',
                            maxWidth: 500,
                            quality: 90
                        }
                    );
            }
        } catch (error) {
            console.warn(
                'Could not create poster URL:',
                error
            );
        }

        return window.ApiClient.getUrl(
            'Items/'
            + encodeURIComponent(itemId)
            + '/Images/Primary'
        );
    }

    function showWinner(item) {
        var card =
            byId('winnerCard');

        var title =
            byId('winnerTitle');

        var meta =
            byId('winnerMeta');

        var genres =
            byId('winnerGenres');

        var overview =
            byId('winnerOverview');

        var poster =
            byId('winnerPoster');

        title.textContent =
            nameOf(item);

        var metaParts = [];

        if (typeOf(item)) {
            metaParts.push(
                typeOf(item)
            );
        }

        if (yearOf(item)) {
            metaParts.push(
                String(yearOf(item))
            );
        }

        if (
            ratingOf(item)
            !== null
            && ratingOf(item)
            !== undefined
        ) {
            metaParts.push(
                '★ '
                + Number(
                    ratingOf(item)
                ).toFixed(1)
            );
        }

        meta.textContent =
            metaParts.join(' • ');

        genres.innerHTML = '';

        genresOf(item).forEach(
            function (genre) {
                var element =
                    document.createElement(
                        'span'
                    );

                element.className =
                    'winnerGenre';

                element.textContent =
                    genre;

                genres.appendChild(
                    element
                );
            }
        );

        overview.textContent =
            overviewOf(item);

        poster.style.display =
            'block';

        poster.src =
            getPosterUrl(
                idOf(item)
            );

        poster.alt =
            nameOf(item);

        poster.onerror =
            function () {
                poster.style.display =
                    'none';
            };

        card.classList.remove(
            'hidden'
        );

        card.scrollIntoView({
            behavior: 'smooth',
            block: 'nearest'
        });
    }

    function hideWinner() {
        state.winner = null;

        byId('winnerCard')
            .classList
            .add('hidden');
    }

    function openWinner() {
        if (!state.winner) {
            return;
        }

        var itemId =
            idOf(state.winner);

        window.location.hash =
            '#/details?id='
            + encodeURIComponent(
                itemId
            );
    }

    function removeWinner() {
        if (!state.winner) {
            return;
        }

        var itemId =
            idOf(state.winner);

        state.removedIds.add(
            itemId
        );

        state.items =
            state.items.filter(
                function (item) {
                    return (
                        idOf(item)
                        !== itemId
                    );
                }
            );

        hideWinner();

        setCount();

        drawWheel();

        if (state.items.length === 0) {
            byId('wwSpin').disabled =
                true;

            setMessage(
                'No choices left in this session.'
            );
        } else {
            setMessage(
                'Removed. '
                + state.items.length
                + ' choices remain.'
            );
        }
    }

    function bindEvents() {
        byId('wwApplyFilters')
            .addEventListener(
                'click',
                loadCandidates
            );

        byId('wwSpin')
            .addEventListener(
                'click',
                spin
            );

        byId('wwOpen')
            .addEventListener(
                'click',
                openWinner
            );

        byId('wwRemove')
            .addEventListener(
                'click',
                removeWinner
            );

        byId('wwAgain')
            .addEventListener(
                'click',
                spin
            );
    }

    async function init(page) {
        if (!page) {
            return;
        }

        // Prevent duplicate initialization
        // of the same rendered page.
        if (
            page.dataset
                .watchWheelInitialized
            === 'true'
        ) {
            return;
        }

        page.dataset
            .watchWheelInitialized =
            'true';

        state.page = page;

        state.canvas =
            byId('watchWheelCanvas');

        state.context =
            state.canvas.getContext(
                '2d'
            );

        bindEvents();

        drawWheel();

        try {
            await loadFilters();
            await loadCandidates();
        } catch (error) {
            console.error(
                'Watch Wheel initialization failed:',
                error
            );

            setMessage(
                'Watch Wheel could not initialize.'
            );
        }
    }

    window.WatchWheelApp = {
        init: init
    };
})();
