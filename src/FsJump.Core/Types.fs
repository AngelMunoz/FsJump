module FsJump.Core.Types

open System
open Microsoft.Xna.Framework
open Mibo.Input

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
type AnchorPoint =
  | BottomCenter
  | Center
  | TopLeft
  | TopCenter
  | BottomLeft

type ModelBounds = {
  Min: Vector3
  Max: Vector3
  Center: Vector3
  Size: Vector3
  HalfSize: Vector3
}

[<Struct>]
type GridPosition = { X: int; Y: int; Anchor: AnchorPoint }

[<Struct>]
type StaticTile = {
  GridPos: GridPosition
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
  WorldPosition: Vector3
  EntityType: EntityType
  ModelPath: string option
  Bounds: ModelBounds option
  StretchX: int // 1 = normal, >1 = stretched across N cells
}

// ============================================
// Player & Physics Types
// ============================================

[<Struct>]
type PlayerAction =
  | MoveLeft
  | MoveRight
  | Jump

type PlayerState = {
  Position: Vector3
  Velocity: Vector3
  IsGrounded: bool
  GroundNormal: Vector3
}

[<Struct>]
type CollisionShape =
  | Box of size: Vector3
  | Capsule of radius: float32 * height: float32

type PhysicsBody = {
  Position: Vector3
  Velocity: Vector3
  Shape: CollisionShape
  IsStatic: bool
}

type GroundInfo = {
  IsGrounded: bool
  GroundHeight: float32
  GroundNormal: Vector3
  SlopeAngle: float32
}

[<Struct>]
type SurfaceType =
  | Flat
  | Slope of angle: float32
  | Steep

// ============================================
// Game State & Messages
// ============================================

type State = {
  Entities: Entity[]
  Player: PlayerState
  StaticBodies: PhysicsBody[]
  Tileset: Tileset
  CameraPosition: Vector3
  CameraTarget: Vector3
  Actions: ActionState<PlayerAction>
  SpawnPoint: Vector3
}

type Msg =
  | Tick of GameTime
  | LevelLoaded of TiledMap
  | InputMapped of ActionState<PlayerAction>
  | Respawn
