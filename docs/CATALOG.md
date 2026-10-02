# Profile/catalog integration — local handoff

The profile's HTML pages and three README languages are generated from `public-release.json`. Do not insert cards manually: regeneration would erase them. Existing `catalog-card-*.html` snippets are visual references, not an integration procedure. No online catalog has been edited.

After publication:

1. Review and upload a tested preview and hashes only after explicit publication approval. Until its URLs exist and are checked, retain `status: source` and empty release fields in SoundLeaf's metadata.
2. Add the SoundLeaf metadata object to `projects` in the profile's `public-release.json` and an entry in `catalog_icons`. Copy `assets/SoundLeaf.png` from the generated Pages tree to the profile's `assets/app-icons/SoundLeaf.png`; it uses the same native leaf artwork. Preserve other projects and parallel edits.
3. Regenerate the three catalog pages and three profile README files using the profile's existing `tools/check_public_docs.py` workflow and its documented command. Review the diff and check it; do not insert HTML manually.
4. `Build-Pages.ps1` generates a separate static guide tree with root assets and corrected local links. Preview this tree before configuring Pages. After publication check Guide-de/ru/en.html, source and feedback URLs, then set the repository Website field to its published guide.
5. Once a tested release is published, update both metadata objects to `preview` with the exact tag, release URL, asset sizes/URLs, download names and checksum files. Label the primary download online per-user Setup and the secondary portable without FFmpeg. Local candidates are not published releases.

The online installer fetches the pinned GyanD FFmpeg 9.0.1 ZIP with user-visible consent and SHA-256 checks of both archive and EXE. It does not redistribute FFmpeg. The offline/bundled option remains gated on a separately reviewed matching corresponding-source/build-materials package, including the encoder's libraries. Original project rights do not replace component licenses. The existing HTML snippets are drafts; metadata status is authoritative.
