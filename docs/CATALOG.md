# Catalog integration draft

The three `catalog-card-*.html` files match the existing profile site's `project`, `project-heading`, `project-icon`, `status`, `platform`, `requirements` and `actions` classes. They are snippets, not replacement pages. No online catalog has been edited.

After publication:

1. Append the corresponding card to each existing language page's `projects` container; preserve all other projects.
2. Copy `assets/SoundLeaf.ico` to the profile site's `assets/app-icons/SoundLeaf.ico`.
3. Add a SoundLeaf row to each language version of the profile README.
4. For the SoundLeaf documentation site, publish the contents of `docs/` at the site root and copy `assets/` to the site root's `assets/`. Change guide references from `../assets/` to `assets/` as part of the deployment step, and replace README links with published documentation/repository links. Current HTML files are local guides, not a completed deployment.
5. Verify all download/source, guide and issue links in all languages; configure an issue template before using `/issues/new/choose`.

The initial card is source-only. Do not change its main button to Download until a tested binary package and release actually exist. Public binary packaging remains gated by the FFmpeg distribution review.
