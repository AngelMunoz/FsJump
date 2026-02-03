module FsJump.Core.Types

open System
open Microsoft.Xna.Framework

let cellSize = 64.0f

[<Struct>]
type WellKnownObjProps = | Rotating

[<Struct>]
type TilesetTile = {
  Id: int
  Image: string
  ImageHeight: int
  ImageWidth: int
}

[<Struct>]
type Tileset = {
  FirstGid: int
  Name: string
  TileCount: int
  TileHeight: int
  TileWidth: int
  Tiles: TilesetTile[]
}

[<Struct>]
type TileLayer = {
  Data: int[]
  Height: int
  Width: int
  Name: string
  Id: int
}

[<Struct>]
type MapProperty = {
  Name: string
  Type: string
  Value: string option
}

[<Struct>]
type MapObject = {
  Id: int
  Gid: int
  X: float32
  Y: float32
  Width: float32
  Height: float32
  Name: string
  Type: string
  Properties: MapProperty[]
  CustomProperties: WellKnownObjProps[]
  Rotation: float32
}

[<Struct>]
type ObjectGroup = {
  Name: string
  Id: int
  Objects: MapObject[]
}

[<Struct>]
type TiledMap = {
  Width: int
  Height: int
  TileWidth: int
  TileHeight: int
  Layers: TileLayer[]
  ObjectGroups: ObjectGroup[]
  Tilesets: Tileset[]
}

[<Struct>]
type StaticTile = {
  Position: Vector3
  TileId: int
  Scale: float32
}

[<Struct>]
type EntityType =
  | Static of StaticTile
  | Player
  | Danger
  | MovingPlatform
  | Goal
  | Decoration

[<Struct>]
type Entity = {
  Id: Guid
  Position: Vector3
  EntityType: EntityType
  ModelPath: string option
}

type State = {
  Entities: Entity[]
  Tileset: Tileset
  CameraPosition: Vector3
  CameraTarget: Vector3
}

type Msg =
  | Tick of GameTime
  | LevelLoaded of TiledMap
