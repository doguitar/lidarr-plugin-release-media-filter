# Release Media Filter

A [Lidarr](https://lidarr.audio) nightly plugin that removes MusicBrainz album releases of unwanted media types (vinyl and cassette by default) so import matching cannot choose them.

Lidarr stores every pressing of a release group. Import scoring looks at **all stored releases**, not just the monitored one. Unmonitoring vinyl is not enough. This plugin **deletes** filtered `AlbumRelease` rows after metadata refresh.

## Requirements

- Lidarr **nightly** (plugin support is not on stable)
- Build against real Lidarr source via `.\setup-lidarr.ps1`

## Installation

1. Switch Lidarr to the `nightly` branch.
2. Open **System → Plugins**.
3. Install from the GitHub repository URL for this plugin (or drop the `Lidarr.Plugin.ReleaseMediaFilter.net8.0.zip` release asset).
4. Restart Lidarr if prompted.

## Configuration

1. Go to **Settings → Connect**.
2. Click **+** and choose **Release Media Filter**.
3. Enable at least one Connect event (for example **On Album Import**) so the connection is enabled. Filtering does not run until then.
4. Save. Defaults blacklist `Vinyl` and `Cassette`.

| Setting | Default | Description |
|---------|---------|-------------|
| Filter mode | Blacklist | Blacklist deletes matching media. Whitelist keeps only matching media. |
| Media types | `Vinyl, Cassette` | Comma-separated MusicBrainz medium formats. |
| When no allowed release remains | Keep as last resort | Keep the last filtered release, or delete anyway. |
| Skip releases that already have files | On | Do not delete a release that already has imported files. |
| Search after file cleanup | Off | After recycle-bin file deletes, search for the remaining monitored release. |
| Sort 1–3 | None | After filtering, choose the monitored release by these keys in order. |
| Sort direction | See Connect | Ascending or descending. Regex fields: descending puts matches first. |
| Sort regex | empty | Used when the sort key is country regex or medium regex. |
| Sort preview | sample list | Ranking of typical pressings. The first row is the copy that would be monitored. |
| Scan interval (minutes) | 1440 | Backfill scan interval. Minimum 60. |

Matching is case-insensitive. `Vinyl` matches `2xVinyl`, `12" Vinyl`, and mixed `Vinyl + CD`. A blacklist drop happens if **any** medium matches. A whitelist keeps a release only if **every** medium matches. Empty/unknown formats are kept.

To prefer a CD with the fewest tracks, then a country (for example US/GB/UK):

1. Sort 1: **Medium regex**, descending, `^CD$`
2. Sort 2: **Track count**, ascending
3. Sort 3: **Country regex**, descending, `US|GB|UK`

With all sort keys set to **None**, the plugin keeps the previous default: Digital Media, then CD, then more tracks.

## How it works

Lidarr has no plugin hook before SkyHook writes to the database. After each artist/album refresh, Lidarr publishes `AlbumUpdatedEvent` synchronously. Release Media Filter then:

1. Loads the album’s releases
2. Deletes filtered releases (and their tracks)
3. Monitors the **first** remaining release in that sort order (or Digital Media / CD when no sort keys are set)
4. Leaves the album with zero releases when nothing allowed remains (automatic import of that album should fail closed)

The next metadata refresh will **re-insert** those MusicBrainz releases. That is expected. The plugin deletes them again on every refresh. A scheduled backfill scan covers the existing library (it may appear under **Settings → Metadata** as a scheduler hook, without a second copy of the Connect settings).

A failed import does **not** automatically search another indexer unless **Search after file cleanup** is on and imported files were deleted.

### Caveats

- By default, releases that already have imported files are skipped so the library is not broken.
- If skip-with-files is off, imported files go through Lidarr’s recycle bin before the album release is removed. A failed file delete leaves that release in place.
- Search after cleanup only runs when at least one file was deleted and an allowed remaining release exists.
- If Lidarr is allowed to add new artists during import and there are zero DB candidates, identification can still pull vinyl from SkyHook (`GetRemoteCandidates`). That mainly affects unmatched/new-artist imports, not a normal grab for an already-tracked album.

## Building

```powershell
.\setup-lidarr.ps1
.\build.ps1
```

`build.ps1` pins Lidarr’s `AssemblyVersion` to match a nightly build (default `3.1.3.4987`). That must match **System → Status** in Lidarr or the plugin will fail to load. Override it with:

```powershell
.\build.ps1 -LidarrVersion 3.1.3.4987
```

Zip for install (DLL and PDB at the zip root):

```powershell
Compress-Archive -Path _temp\bin\Release\ReleaseMediaFilter\Lidarr.Plugin.ReleaseMediaFilter.dll, _temp\bin\Release\ReleaseMediaFilter\Lidarr.Plugin.ReleaseMediaFilter.pdb -DestinationPath Lidarr.Plugin.ReleaseMediaFilter.net8.0.zip -Force
```

The plugin assembly is `Lidarr.Plugin.ReleaseMediaFilter.dll` under `_temp/bin/Release/ReleaseMediaFilter`. Lidarr installs from a GitHub release zip named `Lidarr.Plugin.ReleaseMediaFilter.net8.0.zip`.

## Follow-up (not in this version)

- **SkyHook proxy:** strip filtered releases before insert and from remote import candidates so vinyl never reappears in the release picker.
- Optional unmonitor/delete of the parent album when zero releases remain.

## License

MIT
