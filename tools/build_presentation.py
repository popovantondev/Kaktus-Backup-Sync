"""Build the localized product introductions (landing pages) on the shared kit.

Inputs: public-release.json (release metadata) and tools/presentation.json
(page data: screenshots, palette, stack, installation steps, features, checks).
Outputs: index.html (German), index-en.html, index-ru.html, the redirect
index-de.html and presentation.css. Guide pages are refreshed by polish_guides.py.
"""
from pathlib import Path
import hashlib
import json
import re
from html import escape as e

ROOT = Path(__file__).resolve().parents[1]
LANGS = ('ru', 'de', 'en')
SITE = 'https://popovantondev.github.io/'
PROFILE = SITE + 'popovantondev/'
LANDING = {'de': 'index.html', 'en': 'index-en.html', 'ru': 'index-ru.html'}
CATALOG = {l: PROFILE + LANDING[l] for l in LANGS}
GITHUB = 'https://github.com/popovantondev/'
TEXT = {
 'ru': dict(skip='К содержимому', nav='Основная навигация', all='Все программы', search='Поиск', kbd='Ctrl K',
            search_label='Поиск по программам', theme='Сменить тему', legal='Правовая информация', new='НОВОЕ',
            pill_release='Вышла версия {v}', pill_preview='Вышла предварительная версия {v}', more='Что нового →',
            guide='Руководство', download='Скачать для', source='Исходный код', requirements='Перед запуском',
            start='Первые шаги', release='Выпуск', preview='Предварительная версия', only='Только исходники',
            flow='Как это работает', bento='Что внутри', bento_sub='Установка, функции, данные, проверка.',
            inst='Установка', inst_sub='Проверить архив, распаковать, запустить.', feat='Функции', checked='Проверено',
            shot='Демонстрационный интерфейс с искусственными данными. Открыть изображение целиком.',
            illustration='Демонстрационная иллюстрация, не снимок работающей программы. Открыть целиком.',
            source_note='Готового установочного пакета нет. Инструкции описывают настройку из исходников.',
            terminal='Терминал', lang='Язык', demo_title='Попробовать в браузере',
            demo_sub='Сначала предпросмотр, потом копирование — на примерных файлах. Ничего не копируется.',
            demo_label='Демо — примерные данные', demo_nojs='Для демо нужен JavaScript.'),
 'de': dict(skip='Zum Inhalt', nav='Hauptnavigation', all='Alle Programme', search='Suchen', kbd='Strg K',
            search_label='Programme durchsuchen', theme='Farbschema wechseln', legal='Rechtliches', new='NEU',
            pill_release='Version {v} ist da', pill_preview='Vorabversion {v} ist da', more='Was ist neu →',
            guide='Anleitung', download='Herunterladen für', source='Quellcode', requirements='Vor dem Start',
            start='Erste Schritte', release='Release', preview='Vorabversion', only='Nur Quellcode',
            flow='So funktioniert es', bento='Was drin steckt', bento_sub='Installation, Funktionen, Datenschutz, Prüfung.',
            inst='Installation', inst_sub='Archiv prüfen, entpacken, starten.', feat='Funktionen', checked='Geprüft',
            shot='Demo-Oberfläche mit künstlichen Daten. Vollständiges Bild öffnen.',
            illustration='Demo-Illustration, kein Screenshot der laufenden Anwendung. Vollständig öffnen.',
            source_note='Kein fertiges Installationspaket. Die Anleitung beschreibt die Einrichtung aus dem Quellcode.',
            terminal='Terminal', lang='Sprache', demo_title='Direkt im Browser testen',
            demo_sub='Erst Vorschau, dann Sicherung — mit Beispieldateien. Es wird nichts kopiert.',
            demo_label='Demo — Beispieldaten', demo_nojs='Für die Demo wird JavaScript benötigt.'),
 'en': dict(skip='Skip to content', nav='Main navigation', all='All programs', search='Search', kbd='Ctrl K',
            search_label='Search programs', theme='Switch colour scheme', legal='Legal', new='NEW',
            pill_release='Version {v} is out', pill_preview='Preview {v} is out', more="What's new →",
            guide='User guide', download='Download for', source='Source code', requirements='Before you start',
            start='First steps', release='Release', preview='Preview', only='Source only',
            flow='How it works', bento="What's inside", bento_sub='Installation, features, privacy, checks.',
            inst='Installation', inst_sub='Verify, extract, start.', feat='Features', checked='Checked',
            shot='Demonstration interface with artificial data. Open the full image.',
            illustration='Demonstration illustration, not a screenshot of the running application. Open in full.',
            source_note='No ready-to-install package. The guide explains how to set up the source version.',
            terminal='Terminal', lang='Language', demo_title='Try it in the browser',
            demo_sub='Preview first, then backup — on sample files. Nothing is copied.',
            demo_label='Demo — sample data', demo_nojs='The demo needs JavaScript.')}

