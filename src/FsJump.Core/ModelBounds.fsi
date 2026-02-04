namespace FsJump.Core

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open FsJump.Core.Types

module ModelBounds =
  val extractFromModel: model: Model -> ModelBounds

  val calculateOffset: bounds: ModelBounds -> anchor: AnchorPoint -> Vector3
