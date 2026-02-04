namespace FsJump.Core

open System
open System.IO
open System.Text.Json
open Microsoft.Xna.Framework
open JDeck
open FsJump.Core.Types

module Level =
  let tilesetTileDecoder(jsonElement: JsonElement) = decode {
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

  let tilesetDecoder(jsonElement: JsonElement) = decode {
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

  let tileLayerDecoder(jsonElement: JsonElement) = decode {
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

  let wellKnownObjPropsDecoder(json: JsonElement) = decode {
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

  let valueDecoder(jsonElement: JsonElement) =
    let inline toResultWith e o =
      match o with
      | Some v -> Ok v
      | None -> e()

    let inline noValueErr() =
      Error <| DecodeError.ofError(jsonElement.Clone(), "Value Not Found")

    Decode.oneOf
      [
        Optional.boolean
        >> Result.bind(toResultWith noValueErr)
        >> Result.map string
        Optional.int64
        >> Result.bind(toResultWith noValueErr)
        >> Result.map string
        Optional.float
        >> Result.bind(toResultWith noValueErr)
        >> Result.map string
      ]
      jsonElement
    |> Result.mapError(fun _ ->
      DecodeError.ofError(
        jsonElement.Clone(),
        $"Unable to convert this value to a known type"
      ))

  let mapPropertyDecoder(jsonElement: JsonElement) = decode {
    let! name = Required.Property.get ("name", Required.string) jsonElement
    and! propType = Optional.Property.get ("type", Required.string) jsonElement
    and! valueType = Optional.Property.get ("value", valueDecoder) jsonElement

    return {
      Name = name
      Type = propType |> Option.defaultValue "string"
      Value = propType
    }
  }

  let mapObjectDecoder(jsonElement: JsonElement) = decode {
    let! id = Required.Property.get ("id", Required.int) jsonElement

    and! gidValue =
      Optional.Property.get ("gid", Required.int64) jsonElement
      |> Result.map(Option.defaultValue 0L)

    let gid = int(gidValue &&& 0x1FFFFFFFL)


    let! x = Required.Property.get ("x", Required.single) jsonElement
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

  let objectGroupDecoder(jsonElement: JsonElement) = decode {
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

  let parseLayers(jsonElement: JsonElement) =
    let layers = ResizeArray<TileLayer>()
    let objectGroups = ResizeArray<ObjectGroup>()

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
            | Error e -> printfn $"Failed to decode tile layer: {e}"
          | "objectgroup" ->
            match objectGroupDecoder layerEl with
            | Ok group -> objectGroups.Add(group)
            | Error e -> printfn $"Failed to decode object group: {e}"
          | _ -> ()
        | _ -> ()
    | _ -> ()

    struct (layers.ToArray(), objectGroups.ToArray())

  let tiledMapDecoder(jsonElement: JsonElement) = decode {
    let! width = Required.Property.get ("width", Required.int) jsonElement
    and! height = Required.Property.get ("height", Required.int) jsonElement

    and! tileWidth =
      Required.Property.get ("tilewidth", Required.int) jsonElement

    and! tileHeight =
      Required.Property.get ("tileheight", Required.int) jsonElement

    and! tilesets =
      Optional.Property.array ("tilesets", tilesetDecoder) jsonElement

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

  let loadTiledMap path =
    try
      let json = File.ReadAllText(path)

      match Decoding.fromString(json, tiledMapDecoder) with
      | Ok map -> Ok map
      | Error e -> Error $"Failed to decode Tiled map: {e}"
    with ex ->
      Error $"Failed to load Tiled map from {path}: {ex.Message}"

  let gidToLocalId (gid: int) tileset =
    let cleanGid = gid &&& 0x1FFFFFFF
    cleanGid - tileset.FirstGid

  let getTileByLocalId localId tileset =
    tileset.Tiles |> Array.tryFind(fun t -> t.Id = localId)

  let tileIdToFbxPath (tileId: int) tileset =
    let localId = gidToLocalId tileId tileset

    match getTileByLocalId localId tileset with
    | Some tile ->
      let imageName = Path.GetFileNameWithoutExtension(tile.Image)
      Some $"PlatformerKit/{imageName}"
    | None -> None

  let gridToAnchorPosition
    (gridX: int)
    (gridY: int)
    (mapHeight: int)
    : Vector3 =
    let worldX = float32(gridX) * cellSize + (cellSize / 2.0f)
    let worldY = float32(mapHeight - 1 - gridY) * cellSize
    let worldZ = 0.0f
    Vector3(worldX, worldY, worldZ)

  let objectToAnchorPosition
    (objX: float32)
    (objY: float32)
    (mapHeightPixels: float32)
    : Vector3 =
    let worldX = objX + (cellSize / 2.0f)
    let worldY = mapHeightPixels - objY
    let worldZ = 0.0f
    Vector3(worldX, worldY, worldZ)

  let parseTileLayer(layer: TileLayer) =
    let tiles = ResizeArray<StaticTile>()

    for y in 0 .. layer.Height - 1 do
      for x in 0 .. layer.Width - 1 do
        let index = y * layer.Width + x

        if index < layer.Data.Length then
          let gid = layer.Data.[index]

          if gid > 0 then
            let gridPos = { X = x; Y = y; Anchor = BottomCenter }

            tiles.Add {
              GridPos = gridPos
              TileId = gid
              Scale = 1.0f
            }

    tiles.ToArray()