CACTUS = {
 'ru': dict(window='Окно запуска Kaktus Backup &amp; Sync — живая демонстрация',
            art='Маленький кактус вырастает в горшке. Щелчок или клавиша пропускают рост.',
            mini='Маленький кактус в горшке. Когда блок демо появляется на экране, он вырастает.',
            replay='Вырастить ещё раз', note='Живая демонстрация окна запуска: щелчок или клавиша пропускают рост.',
            alt='Окно запуска с утверждённым маскотом Kaktus.', open='Открыть изображение целиком'),
 'de': dict(window='Startfenster von Kaktus Backup &amp; Sync — lebendige Demonstration',
            art='Ein kleiner Kaktus wächst im Topf. Klick oder Taste überspringt das Wachstum.',
            mini='Ein kleiner Kaktus im Topf. Sobald der Demo-Block im Bild erscheint, wächst er.',
            replay='Erneut wachsen lassen', note='Lebendige Demonstration des Startfensters: Ein Klick oder eine Taste überspringt das Wachstum.',
            alt='Startbildschirm mit dem freigegebenen Kaktus-Maskottchen.', open='Vollständiges Bild öffnen'),
 'en': dict(window='Kaktus Backup &amp; Sync startup window — live demonstration',
            art='A small cactus grows in its pot. A click or key skips the growth.',
            mini='A small cactus in its pot. It grows when the demo block scrolls into view.',
            replay='Grow again', note='Live demonstration of the startup window: a click or key skips the growth.',
            alt='Startup screen with the Kaktus mascot.', open='Open the full image')}
CACTUS_IMAGE = 'assets/cactus/mascot.webp'
CACTUS_STATIC = 'docs/images/manual/startup.png'


REPLAY_ARROWS = ('M5.03 11.39A7 7 0 0 1 17.36 7.5M18.97 12.61A7 7 0 0 1 6.64 16.5',
                 'M20.12 5.19L19.55 10.11L14.6 9.81ZM3.88 18.81L4.45 13.89L9.4 14.19Z')


def cactus_replay(lang):
    """Two thick round arrows on a bare button (no disc, no frame) like the badge on the Kaktus pot: cream arrows
    with a dark green rim so they read on the light window; label and tooltip in the page language."""
    label = CACTUS[lang]['replay']
    arcs, heads = REPLAY_ARROWS
    shape = '<path class="%s" d="' + arcs + '"/><path class="%s cactus-head" d="' + heads + '"/>'
    return ('<button type="button" class="cactus-replay" aria-label="%s" title="%s">'
            '<svg viewBox="0 0 24 24" width="30" height="30" focusable="false" aria-hidden="true">'
            + shape % ('cactus-arrow-rim', 'cactus-arrow-rim') + shape % ('cactus-arrow', 'cactus-arrow')
            + '</svg></button>') % (label, label)

def cactus_scene(d, lang, kind):
    """Live startup window. kind: hero (landing, grows at once), guide (guide hero, with note). Both carry the round replay button."""
    c = CACTUS[lang]
    version = d['version'].split('-')[0].rsplit('.', 1)[0] + (' · Preview' if d['status'] == 'preview' else '')
    window = ('<div class="cactus-demo%s" data-cactus-demo data-start="load" data-image="%s" hidden>\n'
              '<div class="cactus-window" role="group" aria-label="%s">\n'
              '<div class="cactus-copy"><div class="cactus-title">Kaktus</div><div class="cactus-sub">Backup &amp; Sync</div><div class="cactus-bar"></div><div class="cactus-ver">%s</div></div>\n'
              '<div class="cactus-art" tabindex="0" role="img" aria-label="%s"></div>\n%s\n</div>\n') % (
        ' cactus-hero' if kind == 'hero' else '', CACTUS_IMAGE, c['window'], version, c['art'], cactus_replay(lang))
    if kind == 'guide':
        window += '<p class="cactus-note">%s</p>\n' % c['note']
    window += '</div>\n'
    static = ('<p class="cactus-static"><a class="guide-full-image" href="%s" aria-label="%s"><img src="%s" alt="%s" width="600" height="340"></a></p>\n') % (
        CACTUS_STATIC, c['open'], CACTUS_STATIC, c['alt'])
    return '<div class="cactus-scene">\n' + static + window + '</div>'


