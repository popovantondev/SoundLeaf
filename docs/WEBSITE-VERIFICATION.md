# Website verification

The forest presentation uses one static source tree (`website/`), fifteen localized pages plus a Russian default alias, local assets, and persistent light/dark selection. Screenshots show the actual application with demonstration sessions, in the matching language and theme.

## Checked on 2026-10-07

- The supplied website team's browser report contains 68 successful cases, including the fifteen localized pages in both themes at 1440 × 1080 and 390 × 844. It reports UTF-8, no missing images, no horizontal overflow and no offscreen elements. Homepage screenshots in both themes were reviewed again during integration. These are browser viewport checks, not physical-monitor or Windows DPI tests.
- `Build-Pages.ps1` validates sixteen HTML pages, UTF-8 declarations, local HTML/CSS references, an explicit publication path allowlist and unchanged canonical rights/notices before root synchronization.
- The static package was checked against its SHA-256 manifest before import. Original license texts are retained without Git line-ending conversion.
- `Test-PublicSource.ps1 -History` passes the source/private-path/selected-name/credential-pattern checks. Pattern checks cannot guarantee absence of every secret.
- `Run-PublicChecks.ps1` passes source audit, hash helper tests, application build and eight embedded icon images. It is explicitly a partial check: no audio capture or encoder tests were repeated for this website-only change.

Application sources, the installed executable, version 3.0.5, release archives and tag are not changed by this update. Offline release documentation in `docs/` remains independent of the web presentation. Hosted content must still be checked after Pages deployment; local checks do not prove public availability.
