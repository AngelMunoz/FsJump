module FsJump.Core.Game

open System
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
open FsJump.Core.LevelLayout

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

  // Generate level using Mibo Layout3D
  let grid = LevelLayout.createPrototypeLevel()
  let entities = LevelLayout.gridToEntities grid
  let staticBodies = LevelLayout.gridToPhysicsBodies grid

  let spawnPoint =
    match LevelLayout.getSpawnPoint grid with
    | Some pos -> pos
    | None -> Vector3(64.0f, 640.0f, 0.0f) // Default spawn

  let model = {
    Entities = entities
    Player = {
      Position = spawnPoint
      Velocity = Vector3.Zero
      IsGrounded = false
      GroundNormal = Vector3.Up
    }
    StaticBodies = staticBodies
    Tileset = {
      FirstGid = 1
      Name = "Layout3D"
      TileCount = 115
      TileHeight = 64
      TileWidth = 64
      Tiles = [||]
    }
    CameraPosition = (createCamera2_5D spawnPoint vp).Position
    CameraTarget = spawnPoint
    Actions = ActionState.empty
  }

  struct (model, Cmd.none)

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
      let left =
        if keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A) then
          -1.0f
        else
          0.0f

      let right =
        if keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D) then
          1.0f
        else
          0.0f

      left + right

    let jumpPressed =
      keyboard.IsKeyDown(Keys.Up)
      || keyboard.IsKeyDown(Keys.W)
      || keyboard.IsKeyDown(Keys.Space)

    // Start with current velocity
    let velocity = model.Player.Velocity

    // Apply movement
    let velocity = Physics.applyMovement config horizontalInput velocity

    // Apply jump if grounded (use previous frame's grounded state for responsiveness)
    let velocity =
      if jumpPressed && model.Player.IsGrounded then
        Vector3(velocity.X, config.JumpVelocity, 0.0f) // Positive because Y increases upward
      else
        velocity

    // Apply gravity (negative Y because Y increases upward)
    let velocity = Physics.applyGravity config velocity dt

    // Move with collision
    let playerState = {
      model.Player with
          Velocity = velocity
    }

    let struct (newPos, newVel, wasGrounded) =
      Physics.moveAndSlide config playerState model.StaticBodies dt

    // Check grounded at the NEW position after movement
    let groundInfo = Physics.checkGrounded config newPos model.StaticBodies

    let player = {
      Position = newPos
      Velocity = newVel
      IsGrounded = wasGrounded || groundInfo.IsGrounded // Either collision-based or raycast-based
      GroundNormal = groundInfo.GroundNormal
    }

    // Debug output (when moving/jumping)
    if horizontalInput <> 0.0f || jumpPressed || Math.Abs(newVel.Y) > 10.0f then
      printfn
        $"h={horizontalInput}, jump={jumpPressed}, grounded={model.Player.IsGrounded}, Pos=({newPos.X:F0},{newPos.Y:F0}), Vel=({newVel.X:F0},{newVel.Y:F0})"

    // Update camera to follow player
    let cameraTarget = Vector3(newPos.X, newPos.Y, 0.0f)

    struct ({
              model with
                  Player = player
                  CameraTarget = cameraTarget
            },
            Cmd.none)

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
