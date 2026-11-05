(() => {
  const root = document.documentElement;
  function isDark() {
    const t = root.dataset.theme;
    return t === 'dark' || (!t && matchMedia('(prefers-color-scheme: dark)').matches);
  }
  // The theme itself is set by the shared kit (assets/kit); keep the page parts that depend on it in step.
  function sync() {
    const dark = isDark();
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', dark ? '#10271c' : '#e9f0e4');
    document.querySelectorAll('.screen-stack img').forEach(i => {
      const shown = i.classList.contains(dark ? 'screen-dark' : 'screen-light');
      i.setAttribute('aria-hidden', String(!shown));
    });
  }
  sync();
  new MutationObserver(sync).observe(root, { attributes: true, attributeFilter: ['data-theme'] });
  if (!matchMedia('(prefers-reduced-motion:reduce)').matches && !matchMedia('(pointer:coarse)').matches) {
    let pending = false, x = 0, y = 0;
    window.addEventListener('pointermove', e => {
      x = (e.clientX / innerWidth - .5) * 10;
      y = (e.clientY / innerHeight - .5) * 8;
      if (!pending) {
        pending = true;
        requestAnimationFrame(() => {
          document.querySelectorAll('.forest-photo').forEach(a => a.style.transform = 'translate3d(' + x + 'px,' + y + 'px,0)');
          pending = false;
        });
      }
    }, { passive: true });
  }
})();