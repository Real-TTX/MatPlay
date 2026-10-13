// ============ MatPlay – Modul: Munchkin / Munchkin Quest ============

(function () {
    const board = document.getElementById('munchkinBoard');
    const { icon } = MatPlayCore;
    // Lebenspunkt als Spielmarke (das Sprite hat kein Herz); Farbe und Füllung kommen aus dem CSS
    const HEART = '<svg class="mat-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">' +
        '<path d="M12 20.5s-8-4.9-8-11A4.6 4.6 0 0 1 12 6.6a4.6 4.6 0 0 1 8 2.9c0 6.1-8 11-8 11z"/></svg>';

    function playerState(state, player) {
        const cfg = state.config || {};
        const s = player.state || {};
        return {
            level: s.level ?? (cfg.startLevel ?? 1),
            bonus: s.bonus ?? 0,
            health: s.health ?? (cfg.startHealth ?? 4),
        };
    }

    function save(player, ps) {
        MatPlayCore.action('/player-state', { playerId: player.id, state: ps });
    }

    function render(state) {
        const cfg = state.config || {};
        const maxLevel = cfg.maxLevel ?? 10;
        const running = state.status === 0;

        board.innerHTML = '';
        // Gepinnte Spieler zuerst
        const players = [...state.players].sort((a, b) =>
            Number(MatPlayCore.isPinned(b.id)) - Number(MatPlayCore.isPinned(a.id)));
        for (const player of players) {
            const ps = playerState(state, player);
            const won = ps.level >= maxLevel;
            const editableCard = running && MatPlayCore.editable(player.id);

            const card = document.createElement('div');
            card.className = 'mp-player' + (won ? ' is-winner' : '')
                + (MatPlayCore.editable(player.id) ? '' : ' is-readonly');

            card.appendChild(textEl('div', 'mp-player__name', player.name));
            card.appendChild(textEl('div', 'mp-player__tag', won ? `Stufe ${maxLevel} erreicht!` : ''));

            card.appendChild(statRow('Level', ps.level, editableCard && ps.level > 1, editableCard && ps.level < maxLevel,
                delta => save(player, { ...ps, level: ps.level + delta })));
            card.appendChild(statRow('Boni', ps.bonus, editableCard, editableCard,
                delta => save(player, { ...ps, bonus: ps.bonus + delta })));

            if (cfg.trackHealth) {
                const hearts = document.createElement('div');
                hearts.className = 'mp-mk-hearts';
                hearts.appendChild(textEl('span', 'mp-mk-label', 'Leben'));
                const max = Math.max(cfg.startHealth ?? 4, ps.health);
                for (let i = 1; i <= max; i++) {
                    const alive = i <= ps.health;
                    const heart = document.createElement('button');
                    heart.type = 'button';
                    heart.className = 'mat-btn mat-btn--ghost mat-btn--icon mp-mk-heart' + (alive ? '' : ' is-lost');
                    heart.innerHTML = HEART;
                    heart.setAttribute('aria-label', `Auf ${alive ? i - 1 : i} Leben setzen`);
                    heart.addEventListener('click', () => {
                        if (!editableCard) return;
                        save(player, { ...ps, health: alive ? i - 1 : i });
                    });
                    hearts.appendChild(heart);
                }
                const plus = document.createElement('button');
                plus.type = 'button';
                plus.className = 'mat-btn mat-btn--ghost mat-btn--icon mp-mk-heart';
                plus.innerHTML = icon('plus');
                plus.setAttribute('aria-label', 'Extra-Leben');
                plus.addEventListener('click', () => { if (editableCard) save(player, { ...ps, health: ps.health + 1 }); });
                hearts.appendChild(plus);
                card.appendChild(hearts);
                if (ps.health <= 0) card.appendChild(textEl('div', 'mp-mk-dead', 'Tot – Wiederauferstehung!'));
            }

            const power = document.createElement('div');
            power.className = 'mp-mk-power';
            power.innerHTML = `Kampfkraft: <b>${ps.level + ps.bonus}</b>`;
            card.appendChild(power);

            board.appendChild(card);
        }
    }

    function textEl(tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        node.textContent = text;
        return node;
    }

    function statRow(labelText, value, canMinus, canPlus, onChange) {
        const row = document.createElement('div');
        row.className = 'mp-mk-stat';
        const minus = document.createElement('button');
        minus.type = 'button';
        minus.className = 'mat-btn mat-btn--secondary mp-mk-btn';
        minus.innerHTML = icon('minus');
        minus.disabled = !canMinus;
        minus.setAttribute('aria-label', `${labelText} verringern`);
        minus.addEventListener('click', () => onChange(-1));
        const plus = document.createElement('button');
        plus.type = 'button';
        plus.className = 'mat-btn mat-btn--primary mp-mk-btn';
        plus.innerHTML = icon('plus');
        plus.disabled = !canPlus;
        plus.setAttribute('aria-label', `${labelText} erhöhen`);
        plus.addEventListener('click', () => onChange(1));
        row.append(textEl('span', 'mp-mk-label', labelText), minus, textEl('span', 'mp-mk-value', String(value)), plus);
        return row;
    }

    MatPlayCore.init(render);
})();
