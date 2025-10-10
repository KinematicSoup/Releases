# Changelog

## [1.1.0-1] - 2025-10-10

### Added
- Compression is enabled for the local development server.
- The local development server is capped to 32 connected players.

### Fixed
- Fixed a bug where a property owner changing an array property element and reassigning the property would not sync the change to the server.
- Manually entering a server override path now works correctly.
- Fixed a bug that caused running servers of the bound image to appear under "Other Servers" instead of "Running Servers" for several seconds after exiting play mode.
- Setting ksAnimationSync.Animator for the first time in play mode now works correctly.
- Linear predictor improvements to reduce jitter when connecting that was more prominent with player-owned entities.

### Changed
- The ksRandom default constructor no longer initializes the seed to the timestamp so constructing multiple instances at the same time will all use a different seed.

### Removed
- Removed public m_object field from ksMultiType.

## [1.1.0-0] - 2025-08-14

### Added
- Added support for Unity 6 and 6.1
- Added support for the Unity Multiplayer Play Mode package
- Added player ownership and permission rules to server entities which are synced to clients, allowing clients to control an entity's transform or set properties.
- Added a ksEntityComponent toggle to destroy owned entities when the owner disconnects. It is enabled by default.
- Added client-side API calls to spawn entities on the server
- Added ksSpawnParameters with options to spawn entities with properties, owners, and player controllers.
- Added a ksAutoSpawn script which requests the server to spawn and entity when prefabs are instantiated on a client
- Added a ksOwnershipScriptManager script to enable/disable/remove components and child gameobjects when an entity's ownership state changes.
- Added a ksAnimationSync script to sync gameobject animation states as properties on owned entities.
- Added a ksRoomType client sync rate property which controls how often client updates are sent to the server.
- Added client time and frame number on ksIServerPlayers type.
- Added `Room.OnUpdate[X].Parallel` bool that makes all update handlers in group X run in parallel. Note that spawning/deleting entities or updating the same entity from multiple update handlers is not thread-safe.
- Added `Room.OnUpdate[X].SyncFrameOnly` bool that makes the handlers only get called on sync frames.
- Added a ksDestroyReason enum that gives the reason an entity was destroyed.
- Added a ksEventMap.HasHandlers method that can be used to check if property handlers are registered.
- New ksMultiType.Types were added: Short, UShort, ULong, Double, Char, Vector2, Vector3, Color, Quaternion.
- Added a new [client authoritative tutorial](https://docs.kinematicsoup.com/Reactor/Tutorials/Tutorial10-ClientOwnership).

### Fixed
- Fixed server errors caused by client toggling idle mode.
- Fixed repeated log errors when a rigidbody is removed from an entity during playmode.
- Fixed a bug where Reactor client entity scripts with an OnDestroy method would prevent proper entity cleanup.
- Fixed a bug where Time.UnscaledDelta was scaled on the client after sending input to the server.
- Fixed an exception on the client caused by having an abstract player controller when Reactor initializes.
- Setting the position precision override on a ksEntityComponent now works properly.
- Fixed a bug that caused ksSerializableDictionary and ksSerializableHashSet data loss when instantiating from a prefab.
- Rigid Body Existence field is editable in the Reactor Data section of the Rigidbody inspector.
- Fixed a null reference exception when using a linear predictor for room or player properties.
- Fixed a bug where predicted properties would not be interpolated after the entity spawns or teleports until it moved again.
- Disabling prediction will now set predicted properties to their server values instead of retaining their last predicted value.
- Fixed a bug where entities spawned in collections where not scaled correctly.
- Fixed a decoder error resulting in a disconnect that occurred rarely when an entity changed sync groups, then changed back on the next sync frame.
- Fixed a bug with the local server on Windows 11 that caused it to sometimes oversleep, which would reshlt in low frame rates if recover update time was disabled.
- Fixed a bug where scene game objects with entity components were not be reused if the game object was moved to a different scene before the entity was destroyed.
- Change events on the client are now deferred until after all updates are applied so if you need to access other properties or entity data from a change event, you'll always get the latest values.
- Improved prediction with the linear predictor when there is high packet loss.
- Calls into ksLog are hidden from the console stack trace when you enable Strip Logging Callstack in the console context menu.

### Changed
- ksMultiType is now a struct instead of a class. This means `Properties[X].Int = value;` will no longer work.
- ksMultiType can no longer be serialized by Unity scripts. Use ksSerializableMultiType instead.
- ksMultiType.Types.UNDEFINED is obsolete. Use ksMultiType.Types.NULL instead.
- ksMultiType.Types.OBJECT is obsolete. Use ksMultiType.Types.BYTE_ARRAY instead.
- ksMultiType now throws InvalidCastExceptions if the type cannot be cast to the requested type. Use the new `ksMultiType.AsX()` methods to convert to type X without exceptions.
- ksMultiType `==` behaviour has changed. When the types are different, if they are both numeric it will do a numeric comparison, otherwise it returns false. This means `Properties[X] == 0` returns false for a null property where it used to return true. Do `Properties[X].AsInt() == 0` if you want the old behaviour.
- ksMultiType.Data, TypeSize, Clone, Create, and constructors with parameters are obsolete.
- ksMultiType.ArrayLength is obsolete. Use Count instead.
- ksMultiType.KSVector3, KSVector2, KSQuaternion, KSColor, and their array accessors are obsolete. Use the equivalent accessor without `KS`, eg. ksMultiType.Vector2.
- To get the Unity vectors/quaternion/color, use ksMultiType.UnityVector3 or equivalent instead of ksMultiType.Vector3.
- Improved ksMultiType performance.
- Enity.OnDestroy handler parameters on the client were changed to `OnDestroy(ksDestroyReason)`
- ksBitIStream and ksBitOStream are obsolete. Use the new ksStreamBuffer methods and ksBitReader/Writer instead.
- ksEventSet was deprecated. Use ksEvent instead.
- Room.ControlledEntities on the client and Player.ControlledEntities on the server were deprecated. Use OwnedEntities instead and check for a player controller.
- Room.DestroyControlledEntities was deprecated. Use DestroyOwnedEntities instead.
- Room.Info can be edited on the client after constructing the room, as long as you aren't connected or connecting.
- Renamed ksExistanceModes to ksExistenceModes.
- Renamed ksColliderData.Existance and ksEntityComponent.RigidBodyExistance to ksColliderData.Existence and ksEntityComponent.RigidBodyExistence.
- Room.GetTimeAdjuster, Room.AddTimeAdjuster, and Room.TimeAdjusters were deprecated. Instead rooms can have only a single time adjuster you can access from Room.Time.Adjuster.
- Moved ksTimeRestrainer into ksTimeKeeper.Restrainer
- Entity.Id was changed from a uint to a ksEntityId struct with implicit convertions to and from uint.
- ksEntityScript.OnDestroy was changed from private to protected so developer scripts that override it can call the base method.
- Entity game objects DestroyWithServer set to false will not be destroyed when you disconnect and call CleanUp.
- ksMath.ClosestPowerOfTwo and NextPowerOfTwo will throw OverflowExceptions if the correct value would cause an overflow instead of returning an incorrect value.
- The servers window can only start servers for the bound image, and shows only running servers for the bound image under the Running Server section. Running servers for other images are shown under an Other Servers section. 

### Removed
- Removed internal Reactor components from the Add Components menu.
- Removed warning from the ksEntityComponent inspector that the server cannot spawn the entity prefab from non-Resources prefabs in asset bundles.
- Removed deprecated 'Any' location from the launch server window.
- Removed ksPlayerController and replaced it with a static class ksPlayerControllerCache in KS.Reactor.
- Removed deprecated GetPredictionBehaviour and SetPredictionBehaviour from client entities, rooms, and players.
- Removed deprecated ksClientInputPredictor.Create.
- Removed deprecated ksClientTime.ServerTimeScale.
- Removed deprecated GetPropertyChangeTolerance, SetPropertyChangeTolerance, and SetPropertyResetCallback from ksConvergingInputPredictor.ConfigData.
- Removed deprecated ksInputMarshaller.
- Removed deprecated GetPropertyCorrectionRate and SetPropertyCorrectionRate from ksLinearPredictor.ConfigData.
- Removed deprecated ksPredictorUtils.SweepAndSlide overload.
- Removed deprecated ColliderData from ksEntityComponent.
- Removed deprecated Physics query functions.