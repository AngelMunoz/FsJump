module FsJump.Core.Game

open System
open System.IO
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D
open FsJump.Core.Types
open FsJump.Core.Level
open FsJump.Core.Assets

let cellSizeF = 64.0f
let cameraZOffset = 400f
let cameraFOV = MathHelper.PiOver4

let createCamera2_5D (target: Vector3) (vp: Viewport) : Camera =
  let position = Vector3(target.X, target.Y, cameraZOffset)

  Camera.perspectiveDefaults
  |> Camera.lookAt position target
  |> Camera.withUp Vector3.Up
  |> Camera.withFov cameraFOV
  |> Camera.withAspect vp.AspectRatio
  |> Camera.withRange 1.0f 2000f

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

    let cameraTarget =
      match findSpawnPoint ctx tiledMap with
      | Some pos -> pos
      | None -> Vector3(levelWidth / 2.0f, levelHeight / 2.0f, 0.0f)

    let model = {
      Entities = entities
      Tileset =
        if tiledMap.Tilesets.Length > 0 then
          tiledMap.Tilesets.[0]
        else
          failwith "No tilesets found"
      CameraPosition = (createCamera2_5D cameraTarget vp).Position
      CameraTarget = cameraTarget
    }

    struct (model, Cmd.none)

  | Error err ->
    printfn $"Error loading level: {err}"

    let emptyModel = {
      Entities = [||]
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
    }

    struct (emptyModel, Cmd.none)

let update (msg: Msg) (model: State) : struct (State * Cmd<Msg>) =
  match msg with
  | Tick _ -> struct (model, Cmd.none)
  | LevelLoaded _ -> struct (model, Cmd.none)

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

  for entity in model.Entities do
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

let program =
  Program.mkProgram init update
  |> Program.withAssets
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
