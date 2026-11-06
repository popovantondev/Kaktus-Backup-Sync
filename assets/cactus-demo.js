/* Live startup scenes of Kaktus Backup & Sync (landing hero, landing demo, guide hero; ru/de/en pages).
 * Mirrors src/AntonsBackupManager.App/StartupWindow.xaml(.cs): the cactus grows 0.12 -> 1 in 3 s
 * (one clock for both axes, sine ease in-out, centre = base of the plant), the pot stays still and
 * covers the growth seam (two layers of one picture), a click or key skips, reduced motion = final frame.
 * Every [data-cactus-demo] element is one independent scene. data-start="load" grows at once,
 * data-start="view" grows when the scene first scrolls into view. No sound, no network, no storage. */
(function () {
  'use strict';
  var roots = document.querySelectorAll('[data-cactus-demo]');
  if (!roots.length) return;
  var NS_ID = 0;
  var DURATION = 3000, START_SCALE = 0.12;
  var motion = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
  function reduced() { return !!(motion && motion.matches); }
  function ease(t) { return (1 - Math.cos(Math.PI * t)) / 2; } /* SineEase, EaseInOut */

  function init(root) {
    var art = root.querySelector('.cactus-art');
    var win = root.querySelector('.cactus-window') || root;
    var scene = root.parentNode;
    var fallback = scene ? scene.querySelector('.cactus-static') : null;
    var replay = root.querySelector('.cactus-replay');
    var IMAGE = root.getAttribute('data-image');
    var mode = root.getAttribute('data-start') || 'view';
    if (!art || !IMAGE) return;

    /* Art: 300 x 300 box like the program. Layer 1 grows (clipped to the soil line + the ellipse that
     * hides the seam), layer 2 is the same picture clipped to the pot and stays still. Eyelids sit on the growing layer. */
    var n = ++NS_ID;
    var id = function (name) { return 'cactus-' + name + '-' + n; };
    art.innerHTML =
      '<svg class="cactus-svg" viewBox="0 0 300 300" focusable="false" aria-hidden="true">' +
      '<defs>' +
      '<clipPath id="' + id('seam') + '"><rect x="0" y="0" width="300" height="211"/><ellipse cx="150" cy="216" rx="56" ry="7"/></clipPath>' +
      '<clipPath id="' + id('plant') + '"><rect x="0" y="0" width="300" height="224"/></clipPath>' +
      '<clipPath id="' + id('pot') + '"><rect x="0" y="211" width="300" height="89"/></clipPath>' +
      '<radialGradient id="' + id('skin') + '" cx="50%" cy="45%" r="65%"><stop offset="0" stop-color="#86a756"/><stop offset="1" stop-color="#7a9b4d"/></radialGradient>' +
      '</defs>' +
      '<g clip-path="url(#' + id('seam') + ')"><g class="cactus-grow">' +
      '<image href="' + IMAGE + '" width="300" height="300" clip-path="url(#' + id('plant') + ')"/>' +
      '<g class="cactus-eyes">' +
      '<g class="cactus-eye"><ellipse class="cactus-lid" fill="url(#' + id('skin') + ')" cx="136" cy="106.2" rx="6.3" ry="9.3"/><path class="cactus-lash" d="M131.2 108.6 Q136 112.4 140.8 108.6"/></g>' +
      '<g class="cactus-eye"><ellipse class="cactus-lid" fill="url(#' + id('skin') + ')" cx="167.2" cy="109.8" rx="6.8" ry="9.6"/><path class="cactus-lash" d="M161.8 112.4 Q167.2 116.4 172.6 112.4"/></g>' +
      '</g></g></g>' +
      '<image href="' + IMAGE + '" width="300" height="300" clip-path="url(#' + id('pot') + ')"/>' +
      '</svg>';
    var grow = art.querySelector('.cactus-grow');

    function setScale(s) {
      grow.setAttribute('transform', 'translate(150 224) scale(' + s.toFixed(4) + ') translate(-150 -224)');
    }

    var raf = 0, startedAt = 0, running = false, started = false, visible = true, blinkTimer = 0;

    function finish() {
      if (raf) cancelAnimationFrame(raf);
      raf = 0;
      running = false;
      setScale(1);
      win.setAttribute('data-state', 'grown');
      scheduleBlink();
    }
    function frame(now) {
      if (!running) return;
      var t = Math.min(1, (now - startedAt) / DURATION);
      setScale(START_SCALE + (1 - START_SCALE) * ease(t));
      if (t >= 1) finish(); else raf = requestAnimationFrame(frame);
    }
    function start() {
      started = true;
      clearTimeout(blinkTimer);
      if (raf) cancelAnimationFrame(raf);
      if (reduced()) { finish(); return; }
      running = true;
      win.setAttribute('data-state', 'growing');
      setScale(START_SCALE);
      raf = requestAnimationFrame(function (now) { startedAt = now; raf = requestAnimationFrame(frame); });
    }
    function skip() { if (running) finish(); }

    /* blink: every 4-8 s, only when the plant is grown and on screen */
    function blinkOnce() {
      art.classList.remove('is-blink');
      void art.offsetWidth; /* restart the CSS animation */
      art.classList.add('is-blink');
      setTimeout(function () { art.classList.remove('is-blink'); }, 260);
    }
    function scheduleBlink() {
      clearTimeout(blinkTimer);
      if (reduced()) return;
      blinkTimer = setTimeout(function () {
        if (!running && visible && !document.hidden && !reduced()) blinkOnce();
        scheduleBlink();
      }, 4000 + Math.random() * 4000);
    }

    /* lift on hover / focus / touch, like the robot */
    function lift(on) { art.classList.toggle('is-lifted', on && !reduced()); }
    art.addEventListener('pointerenter', function () { lift(true); });
    art.addEventListener('pointerleave', function () { lift(false); });
    art.addEventListener('focus', function () { lift(true); });
    art.addEventListener('blur', function () { lift(false); });
    var tapTimer = 0;
    art.addEventListener('click', function () {
      lift(true);
      clearTimeout(tapTimer);
      tapTimer = setTimeout(function () { if (!art.matches(':hover, :focus')) lift(false); }, 900);
    });

    /* skip: click anywhere in the scene, any key while the scene has focus */
    function fromReplay(e) { return replay && replay.contains(e.target); }
    win.addEventListener('pointerdown', function (e) { if (!fromReplay(e)) skip(); });
    win.addEventListener('keydown', function (e) { if (e.key !== 'Tab' && !fromReplay(e)) skip(); });
    if (replay) replay.addEventListener('click', start);

    /* show the live scene, hide the static picture */
    root.hidden = false;
    if (fallback) fallback.hidden = true;
    setScale(reduced() ? 1 : START_SCALE);
    win.setAttribute('data-state', reduced() ? 'grown' : 'ready');

    if (reduced()) {
      started = true;
      scheduleBlink();
    } else if (mode === 'load' || !('IntersectionObserver' in window)) {
      start();
      if ('IntersectionObserver' in window) {
        new IntersectionObserver(function (entries) {
          entries.forEach(function (en) { visible = en.isIntersecting; });
        }).observe(win);
      }
    } else {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (en) {
          visible = en.isIntersecting;
          if (en.isIntersecting && !started) start();
        });
      }, { threshold: 0.6 });
      io.observe(win);
    }
    if (motion && motion.addEventListener) {
      motion.addEventListener('change', function () { if (reduced()) { finish(); lift(false); } else { scheduleBlink(); } });
    }
  }

  for (var i = 0; i < roots.length; i++) init(roots[i]);
})();
