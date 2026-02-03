module FsJump.Core.Assets

open System
open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open FsJump.Core.Types
open FsJump.Core.Level
open FsJump.Core.ModelBounds

let private modelBoundsCache = Dictionary<string, ModelBounds>()

let getModelBounds (ctx: GameContext) (modelPath: string) : ModelBounds =
  match modelBoundsCache.TryGetValue(modelPath) with
  | true, bounds -> bounds
  | false, _ ->
    let model = Mibo.Elmish.Assets.model modelPath ctx
    let bounds = extractFromModel model
    modelBoundsCache.[modelPath] <- bounds
    bounds

let loadTexture (ctx: GameContext) (path: string) : Texture2D =
  Mibo.Elmish.Assets.texture path ctx

let tileIdToModelPath (tileId: int) (tileset: Tileset) : string option =
  tileIdToFbxPath tileId tileset

let createStaticEntity
  (ctx: GameContext)
  (tile: StaticTile)
  (tileset: Tileset)
  (mapHeight: int)
  : Entity option =
  let anchorPos = gridToAnchorPosition tile.GridPos.X tile.GridPos.Y mapHeight

  Some {
    Id = Guid.NewGuid()
    WorldPosition = anchorPos
    EntityType = Static tile
    ModelPath = tileIdToModelPath tile.TileId tileset
    Bounds = None
  }

let entitiesFromTileLayer
  (ctx: GameContext)
  (layer: TileLayer)
  (tileset: Tileset)
  (mapHeight: int)
  : Entity[] =
  let staticTiles = parseTileLayer layer tileset

  staticTiles
  |> Array.choose(fun tile -> createStaticEntity ctx tile tileset mapHeight)

let entitiesFromObjectGroup
  (ctx: GameContext)
  (group: ObjectGroup)
  (tileset: Tileset)
  (mapHeightInCells: int)
  : Entity[] =
  let entities = ResizeArray<Entity>()

  for obj in group.Objects do
    if obj.Gid > 0 then
      let anchor =
        match obj.Type.ToLowerInvariant() with
        | "spawn" -> BottomLeft
        | "danger" -> BottomLeft
        | "threadmill" -> BottomLeft
        | "objective" -> BottomLeft
        | _ -> BottomLeft

      let gridPos = objectToGridPosition obj.X obj.Y

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
            let anchor = BottomCenter

            EntityType.Static {
              GridPos = { gridPos with Anchor = anchor }
              TileId = obj.Gid
              Scale = 1.0f
            }

      match tileIdToModelPath obj.Gid tileset with
      | Some modelPath ->
        let anchor = BottomCenter

        let anchorPos =
          gridToAnchorPosition gridPos.X gridPos.Y mapHeightInCells

        let worldPos = anchorPos

        printfn
          $"Spawn object: X={obj.X}, Y={obj.Y}, gridX={gridPos.X}, gridY={gridPos.Y}, worldX={anchorPos.X}, worldY={anchorPos.Y}"

        entities.Add {
          Id = Guid.NewGuid()
          WorldPosition = worldPos
          EntityType = entityType
          ModelPath = Some modelPath
          Bounds = None
        }
      | None -> ()

  entities.ToArray()

let loadAllLevelEntities (ctx: GameContext) (tiledMap: TiledMap) : Entity[] =
  modelBoundsCache.Clear()

  let tileset =
    if tiledMap.Tilesets.Length > 0 then
      tiledMap.Tilesets.[0]
    else
      failwith "No tilesets found in Tiled map"

  let allEntities = ResizeArray<Entity>()
  let mapHeight = float32(tiledMap.Height * tiledMap.TileHeight)
  let mapHeightInCells = tiledMap.Height

  // Parse tile layers (Base, Decorations)
  for layer in tiledMap.Layers do
    let entities = entitiesFromTileLayer ctx layer tileset mapHeightInCells
    allEntities.AddRange(entities)

  // Parse object groups (Objects, Triggers)
  for group in tiledMap.ObjectGroups do
    let entities = entitiesFromObjectGroup ctx group tileset mapHeightInCells
    allEntities.AddRange(entities)

  allEntities.ToArray()

let findSpawnPoint (ctx: GameContext) (tiledMap: TiledMap) : Vector3 option =
  let spawnGroup =
    tiledMap.ObjectGroups |> Array.tryFind(fun g -> g.Name = "Triggers")

  match spawnGroup with
  | Some group ->
    let spawnObj =
      group.Objects
      |> Array.tryFind(fun o -> o.Type.ToLowerInvariant() = "spawn")

    match spawnObj with
    | Some obj ->
      let gridPos = objectToGridPosition obj.X obj.Y
      let anchorPos = gridToAnchorPosition gridPos.X gridPos.Y tiledMap.Height
      Some(anchorPos)
    | None -> None
  | None -> None
