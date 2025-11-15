# Changelog

## [2.0.4] - 2025-11-14

### Added
- Added support for Unity 6.0 - 6.2.
- Added a'Sync Prefabs' config option to the Scene Fusion settings that can be configured by the session creator to one of the following before starting a session:
 - Off: prefabs do not sync and cannot be edited during a session.
 - Create Only: new prefabs created during a session will be synced. Prefabs cannot be edited during a session. This is the behaviour for older Scene Fusion versions.
 - Full (Experimental): all referenced prefabs and prefabs modified during the session are synced. Prefab renames and deletion are not synced.
- Added a 'Sync Materials' toggle to the Scene Fusion settings that can be used to disable material syncing.

### Fixed
- Scene Fusion and Reactor packages can now be installed in the same project without build errors.
- Fixed some rare bugs that could cause desyncs when multiple users try to make conflicting edits at the same time.
- Fixed a bug when a prefab had two of the same removed component type in a row and a user restored the second component which caused the first component to be restored for other users instead of the second.
- Fixed a bug when two users restore the same removed prefab instance component at the same time causing a duplicate component to be created.
- Fixed a bug when a prefab instance had a prefab component and an added override component of the same type with the order swapped for two users before starting the session causing one user's prefab component to sync data to the other user's override component, and vice versa.

### Changed
- Scene Fusion is now installed via git url instead of a tarball.

### Known Issues
- Asset deletion/renaming of synced assets does not sync.
- When full prefab syncing is enabled, prefab changes will not be applied to prefab instances in synced scenes that no one has loaded.
- If you create and attach a new script through the add component menu, it will be detached when you reconnect after recompiling.
- If a property instance property overrides the prefab value but has the same value as the prefab and another user joins without having that property override, theirs will not override the prefab value.
