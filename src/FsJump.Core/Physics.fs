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

  // In Y-down coordinates, "up" is negative Y
  let Up = Vector3(0.0f, -1.0f, 0.0f)
  let Down = Vector3(0.0f, 1.0f, 0.0f)

  let DefaultConfig = {
    Gravity = Vector3(0.0f, 900.0f, 0.0f)  // Positive Y because Y increases downward (Tiled style)
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

  // In our coordinate system, Y increases downward (like Tiled)
  // So "bottom" means higher Y value
  let getCapsuleBottom (position: Vector3) (height: float32) =
    position + Vector3(0.0f, height / 2.0f, 0.0f)

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
    // In Y-down coordinates, walkable surfaces have normals pointing up (negative Y)
    normal.Y <= -maxAngleCos

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
    // In Y-down coordinates, walkable surfaces have normals pointing up (negative Y)
    if
      jumpRequested
      && player.IsGrounded
      && player.GroundNormal.Y < -MaxSlopeAngleCos
    then
      Vector3(player.Velocity.X, -config.JumpVelocity, 0.0f) // Negative for upward in Y-down
    else
      player.Velocity

  let tryCutJump
    (velocity: Vector3)
    (jumpReleased: bool)
    (minJumpVelocity: float32)
    =
    // In Y-down coordinates, upward velocity is negative
    // Cut if moving up faster than min (velocity.Y < -minJumpVelocity)
    if jumpReleased && velocity.Y < -minJumpVelocity then
      Vector3(velocity.X, -minJumpVelocity, 0.0f)
    else
      velocity

  let checkGrounded
    (config: PhysicsConfig)
    (playerPos: Vector3)
    (staticBodies: PhysicsBody[])
    =
    // In Y-down coordinates, the "bottom" of the capsule is at higher Y
    // Ray starts near the bottom of the player and casts downward (positive Y)
    let rayOrigin =
      playerPos
      + Vector3(0.0f, config.PlayerHeight / 2.0f - config.PlayerRadius, 0.0f)

    let rayDir = Vector3(0.0f, 1.0f, 0.0f) // Downward in Y-down coordinate system
    let checkDist = config.PlayerRadius + config.GroundCheckDistance



    let mutable closestHit = Single.MaxValue
    let mutable groundY = Single.MinValue
    let mutable groundNormal = Vector3.Up
    let mutable foundGround = false

    for body in staticBodies do
      match body.Shape with
      | Box size ->
        // In Y-down coordinates, "top" surface is at minimum Y (facing upward)
        let boxTop = body.Position.Y - size.Y / 2.0f

        match raycastPlane rayOrigin rayDir boxTop with
        | Some(hitPoint, t) when t <= checkDist ->
          if intersectsBox hitPoint body.Position size then
            if t < closestHit then
              closestHit <- t
              groundY <- boxTop

              // In Y-down, the normal pointing UP is (0, -1, 0)
              // The hit is on the top surface, so the normal should point up (negative Y)
              let normal =
                // If hit is on the top surface (min Y of box), normal points up (negative Y)
                if Math.Abs(hitPoint.Y - boxTop) < Epsilon then
                  Vector3(0.0f, -1.0f, 0.0f) // Up in Y-down coordinates
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
      // Angle from UP direction. In Y-down, UP is (0,-1,0), so we use -normal.Y
      let angleRad = Math.Acos(float(Math.Clamp(-groundNormal.Y, -1.0f, 1.0f)))
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
                // Inside box - determine push direction based on velocity or default to up
                // Don't use negated velocity if it's mostly vertical (can shoot player up/down)
                let pushDir =
                  if newVel.LengthSquared() > Epsilon then
                    let velNorm = Vector3.Normalize(newVel)
                    // If velocity is mostly horizontal, push opposite to it
                    // If mostly vertical, push up (negative Y in Y-down)
                    if Math.Abs(velNorm.X) > Math.Abs(velNorm.Y) then
                      -velNorm
                    else
                      Up
                  else
                    Up

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
            // In Y-down coordinates, "up" is negative Y, so check for normal.Y < -cos(angle)
            if not fromInside && normal.Y < -MaxSlopeAngleCos then
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
