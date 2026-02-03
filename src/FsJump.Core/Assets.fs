module FsJump.Core.Assets

open System
open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open FsJump.Core.Types
open FsJump.Core.Level


let loadModel (ctx: GameContext) (path: string) = Assets.model path ctx

let loadTexture (ctx: GameContext) (path: string) : Texture2D =
  Mibo.Elmish.Assets.texture path ctx

let tileIdToModelPath (tileId: int) (tileset: Tileset) : string option =
  tileIdToFbxPath tileId tileset

let createStaticEntity (tile: StaticTile) (tileset: Tileset) : Entity option =
  match tileIdToModelPath tile.TileId tileset with
  | Some modelPath ->
    Some {
      Id = Guid.NewGuid()
      Position = tile.Position
      EntityType = Static tile
      ModelPath = Some modelPath
    }
  | None -> None

let entitiesFromTileLayer (layer: TileLayer) (tileset: Tileset) : Entity[] =
  let staticTiles = parseTileLayer layer tileset

  staticTiles |> Array.choose(fun tile -> createStaticEntity tile tileset)

let entitiesFromObjectGroup (group: ObjectGroup) (tileset: Tileset) (mapHeight: float32) : Entity[] =
  let entities = ResizeArray<Entity>()

  for obj in group.Objects do
    if obj.Gid > 0 then
      let worldX = float32 obj.X + (obj.Width / 2.0f)
      let worldY = mapHeight - float32 obj.Y + (obj.Height / 2.0f)
      let pos = Vector3(worldX, worldY, 0.0f)

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
            EntityType.Static {
              Position = pos
              TileId = obj.Gid
              Scale = 1.0f
            }

      match tileIdToModelPath obj.Gid tileset with
      | Some modelPath ->
        entities.Add {
          Id = Guid.NewGuid()
          Position = pos
          EntityType = entityType
          ModelPath = Some modelPath
        }
      | None -> ()

  entities.ToArray()

let loadAllLevelEntities(tiledMap: TiledMap) : Entity[] =
  let tileset =
    if tiledMap.Tilesets.Length > 0 then
      tiledMap.Tilesets.[0]
    else
      failwith "No tilesets found in Tiled map"

  let allEntities = ResizeArray<Entity>()
  let mapHeight = float32 (tiledMap.Height * tiledMap.TileHeight)

  // Parse tile layers (Base, Decorations)
  for layer in tiledMap.Layers do
    let entities = entitiesFromTileLayer layer tileset
    allEntities.AddRange(entities)

  // Parse object groups (Objects, Triggers)
  for group in tiledMap.ObjectGroups do
    let entities = entitiesFromObjectGroup group tileset mapHeight
    allEntities.AddRange(entities)

  allEntities.ToArray()

let findSpawnPoint(tiledMap: TiledMap) : Vector3 option =
  let spawnGroup =
    tiledMap.ObjectGroups |> Array.tryFind(fun g -> g.Name = "Triggers")

  match spawnGroup with
  | Some group ->
    let spawnObj =
      group.Objects
      |> Array.tryFind(fun o -> o.Type.ToLowerInvariant() = "spawn")

    match spawnObj with
    | Some obj ->
      let mapHeight = float32 (tiledMap.Height * tiledMap.TileHeight)
      let worldX = float32 obj.X + (obj.Width / 2.0f)
      let worldY = mapHeight - float32 obj.Y + (obj.Height / 2.0f)
      Some(Vector3(worldX, worldY, 0.0f))
    | None -> None
  | None -> None
