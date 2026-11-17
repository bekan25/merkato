(function () {
  'use strict';
  if (!('serviceWorker' in navigator)) return;

  window.addEventListener('load', function () {
    navigator.serviceWorker.register('/sw.js', { scope: '/' })
      .catch(function (error) { console.warn('MERKATO offline support could not be enabled.', error); });
  });

  var deferredPrompt = null;
  var dismissedKey = 'merkato_pwa_install_dismissed';
  var isIos = /iphone|ipad|ipod/i.test(window.navigator.userAgent) &&
    !window.matchMedia('(display-mode: standalone)').matches &&
    !window.navigator.standalone;

  function wasDismissed() {
    try { return window.localStorage.getItem(dismissedKey) === '1'; }
    catch (_) { return false; }
  }

  function createBanner() {
    if (document.getElementById('merkato-install-banner') || wasDismissed()) return null;
    var banner = document.createElement('aside');
    banner.id = 'merkato-install-banner';
    banner.setAttribute('aria-label', 'Install MERKATO app');
    banner.hidden = true;

    var mark = document.createElement('span');
    mark.className = 'merkato-pwa-mark';
    mark.textContent = 'A';

    var copy = document.createElement('span');
    copy.className = 'merkato-pwa-copy';
    var title = document.createElement('strong');
    title.textContent = 'Install MERKATO';
    var detail = document.createElement('small');
    detail.textContent = isIos
      ? 'Tap Share, then “Add to Home Screen”.'
      : 'Add the marketplace to your home screen.';
    copy.append(title, detail);

    var install = document.createElement('button');
    install.type = 'button';
    install.textContent = isIos ? 'Got it' : 'Install';
    install.addEventListener('click', async function () {
      if (isIos) { banner.hidden = true; return; }
      if (!deferredPrompt) return;
      deferredPrompt.prompt();
      var choice = await deferredPrompt.userChoice;
      deferredPrompt = null;
      if (choice && choice.outcome === 'accepted') banner.hidden = true;
    });

    var close = document.createElement('button');
    close.type = 'button';
    close.className = 'merkato-pwa-close';
    close.setAttribute('aria-label', 'Dismiss install suggestion');
    close.textContent = '×';
    close.addEventListener('click', function () {
      banner.hidden = true;
      try { window.localStorage.setItem(dismissedKey, '1'); } catch (_) {}
    });

    banner.append(mark, copy, install, close);
    document.body.appendChild(banner);
    return banner;
  }

  var banner = null;
  function showBanner() {
    if (!banner) banner = createBanner();
    if (banner) banner.hidden = false;
  }

  window.addEventListener('beforeinstallprompt', function (event) {
    event.preventDefault();
    deferredPrompt = event;
    showBanner();
  });

  if (isIos) {
    window.addEventListener('load', function () {
      window.setTimeout(showBanner, 1800);
    });
  }

  window.addEventListener('appinstalled', function () {
    if (banner) banner.hidden = true;
    deferredPrompt = null;
  });
})();
