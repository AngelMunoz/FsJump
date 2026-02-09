namespace FsJump.Core

open System
open Microsoft.Xna.Framework
open Mibo.Layout3D
open FsJump.Core.Types

// ============================================
// Core Domain Types
// ============================================

/// Full bounds and offset metadata for a model
type ModelMetadata = { Bounds: ModelBounds; Offset: Vector3 }

/// Content of a single cell in the 3D grid.
[<Struct>]
type LayoutCell =
  | Terrain of modelPath: string
  | Decoration of modelPath: string
  | Hazard of modelPath: string * hazardType: string
  | MovingPlatform of modelPath: string
  | Goal of modelPath: string
  | SpawnPoint
  | Empty

/// A Flow is a function that takes a cursor (X, Section) and returns an updated cursor.
/// This enables declarative piping: (0, sec) |> ground 4 |> gap 2
type Flow = int * GridSection3D<LayoutCell> -> int * GridSection3D<LayoutCell>

module InternalMetadata =
  open System.Collections.Generic
  let metadataCache = Dictionary<string, ModelMetadata>()

// ============================================
// Asset & Theme Registry
// ============================================

module Theme =
  module Terrain =
    let grassLarge = "PlatformerKit/block-grass-large"
    let grassLow = "PlatformerKit/block-grass-low"
    let grassLowNarrow = "PlatformerKit/block-grass-low-narrow"
    let grassHexagon = "PlatformerKit/block-grass-low-hexagon"
    let slopeSteep = "PlatformerKit/block-grass-large-slope-steep"
    let pipe = "PlatformerKit/pipe"

  module Decor =
    let arrow = "PlatformerKit/arrow"
    let flowers = "PlatformerKit/flowers"
    let coinGold = "PlatformerKit/coin-gold"
    let coinSilver = "PlatformerKit/coin-silver"
    let heart = "PlatformerKit/heart"

  module Hazards =
    let spikes = "PlatformerKit/trap-spikes"
    let saw = "PlatformerKit/saw"
    let tree = "PlatformerKit/tree-pine-small"

  module Special =
    let goal = "PlatformerKit/flowers"
    let movingPlatform = "PlatformerKit/block-moving-blue"


// ============================================
// Metadata Extraction
// ============================================

module Metadata =
  let extractThemeMetadata(ctx: Mibo.Elmish.GameContext) =
    let extract path =
      if not(InternalMetadata.metadataCache.ContainsKey path) then
        let model = Mibo.Elmish.Assets.model path ctx
        let bounds = ModelBounds.extractFromModel model

        InternalMetadata.metadataCache.Add(
          path,
          {
            Bounds = bounds
            Offset = ModelBounds.calculateOffset bounds BottomCenter
          }
        )

    extract Theme.Terrain.grassLarge
    extract Theme.Terrain.grassLow
    extract Theme.Terrain.grassLowNarrow
    extract Theme.Decor.arrow
    extract Theme.Decor.flowers
    extract Theme.Decor.coinGold
    extract Theme.Hazards.spikes
    extract Theme.Hazards.saw
    extract Theme.Special.movingPlatform
    extract "PlatformerKit/character-oobi"

// ============================================
// Platformer DSL (Domain Specific Language)
// ============================================

