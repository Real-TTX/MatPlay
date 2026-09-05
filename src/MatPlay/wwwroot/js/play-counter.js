// ============ MatPlay – Modul: Punktezähler ============

(function () {
    const board = document.getElementById('counterBoard');
    const roundHistory = document.getElementById('roundHistory');
    const roundTable = document.getElementById('roundTable');

    function totals(state) {
        const map = new Map(state.players.map(p => [p.id, state.config.startScore || 0]));
        for (const s of state.scores) {
            if (map.has(s.playerId)) map.set(s.playerId, map.get(s.playerId) + s.value);
        }
        return map;
    }

    function winnerIds(state, totalMap) {
        const cfg = state.config;
        const target = cfg.targetScore;
        if (target === undefined || target === null) return new Set();
        const start = cfg.startScore || 0;
        const entries = [...totalMap.entries()];

        if (start > target) {
            // Runterzählen (z.B. 20 Ab): Ziel erreicht = gewonnen
            return new Set(entries.filter(([, t]) => t <= target).map(([id]) => id));
        }
        const ended = entries.some(([, t]) => t >= target);
        if (!ended) return new Set();
        if (cfg.lowestWins) {
            // Limit erreicht = Spielende, wenigste Punkte gewinnen (z.B. Frantic)
            const min = Math.min(...entries.map(([, t]) => t));
            return new Set(entries.filter(([, t]) => t === min).map(([id]) => id));
        }
        return new Set(entries.filter(([, t]) => t >= target).map(([id]) => id));
    }

    function render(state) {
        const cfg = state.config;
        const totalMap = totals(state);
        const running = state.status === 0;
        const values = [...totalMap.values()];
        const best = cfg.lowestWins ? Math.min(...values) : Math.max(...values);
        const winners = winnerIds(state, totalMap);

        board.innerHTML = '';
        // Gepinnte Spieler zuerst (Reihenfolge sonst stabil)
        const players = [...state.players].sort((a, b) =>
            Number(MatPlayCore.isPinned(b.id)) - Number(MatPlayCore.isPinned(a.id)));
        for (const player of players) {
            const total = totalMap.get(player.id);
            const winner = winners.has(player.id);
            const leader = state.players.length > 1 && total === best && state.scores.length > 0;
            const canEdit = MatPlayCore.editable(player.id);

            const card = document.createElement('div');
            card.className = 'counter-card' + (winner ? ' winner' : leader ? ' leader' : '')
                + (canEdit ? '' : ' readonly');

            const name = document.createElement('div');
            name.className = 'counter-name';
            name.textContent = player.name;
            card.appendChild(name);

            const tag = document.createElement('div');
            tag.className = 'counter-winner-tag';
            tag.textContent = winner ? '🏆 GEWONNEN!' : leader ? '⭐ Führt' : '';
            card.appendChild(tag);

            const totalEl = document.createElement('div');
            totalEl.className = 'counter-total';
            totalEl.textContent = total;
            card.appendChild(totalEl);

            if (running && canEdit) {
                if (cfg.useRounds) {
                    // Rundenmodus: Punkte am Rundenende eintragen (z.B. Flip 7, Frantic)
                    const nextRound = state.scores.filter(s => s.playerId === player.id).length + 1;
                    const roundTag = document.createElement('div');
                    roundTag.className = 'counter-round-tag';
                    roundTag.textContent = `Runde ${nextRound}`;
                    card.appendChild(roundTag);
                    card.appendChild(entryForm(state, player, total, 'Punkte der Runde'));
                } else {
                    const controls = document.createElement('div');
                    controls.className = 'counter-controls';
                    const minus = document.createElement('button');
                    minus.className = 'counter-btn minus';
                    minus.textContent = '−';
                    minus.addEventListener('click', () => addScore(state, player, -(cfg.step || 1), total));
                    const plus = document.createElement('button');
                    plus.className = 'counter-btn plus';
                    plus.textContent = '＋';
                    plus.addEventListener('click', () => addScore(state, player, cfg.step || 1, total));
                    controls.append(minus, plus);
                    card.appendChild(controls);

                    // Freie Punkteeingabe: einklappbares Feld statt prompt()
                    const form = entryForm(state, player, total, 'z.B. 15 oder -3');
                    form.hidden = true;
                    const toggle = document.createElement('button');
                    toggle.type = 'button';
                    toggle.className = 'btn btn-ghost btn-sm counter-entry-toggle';
                    toggle.textContent = '✎ Punkte eingeben';
                    toggle.addEventListener('click', () => {
                        form.hidden = !form.hidden;
                        if (!form.hidden) form.querySelector('input').focus();
                    });
                    totalEl.title = 'Tippen für eigene Punkteeingabe';
                    totalEl.style.cursor = 'pointer';
                    totalEl.addEventListener('click', () => toggle.click());
                    card.appendChild(toggle);
                    card.appendChild(form);
                }
            }
            board.appendChild(card);
        }

        renderRounds(state);
        renderLog(state);
    }

    function renderLog(state) {
        const section = document.getElementById('logHistory');
        const list = document.getElementById('logList');
        if (!state.scores.length) { section.hidden = true; return; }
        section.hidden = false;
        const names = new Map(state.players.map(p => [p.id, p.name]));
        list.innerHTML = '';
        for (const entry of [...state.scores].reverse().slice(0, 30)) {
            const li = document.createElement('li');
            const time = entry.date
                ? new Date(entry.date).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
                : '';
            const value = entry.value > 0 ? `+${entry.value}` : `${entry.value}`;
            li.innerHTML = `<span class="log-time">${time}</span> ` +
                `<b>${escapeHtml(names.get(entry.playerId) || '?')}</b> ` +
                `<span class="log-value ${entry.value >= 0 ? 'pos' : 'neg'}">${value}</span>` +
                (entry.round > 0 ? ` <span class="log-round">Runde ${entry.round}</span>` : '');
            list.appendChild(li);
        }
    }

    function addScore(state, player, value, currentTotal) {
        // Abzieh-Modus (z.B. Darts): eingegebene Punkte wirken negativ
        if (state.config.subtractRounds && state.config.useRounds) value = -value;
        if (!state.config.allowNegative && currentTotal + value < 0) {
            value = -currentTotal;
            if (value === 0) return;
        }
        const round = state.config.useRounds
            ? state.scores.filter(s => s.playerId === player.id).length + 1
            : 0;
        MatPlayCore.action('/score', { playerId: player.id, value, round });
    }

    function entryForm(state, player, total, placeholder) {
        const form = document.createElement('div');
        form.className = 'counter-round-form';
        const input = document.createElement('input');
        input.type = 'number';
        input.placeholder = placeholder;
        input.inputMode = 'numeric';
        input.setAttribute('aria-label', `Punkte für ${player.name}`);
        const btn = document.createElement('button');
        btn.className = 'btn btn-primary btn-sm';
        btn.textContent = '✔';
        btn.title = 'Punkte eintragen';
        const submit = () => {
            const value = parseInt(input.value, 10);
            if (Number.isNaN(value)) return;
            addScore(state, player, value, total);
            input.value = '';
        };
        btn.addEventListener('click', submit);
        input.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
        form.append(input, btn);
        return form;
    }

    function renderRounds(state) {
        if (!state.config.useRounds) { roundHistory.hidden = true; return; }
        const maxRound = Math.max(0, ...state.scores.map(s => s.round));
        if (maxRound === 0) { roundHistory.hidden = true; return; }
        roundHistory.hidden = false;

        let html = '<thead><tr><th>Runde</th>' +
            state.players.map(p => `<th>${escapeHtml(p.name)}</th>`).join('') + '</tr></thead><tbody>';
        for (let r = 1; r <= maxRound; r++) {
            html += `<tr><td>${r}</td>`;
            for (const p of state.players) {
                const entry = state.scores.find(s => s.playerId === p.id && s.round === r);
                html += `<td>${entry ? entry.value : '–'}</td>`;
            }
            html += '</tr>';
        }
        roundTable.innerHTML = html + '</tbody>';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    document.getElementById('undoBtn').addEventListener('click', () => MatPlayCore.action('/undo', {}));

    MatPlayCore.init(render);
})();
