// ProphetOps works online only. This worker lets the app be installed on a phone or computer; it
// stores nothing, so records and forecasts always come fresh from the server. When a page cannot
// be reached it answers with a short page that says so, instead of the browser's own error.

const OFFLINE_PAGE = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="theme-color" content="#F4F5F8">
<title>ProphetOps needs a connection</title>
<style>
  body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 24px; box-sizing: border-box;
    background: #F4F5F8; color: #14213D; font: 16px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
  main { max-width: 26rem; }
  h1 { margin: 0 0 8px; font-size: 20px; font-weight: 600; }
  p { margin: 0 0 20px; color: #3A4763; }
  a { display: inline-flex; align-items: center; min-height: 44px; padding: 0 18px; border-radius: 6px;
    background: #16346B; color: #FFFFFF; font-weight: 600; text-decoration: none; }
  a:focus-visible { outline: 3px solid #9DB3DB; outline-offset: 2px; }
</style>
</head>
<body>
<main>
  <h1>ProphetOps needs an internet connection</h1>
  <p>This page could not be reached. ProphetOps keeps its records on the server, so it needs the connection to show or save them. Check the connection, then try again.</p>
  <a href="/">Try again</a>
</main>
</body>
</html>`;

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

self.addEventListener('fetch', (event) => {
  // Only page loads are answered; data requests go to the server untouched.
  if (event.request.mode !== 'navigate') return;
  event.respondWith(
    fetch(event.request).catch(
      () => new Response(OFFLINE_PAGE, { status: 503, headers: { 'Content-Type': 'text/html; charset=utf-8' } }),
    ),
  );
});