def cactus_mini(lang):
    """Plant only, inside the browser demo; grows when the demo scrolls into view."""
    return ('<div class="cactus-demo cactus-mini" data-cactus-demo data-start="view" data-image="%s" hidden>'
            '<div class="cactus-art" tabindex="0" role="img" aria-label="%s"></div>%s</div>') % (
        CACTUS_IMAGE, CACTUS[lang]['mini'], cactus_replay(lang))


def cactus_head():
    css = (ROOT / 'assets/cactus-demo.css').read_text(encoding='utf-8')
    return '<link rel="stylesheet" href="assets/cactus-demo.css?v=%s">' % file_hash(css)


def cactus_script():
    return '<script src="assets/cactus-demo.js?v=%s" defer></script>' % file_hash((ROOT / 'assets/cactus-demo.js').read_text(encoding='utf-8'))


CSS = '''/* Generated by tools/build_presentation.py from tools/presentation.json. The shared kit lives in assets/kit. */
:root{--ppk-accent:{l[accent]};--ppk-tint:{l[tint]};--ppk-hero:{l[hero]};--ppk-hero-end:{l[hero-end]}}
:root[data-theme="dark"]{--ppk-accent:{d[accent]};--ppk-tint:{d[tint]};--ppk-hero:{d[hero]};--ppk-hero-end:{d[hero-end]}}
@media (prefers-color-scheme:dark){:root:not([data-theme]){--ppk-accent:{d[accent]};--ppk-tint:{d[tint]};--ppk-hero:{d[hero]};--ppk-hero-end:{d[hero-end]}}}
.ppk-stage a{display:block;line-height:0;border-radius:10px}
.ppk-stage img{max-width:100%}
.site-start{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:24px;align-items:center;padding:28px;border:1px solid var(--ppk-line);border-radius:20px;background:var(--ppk-panel)}
.site-start h2{margin:0 0 6px}.site-start p{margin:0;color:var(--ppk-muted)}.site-start .ppk-actions{margin:0}
.site-notice{margin:14px 0 0;padding:12px 14px;border-radius:12px;background:var(--ppk-tint);font-size:15px}
.site-third-note{margin:10px 0 0;font-size:14px;color:var(--ppk-muted)}
.ppk-rows small{margin-left:4px;color:var(--ppk-muted)}
.site-shot-section{margin-top:28px}
.site-shot-section .ppk-preview{max-width:980px;margin-inline:auto}
.ppk-demo-body{grid-template-columns:220px minmax(0,1fr) 240px;align-items:stretch}
.ppk-demo-body>.cactus-mini{align-self:center;padding:12px 16px 12px 0}
@media(max-width:900px){.ppk-demo-body{grid-template-columns:220px minmax(0,1fr)}.ppk-demo-body>.cactus-mini{grid-column:1/-1;padding:0 16px 12px}}
@media(max-width:560px){.ppk-demo-body{grid-template-columns:1fr}}
.ppk-demo-empty:focus-visible{outline:2px solid var(--ppk-focus);outline-offset:2px}
@media(max-width:760px){.site-start{grid-template-columns:1fr;padding:22px}}
'''


def load():
    cfg = json.loads((ROOT / 'tools/presentation.json').read_text(encoding='utf-8'))
    data = json.loads((ROOT / 'public-release.json').read_text(encoding='utf-8'))
    return cfg, data


def kit_version():
    return (ROOT / load()[0]['folder'] / 'assets/kit/KIT_VERSION').read_text(encoding='utf-8').strip()


