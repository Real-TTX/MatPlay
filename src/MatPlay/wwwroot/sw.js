// MatPlay Service Worker – Shell-Caching für PWA
const CACHE = 'matplay-v2';
const SHELL = [
    '/css/site.css',
    '/js/site.js',
    '/js/play-core.js',
    '/js/play-counter.js',
    '/js/play-qwixx.js',
    '/js/play-kniffel.js',
    '/js/play-munchkin.js',
    '/icons/favicon.svg',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/manifest.webmanifest',
];

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', e => {
    const url = new URL(e.request.url);
    if (e.request.method !== 'GET' || url.origin !== location.origin) return;
    if (url.pathname.startsWith('/api/')) return; // Spielstand immer live

    // Statische Assets: Cache-first, Seiten: Network-first mit Cache-Fallback
    const isStatic = /\.(css|js|png|svg|webmanifest|woff2?)$/.test(url.pathname);
    if (isStatic) {
        e.respondWith(
            caches.match(e.request, { ignoreSearch: true }).then(hit => hit ||
                fetch(e.request).then(res => {
                    const copy = res.clone();
                    caches.open(CACHE).then(c => c.put(e.request, copy));
                    return res;
                }))
        );
    } else {
        e.respondWith(
            fetch(e.request)
                .then(res => {
                    const copy = res.clone();
                    caches.open(CACHE).then(c => c.put(e.request, copy));
                    return res;
                })
                .catch(() => caches.match(e.request).then(hit => hit || caches.match('/')))
        );
    }
});
