// ============ MatPlay – Modul: Munchkin / Munchkin Quest ============

(function () {
    const board = document.getElementById('munchkinBoard');

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
        for (const player of state.players) {
            const ps = playerState(state, player);
            const won = ps.level >= maxLevel;

            const card = document.createElement('div');
            card.className = 'counter-card munchkin-card' + (won ? ' winner' : '');

            const name = document.createElement('div');
            name.className = 'counter-name';
            name.textContent = player.name;
            card.appendChild(name);

            const tag = document.createElement('div');
            tag.className = 'counter-winner-tag';
            tag.textContent = won ? '🏆 STUFE ' + maxLevel + '!' : '';
            card.appendChild(tag);

            card.appendChild(statRow('Level', ps.level, running && ps.level > 1, running && ps.level < maxLevel,
                delta => save(player, { ...ps, level: ps.level + delta })));
            card.appendChild(statRow('Boni', ps.bonus, running, running,
                delta => save(player, { ...ps, bonus: ps.bonus + delta })));

            if (cfg.trackHealth) {
                const hearts = document.createElement('div');
                hearts.className = 'munchkin-hearts';
                const label = document.createElement('span');
                label.className = 'munchkin-stat-label';
                label.textContent = 'Leben';
                hearts.appendChild(label);
                const max = Math.max(cfg.startHealth ?? 4, ps.health);
                for (let i = 1; i <= max; i++) {
                    const heart = document.createElement('button');
                    heart.type = 'button';
                    heart.className = 'munchkin-heart';
                    heart.textContent = i <= ps.health ? '❤️' : '🖤';
                    heart.title = `Auf ${i <= ps.health ? i - 1 : i} Leben setzen`;
                    heart.addEventListener('click', () => {
                        if (!running) return;
                        save(player, { ...ps, health: i <= ps.health ? i - 1 : i });
                    });
                    hearts.appendChild(heart);
                }
                const plus = document.createElement('button');
                plus.type = 'button';
                plus.className = 'munchkin-heart plus';
                plus.textContent = '＋';
                plus.title = 'Extra-Leben';
                plus.addEventListener('click', () => { if (running) save(player, { ...ps, health: ps.health + 1 }); });
                hearts.appendChild(plus);
                card.appendChild(hearts);
                if (ps.health <= 0) {
                    const dead = document.createElement('div');
                    dead.className = 'munchkin-dead';
                    dead.textContent = '💀 Tot – Wiederauferstehung!';
                    card.appendChild(dead);
                }
            }

            const power = document.createElement('div');
            power.className = 'munchkin-power';
            power.innerHTML = `⚔️ Kampfkraft: <b>${ps.level + ps.bonus}</b>`;
            card.appendChild(power);

            board.appendChild(card);
        }
    }

    function statRow(labelText, value, canMinus, canPlus, onChange) {
        const row = document.createElement('div');
        row.className = 'munchkin-stat';
        const label = document.createElement('span');
        label.className = 'munchkin-stat-label';
        label.textContent = labelText;
        const minus = document.createElement('button');
        minus.type = 'button';
        minus.className = 'counter-btn minus small';
        minus.textContent = '−';
        minus.disabled = !canMinus;
        minus.addEventListener('click', () => onChange(-1));
        const val = document.createElement('span');
        val.className = 'munchkin-stat-value';
        val.textContent = value;
        const plus = document.createElement('button');
        plus.type = 'button';
        plus.className = 'counter-btn plus small';
        plus.textContent = '＋';
        plus.disabled = !canPlus;
        plus.addEventListener('click', () => onChange(1));
        row.append(label, minus, val, plus);
        return row;
    }

    document.getElementById('addPlayerBtn').addEventListener('click', () => {
        const name = prompt('Name des neuen Spielers:');
        if (name && name.trim()) MatPlayCore.action('/player', { name: name.trim() });
    });

    MatPlayCore.init(render);
})();
