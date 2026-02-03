module FsJump.WindowsDX.Program

open System
open FsJump.Core.Game
open Mibo.Elmish

[<EntryPoint>]
[<STAThread>]
let main _ =
  let program =
    program
    |> Program.withConfig(fun (game, graphics) ->
      game.IsMouseVisible <- true
      game.Window.Title <- "FsJump Windows"
      graphics.PreferredBackBufferWidth <- 800
      graphics.PreferredBackBufferHeight <- 600)

  use game = new ElmishGame<_, _>(program)
  game.Run()
  0
