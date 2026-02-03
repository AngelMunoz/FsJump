module FsJump.Core.ModelBounds

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open FsJump.Core.Types

let extractFromModel(model: Model) : ModelBounds =
  let mutable min =
    Vector3(
      System.Single.MaxValue,
      System.Single.MaxValue,
      System.Single.MaxValue
    )

  let mutable max =
    Vector3(
      System.Single.MinValue,
      System.Single.MinValue,
      System.Single.MinValue
    )

  for mesh in model.Meshes do
    let boneTransform = mesh.ParentBone.Transform

    for meshPart in mesh.MeshParts do
      let vertexStride = meshPart.VertexBuffer.VertexDeclaration.VertexStride
      let vertexCount = meshPart.NumVertices
      let vertexData = Array.create (vertexCount * vertexStride / 4) 0.0f

      meshPart.VertexBuffer.GetData(vertexData)

      for i in 0 .. vertexCount - 1 do
        let idx = i * (vertexStride / 4)

        if idx + 2 < vertexData.Length then
          let localPos =
            Vector3(
              vertexData.[idx],
              vertexData.[idx + 1],
              vertexData.[idx + 2]
            )

          let worldPos = Vector3.Transform(localPos, boneTransform)

          min <- Vector3.Min(min, worldPos)
          max <- Vector3.Max(max, worldPos)

  let size = max - min

  {
    Min = min
    Max = max
    Center = (min + max) * 0.5f
    Size = size
    HalfSize = size * 0.5f
  }

let calculateOffset (bounds: ModelBounds) (anchor: AnchorPoint) : Vector3 =
  match anchor with
  | BottomCenter -> Vector3(-bounds.Center.X, -bounds.Min.Y, -bounds.Center.Z)
  | Center -> -bounds.Center
  | TopLeft -> Vector3(-bounds.Min.X, -bounds.Max.Y, -bounds.Min.Z)
  | TopCenter -> Vector3(-bounds.Center.X, -bounds.Max.Y, -bounds.Center.Z)
  | BottomLeft -> Vector3(-bounds.Min.X, -bounds.Min.Y, -bounds.Min.Z)
