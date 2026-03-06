self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'ledgerflow-cache';
const cacheName = `${cacheNamePrefix}-${self.assetsManifest.version}`;

const offlineAssetsInclude = [
    /\.dll$/,
    /\.pdb$/,
    /\.wasm$/,
    /\.html$/,
    /\.js$/,
    /\.json$/,
    /\.css$/,
    /\.png$/,
    /\.jpg$/,
    /\.jpeg$/,
    /\.gif$/,
    /\.ico$/,
    /\.blat$/,
    /\.dat$/,
    /\.webmanifest$/
];

const offlineAssetsExclude = [
    /^service-worker\.js$/,
    /^service-worker\.published\.js$/
];

const navigationExcludePatterns = [
    /^\/Identity\//,
    /^\/api\//
];

self.addEventListener('install', event => {
    event.waitUntil(onInstall(event));
});

self.addEventListener('activate', event => {
    event.waitUntil(onActivate(event));
});

self.addEventListener('fetch', event => {
    event.respondWith(onFetch(event));
});

async function onInstall(event) {
    console.info('Installing service worker...');

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);

    self.skipWaiting();
}

async function onActivate(event) {
    console.info('Activating service worker...');

    const cacheKeys = await caches.keys();
    const oldCacheKeys = cacheKeys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName);

    await Promise.all(oldCacheKeys.map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    const request = event.request;

    if (request.method !== 'GET') {
        return fetch(request);
    }

    const url = new URL(request.url);

    if (url.origin !== self.location.origin) {
        return fetch(request);
    }

    const isNavigationRequest =
        request.mode === 'navigate' ||
        (request.headers.get('accept') || '').includes('text/html');

    if (isNavigationRequest && !navigationExcludePatterns.some(pattern => pattern.test(url.pathname))) {
        const cache = await caches.open(cacheName);
        const cachedIndex = await cache.match('/');
        return cachedIndex || fetch(request);
    }

    const cachedResponse = await caches.match(request);
    return cachedResponse || fetch(request);
}