def css_text(cfg):
    pal = cfg['palette']
    return CSS.replace('{l[accent]}', pal['light']['accent']).replace('{l[tint]}', pal['light']['tint']) \
        .replace('{l[hero]}', pal['light']['hero']).replace('{l[hero-end]}', pal['light']['hero-end']) \
        .replace('{d[accent]}', pal['dark']['accent']).replace('{d[tint]}', pal['dark']['tint']) \
        .replace('{d[hero]}', pal['dark']['hero']).replace('{d[hero-end]}', pal['dark']['hero-end'])


def file_hash(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:8]


def status_text(d, lang):
    t = TEXT[lang]
    return t[{'release': 'release', 'preview': 'preview', 'source': 'only'}[d['status']]] + ' · ' + d['version']


def head_block(cfg, d, version, extra_css=''):
    """Kit includes shared by landing pages and guides (kit-theme.js first: no theme flash)."""
    css = css_text(cfg)
    v = file_hash(css)
    out = ['<link rel="icon" href="%s">' % e(cfg['icon']),
           '<script src="assets/kit/kit-theme.js"></script>',
           '<link rel="stylesheet" href="assets/kit/kit.css?v=%s">' % version,
           '<link rel="stylesheet" href="presentation.css?v=%s">' % v]
    if extra_css:
        out.append(extra_css)
    out += ['<script src="assets/kit/programs.js?v=%s" defer></script>' % version,
            '<script src="assets/kit/kit.js?v=%s" defer></script>' % version]
    return '\n'.join(out)


def header_block(cfg, d, lang, pages):
    """Kit header. pages maps language to the file of the same page in that language."""
    t = TEXT[lang]
    langs = ''.join('<a href="%s" lang="%s" hreflang="%s"%s>%s</a>' % (
        pages[x], x, x, ' aria-current="page"' if x == lang else '', x.upper()) for x in LANGS)
    return ('<div class="ppk-nav-wrap" id="ppk-nav-wrap">\n'
            '<nav class="ppk-nav" aria-label="%s">'
            '<a class="ppk-brand" href="%s"><img src="%s" alt="" width="34" height="34"><span>%s</span></a>'
            '<span class="ppk-spacer"></span>'
            '<a class="ppk-back" href="%s">← %s</a>'
            '<button class="ppk-search-btn" type="button" data-ppk-open-search aria-label="%s"><span>%s</span><kbd>%s</kbd></button>'
            '<div class="ppk-langs">%s</div>'
            '<button class="ppk-icon-btn ppk-theme-btn" type="button" data-ppk-theme aria-label="%s"></button>'
            '<a class="ppk-icon-btn ppk-gh-btn" href="%s" aria-label="GitHub"></a>'
            '</nav>\n</div>') % (
        t['nav'], LANDING[lang], e(cfg['icon']), e(d['name']), CATALOG[lang], t['all'], t['search_label'],
        t['search'], t['kbd'], langs, t['theme'], e(d['source_url']))


LEGAL_LINKS = {'ru': ('Сообщить о проблеме', 'Условия использования'),
               'de': ('Problem melden', 'Nutzungsbedingungen'),
               'en': ('Report a problem', 'Terms of use')}


def rights_url(d, lang):
    """Link to the terms of use of the repository: RIGHTS in the page language, else RIGHTS.md, else LICENSE."""
    for name in ('RIGHTS.%s.md' % lang, 'RIGHTS.md', 'LICENSE'):
        if (ROOT / name).is_file():
            return d['source_url'] + '/blob/main/' + name
    raise SystemExit('no RIGHTS or LICENSE file for the footer link')


def footer_block(d, lang):
    t = TEXT[lang]
    issue_label, rights_label = LEGAL_LINKS[lang]
    return ('<footer class="ppk-footer">\n<span>© 2026 Anton Popov · %s %s</span>\n'
            '<nav aria-label="%s"><a href="%simpressum.html">Impressum</a><a href="%sdatenschutz.html">Datenschutz</a>'
            '<a href="%s">GitHub</a><a href="%s">%s</a><a href="%s">%s</a></nav>\n</footer>') % (
        e(d['name']), e(d['version']), t['legal'], PROFILE, PROFILE, e(d['source_url']),
        e(d['feedback_url']), issue_label, e(rights_url(d, lang)), rights_label)


