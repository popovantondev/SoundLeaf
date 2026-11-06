"""Check user navigation, screenshots and website copies before publication."""
from pathlib import Path
from html.parser import HTMLParser
import re

root = Path(__file__).resolve().parent
base = 'https://popovantondev.github.io/SoundLeaf/'
errors = []
for name in ('README.md', 'README.en.md', 'README.ru.md', 'distribution/README.md'):
    path = root / name
    text = path.read_text(encoding='utf-8')
    for label, url in re.findall(r'\[([^\]]+)\]\(([^)]+)\)', text):
        if 'Guide-' in url and '.html' in url and not url.startswith(base):
            errors.append(name + ': guide must open a published page: ' + url)
        if '/releases/latest' in url or '/raw/' in url:
            errors.append(name + ': incorrect user destination: ' + url)
        if not url.startswith(('https://', 'http://', '#')) and not (path.parent / url.split('#')[0]).exists():
            errors.append(name + ': missing local link: ' + url)
    for url in re.findall(r'(?:src|srcset)="([^"]+)"', text):
        if not url.startswith('https://') and not (path.parent / url).exists():
            errors.append(name + ': missing screenshot: ' + url)
    if '3.0.5' not in text:
        errors.append(name + ': release version missing')
HOME = {'ru':'← На страницу программы', 'de':'← Zur Programmseite', 'en':'← Program page'}
LAND = {'de': 'index.html', 'en': 'index-en.html', 'ru': 'index-ru.html'}
for lang in HOME:
    guide = (root / 'website' / ('Guide-' + lang + '.html')).read_text(encoding='utf-8')
    if not re.search(r'<a\b[^>]*href="' + LAND[lang] + r'"[^>]*>\s*' + HOME[lang] + r'\s*</a>', guide):
        errors.append('Guide-' + lang + '.html: no button to the program page')
for lang in ('de', 'ru', 'en'):
    for kind in ('Guide', 'index', 'rights', 'notices', 'licenses'):
        name = kind + '-' + lang + '.html'
        source = root / 'website' / name
        published = root / name
        if source.read_bytes() != published.read_bytes():
            errors.append(name + ': website source differs from published copy')
        if 'lang="' + lang + '"' not in source.read_text(encoding='utf-8'):
            errors.append(name + ': language mismatch')
for name in ('index.html', 'site.css', 'site.js', 'guide-polish.css'):
    if (root/name).read_bytes() != (root/'website'/name).read_bytes():
        errors.append(name + ': website source differs from published copy')
for kit in sorted((root/'website'/'assets'/'kit').glob('*')):
    if not (root/'assets'/'kit'/kit.name).is_file() or kit.read_bytes() != (root/'assets'/'kit'/kit.name).read_bytes():
        errors.append('assets/kit/' + kit.name + ': website kit differs from published copy')
for name in ('RIGHTS.md', 'THIRD_PARTY_NOTICES.md'):
    if (root/name).read_bytes() != (root/'website'/name).read_bytes():
        errors.append(name + ': canonical website copy differs')
css = (root/'site.css').read_text(encoding='utf-8')
for theme in ('light', 'dark'):
    for folder in (root, root/'website'):
        webp = folder/'assets'/('forest-' + theme + '.webp')
        if not webp.is_file() or webp.stat().st_size > 400_000:
            errors.append(str(webp.relative_to(root)) + ': missing or over 400 KB')
if '.png' in ''.join(re.findall(r'url\(([^)]*forest[^)]*)\)', css)):
    errors.append('site.css: forest background must be WebP')
if not re.search(r'forest-night\s*\{[^}]*\}', css) or re.search(r'(?m)^\s*\.forest-night\s*\{[^}]*background-image', css):
    errors.append('site.css: the night photo must load only in the dark theme')
for page in sorted(root.glob('*.html')) + sorted((root/'website').glob('*.html')):
    text = page.read_text(encoding='utf-8')
    if 'site.css' in text and 'href="site.css?v=' not in text:
        errors.append(str(page.relative_to(root)) + ': site.css without ?v=')
if errors:
    raise SystemExit('\n'.join(errors))
print('User documentation links, localized pages and screenshots verified.')
