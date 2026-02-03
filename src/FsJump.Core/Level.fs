module FsJump.Core.Level

open System
open System.IO
open System.Text.Json
open Microsoft.Xna.Framework
open JDeck
open FsJump.Core.Types

let tilesetTileDecoder: Decoder<TilesetTile> =
  fun jsonElement -> decode {
    let! id = Required.Property.get ("id", Required.int) jsonElement
    and! image = Required.Property.get ("image", Required.string) jsonElement

    and! imageHeight =
      Required.Property.get ("imageheight", Required.int) jsonElement

    and! imageWidth =
      Required.Property.get ("imagewidth", Required.int) jsonElement

    return {
      Id = id
      Image = image
      ImageHeight = imageHeight
      ImageWidth = imageWidth
    }
  }

let tilesetDecoder: Decoder<Tileset> =
  fun jsonElement -> decode {
    let! firstGid = Required.Property.get ("firstgid", Required.int) jsonElement
    and! name = Required.Property.get ("name", Required.string) jsonElement

    and! tileCount =
      Required.Property.get ("tilecount", Required.int) jsonElement

    and! tileHeight =
      Required.Property.get ("tileheight", Required.int) jsonElement

    and! tileWidth =
      Required.Property.get ("tilewidth", Required.int) jsonElement

    and! tiles =
      Optional.Property.array ("tiles", tilesetTileDecoder) jsonElement

    return {
      FirstGid = firstGid
      Name = name
      TileCount = tileCount
      TileHeight = tileHeight
      TileWidth = tileWidth
      Tiles = tiles |> Option.defaultValue Array.empty
    }
  }

let tileLayerDecoder: Decoder<TileLayer> =
  fun jsonElement -> decode {
    let! height = Required.Property.get ("height", Required.int) jsonElement
    and! width = Required.Property.get ("width", Required.int) jsonElement
    and! name = Required.Property.get ("name", Required.string) jsonElement
    and! id = Required.Property.get ("id", Required.int) jsonElement
    and! data = Optional.Property.array ("data", Required.int) jsonElement

    return {
      Data = data |> Option.defaultValue Array.empty
      Height = height
      Width = width
      Name = name
      Id = id
    }
  }

let wellKnownObjPropsDecoder: Decoder<WellKnownObjProps> =
  fun json -> decode {
    let! name = Required.Property.get ("name", Required.string) json

    match name with
    | "IsRotating" -> return WellKnownObjProps.Rotating
    | value ->
      return!
        DecodeError.ofError(
          json.Clone(),
          $"Unknown Property '{value}' for '{nameof WellKnownObjProps}'"
        )
        |> Error
  }

let mapPropertyDecoder: Decoder<MapProperty> =
  fun jsonElement -> decode {
    let! name = Required.Property.get ("name", Required.string) jsonElement
    and! propType = Optional.Property.get ("type", Required.string) jsonElement
    and! value = Optional.Property.get ("value", Required.string) jsonElement

    return {
      Name = name
      Type = propType |> Option.defaultValue "string"
      Value = value
    }
  }

let mapObjectDecoder: Decoder<MapObject> =
  fun jsonElement -> decode {
    let! id = Required.Property.get ("id", Required.int) jsonElement

    and! gid =
      Optional.Property.get ("gid", Required.int) jsonElement
      |> Result.map(Option.defaultValue 0)

    and! x = Required.Property.get ("x", Required.single) jsonElement
    and! y = Required.Property.get ("y", Required.single) jsonElement

    and! width =
      Optional.Property.get ("width", Required.single) jsonElement
      |> Result.map(Option.defaultValue 64f)

    and! height =
      Optional.Property.get ("height", Required.single) jsonElement
      |> Result.map(Option.defaultValue 64f)

    and! name =
      Optional.Property.get ("name", Required.string) jsonElement
      |> Result.map(Option.defaultValue String.Empty)

    and! objType =
      Optional.Property.get ("type", Required.string) jsonElement
      |> Result.map(Option.defaultValue String.Empty)

    and! properties =
      Optional.Property.array ("properties", mapPropertyDecoder) jsonElement
      |> Result.map(Option.defaultValue Array.empty)

    and! customProperties =
      Optional.Property.array
        ("properties", wellKnownObjPropsDecoder)
        jsonElement
      |> Result.map(Option.defaultValue Array.empty)

    and! rotation =
      Optional.Property.get ("rotation", Required.single) jsonElement
      |> Result.map(Option.defaultValue 0f)

    return {
      Id = id
      Gid = gid
      X = x
      Y = y
      Width = width
      Height = height
      Name = name
      Type = objType
      Properties = properties
      CustomProperties = customProperties
      Rotation = rotation
    }
  }

