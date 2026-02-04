namespace FsJump.Core

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
  val DefaultConfig: PhysicsConfig
  
  // Direction constants for Y-up coordinate system
  val Up: Vector3    // (0, 1, 0) - upward in Y-up coordinates
  val Down: Vector3  // (0, -1, 0) - downward in Y-up coordinates

  // Intersection helpers
  val intersectsBox: point: Vector3 -> boxPos: Vector3 -> boxSize: Vector3 -> bool

  // Movement & forces
  val applyGravity:
    config: PhysicsConfig -> velocity: Vector3 -> dt: float32 -> Vector3

  val applyMovement:
    config: PhysicsConfig ->
    horizontalInput: float32 ->
    velocity: Vector3 ->
      Vector3

  val tryJump:
    config: PhysicsConfig ->
    player: PlayerState ->
    jumpRequested: bool ->
      Vector3

  val tryCutJump:
    velocity: Vector3 ->
    jumpReleased: bool ->
    minJumpVelocity: float32 ->
      Vector3

  // Ground detection
  val checkGrounded:
    config: PhysicsConfig ->
    playerPos: Vector3 ->
    staticBodies: PhysicsBody[] ->
      GroundInfo

  // Collision & movement
  val moveAndSlide:
    config: PhysicsConfig ->
    player: PlayerState ->
    staticBodies: PhysicsBody[] ->
    dt: float32 ->
      struct (Vector3 * Vector3 * bool) // newPos, newVel, wasGrounded

  // Surface analysis
  val analyzeSurface: bounds: ModelBounds -> SurfaceType

  // Factory
  val createConfig: unit -> PhysicsConfig