def resolve(text, d):
    """Fill {zip}, {sha}, {exe}, {version}, {name} in installation lines."""
    zipname = d['downloads'][0] if d['downloads'] else ''
    return (text.replace('{base}', zipname[:-4] if zipname.endswith('.zip') else zipname)
            .replace('{zip}', zipname)
            .replace('{sha}', d['checksums'][0] if d['checksums'] else '')
            .replace('{version}', d['version']).replace('{name}', d['name']))


def pick(value, lang):
    return value[lang] if isinstance(value, dict) else value


def bento(cfg, d, lang):
    t = TEXT[lang]
    b = cfg['bento']
    lines = ''.join('<div class="ppk-%s">%s</div>' % (cls, e(resolve(pick(text, lang), d))) for cls, text in b['terminal'])
    third = b['third']
    if third.get('live'):
        extra = '<span class="ppk-live">%s</span>' % e(pick(third['label'], lang))
    else:
        extra = '<div class="ppk-chips">%s</div>' % ''.join('<span>%s</span>' % e(x) for x in pick(third['label'], lang))
    chips = ''.join('<span>%s</span>' % e(x) for x in b['features'][lang])
    checks = ''.join('<li>%s</li>' % e(x) for x in b['checks'][lang])
    return (
        '<section class="ppk-bento-section ppk-section ppk-reveal" aria-labelledby="bento-title">\n'
        '<h2 id="bento-title">%s</h2><p class="ppk-sub">%s</p>\n<div class="ppk-bento">\n'
        '<div class="ppk-tile ppk-wide"><h3>%s</h3><p>%s</p>'
        '<div class="ppk-term" role="group" aria-label="%s"><div class="ppk-term-bar"><i></i><i></i><i></i></div>'
        '<div class="ppk-term-out">%s</div></div></div>\n'
        '<div class="ppk-tile"><h3>%s</h3><p>%s</p>%s</div>\n'
        '<div class="ppk-tile"><h3>%s</h3><div class="ppk-chips">%s</div></div>\n'
        '<div class="ppk-tile ppk-wide"><h3>%s</h3><ul class="ppk-checks">%s</ul></div>\n</div>\n</section>') % (
        t['bento'], t['bento_sub'], t['inst'], t['inst_sub'], t['terminal'], lines,
        e(pick(third['title'], lang)), e(pick(third['text'], lang)), extra, t['feat'], chips, t['checked'], checks)


def demo_section(d, lang):
    """Live demo skeleton; assets/demo.js builds the controls (in-memory sample data only)."""
    t = TEXT[lang]
    return ('<section class="ppk-section ppk-demo-section ppk-reveal" id="demo" data-demo="kaktus" aria-labelledby="demo-title">\n'
            '<h2 id="demo-title">%s</h2><p class="ppk-sub">%s</p>\n'
            '<div class="ppk-demo"><div class="ppk-demo-head"><span>%s</span><span class="ppk-demo-label">%s</span></div>\n'
            '<div class="ppk-demo-body"><div class="ppk-demo-main"><noscript><p class="ppk-demo-empty">%s</p></noscript></div>%s</div></div>\n'
            '</section>') % (e(t['demo_title']), e(t['demo_sub']), e(d['name']), e(t['demo_label']), e(t['demo_nojs']), cactus_mini(lang))


def demo_script():
    """Script tag of the demo (landing pages only); the query string changes with the file."""
    return '<script src="assets/demo.js?v=%s" defer></script>' % file_hash((ROOT / 'assets/demo.js').read_text(encoding='utf-8'))