module Platformer =
  /// A basic gap (empty space).
  let inline gap(width: int) : Flow = fun (x, sec) -> (x + width, sec)

  /// Solid ground at y=0.
  let inline ground(width: int) : Flow =
    fun (x, sec) ->
      sec
      |> Layout3D.section
        x
        0
        0
        (Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge))
      |> ignore

      (x + width, sec)

  /// A platform at a specific height.
  let inline platform (width: int) (height: float32) : Flow =
    fun (x, sec) ->
      let y = int(Math.Round(float height))

      let model =
        if width = 1 then
          Theme.Terrain.grassLow
        else
          Theme.Terrain.grassLowNarrow

      sec
      |> Layout3D.section x y 0 (Layout3D.fill 0 0 0 width 1 1 (Terrain model))
      |> ignore

      (x + width, sec)

  /// The starting area for the player.
  let inline spawnArea(width: int) : Flow =
    fun (x, sec) ->
      sec
      |> Layout3D.section x 0 0 (fun s ->
        s
        |> Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge)
        |> Layout3D.set (width / 2) 1 0 SpawnPoint
        |> Layout3D.set (width / 2) 1 1 (Decoration Theme.Decor.arrow))
      |> ignore

      (x + width, sec)

  /// A pit full of spikes.
  let inline spikePit(width: int) : Flow =
    fun (x, sec) ->
      sec
      |> Layout3D.section x 0 0 (fun s ->
        s
        |> Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge)
        |> Layout3D.fill
          0
          1
          0
          width
          1
          1
          (Hazard(Theme.Hazards.spikes, "spikes")))
      |> ignore

      (x + width, sec)

  /// A platform with a coin on top.
  let inline platformWithCoin (width: int) (height: float32) : Flow =
    fun (x, sec) ->
      let y = int(Math.Round(float height))

      sec
      |> Layout3D.section x y 0 (fun s ->
        s
        |> (fun s2 ->
          s2
          |> Layout3D.fill
            0
            0
            0
            width
            1
            1
            (Terrain Theme.Terrain.grassLowNarrow))
        |> Layout3D.set (width / 2) 1 1 (Decoration Theme.Decor.coinGold))
      |> ignore

      (x + width, sec)

  /// The end of the level.
  let inline goalArea(width: int) : Flow =
    fun (x, sec) ->
      sec
      |> Layout3D.section x 0 0 (fun s ->
        s
        |> Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge)
        |> Layout3D.set (width / 2) 1 0 (Goal Theme.Special.goal)
        |> Layout3D.set (width / 2) 1 1 (Decoration Theme.Decor.flowers))
      |> ignore

      (x + width, sec)


// ============================================
// Level Layout Implementation
// ============================================

