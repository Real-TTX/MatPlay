// ============ MatPlay – Modul: Wizard (Ansage vs. Stiche, Auto-Scoring) ============

(function () {
    const info = document.getElementById('wizardInfo');
    const board = document.getElementById('wizardBoard');
    const history = document.getElementById('wizardHistory');
    const table = document.getElementById('wizardTable');
    const { icon, escapeHtml } = MatPlayCore;

    function playerRounds(player) {
        return (player.state && player.state.rounds) || [];
    }

    function roundScore(entry) {
        return entry.bid === entry.tricks
            ? 20 + 10 * entry.tricks
            : -10 * Math.abs(entry.bid - entry.tricks);
    }

    function totalScore(player) {
        return playerRounds(player).reduce((sum, entry) => sum + roundScore(entry), 0);
    }

    function maxRounds(state) {
        return Math.max(1, Math.floor(60 / Math.max(state.players.length, 1)));
    }

    function render(state) {
        if (!state.players.length) { board.innerHTML = '<p class="mat-muted">Noch keine Spieler.</p>'; return; }
        const running = state.status === 0;
        const max = maxRounds(state);
        const minRound = Math.min(...state.players.map(p => playerRounds(p).length));
        const finished = minRound >= max;

        info.textContent = finished
            ? `Alle ${max} Runden gespielt`
            : `Runde ${minRound + 1} von ${max} · ${minRound + 1} Karte${minRound + 1 > 1 ? 'n' : ''} pro Spieler`;

        const totals = new Map(state.players.map(p => [p.id, totalScore(p)]));
        const best = Math.max(...totals.values());

        board.innerHTML = '';
        const players = [...state.players].sort((a, b) =>
            Number(MatPlayCore.isPinned(b.id)) - Number(MatPlayCore.isPinned(a.id)));
        for (const player of players) {
            const rounds = playerRounds(player);
            const total = totals.get(player.id);
            const canEdit = MatPlayCore.editable(player.id);
            const winner = finished && total === best;
            const leader = !finished && state.players.length > 1 && total === best && rounds.length > 0;

            const card = document.createElement('div');
            card.className = 'mp-player' + (winner ? ' is-winner' : leader ? ' is-leader' : '')
                + (canEdit ? '' : ' is-readonly');

            card.appendChild(textEl('div', 'mp-player__name', player.name));
            card.appendChild(textEl('div', 'mp-player__tag', winner ? 'Gewonnen!' : leader ? 'Führt' : ''));
            card.appendChild(textEl('div', 'mp-total', String(total)));

            if (running && canEdit && rounds.length < max) {
                card.appendChild(textEl('div', 'mp-round-tag', `Runde ${rounds.length + 1}`));

                const form = document.createElement('div');
                form.className = 'mp-entry';
                const bid = numberInput('Ansage', rounds.length + 1, `Ansage von ${player.name}`);
                const tricks = numberInput('Stiche', rounds.length + 1, `Stiche von ${player.name}`);
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'mat-btn mat-btn--primary mat-btn--icon';
                btn.innerHTML = icon('check');
                btn.setAttribute('aria-label', 'Runde eintragen');
                const submit = () => {
                    const b = parseInt(bid.value, 10);
                    const t = parseInt(tricks.value, 10);
                    if (Number.isNaN(b) || Number.isNaN(t) || b < 0 || t < 0) return;
                    MatPlayCore.action('/player-state', { playerId: player.id, state: { rounds: [...rounds, { bid: b, tricks: t }] } });
                };
                btn.addEventListener('click', submit);
                tricks.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
                form.append(bid, tricks, btn);
                card.appendChild(form);
            }

            if (running && canEdit && rounds.length > 0) card.appendChild(undoButton(player, rounds));
            board.appendChild(card);
        }

        renderHistory(state, max);
    }

    function undoButton(player, rounds) {
        const undo = document.createElement('button');
        undo.type = 'button';
        undo.className = 'mat-btn mat-btn--ghost mat-btn--sm';
        undo.innerHTML = icon('restore') + '<span>Letzte Runde</span>';
        undo.title = 'Letzte Runde dieses Spielers zurücknehmen';
        undo.addEventListener('click', () => {
            MatPlayCore.action('/player-state', { playerId: player.id, state: { rounds: rounds.slice(0, -1) } });
        });
        return undo;
    }

    function textEl(tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        node.textContent = text;
        return node;
    }

    function numberInput(placeholder, maxValue, label) {
        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'mat-input mat-input--narrow';
        input.inputMode = 'numeric';
        input.min = 0;
        input.max = maxValue;
        input.placeholder = placeholder;
        input.setAttribute('aria-label', label);
        return input;
    }

    function renderHistory(state, max) {
        const played = Math.max(...state.players.map(p => playerRounds(p).length));
        if (played === 0) { history.hidden = true; return; }
        history.hidden = false;

        let html = '<thead><tr><th scope="col">Runde</th>' +
            state.players.map(p => `<th scope="col" class="mat-num">${escapeHtml(p.name)}</th>`).join('') + '</tr></thead><tbody>';
        for (let r = 0; r < Math.min(played, max); r++) {
            html += `<tr><td data-label="Runde">${r + 1}</td>`;
            for (const p of state.players) {
                const entry = playerRounds(p)[r];
                if (!entry) { html += `<td data-label="${escapeHtml(p.name)}" class="mat-num">–</td>`; continue; }
                const score = roundScore(entry);
                const hit = entry.bid === entry.tricks;
                html += `<td data-label="${escapeHtml(p.name)}" class="mat-num"><span class="${hit ? 'mp-pos' : 'mp-neg'}">${score > 0 ? '+' : ''}${score}</span>` +
                    ` <span class="mp-dim">${entry.tricks}/${entry.bid}</span></td>`;
            }
            html += '</tr>';
        }
        table.innerHTML = html + '</tbody>';
    }

    MatPlayCore.init(render);
})();