def landing(cfg, d, lang, version):
    t = TEXT[lang]
    guide = 'Guide-%s.html' % lang
    status = status_text(d, lang)
    ready = d['status'] != 'source' and d.get('release_url')
    action = t['download'] + ' ' + ('macOS' if 'macOS' in d['platform'] else 'Windows') if ready else t['source']
    target = d['release_url'] if ready else d['source_url']
    caption = t[cfg.get('caption', 'shot')]
    shot = cfg['shots'][lang]
    flow = ''.join('<li>%s</li>' % e(x) for x in cfg['flow'][lang])
    pill = ''
    if d['status'] != 'source' or re.match(r'\d', d['version']):
        key = 'pill_preview' if d['status'] == 'preview' else 'pill_release'
        pill = '<a class="ppk-pill" href="%s"><span class="ppk-new">%s</span><span>%s</span> <span class="ppk-more">%s</span></a>' % (
            e(d.get('release_url') or d['source_url'] + '/blob/main/CHANGELOG.md'), t['new'], e(t[key].format(v=d['version'])), t['more'])
    notice = '' if ready else '<p class="site-notice">%s</p>' % e(t['source_note'])
    pages = dict(LANDING)
    csspath = css_text(cfg)
    return '''<!doctype html>
<html lang="%(lang)s"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="description" content="%(desc)s"><title>%(name)s · %(status)s</title>
%(head)s
</head>
<body class="ppk-page">
<a class="ppk-skip" href="#main">%(skip)s</a>
%(header)s
<main class="ppk-shell" id="main">
<section class="ppk-hero"><div>
%(pill)s
<h1>%(name)s</h1><p class="ppk-lead">%(desc)s</p>
<span class="ppk-status">%(status)s</span> <span class="ppk-chip">%(platform)s</span>
<div class="ppk-actions"><a class="ppk-btn ppk-btn-primary" href="%(target)s">%(action)s</a><a class="ppk-btn ppk-btn-gh" href="%(src)s">%(source)s <span class="ppk-chip"><i class="ppk-dot"></i>%(stack)s</span></a><a class="ppk-btn" href="%(guide)s">%(guide_label)s ↗</a></div>
<p class="ppk-requirements"><strong>%(req_title)s</strong>%(req)s</p>%(notice)s
</div>
%(scene)s
</section>
<section class="ppk-section ppk-reveal site-shot-section" aria-label="%(caption)s"><figure class="ppk-preview"><div class="ppk-stage"><a href="%(shot)s"><img src="%(shot)s" alt="%(alt)s" decoding="async"></a></div><figcaption>%(caption)s</figcaption></figure></section>
%(bento)s
%(demo)s
<section class="ppk-section ppk-process ppk-reveal" aria-labelledby="process-title"><h2 id="process-title">%(flow_title)s</h2><ol class="ppk-flow">%(flow)s</ol></section>
<section class="ppk-section ppk-reveal"><div class="site-start"><div><h2>%(start_title)s</h2><p>%(start)s</p></div><div class="ppk-actions"><a class="ppk-btn" href="%(guide)s">%(guide_label)s →</a></div></div></section>
</main>
%(footer)s
%(demo_js)s
%(cactus_js)s
</body></html>
''' % dict(lang=lang, desc=e(d['description'][lang]), name=e(d['name']), status=e(status),
           head=head_block(cfg, d, version, cactus_head()), scene=cactus_scene(d, lang, 'hero'), cactus_js=cactus_script(), skip=t['skip'], header=header_block(cfg, d, lang, pages), pill=pill,
           platform=e(d['platform']), target=e(target), action=e(action), src=e(d['source_url']), source=t['source'],
           stack=e(cfg['stack']), guide=guide, guide_label=t['guide'], req_title=t['requirements'],
           req=e(d['requirements'][lang]), notice=notice, shot=e(shot), alt=e(d['name'] + ' — ' + caption),
           caption=e(caption), bento=bento(cfg, d, lang), demo=demo_section(d, lang), demo_js=demo_script(), flow_title=t['flow'], flow=flow, start_title=t['start'],
           start=e(d['start'][lang]), footer=footer_block(d, lang))


def redirect(d):
    """Old address of the German page: forwards to index.html and keeps all language links."""
    base = SITE + d['repo'] + '/'
    return ('<!doctype html>\n<html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
            '<script>location.replace(\'index.html\'+location.hash)</script><meta http-equiv="refresh" content="0; url=index.html"><link rel="canonical" href="%sindex.html"><meta name="robots" content="noindex">'
            '<title>%s</title></head>\n<body><p><a href="index.html">Weiter zur deutschen Seite</a> · '
            '<a href="index-en.html" lang="en" hreflang="en">English</a> · <a href="index-ru.html" lang="ru" hreflang="ru">Русский</a></p></body></html>\n') % (
        base, e(d['name']))


def write(path, text):
    path.write_bytes(text.encode('utf-8'))


def main():
    cfg, d = load()
    site = ROOT / cfg['folder']
    version = kit_version()
    write(site / 'presentation.css', css_text(cfg))
    for lang in LANGS:
        write(site / LANDING[lang], landing(cfg, d, lang, version))
    write(site / 'index-de.html', redirect(d))


if __name__ == '__main__':
    main()
    import polish_guides
    polish_guides.main()
