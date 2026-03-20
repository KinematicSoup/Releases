# Reactor Multiplayer for Unity DOTS

This is an early alpha build. As such, any part of the Reactor DOTS API could change in the official release.

The Reactor server is indenpendant from Unity. How you write server code when using DOTS on the client will not change. The Reactor API for Unity
uses classes and managed data which cannot be accessed in jobs or burst code.

### Getting Started
For the most part you can follow the tutorials at https://docs.kinematicsoup.com/Reactor/#gsc.tab=0 to get started, with some changes on the
client-side to work with DOTS.
- Currently the server can only load ksEntities from one subscene. The game object with the ksRoomType component and any game objects with
ksEntityComponents you want loaded by the server when it starts should be part of the same authoring subscene.
- Prefabs in Resources folders with ksEntityComponents will be baked to DOTS entity prefabs for authoring subscenes that have a game object with
a ksRoomType, so they can be spawned on clients when the server spawns them. We are planning to remove the requirement that the prefabs be in
Resources folders in a future release.
- Use ksRoomDOTS, ksEntityDOTS, and ksPlayerDOTS from the KS.Reactor.Client.Unity.DOTS namespace instead of ksRoom, ksEntity, and ksPlayer. Each
room, entity, and player has a dots entity spawned for it which can be accessed via the DOTSEntity property.
  - Each DOTS entity linked to a ksEntityDOTS has a ksEntityData struct component.
  - Each DOTS entity linked to a ksPlayerDOTS has a ksPlayerData struct component.
  - Each DOTS entity linked to a ksRoomDOTS (there will only be one unless you have multiple room connections) has a ksRoomData struct component.
  - Each DOTS entity linked to an entity, player, or room has a ksSharedRoomId shared component.
  
### Connecting from a DOTS subscene
The ksConnectSystem can be used to connect to a server. The ksConnect script will be baked to a ksConnectComponent managed component with the data
needed to make a connection. 
- If 'Connect On Start' is checked on the ksConnect script, a ksAutoConnectTag will be baked as well, which tells the ksConnectSystem
to connect automatically when the scene loads. 
- You can use `ksConnectSystem.Get().BeginConnect()` to make connections with any ksConnectComponents that don't have active connections,
  or you can use `ksConnectSystem.Get().Connect(connectComponent, roomInfo)` to connect to a specific room using the address from roomInfo.
- Once a connection is started, the ksRoomDOTS is stored in ksConnectComponent.Room. 
- `ksConnectSystem.Get().OnConnect` fires when a connection attempt completes, even if the connection failed.
- `ksConnectSystem.Get().OnDisconnect` fires when a room disconnects.
- `ksConnectSystem.Get().OnGetRooms` fires after getting the list of running rooms. If making a local or remove connection, the list will
  have one room info with the local or remote address. If you do not register a handler, it will automatically connect to the first room in
  the list. If you register a handler, you can connect to one of the rooms using
  `ksConnectSystem.Get().Connect(eventArgs.Component, eventArgs.Rooms[index])`.
- Authentication arguments can be passed to `ksConnectSystem.Get().Connect` after the room info. These arguments will be passed to the
  OnAuthenticate handler on the server.

### Unsupported features
The following Reactor features that work in regular Unity are not yet supported or have no equivalent in DOTS.
- Client entity/room/player scripts
- Room.Physics on the client. This means if you have a player controller that uses physics or physics queries and want to use input prediction,
the predictor will not be able to predict physics interations.
- ksRPCAttribute on the client. Instead register an RPC handler using `ksRoomDOTS.OnRPC[rpcId] += (ksMultiType[] args) => { }` or
 `ksEntityDOTS.OnRPC[rpcId] += (ksMultiType[] args) => { }`.
- ksAutoSpawn
- ksAnimationSync
- ksOwnershipScriptManager
- Player-spawned entities. All spawns must be done through the server currently.
- ksEntityFactory
- ksRoomType.ApplyServerTimeScale
- ksEntityDOTS are not created for permanent entities.