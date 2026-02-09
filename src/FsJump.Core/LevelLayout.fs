namespace FsJump.Core

open System
open Microsoft.Xna.Framework
open Mibo.Layout3D
open FsJump.Core.Types

// ============================================
// Core Domain Types
// ============================================

/// Represents the content of a single cell in the 3D grid.
/// This is the "Atomic Unit" of our level.
[<Struct>]
type LayoutCell =
  | Terrain of modelPath: string
  | Decoration of modelPath: string
  | Hazard of modelPath: string * hazardType: string
  | MovingPlatform of modelPath: string
  | Goal of modelPath: string
  | SpawnPoint
  | Empty

/// A Stamp is a function that modifies a grid section.
type Stamp = GridSection3D<LayoutCell> -> GridSection3D<LayoutCell>

/// A ScenarioPiece is a higher-level building block.
/// It knows its width, allowing for automatic sequential layout.
type ScenarioPiece = {
  Width: int
  Stamp: Stamp
}

// ============================================
// Asset & Theme Registry
// ============================================

/// Centralized repository for asset paths.
/// Makes it easier to reskin the level later.
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
    let goal = "PlatformerKit/flowers" // Using flowers as goal for now
    let movingPlatform = "PlatformerKit/block-moving-blue"


// ============================================
// Platformer DSL (Domain Specific Language)
// ============================================

module Platformer =
  /// The engine of the DSL.
  /// Chains multiple scenario pieces together sequentially along the X-axis.
  let chain (pieces: seq<ScenarioPiece>) : Stamp =
    fun section ->
      // Fold through the pieces, tracking the current X offset
      let finalSection, _ =
        pieces
        |> Seq.fold (fun (sec: GridSection3D<LayoutCell>, xOffset: int) piece ->
            // Apply the stamp at the current offset
            let nextSec = sec |> Layout3D.section xOffset 0 0 piece.Stamp
            // Return the modified section and the new offset
            (nextSec, xOffset + piece.Width)
        ) (section, 0)
      
      finalSection

  // --- Primitive Builders ---

  /// A basic gap (empty space).
  let gap (width: int) : ScenarioPiece = 
    { Width = width; Stamp = id }

  /// Solid ground at y=0.
  let ground (width: int) : ScenarioPiece =
    { Width = width
      Stamp = Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge) }

  /// A platform at a specific height.
  /// Supports fractional heights for precise jump tuning.
  let platform (width: int) (height: float32) : ScenarioPiece =
    let model = if width = 1 then Theme.Terrain.grassLow else Theme.Terrain.grassLowNarrow
    { Width = width
      Stamp = fun sec ->
        // Convert float height to internal Mibo coordinates if necessary
        // or round to nearest cell if the grid only supports integers.
        // Mibo Layout3D typically uses integer coordinates for the grid cells.
        let y = int (Math.Round(float height))
        sec |> Layout3D.fill 0 y 0 width 1 1 (Terrain model) }

  // --- Semantic Gameplay Pieces ---

  /// The starting area for the player.
  let spawnArea (width: int) : ScenarioPiece =
    { Width = width
      Stamp = fun sec ->
        sec
        |> Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge)
        |> Layout3D.set (width / 2) 1 0 SpawnPoint
        |> Layout3D.set (width / 2) 1 1 (Decoration Theme.Decor.arrow) }

  /// A pit full of spikes.
  let spikePit (width: int) : ScenarioPiece =
    { Width = width
      Stamp = fun sec ->
        sec
        |> Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge)
        |> Layout3D.fill 0 1 0 width 1 1 (Hazard(Theme.Hazards.spikes, "spikes")) }

  /// A platform with a coin on top.
  let platformWithCoin (width: int) (height: float32) : ScenarioPiece =
    { Width = width
      Stamp = fun sec ->
        sec
        |> Layout3D.section 0 0 0 (platform width height).Stamp
        |> ignore
        let y = int (Math.Round(float height))
        sec |> Layout3D.set (width / 2) (y + 1) 1 (Decoration Theme.Decor.coinGold) }

  /// A simple jump challenge: [Ground] -> [Gap] -> [Platform]
  let jumpChallenge (gapWidth: int) (platWidth: int) (platHeight: float32) : seq<ScenarioPiece> =
    seq {
      gap gapWidth
      platform platWidth platHeight
    }
    
  /// The end of the level.
  let goalArea (width: int) : ScenarioPiece =
    { Width = width
      Stamp = fun sec ->
        sec
        |> Layout3D.fill 0 0 0 width 1 1 (Terrain Theme.Terrain.grassLarge)
        |> Layout3D.set (width / 2) 1 0 (Goal Theme.Special.goal)
        |> Layout3D.set (width / 2) 1 1 (Decoration Theme.Decor.flowers) }

  /// Rotating saw hazard.
  let sawRotating (x: int) (y: int) : Stamp =
      Layout3D.set x y 0 (Hazard(Theme.Hazards.saw, "rotating"))


