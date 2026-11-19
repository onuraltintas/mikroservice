// Live test 19.11.2026 (A6, decision K2): Master Hızlı Okuma no longer uses a service worker.
// Browsers that installed the old Angular worker fetch this file on their next visit. It removes the old
// caches, unregisters itself and reloads open tabs so they load the current release from the server.
self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.map(key => caches.delete(key)));
    await self.registration.unregister();
    const windows = await self.clients.matchAll({ type: 'window' });
    await Promise.all(windows.map(client => client.navigate(client.url).catch(() => undefined)));
  })());
});
