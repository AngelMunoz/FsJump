namespace FsJump.Core

open System
open Microsoft.Xna.Framework
open Mibo.Input
open FsJump.Core.Types

type PhysicsConfig = {
  Gravity: Vector3
  MoveSpeed: float32
  JumpVelocity: float32
  MinJumpVelocity: float32
  MaxSlopeAngleDegrees: float32
  GroundCheckDistance: float32
  Friction: float32
  PlayerRadius: float32
  PlayerHeight: float32
}

module Physics =
  // ============================================
  // Constants
  // ============================================

  [<Literal>]
  let Epsilon = 0.001f

  [<Literal>]
  let MaxSlopeAngleCos = 0.707f // cos(45°)

  // In Y-up coordinates, "up" is positive Y
  let Up = Vector3(0.0f, 1.0f, 0.0f)
  let Down = Vector3(0.0f, -1.0f, 0.0f)

  let DefaultConfig = {
    Gravity = Vector3(0.0f, -900.0f, 0.0f) // Negative Y because Y increases upward
    MoveSpeed = 200.0f
    JumpVelocity = 400.0f
    MinJumpVelocity = 200.0f
    MaxSlopeAngleDegrees = 45.0f
    GroundCheckDistance = 2.0f // Increased for better detection
    Friction = 0.8f
    PlayerRadius = 16.0f
    PlayerHeight = 64.0f
  }

  // ============================================
  // Internal Helpers
  // ============================================

  // In our coordinate system, Y increases upward
  // So "bottom" means lower Y value
  let getCapsuleBottom (position: Vector3) (height: float32) =
    position - Vector3(0.0f, height / 2.0f, 0.0f)

  let raycastPlane (rayOrigin: Vector3) (rayDir: Vector3) (planeY: float32) =
    if Math.Abs(rayDir.Y) < Epsilon then
      None
    else
      let t = (planeY - rayOrigin.Y) / rayDir.Y
      if t >= 0.0f then Some(rayOrigin + t * rayDir, t) else None

  let intersectsBox (point: Vector3) (boxPos: Vector3) (boxSize: Vector3) =
    let halfSize = boxSize / 2.0f
    let min = boxPos - halfSize
    let max = boxPos + halfSize

    point.X >= min.X
    && point.X <= max.X
    && point.Y >= min.Y
    && point.Y <= max.Y
    && point.Z >= min.Z
    && point.Z <= max.Z

  let closestPointOnBox (point: Vector3) (boxPos: Vector3) (boxSize: Vector3) =
    let halfSize = boxSize / 2.0f
    let min = boxPos - halfSize
    let max = boxPos + halfSize

    Vector3(
      Math.Clamp(point.X, min.X, max.X),
      Math.Clamp(point.Y, min.Y, max.Y),
      Math.Clamp(point.Z, min.Z, max.Z)
    )

  let slideVelocity (velocity: Vector3) (normal: Vector3) =
    let dot = Vector3.Dot(velocity, normal)
    if dot < 0.0f then velocity - (dot * normal) else velocity

  let isWalkableSlope (maxAngleCos: float32) (normal: Vector3) =
    // In Y-up coordinates, walkable surfaces have normals pointing up (positive Y)
    normal.Y >= maxAngleCos

  // ============================================
  // Public API
  // ============================================

  let applyGravity (config: PhysicsConfig) (velocity: Vector3) (dt: float32) =
    velocity + config.Gravity * dt

  let applyMovement
    (config: PhysicsConfig)
    (horizontalInput: float32)
    (velocity: Vector3)
    =
    if horizontalInput <> 0.0f then
      let targetVel =
        Vector3(horizontalInput * config.MoveSpeed, velocity.Y, 0.0f)

      Vector3.Lerp(velocity, targetVel, 0.2f)
    else
      // Apply friction when no input
      let frictionVel = Vector3(velocity.X * config.Friction, velocity.Y, 0.0f)

      if Math.Abs(frictionVel.X) < 1.0f then
        Vector3(0.0f, velocity.Y, 0.0f)
      else
        frictionVel

  let tryJump
    (config: PhysicsConfig)
    (player: PlayerState)
    (jumpRequested: bool)
    =
    // Use strict comparison - only jump if strictly walkable (not on steep slopes)
    // In Y-up coordinates, walkable surfaces have normals pointing up (positive Y)
    if
      jumpRequested
      && player.IsGrounded
      && player.GroundNormal.Y > MaxSlopeAngleCos
    then
      Vector3(player.Velocity.X, config.JumpVelocity, 0.0f) // Positive for upward in Y-up
    else
      player.Velocity

  let tryCutJump
    (velocity: Vector3)
    (jumpReleased: bool)
    (minJumpVelocity: float32)
    =
    // In Y-up coordinates, upward velocity is positive
    // Cut if moving up faster than min (velocity.Y > minJumpVelocity)
    if jumpReleased && velocity.Y > minJumpVelocity then
      Vector3(velocity.X, minJumpVelocity, 0.0f)
    else
      velocity

  let checkGrounded
    (config: PhysicsConfig)
    (playerPos: Vector3)
    (staticBodies: PhysicsBody[])
    =
    // In Y-up coordinates, the "bottom" of the capsule is at lower Y
    // Ray starts near the bottom of the player and casts downward (negative Y)
    let rayOrigin =
      playerPos
      - Vector3(0.0f, config.PlayerHeight / 2.0f - config.PlayerRadius, 0.0f)

    let rayDir = Vector3(0.0f, -1.0f, 0.0f) // Downward in Y-up coordinate system
    let checkDist = config.PlayerRadius + config.GroundCheckDistance



    let mutable closestHit = Single.MaxValue
    let mutable groundY = Single.MinValue
    let mutable groundNormal = Vector3.Up
    let mutable foundGround = false

    for body in staticBodies do
      match body.Shape with
      | Box size ->
        // In Y-up coordinates, "top" surface is at maximum Y (facing upward)
        let boxTop = body.Position.Y + size.Y / 2.0f

        match raycastPlane rayOrigin rayDir boxTop with
        | Some(hitPoint, t) when t <= checkDist ->
          if intersectsBox hitPoint body.Position size then
            if t < closestHit then
              closestHit <- t
              groundY <- boxTop

              // In Y-up, the normal pointing UP is (0, 1, 0)
              // The hit is on the top surface, so the normal should point up (positive Y)
              let normal =
                // If hit is on the top surface (max Y of box), normal points up (positive Y)
                if Math.Abs(hitPoint.Y - boxTop) < Epsilon then
                  Vector3(0.0f, 1.0f, 0.0f) // Up in Y-up coordinates
                else
                  // For other surfaces, estimate from geometry
                  let closest = closestPointOnBox hitPoint body.Position size
                  let surfaceToCenter = body.Position - closest

                  if surfaceToCenter.LengthSquared() > Epsilon then
                    Vector3.Normalize(surfaceToCenter)
                  else
                    Vector3.Up

              groundNormal <- normal
              foundGround <- true
        | _ -> ()
      | _ -> ()

    let slopeAngle =
      // Angle from UP direction. In Y-up, UP is (0,1,0), so we use normal.Y
      let angleRad = Math.Acos(float(Math.Clamp(groundNormal.Y, -1.0f, 1.0f)))
      float32(angleRad * 180.0 / Math.PI)

    {
      IsGrounded = foundGround && isWalkableSlope MaxSlopeAngleCos groundNormal
      GroundHeight = groundY
      GroundNormal = groundNormal
      SlopeAngle = slopeAngle
    }

  let moveAndSlide
    (config: PhysicsConfig)
    (player: PlayerState)
    (staticBodies: PhysicsBody[])
    (dt: float32)
    =
    let delta = player.Velocity * dt
    let mutable newPos = player.Position + delta
    let mutable newVel = player.Velocity
    // IMPORTANT: Start as NOT grounded, only set to true when we detect ground contact
    // This allows the player to fall off ledges
    let mutable wasGrounded = false
    let radius = config.PlayerRadius
    let halfHeight = config.PlayerHeight / 2.0f

    // Check collisions and slide - use iterative approach for better response
    let maxIterations = 3
    let mutable iteration = 0
    let mutable hadCollision = true

    while hadCollision && iteration < maxIterations do
      hadCollision <- false
      iteration <- iteration + 1

      for body in staticBodies do
        match body.Shape with
        | Box size ->
          let halfSize = size / 2.0f
          let boxMin = body.Position - halfSize
          let boxMax = body.Position + halfSize

          // For a capsule, we check collision at multiple points:
          // 1. Feet sphere (bottom of capsule) - most important for ground
          // 2. Center sphere
          // Player feet position is center - (halfHeight - radius)
          let feetPos = newPos - Vector3(0f, halfHeight - radius, 0f)

          // Check feet collision with box top (ground detection)
          // If feet sphere overlaps box, push up
          let closestToFeet = closestPointOnBox feetPos body.Position size
          let feetDiff = feetPos - closestToFeet
          let feetDistSq = feetDiff.LengthSquared()

          if feetDistSq < radius * radius then
            let feetDist = Math.Sqrt(float feetDistSq) |> float32

            let normal, penetration =
              if feetDist < Epsilon then
                // Feet inside box - push up
                Up, radius
              else
                let n = feetDiff / feetDist
                n, radius - feetDist

            // Only apply if pushing upward (ground contact)
            if normal.Y > 0.0f then
              newPos <- newPos + normal * penetration
              newVel <- slideVelocity newVel normal

              if normal.Y > MaxSlopeAngleCos then
                wasGrounded <- true

              hadCollision <- true

          // Also check center for horizontal collisions (walls)
          let closestToCenter = closestPointOnBox newPos body.Position size
          let centerDiff = newPos - closestToCenter
          let centerDistSq = centerDiff.LengthSquared()

          // Handle when center is inside the box (centerDistSq very small)
          if centerDistSq <= Epsilon then
            // Center is inside box - push opposite to velocity direction
            let halfSize = size / 2.0f
            let boxMin = body.Position - halfSize
            let boxMax = body.Position + halfSize

            // Calculate penetration depth on each axis
            let dx1 = newPos.X - boxMin.X // distance to left face
            let dx2 = boxMax.X - newPos.X // distance to right face
            let dz1 = newPos.Z - boxMin.Z // distance to back face
            let dz2 = boxMax.Z - newPos.Z // distance to front face

            // Push opposite to velocity direction (back the way we came)
            if
              Math.Abs(newVel.X) >= Math.Abs(newVel.Z)
              && Math.Abs(newVel.X) > Epsilon
            then
              if newVel.X > 0.0f then
                // Moving right, push back to left face
                let pen = dx1 + radius
                newPos <- newPos - Vector3(pen, 0f, 0f)
                newVel <- slideVelocity newVel (Vector3(-1f, 0f, 0f))
              else
                // Moving left, push back to right face
                let pen = dx2 + radius
                newPos <- newPos + Vector3(pen, 0f, 0f)
                newVel <- slideVelocity newVel (Vector3(1f, 0f, 0f))

              hadCollision <- true
            elif Math.Abs(newVel.Z) > Epsilon then
              if newVel.Z > 0.0f then
                // Moving forward, push back
                let pen = dz1 + radius
                newPos <- newPos - Vector3(0f, 0f, pen)
                newVel <- slideVelocity newVel (Vector3(0f, 0f, -1f))
              else
                // Moving backward, push forward
                let pen = dz2 + radius
                newPos <- newPos + Vector3(0f, 0f, pen)
                newVel <- slideVelocity newVel (Vector3(0f, 0f, 1f))

              hadCollision <- true
          // else: no significant horizontal velocity, let feet check handle vertical

          elif centerDistSq < radius * radius then
            // Center close to but outside box surface
            let centerDist = Math.Sqrt(float centerDistSq) |> float32
            let normal = centerDiff / centerDist
            let penetration = radius - centerDist

            // Only apply horizontal push (walls), skip if mostly vertical
            if Math.Abs(normal.Y) < 0.5f then
              newPos <- newPos + normal * penetration
              newVel <- slideVelocity newVel normal
              hadCollision <- true

        | _ -> ()

    struct (newPos, newVel, wasGrounded)

  let analyzeSurface(bounds: ModelBounds) =
    // Analyze the top surface of the mesh bounds
    // A flat surface has normal = Up (0, 1, 0) in Y-up coordinate system
    // We estimate from bounds - if Y is significantly different from X/Z extent
    let yExtent = bounds.HalfSize.Y
    let xzExtent = (bounds.HalfSize.X + bounds.HalfSize.Z) / 2.0f

    // Calculate ratio of Y to XZ extent
    let ratio = yExtent / (xzExtent + Epsilon)

    if ratio < 0.3f then
      // Very flat in Y - likely a flat tile (ratio < 0.3)
      Flat
    elif ratio < 1.0f then
      // Moderate slope (0.3 <= ratio < 1.0)
      // Estimated angle: arctan(xz / y)
      let estimatedAngle =
        Math.Atan(float(xzExtent / (yExtent + Epsilon))) * 180.0 / Math.PI
        |> float32

      if estimatedAngle < DefaultConfig.MaxSlopeAngleDegrees then
        Slope estimatedAngle
      else
        Steep
    else
      // Very steep or vertical (ratio >= 1.0)
      Steep

  // ============================================
  // Factory
  // ============================================

  let createConfig() = DefaultConfig
