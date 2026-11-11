(() => {
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* Resolve versioned zip from GitHub latest release (asset names include the tag). */
  fetch('https://api.github.com/repos/rockyflux/QuickText/releases/latest')
    .then((r) => (r.ok ? r.json() : null))
    .then((rel) => {
      if (!rel?.assets) return;
      const zip = rel.assets.find((a) => /-win-x64\.zip$/i.test(a.name) && !/needs-dotnet/i.test(a.name));
      if (!zip) return;
      $$('a[data-os="win"]').forEach((a) => { a.href = zip.browser_download_url; });
      const ver = (rel.tag_name || '').replace(/^v/, '');
      $$('.dl-card[data-os="win"] span').forEach((el, i) => {
        if (i === 0 && ver) el.textContent = `v${ver} · x64 · 自带运行时`;
      });
      const pill = $('.pill');
      if (pill && rel.tag_name) {
        pill.href = rel.html_url;
        pill.childNodes.forEach((n) => {
          if (n.nodeType === 3 && n.textContent.trim()) n.textContent = `${rel.tag_name} 已发布`;
        });
        // rebuild pill text cleanly
        const arrow = pill.querySelector('svg');
        pill.replaceChildren();
        const dot = document.createElement('span');
        dot.className = 'pill-dot';
        pill.append(dot, document.createTextNode(`${rel.tag_name} 已发布`), arrow);
      }
    })
    .catch(() => { /* keep static hrefs */ });

  /* nav border on scroll */
  const nav = $('.nav');
  const onScroll = () => nav.classList.toggle('scrolled', scrollY > 8);
  addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  /* language stub */
  const langToast = $('#lang-toast');
  $('#lang-toggle')?.addEventListener('click', () => {
    langToast.classList.add('on');
    clearTimeout(langToast._t);
    langToast._t = setTimeout(() => langToast.classList.remove('on'), 2200);
  });

  /* ---------- hero stage ---------- */
  const shell = $('#stage-shell');
  const stage = $('#stage');
  const fit = () => {
    if (!shell || !stage) return;
    const s = Math.min(1, shell.clientWidth / stage.offsetWidth);
    stage.style.setProperty('--s', s);
    shell.style.height = `${stage.offsetHeight * s}px`;
  };
  if (shell && stage) {
    new ResizeObserver(fit).observe(shell);
    fit();
  }

  const palette = $('#palette');
  const toast = $('#toast');
  const keys = $('#keys');
  const host = $('#host-field');
  const pq = $('#pq');
  let token = 0;

  const sleep = (ms, t) => new Promise((res, rej) => setTimeout(() => (t === token ? res() : rej('cancel')), ms));

  const PASTE = '你好，这是我常用的一段话，请查收。';
  const HOST_BASE = '你好，关于上周提到的方案，我想补充几点：';

  function setQuery(text) {
    if (!pq) return;
    pq.replaceChildren();
    pq.append(document.createTextNode(text));
    const cur = document.createElement('span');
    cur.className = 'cur';
    pq.append(cur);
  }

  function reset() {
    palette?.classList.remove('on');
    toast?.classList.remove('on');
    keys?.classList.remove('on', 'press');
    if (host) {
      const caret = document.createElement('span');
      caret.className = 'caret';
      caret.id = 'host-caret';
      host.replaceChildren(document.createTextNode(HOST_BASE), caret);
    }
    setQuery('');
  }

  async function pressKeys(t) {
    keys.classList.add('on');
    await sleep(280, t);
    keys.classList.add('press');
    await sleep(160, t);
    keys.classList.remove('press');
    await sleep(420, t);
    keys.classList.remove('on');
  }

  function pasteHost() {
    if (!host) return;
    const pasted = document.createElement('span');
    pasted.className = 'pasted';
    pasted.textContent = PASTE;
    const caret = document.createElement('span');
    caret.className = 'caret';
    host.replaceChildren(document.createTextNode(HOST_BASE), pasted, caret);
  }

  async function demo() {
    const t = ++token;
    try {
      for (;;) {
        reset();
        await sleep(800, t);
        await pressKeys(t);
        palette.classList.add('on');
        await sleep(350, t);
        let q = '';
        for (const ch of 'hf') {
          q += ch;
          setQuery(q);
          await sleep(140, t);
        }
        await sleep(900, t);
        palette.classList.remove('on');
        pasteHost();
        toast.classList.add('on');
        await sleep(2200, t);
        toast.classList.remove('on');
        await sleep(1800, t);
      }
    } catch (_) { /* cancelled */ }
  }

  function stopDemo() { token++; }

  function staticFinal() {
    reset();
    setQuery('hf');
    palette.classList.add('on');
    pasteHost();
  }

  if (shell) {
    if (reduce) staticFinal();
    else {
      let running = false;
      new IntersectionObserver(([en]) => {
        if (en.isIntersecting && !running) { running = true; demo(); }
        else if (!en.isIntersecting && running) { running = false; stopDemo(); }
      }, { threshold: 0.25 }).observe(shell);
    }
  }

  $('#try')?.addEventListener('click', () => {
    shell?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'center' });
    stopDemo();
    if (reduce) staticFinal();
    else demo();
  });

  /* reveal on scroll */
  if (!reduce) {
    const io = new IntersectionObserver((entries) => {
      entries.forEach((e) => {
        if (e.isIntersecting) {
          e.target.classList.add('in');
          io.unobserve(e.target);
        }
      });
    }, { threshold: 0.12 });
    $$('.section').forEach((el) => { el.classList.add('reveal'); io.observe(el); });
  }
})();
