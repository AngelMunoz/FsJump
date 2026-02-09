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
    =
    let modelPath = tileIdToFbxPath tileId tileset

    {
      Id = Guid.NewGuid()
      WorldPosition = worldPos
      EntityType = entityType
      ModelPath = modelPath
      Bounds = None
      StretchX = 1
    }

  let entitiesFromTileLayer ctx layer tileset mapHeightInCells =
    let staticTiles = parseTileLayer layer

    staticTiles
    |> Array.map(fun tile ->
      let worldPos =
        gridToAnchorPosition tile.GridPos.X tile.GridPos.Y mapHeightInCells

      createEntity
        (ctx, tileset, mapHeightInCells)
        worldPos
        (Static tile)
        tile.TileId)

  let entitiesFromObjectGroup
    ctx
    group
    tileset
    mapHeightPixels
    mapHeightInCells
    =
    let entities = ResizeArray<Entity>()

    for obj in group.Objects do
      if obj.Gid > 0 then
        let worldPos = objectToAnchorPosition obj.X obj.Y mapHeightPixels

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
      objectToAnchorPosition obj.X obj.Y mapHeightPixels)
