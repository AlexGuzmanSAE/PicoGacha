// Service Worker de PicoGacha: cachea el "app shell" para que la PWA
// abra incluso sin internet. Los datos de Firebase NO se cachean
// (siempre van a la red, así la tienda está actualizada).
const CACHE = "picogacha-v2";
const SHELL = [
  "./",
  "./index.html",
  "./styles.css",
  "./app.js",
  "./manifest.webmanifest",
  "./img/icon-192.png",
  "./img/icon-512.png",
  "./img/mage.png",
  "./img/engineer.png",
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE).then((cache) => cache.addAll(SHELL)).then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (event) => {
  const url = event.request.url;
  // Firebase y Google: siempre red.
  if (url.includes("googleapis.com") || url.includes("gstatic.com") || url.includes("firebaseio.com")) return;
  event.respondWith(
    caches.match(event.request).then((cached) => cached || fetch(event.request))
  );
});
