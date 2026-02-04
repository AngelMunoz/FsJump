namespace FsJump.Core

open System
open System.Collections.Generic
open Microsoft.Xna.Framework
open Mibo.Elmish
open FsJump.Core.Types
open FsJump.Core.Level
open FsJump.Core.ModelBounds

module Assets =
  let private modelBoundsCache = Dictionary<string, ModelBounds>()

  let getModelBounds ctx modelPath =
    match modelBoundsCache.TryGetValue(modelPath) with
    | true, bounds -> bounds
    | false, _ ->
      let model = Mibo.Elmish.Assets.model modelPath ctx
      let bounds = extractFromModel model
      modelBoundsCache.[modelPath] <- bounds
      bounds

  let private createEntity
    (ctx, tileset, mapHeightInCells)
    worldPos
    entityType
    tileId
    layerName
    rotation
    =
    let modelPath = tileIdToFbxPath tileId tileset
    let bounds = modelPath |> Option.map (getModelBounds ctx)

    {
      Id = Guid.NewGuid()
      WorldPosition = worldPos
      Rotation = rotation
      EntityType = entityType
      ModelPath = modelPath
      Bounds = bounds
      LayerName = layerName
    }

  let entitiesFromTileLayer ctx layer tileset mapHeightInCells =
    let staticTiles = parseTileLayer layer
    let worldZ = if layer.Name = "Decorations" then -50.0f else 0.0f

    staticTiles
    |> Array.map(fun tile ->
      let worldPos =
        gridToAnchorPosition tile.GridPos.X tile.GridPos.Y mapHeightInCells worldZ

      let modelPath = tileIdToFbxPath tile.TileId tileset
      let rotation = 
        match modelPath with
        | Some path when path.Contains("slope") -> -90.0f
        | _ -> 0.0f

      createEntity
        (ctx, tileset, mapHeightInCells)
        worldPos
        (Static tile)
        tile.TileId
        layer.Name
        rotation)

  let entitiesFromObjectGroup
    ctx
    group
    tileset
    mapHeightPixels
    mapHeightInCells
    =
    let entities = ResizeArray<Entity>()
    let worldZ = if group.Name = "Decorations" then -50.0f else 0.0f

    for obj in group.Objects do
      if obj.Gid > 0 then
        let worldPos = objectToAnchorPosition obj.X obj.Y mapHeightPixels worldZ

        let entityType =
          match obj.Type.ToLowerInvariant() with
          | "spawn" -> EntityType.Player
          | "danger" -> EntityType.Danger
          | "threadmill" -> EntityType.MovingPlatform
          | "objective" -> EntityType.Goal
          | _ ->
            if group.Name = "Triggers" then
              EntityType.Player
            else
              let gridX = int(obj.X / cellSize)
              let gridY = int(obj.Y / cellSize)

              EntityType.Static {
                GridPos = {
                  X = gridX
                  Y = gridY
                  Anchor = BottomCenter
                }
                TileId = obj.Gid
                Scale = 1.0f
              }

        entities.Add(
          createEntity
            (ctx, tileset, mapHeightInCells)
            worldPos
            entityType
            obj.Gid
            group.Name
            obj.Rotation
        )

    entities.ToArray()

  let loadAllLevelEntities (ctx: GameContext) tiledMap =
    modelBoundsCache.Clear()

    let tileset =
      if tiledMap.Tilesets.Length > 0 then
        tiledMap.Tilesets.[0]
      else
        failwith "No tilesets found in Tiled map"

    let allEntities = ResizeArray<Entity>()
    let mapHeightPixels = float32(tiledMap.Height * tiledMap.TileHeight)
    let mapHeightInCells = tiledMap.Height

    for layer in tiledMap.Layers do
      let entities = entitiesFromTileLayer ctx layer tileset mapHeightInCells
      allEntities.AddRange(entities)

    for group in tiledMap.ObjectGroups do
      let entities =
        entitiesFromObjectGroup
          ctx
          group
          tileset
          mapHeightPixels
          mapHeightInCells

      allEntities.AddRange(entities)

    allEntities.ToArray()

  let findSpawnPoint (ctx: GameContext) tiledMap =
    tiledMap.ObjectGroups
    |> Array.tryFind(fun g -> g.Name = "Triggers")
    |> Option.bind(fun group ->
      group.Objects
      |> Array.tryFind(fun o -> o.Type.ToLowerInvariant() = "spawn"))
    |> Option.map(fun obj ->
      let mapHeightPixels = float32(tiledMap.Height * tiledMap.TileHeight)
      objectToAnchorPosition obj.X obj.Y mapHeightPixels 0.0f)

  /// Convert static tile entities to physics bodies for collision
  let entitiesToPhysicsBodies(entities: Entity[]) =
    entities
    |> Array.choose(fun entity ->
      // Skip decorations and triggers for regular collision
      if entity.LayerName = "Decorations" || entity.LayerName = "Triggers" then
        None
      else
        match entity.EntityType with
        | Static _ 
        | MovingPlatform ->
          // Use model bounds for accurate collision if available, otherwise default to tile size
          let size, centerOffset =
            match entity.Bounds with
            | Some b -> 
                b.Size, b.Center
            | None -> 
                Vector3(cellSize, cellSize, cellSize), Vector3(0.0f, cellSize / 2.0f, 0.0f)

          Some {
            Position = entity.WorldPosition + centerOffset
            Velocity = Vector3.Zero
            Shape = Box size
            IsStatic = true
            EntityId = Some entity.Id
          }
        | _ -> None)