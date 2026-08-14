# Release Media Filter — follow-up work

From the source review (excluding `ext/`). Highest risk is data loss: the plugin deletes Lidarr `AlbumRelease` and `Track` rows.

## Critical

- [x] **Fail closed without an enabled Connect notification.** Do not filter unless an enabled `ReleaseMediaFilterNotification` exists. Treat missing settings as off, not as Vinyl/Cassette delete.
- [ ] **Honor `Enable`.** `INotificationFactory.All()` currently matches on settings type only; disabled connections can still drive deletes.
- [x] **Do not apply constructor defaults on refresh/scan.** `Resolve()` must not return `new ReleaseMediaFilterSettings().ToFilterOptions()` when no definition is found.

## High

- [ ] **Fail closed on settings load errors.** Exceptions from the notification factory must not fall back to destructive defaults.
- [ ] **Never delete a release that still has files** unless using Lidarr’s file-aware delete path. When skip is off, do not half-delete tracks then `DeleteMany` the release.
- [ ] **Safer default when no allowed release remains.** Default `DeleteFiltered` can wipe every `AlbumRelease` for an album (including during incomplete refresh). Prefer keep-last-resort as default, or refuse to delete the last remaining copies.
- [ ] **Empty media types must not silently become Vinyl, Cassette.** Validate the raw string; empty should be invalid or disable filtering. `ParseMediaTypes("")` currently always succeeds.

## Medium

- [ ] **Tokenize all format matching.** Drop substring `Contains` for long types. Require whole-token match (including `CD` vs `CD-R` / mixed `Vinyl + CD`).
- [ ] **Keep-last-resort should keep one release**, not every filtered pressing (`PickPreferred(filtered)` then delete the rest, still respecting skip-with-files).
- [ ] **Single settings source.** Refresh, scan, and import must use the same enabled definition. If several are enabled, fail closed or merge explicitly.
- [ ] **Wire scan interval from Connect.** Changing the Connect number should update the job without relying on Metadata `ProviderUpdatedEvent`.
- [ ] **Avoid a second settings UI under Metadata** that the resolver does not read (`ScheduledTaskBase` as `MetadataBase`).
- [ ] **Serialize deletes.** Track delete → release delete → re-query → `SetMonitored` is not transactional. Serialize scan vs refresh for the same album.
- [ ] **Do not backdate `LastExecution`** so the library scan is eligible immediately on first registration.
- [ ] **Import handler:** do not filter with constructor defaults if `Definition.Settings` is missing.

## Low

- [ ] Warn or confirm when switching to whitelist while media types are still Vinyl/Cassette (would delete CD/digital).
- [ ] Add a match timeout on `Regex.IsMatch` in `MediaTypeMatcher`.
- [ ] Make Connect **Test** actually validate settings (or at least that filtering is enabled).
- [ ] Decide whether unmonitored artists should be included in the backfill scan.
- [ ] Narrow `catch (Exception)` so settings failures cannot become deletes and partial deletes are visible.
- [ ] CI: grant `contents: write` only on `master` / tags, not on `pull_request`.
- [ ] `setup-lidarr.ps1`: confine `ExtPath` under the repo; sanitize `Branch`.
- [ ] `build.ps1`: restrict `$LidarrVersion` to version digits before writing XML.
- [ ] Turn on analyzers / treat warnings as errors for the plugin project.
- [ ] Make `FilterOptions.MediaTypes` immutable after construction.

## Already in good shape (do not regress)

- Default skip-releases-with-files is implemented and tested.
- Empty/unknown formats are kept.
- Blacklist “any medium” vs whitelist “all mediums” for **separate** medium format strings.
- Constructor null checks; refresh failures do not throw into Lidarr’s event pipeline.
- `CleanupOrphanedTasks` is scoped to command types this service registered.
