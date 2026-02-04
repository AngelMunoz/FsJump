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

  let DefaultConfig = {
    Gravity = Vector3(0.0f, -900.0f, 0.0f)
    MoveSpeed = 200.0f
    JumpVelocity = 400.0f
    MinJumpVelocity = 200.0f
    MaxSlopeAngleDegrees = 45.0f
    GroundCheckDistance = 0.1f
    Friction = 0.8f
    PlayerRadius = 16.0f
    PlayerHeight = 64.0f
  }

  // ============================================
  // Internal Helpers
  // ============================================

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
    if
      jumpRequested
      && player.IsGrounded
      && player.GroundNormal.Y > MaxSlopeAngleCos
    then
      Vector3(player.Velocity.X, config.JumpVelocity, 0.0f)
    else
      player.Velocity

  let tryCutJump
    (velocity: Vector3)
    (jumpReleased: bool)
    (minJumpVelocity: float32)
    =
    if jumpReleased && velocity.Y > minJumpVelocity then
      Vector3(velocity.X, minJumpVelocity, 0.0f)
    else
      velocity

  let checkGrounded
    (config: PhysicsConfig)
    (playerPos: Vector3)
    (staticBodies: PhysicsBody[])
    =
    let rayOrigin =
      playerPos
      - Vector3(0.0f, config.PlayerHeight / 2.0f - config.PlayerRadius, 0.0f)

    let rayDir = Vector3.Down
    let checkDist = config.PlayerRadius + config.GroundCheckDistance

    let mutable closestHit = Single.MaxValue
    let mutable groundY = Single.MinValue
    let mutable groundNormal = Vector3.Up
    let mutable foundGround = false

    for body in staticBodies do
      match body.Shape with
      | Box size ->
        // Simple raycast down against box top surface
        let boxTop = body.Position.Y + size.Y / 2.0f

        match raycastPlane rayOrigin rayDir boxTop with
        | Some(hitPoint, t) when t <= checkDist ->
          if intersectsBox hitPoint body.Position size then
            if t < closestHit then
              closestHit <- t
              groundY <- boxTop
              // Estimate normal based on hit position relative to box
              let closest = closestPointOnBox hitPoint body.Position size
              let toCenter = body.Position - hitPoint

              let normal =
                if toCenter.LengthSquared() > Epsilon then
                  let n =
                    Vector3(toCenter.X, Math.Max(0.0f, toCenter.Y), toCenter.Z)

                  if n.LengthSquared() > Epsilon then
                    Vector3.Normalize(n)
                  else
                    Vector3.Up
                else
                  Vector3.Up

              groundNormal <- normal
              foundGround <- true
        | _ -> ()
      | _ -> ()

    let slopeAngle =
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
          // Treat capsule as sphere for broad collision check
          let closest = closestPointOnBox newPos body.Position size
          let diff = newPos - closest
          let distSq = diff.LengthSquared()

          // Collision when inside or touching the box
          if distSq < radius * radius then
            let dist = Math.Sqrt(float distSq) |> float32

            // Handle case when center is inside box (dist ~ 0)
            let normal, penetration, fromInside =
              if dist < Epsilon then
                // Inside box - push up and use velocity direction to determine normal
                let pushDir =
                  if newVel.LengthSquared() > Epsilon then
                    -Vector3.Normalize(newVel)
                  else
                    Vector3.Up

                pushDir, radius, true
              else
                let n = diff / dist
                n, radius - dist, false

            // Push out of collision
            newPos <- newPos + normal * penetration

            // Slide velocity along surface
            newVel <- slideVelocity newVel normal

            // Check if this is ground contact - but NOT if we were inside the box
            // (that means we spawned inside geometry, not standing on it)
            if not fromInside && normal.Y > MaxSlopeAngleCos then
              wasGrounded <- true

            hadCollision <- true
        | _ -> ()

    struct (newPos, newVel, wasGrounded)

  let analyzeSurface(bounds: ModelBounds) =
    // Analyze the top surface of the mesh bounds
    // A flat surface has normal = Up (0, 1, 0)
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
