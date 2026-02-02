# AGENTS.md - FsJump Development Guide

F# MonoGame project using Mibo's Elmish (MVU) architecture with JDeck for JSON serialization.

## Project Structure
```
FsJump/
├── src/
│   ├── FsJump.Core/       # Shared game logic (net10.0)
│   ├── FsJump.Desktop/     # DesktopGL target (net10.0)
│   ├── FsJump.WindowsDX/   # Windows DirectX (net10.0-windows)
│   ├── FsJump.Android/     # Android (net10.0-android)
│   └── FsJump.iOS/         # iOS (net10.0-ios)
└── .config/dotnet-tools.json
```

## Build Commands
```bash
dotnet build                           # Build entire solution
dotnet build src/FsJump.Core            # Build specific project
dotnet run --project src/FsJump.Desktop # Run Desktop version
dotnet clean && dotnet restore         # Clean/Restore
```

## Formatting & Content
```bash
dotnet fantomas .                                         # Format all F# files
dotnet mgcb src/FsJump.Core/Content/Content.mgcb          # Build MonoGame content
dotnet mgcb-editor                                        # Open content editor
```

## Testing
Currently no test suite. When adding tests:
```bash
dotnet test
dotnet test --filter "FullyQualifiedName~YourTestName"
```

## Documentation

For detailed framework documentation, fetch and reference:
- **Mibo Framework**: https://angelmunoz.github.io/Mibo/
  - Elmish architecture: https://angelmunoz.github.io/Mibo/elmish.html
  - Program composition: https://angelmunoz.github.io/Mibo/program.html
  - Input handling: https://angelmunoz.github.io/Mibo/input.html
  - Rendering: https://angelmunoz.github.io/Mibo/rendering.html

- **JDeck (JSON)**: https://angelmunoz.github.io/JDeck/
  - Decoding guide: https://angelmunoz.github.io/JDeck/decoding.html
  - Codecs: https://angelmunoz.github.io/JDeck/codecs.html

- **AppEnv DI Pattern**: https://www.bartoszsypytkowski.com/dealing-with-complex-dependency-injection-in-f/
  - Use single `AppEnv` type with capability interfaces instead of partial application
  - F# compiler infers union of constraints, no manual wiring needed

## Dependency Injection: AppEnv Pattern

This project encourages the AppEnv pattern for dependency management. Instead of passing many partially applied parameters, define capability interfaces and a single environment type:

```fsharp
// Define capability interfaces
[<Interface>] type ILog = abstract Logger: ILogger
[<Interface>] type IDb = abstract Database: IDatabase

// Module functions accept generic env: #ILog
module Log =
    let debug (env: #ILog) fmt = Printf.kprintf env.Logger.Debug fmt
    let error (env: #ILog) fmt = Printf.kprintf env.Logger.Error fmt

module Db =
    let fetchUser (env: #IDb) userId = env.Database.Query(sql, {| userId = userId |})

// AppEnv implements all required capabilities
[<Struct>]
type AppEnv =
    interface ILog with member _.Logger = Log.live
    interface IDb with member _.Database = Db.live connectionString

// Functions using env - F# infers union of constraints
let changePassword env userId newPassword = task {
    let! user = Db.fetchUser env userId
    Log.info env "Changed password for user %i" user.Id
    // ...
}
```

See Bartosz Sypytkowski's article for complete explanation.

## Code Style Guidelines

### Module/Namespace
```fsharp
module FsJump.Core.Game    // Game logic
namespace FsJump.Android   // Platform-specific
```

### Imports
Group `open` statements logically at top:
```fsharp
open System
open Microsoft.Xna.Framework
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Input
open JDeck
```

### Type Definitions
```fsharp
// Record types for models
type Model = { PlayerPos: Vector3; Score: int; IsPaused: bool }

// Discriminated unions for messages
type Msg = MoveRequested of Vector3 | CoinCollected of int | Tick of GameTime

// Type annotations on parameters
let init (ctx: GameContext) : struct (Model * Cmd<Msg>) =
```

### Naming Conventions
- **Types**: PascalCase (`Model`, `Msg`, `Game`)
- **Functions/Values**: camelCase (`init`, `update`, `view`)
- **Module members**: camelCase (`Position`, `Velocity`)
- **Parameters**: camelCase, short when clear (`ctx`, `gt`, `dt`)

### Elmish Pattern
```fsharp
let init (ctx: GameContext) : struct (Model * Cmd<Msg>) =
    model, Cmd.none

let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
    match msg with
    | Tick gt -> newModel, Cmd.none

let view (ctx: GameContext) (model: Model) (buffer: PipelineBuffer<RenderCommand>) =
    Buffer.submit

let subscribe (ctx: GameContext) (model: Model) =
    Sub.batch [ InputMapper.subscribeStatic map InputMapped ctx ]
```

### Syntax Style
- Use `=` for bindings, `|>` for piping, `|||` for flag OR
- Use `match/with` for pattern matching
- Prefer immutability, `let mutable` sparingly
- Record update: `{ model with Position = newPosition }`

### Numeric Types
Use `float32` suffix: `Vector3(100.f, 0.f, 0.f)`, `float32 gt.ElapsedGameTime.TotalSeconds`

### Platform-Specific
- Desktop/WindowsDX: `[<EntryPoint>]` (Windows: also `[<STAThread>]`)
- Android: `inherit AndroidGameActivity`, `[<Activity>]` attribute
- iOS: `[<Register>]` on AppDelegate, `UIApplication.Main` for entry

### Project Configuration
- Core/Desktop: `net10.0`, others platform-specific
- MonoGame: `Version="3.8.*"` wildcard
- Mibo: `Version="1.*"` wildcard
- Content: `<MonoGameContentReference Include="Content/Content.mgcb" />`
- References: `<ProjectReference Include="..\FsJump.Core\FsJump.Core.fsproj" />`
- Compile order: Ensure dependencies compile first in `<Compile Include="..." />`

## Key Dependencies
- **Mibo**: Elmish architecture, rendering, input, assets
- **JDeck**: JSON serialization (System.Text.Json wrapper)
- **MonoGame.Framework**: Cross-platform game framework
- **MonoGame.Content.Builder.Task**: Content pipeline

## Platform Development
- **macOS**: Use `FsJump.Desktop` (DesktopGL)
- **Windows**: Either DesktopGL or WindowsDX target
- **Android/iOS**: Requires respective SDKs

Keep shared logic in `FsJump.Core`, platform setup only in platform projects.
