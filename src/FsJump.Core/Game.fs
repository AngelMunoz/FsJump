module FsJump.Core.Game

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

type Model =
    { Position: Vector3; Velocity: Vector3 }

type Msg = Tick of GameTime

let init (ctx: GameContext) : struct (Model * Cmd<Msg>) =
    let model =
        { Position = Vector3(100.f, 0f, 0.f)
          Velocity = Vector3(150.f, 0.f, 0f) }

    model, Cmd.none

let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
    match msg with
    | Tick gt ->
        let dt = float32 gt.ElapsedGameTime.TotalSeconds
        let mutable velocity = model.Velocity
        let mutable position = model.Position + (velocity * dt)

        // Simple bounce (assuming some bounds, though resolution varies)
        if position.X < 0.f || position.X > 750.f then
            velocity.X <- -velocity.X

        { model with
            Position = position
            Velocity = velocity },
        Cmd.none

let view (ctx: GameContext) (model: Model) (buffer: PipelineBuffer<RenderCommand>) =
    let camera = Camera.perspectiveDefaults

    Buffer.camera camera buffer
    |> Buffer.draw (draw { at (Vector3(1f, 1f, 1f)) })
    |> Buffer.submit

let program =
    Program.mkProgram init update
    |> Program.withAssets
    |> Program.withRenderer (fun g ->
        RenderPipeline.create PipelineConfig.defaults g
        |> PipelineRenderer.create g view)
    |> Program.withTick Tick
    |> Program.withConfig (fun (game, graphics) -> game.Content.RootDirectory <- "Content")