// ============================================
// Level Layout Implementation
// ============================================

module LevelLayout =
  // Map dimensions
  let mapWidth = 200 // Increased width to accommodate sequential layout
  let mapHeight = 20
  let mapDepth = 2

  /// Defines the sequence of the prototype level.
  /// This is the "Script" of the level.
  let private prototypeSequence =
    seq {
        // Intro
        Platformer.spawnArea 4
        
        // Basic Jumping (reachable heights)
        Platformer.ground 2
        Platformer.gap 2
        Platformer.platformWithCoin 3 1.0f
        
        // Hazard Introduction
        Platformer.gap 2
        Platformer.spikePit 4
        
        // Verticality (Step-like progression)
        Platformer.gap 1
        Platformer.platform 2 1.0f
        Platformer.gap 1
        Platformer.platform 2 2.0f
        Platformer.gap 1
        Platformer.platform 2 3.0f

        // Tricky Jump
        Platformer.gap 3
        Platformer.platformWithCoin 2 2.0f
        
        // Moving Platform Section
        { Width = 6
          Stamp = fun sec -> 
            sec
            |> Layout3D.set 1 1 0 (MovingPlatform Theme.Special.movingPlatform)
            |> Layout3D.section 0 0 0 (Platformer.sawRotating 4 3)
        }

        // The End
        Platformer.gap 2
        Platformer.goalArea 6
    }

  /// Creates the 3D grid for the level.
  let createPrototypeLevel() : CellGrid3D<LayoutCell> =
    CellGrid3D.create
      mapWidth
      mapHeight
      mapDepth
      (Vector3(cellSize, cellSize, cellSize))
      Vector3.Zero
    |> Layout3D.run (Platformer.chain prototypeSequence)


  // ============================================
  // Converters (Grid -> Game Entities)
  // ============================================

  /// Converts the 3D grid to game entities.
  let gridToEntities(grid: CellGrid3D<LayoutCell>) : Entity[] =
    let entities = ResizeArray<Entity>()
    
    grid
    |> CellGrid3D.iter (fun x y z cell ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      let newId() = Guid.NewGuid()

      match cell with
      | Terrain modelPath 
      | Decoration modelPath ->
          entities.Add {
            Id = newId()
            WorldPosition = worldPos
            EntityType = Static { GridPos = { X = x; Y = y; Anchor = BottomCenter }; TileId = 0; Scale = 1.0f }
            ModelPath = Some modelPath
            Bounds = None
          }
      | Hazard (modelPath, _) ->
          entities.Add {
            Id = newId()
            WorldPosition = worldPos
            EntityType = Danger
            ModelPath = Some modelPath
            Bounds = None
          }
      | MovingPlatform modelPath ->
          entities.Add {
            Id = newId()
            WorldPosition = worldPos
            EntityType = EntityType.MovingPlatform
            ModelPath = Some modelPath
            Bounds = None
          }
      | Goal modelPath ->
          entities.Add {
            Id = newId()
            WorldPosition = worldPos
            EntityType = EntityType.Goal
            ModelPath = Some modelPath
            Bounds = None
          }
      | SpawnPoint ->
          entities.Add {
            Id = newId()
            WorldPosition = worldPos
            EntityType = Player
            ModelPath = None
            Bounds = None
          }
      | Empty -> ()
    )
    entities.ToArray()

  /// Extracts the player spawn position from the grid.
  let getSpawnPoint(grid: CellGrid3D<LayoutCell>) : Vector3 option =
    let mutable spawnPos = None
    grid
    |> CellGrid3D.iter (fun x y z cell ->
      match cell with
      | SpawnPoint -> 
          spawnPos <- Some (CellGrid3D.getWorldPos x y z grid)
      | _ -> ()
    )
    spawnPos

  /// Generates physics bodies from the grid terrain.
  let gridToPhysicsBodies(grid: CellGrid3D<LayoutCell>) : PhysicsBody[] =
    let bodies = ResizeArray<PhysicsBody>()
    let size = Vector3(cellSize, cellSize, cellSize)

    grid
    |> CellGrid3D.iter (fun x y z cell ->
      match cell with
      | Terrain _ ->
          let worldPos = CellGrid3D.getWorldPos x y z grid
          bodies.Add {
            Position = worldPos
            Velocity = Vector3.Zero
            Shape = Box size
            IsStatic = true
          }
      | _ -> ()
    )
    bodies.ToArray()