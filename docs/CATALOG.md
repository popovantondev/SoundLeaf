# Profile/catalog integration — local handoff

The profile's HTML pages and three README languages are generated from `public-release.json`. Do not insert cards manually: regeneration would erase them. Existing `catalog-card-*.html` snippets are visual references, not an integration procedure. No online catalog has been edited.

After publication:

1. Review and upload a tested preview and hashes only after explicit publication approval. Until its URLs exist and are checked, retain `status: source` and empty release fields in SoundLeaf's metadata.
2. Add the SoundLeaf metadata object to `projects` in the profile's `public-release.json` and an entry in `catalog_icons`. Copy `assets/SoundLeaf.png` from the generated Pages tree to the profile's `assets/app-icons/SoundLeaf.png`; it uses the same native leaf artwork. Preserve other projects and parallel edits.
3. Regenerate the three catalog pages and three profile README files using the profile's existing `tools/check_public_docs.py` workflow and its documented command. Review the diff and check it; do not insert HTML manually.
4. `Build-Pages.ps1` generates a separate static guide tree with root assets and corrected local links. Preview this tree before configuring Pages. After publication check Guide-de/ru/en.html, source and feedback URLs, then set the repository Website field to its published guide.
5. Once a tested release is published, update both metadata objects to `preview`, version `3.0.5`, tag `v3.0.5`, with exact release URL, asset sizes/hashes/URLs, download names and `SHA256SUMS.txt`. Primary: offline per-user Setup. Secondary: complete portable ZIP. Both include FFmpeg 9.0.2 and its matching source kit. Local candidates are not published releases.

The 3.0.5 offline installer verifies embedded runtime, encoder and source-kit hashes before publishing the installation. Original project rights do not replace component licenses. Only the encoder build script has the approved LGPL exception. Existing HTML card snippets are historical drafts; generated metadata is authoritative.
