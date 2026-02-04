module FsJump.Core.Game

open System
open System.IO
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D
open Mibo.Input
open FsJump.Core.Types
open FsJump.Core.Level
open FsJump.Core.Assets
open FsJump.Core.Physics

let cellSizeF = 64.0f
let cameraZOffset = 400f
let cameraFOV = MathHelper.PiOver4

// ============================================
// Input Configuration
// ============================================

let inputMap =
  InputMap.empty
  |> InputMap.key MoveLeft Keys.Left
  |> InputMap.key MoveLeft Keys.A
  |> InputMap.key MoveRight Keys.Right
  |> InputMap.key MoveRight Keys.D
  |> InputMap.key Jump Keys.Up
  |> InputMap.key Jump Keys.W
  |> InputMap.key Jump Keys.Space

// ============================================
// Camera
// ============================================

let createCamera2_5D (target: Vector3) (vp: Viewport) : Camera =
  let position = Vector3(target.X, target.Y, cameraZOffset)

  Camera.perspectiveDefaults
  |> Camera.lookAt position target
  |> Camera.withUp Vector3.Up
  |> Camera.withFov cameraFOV
  |> Camera.withAspect vp.AspectRatio
  |> Camera.withRange 1.0f 2000f

// ============================================
// Init
// ============================================

let init(ctx: GameContext) : struct (State * Cmd<Msg>) =
  let vp = ctx.GraphicsDevice.Viewport

  let contentPath =
    Path.Combine(
      AppContext.BaseDirectory,
      ctx.Game.Content.RootDirectory,
      "Prototype.tmj"
    )

  match loadTiledMap contentPath with
  | Ok tiledMap ->
    let entities = loadAllLevelEntities ctx tiledMap
    let levelWidth = float32 tiledMap.Width * cellSizeF
    let levelHeight = float32 tiledMap.Height * cellSizeF

    let spawnPoint =
      match findSpawnPoint ctx tiledMap with
      | Some pos -> pos
      | None -> Vector3(levelWidth / 2.0f, levelHeight / 2.0f, 0.0f)

    let staticBodies = entitiesToPhysicsBodies entities

    let model = {
      Entities = entities
      Player = {
        Position = spawnPoint
        Velocity = Vector3.Zero
        IsGrounded = false
        GroundNormal = Vector3.Up
      }
      StaticBodies = staticBodies
      Tileset =
        if tiledMap.Tilesets.Length > 0 then
          tiledMap.Tilesets.[0]
        else
          failwith "No tilesets found"
      CameraPosition = (createCamera2_5D spawnPoint vp).Position
      CameraTarget = spawnPoint
      Actions = ActionState.empty
    }

    struct (model, Cmd.none)

  | Error err ->
    printfn $"Error loading level: {err}"

    let emptyModel = {
      Entities = [||]
      Player = {
        Position = Vector3.Zero
        Velocity = Vector3.Zero
        IsGrounded = false
        GroundNormal = Vector3.Up
      }
      StaticBodies = [||]
      Tileset = {
        FirstGid = 1
        Name = "Empty"
        TileCount = 0
        TileHeight = 64
        TileWidth = 64
        Tiles = [||]
      }
      CameraPosition = Vector3(0.0f, 0.0f, cameraZOffset)
      CameraTarget = Vector3(0.0f, 0.0f, 0.0f)
      Actions = ActionState.empty
    }

    struct (emptyModel, Cmd.none)

// ============================================
// Update
// ============================================

