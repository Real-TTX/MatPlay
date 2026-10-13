// ============ MatPlay – Modul: Kniffel ============

(function () {
    const tabs = document.getElementById('kniffelTabs');
    const pad = document.getElementById('kniffelPad');
    const scoresTable = document.getElementById('kniffelScores');
    const { icon, escapeHtml } = MatPlayCore;

    // key, Label, fester Wert (null = Würfelsumme eingeben), max für Eingabe
    const UPPER = [
        { key: 'ones', label: '1er', face: 1 },
        { key: 'twos', label: '2er', face: 2 },
        { key: 'threes', label: '3er', face: 3 },
        { key: 'fours', label: '4er', face: 4 },
        { key: 'fives', label: '5er', face: 5 },
        { key: 'sixes', label: '6er', face: 6 },
    ];
    const LOWER = [
        { key: 'threeKind', label: 'Dreierpasch', fixed: null, max: 30 },
        { key: 'fourKind', label: 'Viererpasch', fixed: null, max: 30 },
        { key: 'fullHouse', label: 'Full House', fixed: 25 },
        { key: 'smallStraight', label: 'Kleine Straße', fixed: 30 },
        { key: 'largeStraight', label: 'Große Straße', fixed: 40 },
        { key: 'yahtzee', label: 'Kniffel', fixed: 50 },
        { key: 'chance', label: 'Chance', fixed: null, max: 30 },
    ];
    const ALL_KEYS = [...UPPER, ...LOWER].map(c => c.key);

    let activePlayerId = null;

    function playerState(player) {
        const s = player.state || {};
        const result = {};
        for (const key of ALL_KEYS) result[key] = (key in s) ? s[key] : null;
        return result;
    }

    function upperSum(ps) { return UPPER.reduce((sum, c) => sum + (ps[c.key] ?? 0), 0); }
    function bonus(ps) { return upperSum(ps) >= 63 ? 35 : 0; }
    function lowerSum(ps) { return LOWER.reduce((sum, c) => sum + (ps[c.key] ?? 0), 0); }
    function totalScore(ps) { return upperSum(ps) + bonus(ps) + lowerSum(ps); }

    function save(player, ps) {
        MatPlayCore.action('/player-state', { playerId: player.id, state: ps });
    }

    function render(state) {
        if (!state.players.length) { pad.innerHTML = '<p class="mat-muted">Noch keine Spieler.</p>'; return; }
        if (!state.players.some(p => p.id === activePlayerId)) {
            const pinned = state.players.find(p => MatPlayCore.isPinned(p.id));
            activePlayerId = (pinned ?? state.players[0]).id;
        }

        tabs.innerHTML = '';
        for (const player of state.players) {
            const selected = player.id === activePlayerId;
            const tab = document.createElement('button');
            tab.type = 'button';
            tab.className = 'mat-tab';
            tab.setAttribute('role', 'tab');
            tab.setAttribute('aria-selected', String(selected));
            if (!selected) tab.tabIndex = -1;
            tab.textContent = `${player.name} · ${totalScore(playerState(player))}`;
            tab.addEventListener('click', () => { activePlayerId = player.id; render(state); });
            tabs.appendChild(tab);
        }

        const player = state.players.find(p => p.id === activePlayerId);
        // Bearbeiten nur für an diesem Gerät gepinnte Spieler (ohne Pins: alle)
        const running = state.status === 0 && MatPlayCore.editable(player.id);
        const ps = playerState(player);

        pad.innerHTML = '';
        pad.appendChild(section('Oberer Teil', UPPER, ps, player, running));

        const upperInfo = document.createElement('div');
        upperInfo.className = 'mp-kn-subtotal';
        upperInfo.innerHTML = `Summe oben: <strong>${upperSum(ps)}</strong> / 63 · Bonus: <strong>${bonus(ps)}</strong>`;
        pad.appendChild(upperInfo);

        pad.appendChild(section('Unterer Teil', LOWER, ps, player, running));

        const total = document.createElement('div');
        total.className = 'mp-kn-total';
        total.textContent = `Gesamt: ${totalScore(ps)}`;
        pad.appendChild(total);

        renderScoreboard(state);
    }

    function section(title, categories, ps, player, running) {
        const box = document.createElement('div');
        box.className = 'mp-kn-section';
        const head = document.createElement('h3');
        head.textContent = title;
        box.appendChild(head);

        for (const cat of categories) {
            const row = document.createElement('div');
            row.className = 'mp-kn-row';

            const label = document.createElement('span');
            label.className = 'mp-kn-label';
            label.textContent = cat.label + (cat.fixed ? ` (${cat.fixed})` : '');
            row.appendChild(label);

            const value = ps[cat.key];
            if (value !== null) {
                const val = document.createElement('button');
                val.type = 'button';
                val.className = 'mp-kn-value' + (value === 0 ? ' is-struck' : '');
                val.innerHTML = value === 0 ? icon('x') : String(value);
                val.title = 'Tippen zum Zurücksetzen';
                val.setAttribute('aria-label', `${cat.label}: ${value === 0 ? 'gestrichen' : value}, zurücksetzen`);
                val.addEventListener('click', async () => {
                    if (!running) return;
                    const ok = await MatUi.confirm(`${cat.label} für ${player.name} zurücksetzen?`, { ok: 'Zurücksetzen' });
                    if (ok) save(player, { ...ps, [cat.key]: null });
                });
                row.appendChild(val);
            } else if (running) {
                const controls = document.createElement('div');
                controls.className = 'mp-kn-controls';
                if (cat.fixed) {
                    controls.appendChild(button(String(cat.fixed), 'mat-btn mat-btn--primary mat-btn--sm',
                        () => save(player, { ...ps, [cat.key]: cat.fixed })));
                } else {
                    const max = cat.face ? cat.face * 5 : cat.max;
                    const input = document.createElement('input');
                    input.type = 'number';
                    input.className = 'mat-input';
                    input.inputMode = 'numeric';
                    input.min = 0;
                    input.max = max;
                    if (cat.face) input.step = cat.face;
                    input.placeholder = `0–${max}`;
                    input.setAttribute('aria-label', `${cat.label} für ${player.name}`);
                    const ok = button(icon('check'), 'mat-btn mat-btn--primary mat-btn--icon mat-btn--sm', () => {
                        const v = parseInt(input.value, 10);
                        if (Number.isNaN(v) || v < 0 || v > max) return;
                        if (cat.face && v % cat.face !== 0) {
                            MatUi.toast(`Muss ein Vielfaches von ${cat.face} sein.`, { tone: 'warning' });
                            return;
                        }
                        save(player, { ...ps, [cat.key]: v });
                    }, 'Eintragen');
                    input.addEventListener('keydown', e => { if (e.key === 'Enter') ok.click(); });
                    controls.append(input, ok);
                }
                controls.appendChild(button(icon('x'), 'mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm',
                    () => save(player, { ...ps, [cat.key]: 0 }), 'Streichen'));
                row.appendChild(controls);
            } else {
                const dash = document.createElement('span');
                dash.className = 'mp-kn-value is-open';
                dash.textContent = '–';
                row.appendChild(dash);
            }
            box.appendChild(row);
        }
        return box;
    }

    function button(html, className, onClick, label) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = className;
        btn.innerHTML = html;
        if (label) { btn.setAttribute('aria-label', label); btn.title = label; }
        btn.addEventListener('click', onClick);
        return btn;
    }

    function renderScoreboard(state) {
        const list = state.players
            .map(p => {
                const ps = playerState(p);
                const open = ALL_KEYS.filter(k => ps[k] === null).length;
                return { name: p.name, score: totalScore(ps), open };
            })
            .sort((a, b) => b.score - a.score);
        let html = '<thead><tr><th scope="col">#</th><th scope="col">Spieler</th><th scope="col" class="mat-num">Punkte</th><th scope="col" class="mat-num">Offen</th></tr></thead><tbody>';
        list.forEach((r, i) => {
            html += `<tr><td data-label="Platz">${i + 1}</td><td data-label="Spieler">${escapeHtml(r.name)}</td>` +
                `<td data-label="Punkte" class="mat-num">${r.score}</td><td data-label="Offen" class="mat-num">${r.open}</td></tr>`;
        });
        scoresTable.innerHTML = html + '</tbody>';
    }

    MatPlayCore.init(render);
})();
