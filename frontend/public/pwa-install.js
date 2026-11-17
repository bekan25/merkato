(() => {
  const button = document.getElementById('merkato-install-button');
  if (!button) return;
  let installPrompt = null;
  const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
  if (isStandalone) {
    button.hidden = true;
    return;
  }
  button.hidden = false;
  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault();
    installPrompt = event;
    button.textContent = 'Install MERKATO';
  });
  window.addEventListener('appinstalled', () => {
    installPrompt = null;
    button.hidden = true;
  });
  button.addEventListener('click', async () => {
    if (!installPrompt) {
      window.alert('To install MERKATO, open your browser menu and choose “Add to Home screen” or “Install app”.');
      return;
    }
    installPrompt.prompt();
    await installPrompt.userChoice;
    installPrompt = null;
  });
})();