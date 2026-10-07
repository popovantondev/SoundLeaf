"""Check user navigation, screenshots and website copies before publication."""
from pathlib import Path
from html.parser import HTMLParser
import re

root = Path(__file__).resolve().parent
base = 'https://popovantondev.github.io/SoundLeaf/'
errors = []
for name in ('README.md', 'docs/README.de.md', 'docs/README.ru.md', 'distribution/README.md'):
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
for lang in ('de', 'ru', 'en'):
    for kind in ('Guide', 'index', 'rights', 'notices', 'licenses'):
        name = kind + '-' + lang + '.html'
        source = root / 'website' / name
        published = root / name
        if source.read_bytes() != published.read_bytes():
            errors.append(name + ': website source differs from published copy')
        if 'lang="' + lang + '"' not in source.read_text(encoding='utf-8'):
            errors.append(name + ': language mismatch')
for name in ('RIGHTS.md', 'THIRD_PARTY_NOTICES.md'):
    if (root/name).read_bytes() != (root/'website'/name).read_bytes():
        errors.append(name + ': canonical website copy differs')
if errors:
    raise SystemExit('\n'.join(errors))
print('User documentation links, localized pages and screenshots verified.')
