namespace FsJump.Core

open Microsoft.Xna.Framework
open Mibo.Elmish
open FsJump.Core.Types

module Assets =
  val getModelBounds: ctx: GameContext -> modelPath: string -> ModelBounds

  val loadAllLevelEntities: ctx: GameContext -> tiledMap: TiledMap -> Entity[]

  val findSpawnPoint: ctx: GameContext -> tiledMap: TiledMap -> Vector3 option

  val entitiesToPhysicsBodies: entities: Entity[] -> PhysicsBody[]
