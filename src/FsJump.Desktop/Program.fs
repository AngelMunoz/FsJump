module FsJump.Desktop.Program

open Mibo.Elmish
open FsJump.Core.Game

[<EntryPoint>]
let main _ =
  // Configure window here if needed, or pass config to program in Core
  let program =
    program
    |> Program.withConfig(fun (game, graphics) ->
      game.IsMouseVisible <- true
      game.Window.Title <- "FsJump Desktop"
      graphics.PreferredBackBufferWidth <- 800
      graphics.PreferredBackBufferHeight <- 600)

  use game = new ElmishGame<_, _>(program)
  game.Run()
  0
