namespace FsJump.Core

open Microsoft.Xna.Framework
open FsJump.Core.Types

module Level =
  val loadTiledMap: path: string -> Result<TiledMap, string>

  val tileIdToFbxPath: tileId: int -> tileset: Tileset -> string option

  val gridToAnchorPosition:
    gridX: int -> gridY: int -> mapHeight: int -> worldZ: float32 -> Vector3

  val objectToAnchorPosition:
    objX: float32 -> objY: float32 -> mapHeightPixels: float32 -> worldZ: float32 -> Vector3

  val parseTileLayer: layer: TileLayer -> StaticTile[]
