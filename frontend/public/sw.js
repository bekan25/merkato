const CACHE_PREFIX = 'merkato-pwa-';
const CACHE_VERSION = 'v2';
const APP_CACHE = CACHE_PREFIX + CACHE_VERSION;
const OFFLINE_URL = '/offline.html';
const NEVER_CACHE_PREFIXES = ['/api/', '/auth/', '/account', '/merchant', '/admin', '/payment', '/checkout'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(APP_CACHE)
      .then((cache) => cache.addAll([OFFLINE_URL, '/icons/merkato-icon.svg']))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys
        .filter((key) => key.startsWith(CACHE_PREFIX) && key !== APP_CACHE)
        .map((key) => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (NEVER_CACHE_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))) return;

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).then((response) => {
        if (response && response.ok && url.pathname === '/') {
          const copy = response.clone();
          caches.open(APP_CACHE).then((cache) => cache.put('/', copy));
        }
        return response;
      }).catch(async () => {
        if (url.pathname === '/') {
          const cachedHome = await caches.match('/');
          if (cachedHome) return cachedHome;
        }
        return (await caches.match(OFFLINE_URL)) || Response.error();
      })
    );
    return;
  }

  const staticDestination = ['script', 'style', 'image', 'font', 'worker'].includes(request.destination);
  const publicAsset = url.pathname.startsWith('/assets/') || url.pathname.startsWith('/icons/');
  if (staticDestination || publicAsset) {
    event.respondWith(
      caches.match(request).then((cached) => {
        const update = fetch(request).then((response) => {
          if (response && response.ok) {
            const copy = response.clone();
            caches.open(APP_CACHE).then((cache) => cache.put(request, copy));
          }
          return response;
        });
        return cached || update;
      })
    );
  }
});