module LevelLayout =
  let mapWidth = 200
  let mapHeight = 20
  let mapDepth = 2

  /// Calculates world position for a cell's bottom.
  let inline cellBottom x y z =
    Vector3(
      float32 x * cellSize + (cellSize / 2.0f),
      float32 y * cellSize,
      float32 z * 0.1f // Tiny epsilon for layering
    )

  /// Creates the 3D grid using a declarative piping DSL.
  let createPrototypeLevel() : CellGrid3D<LayoutCell> =
    CellGrid3D.create
      mapWidth
      mapHeight
      mapDepth
      (Vector3(cellSize, cellSize, cellSize))
      Vector3.Zero
    |> Layout3D.run(fun sec ->
      let finalX, finalSec =
        (0, sec)
        |> Platformer.spawnArea 4
        |> Platformer.ground 2
        |> Platformer.gap 1
        |> Platformer.platformWithCoin 3 0.8f // ~51 units, reachable from ground
        |> Platformer.gap 1
        |> Platformer.spikePit 4
        |> Platformer.gap 1
        |> Platformer.platform 2 0.5f // low platform after spikes
        |> Platformer.gap 1
        |> Platformer.platform 2 1.0f // step up ~32 units
        |> Platformer.gap 1
        |> Platformer.platform 2 1.4f // step up ~25 units
        |> Platformer.gap 2
        |> Platformer.platformWithCoin 2 1.0f // back down for variety
        |> (fun (x, s) ->
          s
          |> Layout3D.section x 1 0 (fun s2 ->
            s2
            |> Layout3D.set
              1
              0
              0
              (MovingPlatform Theme.Special.movingPlatform)
            |> Layout3D.set 4 2 0 (Hazard(Theme.Hazards.saw, "rotating")))
          |> ignore

          (x + 6, s))
        |> Platformer.gap 2
        |> Platformer.goalArea 6

      finalSec)


  // ============================================
  // Converters (Grid -> Game Entities)
  // ============================================

  /// Gets the visual offset for a model to align its bottom-center with target position
  let inline applyMetadata (modelPath: string) (targetBottom: Vector3) =
    match InternalMetadata.metadataCache.TryGetValue(modelPath) with
    | (true, meta) -> targetBottom + meta.Offset
    | _ -> targetBottom + Vector3(0.0f, cellSize / 2.0f, 0.0f)

  /// Gets the model bounds from cache
  let inline getBoundsFromCache(modelPath: string) : ModelBounds option =
    match InternalMetadata.metadataCache.TryGetValue(modelPath) with
    | (true, meta) -> Some meta.Bounds
    | _ -> None

  let gridToEntities(grid: CellGrid3D<LayoutCell>) : Entity[] =
    let entities = ResizeArray<Entity>()

    grid
    |> CellGrid3D.iter(fun x y z cell ->
      let bottom = cellBottom x y z
      let newId() = Guid.NewGuid()

      match cell with
      | Terrain modelPath
      | Decoration modelPath ->
        let entityType =
          if z > 0 then
            EntityType.Decoration
          else
            EntityType.Static {
              GridPos = { X = x; Y = y; Anchor = BottomCenter }
              TileId = 0
              Scale = 1.0f
            }

        entities.Add {
          Id = newId()
          WorldPosition = applyMetadata modelPath bottom
          EntityType = entityType
          ModelPath = Some modelPath
          Bounds = getBoundsFromCache modelPath
        }
      | Hazard(modelPath, _) ->
        entities.Add {
          Id = newId()
          WorldPosition = applyMetadata modelPath bottom
          EntityType = Danger
          ModelPath = Some modelPath
          Bounds = getBoundsFromCache modelPath
        }
      | MovingPlatform modelPath ->
        entities.Add {
          Id = newId()
          WorldPosition = applyMetadata modelPath bottom
          EntityType = EntityType.MovingPlatform
          ModelPath = Some modelPath
          Bounds = getBoundsFromCache modelPath
        }
      | Goal modelPath ->
        entities.Add {
          Id = newId()
          WorldPosition = applyMetadata modelPath bottom
          EntityType = EntityType.Goal
          ModelPath = Some modelPath
          Bounds = getBoundsFromCache modelPath
        }
      | SpawnPoint ->
        entities.Add {
          Id = newId()
          WorldPosition = bottom + Vector3(0.0f, cellSize / 2.0f, 0.0f)
          EntityType = Player
          ModelPath = None
          Bounds = None
        }
      | Empty -> ())

    entities.ToArray()

  let getSpawnPoint(grid: CellGrid3D<LayoutCell>) : Vector3 option =
    let mutable spawnPos = None

    grid
    |> CellGrid3D.iter(fun x y z cell ->
      match cell with
      | SpawnPoint ->
        spawnPos <- Some(cellBottom x y z + Vector3(0.0f, 32.0f, 0.0f))
      | _ -> ())

    spawnPos

  /// Derives physics bodies from entities using their WorldPosition and Bounds.
  /// Entity.WorldPosition is the visual draw position where the model is rendered.
  /// The model's actual center in world space is WorldPosition + bounds.Center.
  let entitiesToPhysicsBodies(entities: Entity[]) : PhysicsBody[] =
    entities
    |> Array.choose(fun entity ->
      match entity.EntityType, entity.Bounds with
      | Static _, Some bounds
      | EntityType.MovingPlatform, Some bounds ->
        // The model is drawn at WorldPosition
        // Its center in world space is WorldPosition + bounds.Center
        let physicsCenter = entity.WorldPosition + bounds.Center

        Some {
          Position = physicsCenter
          Velocity = Vector3.Zero
          Shape = Box bounds.Size
          IsStatic = true // MovingPlatform treated as static for now
        }
      | _ -> None)
