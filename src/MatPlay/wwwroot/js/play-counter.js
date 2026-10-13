// ============ MatPlay – Modul: Punktezähler ============

(function () {
    const board = document.getElementById('counterBoard');
    const roundHistory = document.getElementById('roundHistory');
    const roundTable = document.getElementById('roundTable');
    const { icon, escapeHtml } = MatPlayCore;

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
            card.className = 'mp-player' + (winner ? ' is-winner' : leader ? ' is-leader' : '')
                + (canEdit ? '' : ' is-readonly');

            card.appendChild(el('div', 'mp-player__name', player.name));
            card.appendChild(el('div', 'mp-player__tag', winner ? 'Gewonnen!' : leader ? 'Führt' : ''));
            const totalEl = el('div', 'mp-total', String(total));
            card.appendChild(totalEl);

            if (running && canEdit) {
                if (cfg.useRounds) {
                    // Rundenmodus: Punkte am Rundenende eintragen (z.B. Flip 7, Frantic)
                    const nextRound = state.scores.filter(s => s.playerId === player.id).length + 1;
                    card.appendChild(el('div', 'mp-round-tag', `Runde ${nextRound}`));
                    card.appendChild(entryForm(state, player, total, 'Punkte der Runde'));
                } else {
                    const steps = el('div', 'mp-steps');
                    const minus = button('mat-btn mat-btn--secondary mp-step', icon('minus'), `${player.name}: minus`);
                    minus.addEventListener('click', () => addScore(state, player, -(cfg.step || 1), total));
                    const plus = button('mat-btn mat-btn--primary mp-step', icon('plus'), `${player.name}: plus`);
                    plus.addEventListener('click', () => addScore(state, player, cfg.step || 1, total));
                    steps.append(minus, plus);
                    card.appendChild(steps);

                    // Freie Punkteeingabe: einklappbares Feld
                    const form = entryForm(state, player, total, 'z.B. 15 oder -3');
                    form.hidden = true;
                    const toggle = document.createElement('button');
                    toggle.type = 'button';
                    toggle.className = 'mat-btn mat-btn--ghost mat-btn--sm';
                    toggle.innerHTML = icon('edit') + '<span>Punkte eingeben</span>';
                    toggle.addEventListener('click', () => {
                        form.hidden = !form.hidden;
                        if (!form.hidden) form.querySelector('input').focus();
                    });
                    totalEl.classList.add('is-link');
                    totalEl.title = 'Tippen für eigene Punkteeingabe';
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

    function el(tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function button(className, html, label) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = className;
        btn.innerHTML = html;
        btn.setAttribute('aria-label', label);
        return btn;
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
            li.innerHTML = `<span class="mp-log__time">${time}</span> ` +
                `<strong>${escapeHtml(names.get(entry.playerId) || '?')}</strong> ` +
                `<span class="${entry.value >= 0 ? 'mp-pos' : 'mp-neg'}">${value}</span>` +
                (entry.round > 0 ? ` <span class="mp-dim">Runde ${entry.round}</span>` : '');
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
        const form = el('div', 'mp-entry');
        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'mat-input';
        input.placeholder = placeholder;
        input.inputMode = 'numeric';
        input.setAttribute('aria-label', `Punkte für ${player.name}`);
        const btn = button('mat-btn mat-btn--primary mat-btn--icon', icon('check'), 'Punkte eintragen');
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

        let html = '<thead><tr><th scope="col">Runde</th>' +
            state.players.map(p => `<th scope="col" class="mat-num">${escapeHtml(p.name)}</th>`).join('') + '</tr></thead><tbody>';
        for (let r = 1; r <= maxRound; r++) {
            html += `<tr><td data-label="Runde">${r}</td>`;
            for (const p of state.players) {
                const entry = state.scores.find(s => s.playerId === p.id && s.round === r);
                html += `<td data-label="${escapeHtml(p.name)}" class="mat-num">${entry ? entry.value : '–'}</td>`;
            }
            html += '</tr>';
        }
        roundTable.innerHTML = html + '</tbody>';
    }

    document.getElementById('undoBtn').addEventListener('click', () => MatPlayCore.action('/undo', {}));

    MatPlayCore.init(render);
})();
