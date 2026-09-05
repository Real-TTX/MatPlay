// ============ MatPlay – Modul: Kniffel ============

(function () {
    const tabs = document.getElementById('kniffelTabs');
    const pad = document.getElementById('kniffelPad');
    const scoresTable = document.getElementById('kniffelScores');

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
        if (!state.players.length) { pad.innerHTML = '<p class="form-hint">Noch keine Spieler.</p>'; return; }
        if (!state.players.some(p => p.id === activePlayerId)) {
            const pinned = state.players.find(p => MatPlayCore.isPinned(p.id));
            activePlayerId = (pinned ?? state.players[0]).id;
        }

        tabs.innerHTML = '';
        for (const player of state.players) {
            const tab = document.createElement('button');
            tab.type = 'button';
            tab.className = 'tab' + (player.id === activePlayerId ? ' active' : '');
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
        upperInfo.className = 'kniffel-subtotal';
        upperInfo.innerHTML = `Summe oben: <b>${upperSum(ps)}</b> / 63 &nbsp;·&nbsp; Bonus: <b>${bonus(ps)}</b>`;
        pad.appendChild(upperInfo);

        pad.appendChild(section('Unterer Teil', LOWER, ps, player, running));

        const total = document.createElement('div');
        total.className = 'qwixx-total';
        total.textContent = `Gesamt: ${totalScore(ps)}`;
        pad.appendChild(total);

        renderScoreboard(state);
    }

    function section(title, categories, ps, player, running) {
        const box = document.createElement('div');
        box.className = 'kniffel-section';
        const head = document.createElement('h4');
        head.textContent = title;
        box.appendChild(head);

        for (const cat of categories) {
            const row = document.createElement('div');
            row.className = 'kniffel-row';

            const label = document.createElement('span');
            label.className = 'kniffel-label';
            label.textContent = cat.label + (cat.fixed ? ` (${cat.fixed})` : '');
            row.appendChild(label);

            const value = ps[cat.key];
            if (value !== null) {
                const val = document.createElement('button');
                val.type = 'button';
                val.className = 'kniffel-value' + (value === 0 ? ' struck' : '');
                val.textContent = value === 0 ? '✖' : value;
                val.title = 'Tippen zum Zurücksetzen';
                val.addEventListener('click', () => {
                    if (!running) return;
                    if (confirm(`${cat.label} für ${player.name} zurücksetzen?`)) {
                        save(player, { ...ps, [cat.key]: null });
                    }
                });
                row.appendChild(val);
            } else if (running) {
                const controls = document.createElement('div');
                controls.className = 'kniffel-controls';
                if (cat.fixed) {
                    controls.appendChild(button(`${cat.fixed}`, 'btn btn-primary btn-sm',
                        () => save(player, { ...ps, [cat.key]: cat.fixed })));
                } else {
                    const input = document.createElement('input');
                    input.type = 'number';
                    input.inputMode = 'numeric';
                    input.min = 0;
                    input.max = cat.face ? cat.face * 5 : cat.max;
                    if (cat.face) input.step = cat.face;
                    input.placeholder = cat.face ? `0–${cat.face * 5}` : `0–${cat.max}`;
                    const ok = button('✔', 'btn btn-primary btn-sm', () => {
                        const v = parseInt(input.value, 10);
                        const max = cat.face ? cat.face * 5 : cat.max;
                        if (Number.isNaN(v) || v < 0 || v > max) return;
                        if (cat.face && v % cat.face !== 0) { alert(`Muss ein Vielfaches von ${cat.face} sein.`); return; }
                        save(player, { ...ps, [cat.key]: v });
                    });
                    input.addEventListener('keydown', e => { if (e.key === 'Enter') ok.click(); });
                    controls.append(input, ok);
                }
                controls.appendChild(button('✖', 'btn btn-danger btn-sm strike',
                    () => save(player, { ...ps, [cat.key]: 0 })));
                controls.lastChild.title = 'Streichen';
                row.appendChild(controls);
            } else {
                const dash = document.createElement('span');
                dash.className = 'kniffel-value';
                dash.textContent = '–';
                row.appendChild(dash);
            }
            box.appendChild(row);
        }
        return box;
    }

    function button(text, cls, onClick) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = cls;
        btn.textContent = text;
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
        let html = '<thead><tr><th>#</th><th>Spieler</th><th>Punkte</th><th>Offen</th></tr></thead><tbody>';
        list.forEach((r, i) => {
            html += `<tr><td>${i === 0 ? '🏆' : i + 1}</td><td>${escapeHtml(r.name)}</td><td>${r.score}</td><td>${r.open}</td></tr>`;
        });
        scoresTable.innerHTML = html + '</tbody>';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    MatPlayCore.init(render);
})();