let objectGroupDecoder: Decoder<ObjectGroup> =
  fun jsonElement -> decode {
    let! name = Required.Property.get ("name", Required.string) jsonElement
    and! id = Required.Property.get ("id", Required.int) jsonElement

    and! objects =
      Optional.Property.array ("objects", mapObjectDecoder) jsonElement

    return {
      Name = name
      Id = id
      Objects = objects |> Option.defaultValue [||]
    }
  }

// Smart layer parser that filters by type
let parseLayers
  (jsonElement: JsonElement)
  : struct (TileLayer[] * ObjectGroup[]) =
  let layers = ResizeArray<TileLayer>()
  let objectGroups = ResizeArray<ObjectGroup>()

  // Get layers array directly from JSON
  match jsonElement.TryGetProperty("layers") with
  | true, layersProp when layersProp.ValueKind = JsonValueKind.Array ->
    for layerEl in layersProp.EnumerateArray() do
      match layerEl.TryGetProperty("type") with
      | true, typeProp when typeProp.ValueKind = JsonValueKind.String ->
        let layerType = typeProp.GetString()

        match layerType with
        | "tilelayer" ->
          match tileLayerDecoder layerEl with
          | Ok layer -> layers.Add(layer)
          | Error _ -> ()
        | "objectgroup" ->
          match objectGroupDecoder layerEl with
          | Ok group -> objectGroups.Add(group)
          | Error _ -> ()
        | _ -> ()
      | _ -> ()
  | _ -> ()

  struct (layers.ToArray(), objectGroups.ToArray())

let tiledMapDecoder: Decoder<TiledMap> =
  fun jsonElement -> decode {
    let! width = Required.Property.get ("width", Required.int) jsonElement
    and! height = Required.Property.get ("height", Required.int) jsonElement

    and! tileWidth =
      Required.Property.get ("tilewidth", Required.int) jsonElement

    and! tileHeight =
      Required.Property.get ("tileheight", Required.int) jsonElement

    and! tilesets =
      Optional.Property.array ("tilesets", tilesetDecoder) jsonElement

    // Parse layers with type filtering
    let struct (layers, objectGroups) = parseLayers jsonElement

    return {
      Width = width
      Height = height
      TileWidth = tileWidth
      TileHeight = tileHeight
      Layers = layers
      ObjectGroups = objectGroups
      Tilesets = tilesets |> Option.defaultValue [||]
    }
  }

let loadTiledMap(path: string) : Result<TiledMap, string> =
  try
    let json = File.ReadAllText(path)

    match Decoding.fromString(json, tiledMapDecoder) with
    | Ok map -> Ok map
    | Error e -> Error $"Failed to decode Tiled map: {e}"
  with ex ->
    Error $"Failed to load Tiled map from {path}: {ex.Message}"

let tileIdToGid (tileId: int) (tileset: Tileset) : int =
  tileset.FirstGid + tileId

let gidToLocalId (gid: int) (tileset: Tileset) : int = gid - tileset.FirstGid

let getTileByLocalId (localId: int) (tileset: Tileset) : TilesetTile option =
  tileset.Tiles |> Array.tryFind(fun t -> t.Id = localId)

let tileIdToFbxPath (tileId: int) (tileset: Tileset) : string option =
  let localId = gidToLocalId tileId tileset

  match getTileByLocalId localId tileset with
  | Some tile ->
    let imageName = Path.GetFileNameWithoutExtension(tile.Image)
    Some $"PlatformerKit/{imageName}"
  | None -> None

let gridToAnchorPosition (gridX: int) (gridY: int) (mapHeight: int) : Vector3 =
  let worldX = float32(gridX) * cellSize + (cellSize / 2.0f)
  let worldY = float32(mapHeight - gridY) * cellSize
  let worldZ = 0.0f
  Vector3(worldX, worldY, worldZ)

let objectToGridPosition (objX: float32) (objY: float32) (mapHeight: float32) : GridPosition =
  let gridX = int(objX / cellSize)
  let gridY = int((mapHeight - objY) / cellSize)
  {
    X = gridX
    Y = gridY
    Anchor = AnchorPoint.BottomCenter
  }

let parseTileLayer (layer: TileLayer) (tileset: Tileset) : StaticTile[] =
  let tiles = ResizeArray<StaticTile>()

  for y in 0 .. layer.Height - 1 do
    for x in 0 .. layer.Width - 1 do
      let index = y * layer.Width + x

      if index < layer.Data.Length then
        let gid = layer.Data.[index]

        if gid > 0 then
          let gridPos = {
            X = x
            Y = y
            Anchor = BottomCenter
          }

          tiles.Add {
            GridPos = gridPos
            TileId = gid
            Scale = 1.0f
          }

  tiles.ToArray()