let update (msg: Msg) (model: State) : struct (State * Cmd<Msg>) =
  match msg with
  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds
    let config = Physics.DefaultConfig

    // Poll input directly from keyboard (standard MonoGame API)
    let keyboard = Keyboard.GetState()
    
    let horizontalInput =
      let left = if keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A) then -1.0f else 0.0f
      let right = if keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D) then 1.0f else 0.0f
      left + right

    let jumpPressed = keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Space)
    let jumpRequested = jumpPressed && not model.Player.IsGrounded

    // Check grounded state
    let groundInfo =
      Physics.checkGrounded config model.Player.Position model.StaticBodies

    // Start with current velocity
    let velocity = model.Player.Velocity

    // Apply movement
    let velocity = Physics.applyMovement config horizontalInput velocity

    // Apply jump if requested and grounded
    let velocity =
      if jumpPressed && groundInfo.IsGrounded then
        Vector3(velocity.X, config.JumpVelocity, 0.0f)
      else
        velocity

    // Apply gravity
    let velocity = Physics.applyGravity config velocity dt

    // Move with collision
    let playerState = {
      model.Player with
        Velocity = velocity
        IsGrounded = groundInfo.IsGrounded
        GroundNormal = groundInfo.GroundNormal
    }

    let struct (newPos, newVel, wasGrounded) =
      Physics.moveAndSlide config playerState model.StaticBodies dt

    let player = {
      Position = newPos
      Velocity = newVel
      IsGrounded = wasGrounded
      GroundNormal = groundInfo.GroundNormal
    }

    // Debug output (only when moving or jumping)
    if horizontalInput <> 0.0f || jumpPressed then
      printfn $"Input: h={horizontalInput}, jump={jumpPressed}, Pos=({newPos.X:F1},{newPos.Y:F1}), Vel=({newVel.X:F1},{newVel.Y:F1})"

    // Update camera to follow player
    let cameraTarget = Vector3(newPos.X, newPos.Y, 0.0f)

    struct ({ model with Player = player; CameraTarget = cameraTarget }, Cmd.none)

  | LevelLoaded _ -> struct (model, Cmd.none)

  | InputMapped _actions ->
    // Input is polled directly in Tick - ignore subscription
    struct (model, Cmd.none)

// ============================================
// View
// ============================================

let view
  (ctx: GameContext)
  (model: State)
  (buffer: PipelineBuffer<RenderCommand>)
  =
  let vp = ctx.GraphicsDevice.Viewport
  let camera = createCamera2_5D model.CameraTarget vp

  buffer
  |> Buffer.clear Color.CornflowerBlue
  |> Buffer.clearDepth
  |> Buffer.camera camera
  |> Buffer.submit

  // Render static entities (exclude Player entities - those are rendered separately)
  for entity in model.Entities do
    match entity.EntityType with
    | Player -> () // Skip player entities (spawn point marker)
    | _ ->
      entity.ModelPath
      |> Option.iter(fun path ->
        let modelAsset = Mibo.Elmish.Assets.model path ctx
        let mesh = Mesh.fromModel modelAsset

        for mesh_ in mesh do
          buffer
          |> Buffer.draw(
            draw {
              mesh mesh_
              at entity.WorldPosition
            }
          )
          |> Buffer.submit)

  // Render player
  let playerMesh =
    Mibo.Elmish.Assets.model "PlatformerKit/character-oobi" ctx
    |> Mesh.fromModel

  for mesh_ in playerMesh do
    buffer
    |> Buffer.draw(
      draw {
        mesh mesh_
        at model.Player.Position
      }
    )
    |> Buffer.submit

// ============================================
// Subscription
// ============================================

let subscribe (ctx: GameContext) (_model: State) : Sub<Msg> =
  Sub.batch [ InputMapper.subscribeStatic inputMap InputMapped ctx ]

// ============================================
// Program
// ============================================

let program =
  Program.mkProgram init update
  |> Program.withAssets
  |> Program.withInput
  |> Program.withSubscription subscribe
  |> Program.withRenderer(fun g ->
    let config =
      PipelineConfig.defaults
      |> PipelineConfig.withDefaultLighting Lighting.defaultSunlight

    RenderPipeline.create config g |> PipelineRenderer.create g view)
  |> Program.withTick Tick
  |> Program.withConfig(fun (game, graphics) ->
    game.Content.RootDirectory <- "Content"
    graphics.PreferredBackBufferWidth <- 800
    graphics.PreferredBackBufferHeight <- 600)
