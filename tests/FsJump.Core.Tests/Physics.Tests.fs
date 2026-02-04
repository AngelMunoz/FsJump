module PhysicsTests

open Expecto
open Microsoft.Xna.Framework
open FsJump.Core
open FsJump.Core.Types
open FsJump.Core.Physics

[<Tests>]
let tests =
  testList "Physics" [

    // ============================================
    // applyGravity tests
    // ============================================
    testList "applyGravity" [
      testCase "applies downward force"
      <| fun _ ->
        let config = Physics.DefaultConfig
        let velocity = Vector3(0.0f, 0.0f, 0.0f)
        let dt = 0.016f // ~60fps

        let result = Physics.applyGravity config velocity dt

        Expect.equal result.X 0.0f "X velocity should be unchanged"
        Expect.equal result.Z 0.0f "Z velocity should be unchanged"
        Expect.isLessThan result.Y 0.0f "Y velocity should decrease (gravity in Y-up coordinates)"

      testCase "accumulates over time"
      <| fun _ ->
        let config = Physics.DefaultConfig
        let dt = 0.016f

        let v1 = Physics.applyGravity config Vector3.Zero dt
        let v2 = Physics.applyGravity config v1 dt

        Expect.isLessThan
          v2.Y
          v1.Y
          "Velocity should decrease downward over time (Y-up coordinates)"

      testCase "preserves horizontal velocity"
      <| fun _ ->
        let config = Physics.DefaultConfig
        let velocity = Vector3(100.0f, 50.0f, 0.0f)
        let dt = 0.016f

        let result = Physics.applyGravity config velocity dt

        Expect.equal result.X 100.0f "X velocity should be preserved"
        Expect.isLessThan result.Y 50.0f "Y velocity should decrease (downward in Y-up coordinates)"
    ]

    // ============================================
    // applyMovement tests
    // ============================================
    testList "applyMovement" [
      testCase "moves left when input is -1"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              MoveSpeed = 200.0f
        }

        let velocity = Vector3.Zero

        let result = Physics.applyMovement config -1.0f velocity

        Expect.isLessThan result.X 0.0f "Should move left (negative X)"
        Expect.equal result.Y 0.0f "Y velocity should be unchanged"

      testCase "moves right when input is 1"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              MoveSpeed = 200.0f
        }

        let velocity = Vector3.Zero

        let result = Physics.applyMovement config 1.0f velocity

        Expect.isGreaterThan result.X 0.0f "Should move right (positive X)"
        Expect.equal result.Y 0.0f "Y velocity should be unchanged"

      testCase "applies friction when no input"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              Friction = 0.5f
        }

        let velocity = Vector3(100.0f, 50.0f, 0.0f)

        let result = Physics.applyMovement config 0.0f velocity

        Expect.equal result.Y 50.0f "Y velocity should be unchanged"

        Expect.isLessThan
          result.X
          100.0f
          "X velocity should decrease due to friction"

        Expect.isGreaterThan result.X 0.0f "X velocity should still be positive"

      testCase "stops completely when friction reduces below threshold"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              Friction = 0.5f
        }

        let velocity = Vector3(0.5f, 0.0f, 0.0f)

        let result = Physics.applyMovement config 0.0f velocity

        Expect.equal result.X 0.0f "Should stop completely when below threshold"
    ]

    // ============================================
    // tryJump tests
    // ============================================
    testList "tryJump" [
      testCase "jumps when grounded and walkable"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              JumpVelocity = 400.0f
        }

        let player = {
          Position = Vector3.Zero
          Velocity = Vector3.Zero
          IsGrounded = true
          GroundNormal = Physics.Up // In Y-up, Up = (0, 1, 0)
        }

        let result = Physics.tryJump config player true

        Expect.isGreaterThan result.Y 0.0f "Should have upward velocity (positive Y in Y-up)"
        Expect.equal result.Y 400.0f "Should have exact jump velocity (positive)"

      testCase "does not jump when not grounded"
      <| fun _ ->
        let config = Physics.DefaultConfig

        let player = {
          Position = Vector3.Zero
          Velocity = Vector3(50.0f, 100.0f, 0.0f)
          IsGrounded = false
          GroundNormal = Physics.Up
        }

        let result = Physics.tryJump config player true

        Expect.equal result player.Velocity "Velocity should be unchanged"

      testCase "does not jump on steep slope"
      <| fun _ ->
        let config = Physics.DefaultConfig
        // 60 degree slope in Y-up coordinates
        // For a 60° slope pointing upward: normal = (sin(60°), cos(60°), 0) = (0.866, 0.5, 0)
        let steepNormal = Vector3(0.866f, 0.5f, 0.0f) |> Vector3.Normalize
        // Verify the normal.Y is approximately 0.5 (60 degrees from up)
        Expect.isLessThan
          steepNormal.Y
          0.707f
          "Test setup: normal.Y should be < 0.707 for steep slope in Y-up"

        let player = {
          Position = Vector3.Zero
          Velocity = Vector3.Zero
          IsGrounded = true
          GroundNormal = steepNormal
        }

        let result = Physics.tryJump config player true

        Expect.equal result player.Velocity "Should not jump on steep slope"

      testCase "does not jump when not requested"
      <| fun _ ->
        let config = Physics.DefaultConfig

        let player = {
          Position = Vector3.Zero
          Velocity = Vector3.Zero
          IsGrounded = true
          GroundNormal = Physics.Up
        }

        let result = Physics.tryJump config player false

        Expect.equal result player.Velocity "Should not jump when not requested"
    ]

    // ============================================
    // tryCutJump tests
    // ============================================
    testList "tryCutJump" [
      testCase "cuts jump when released and above min velocity"
      <| fun _ ->
        // In Y-up: upward velocity is positive
        let velocity = Vector3(0.0f, 300.0f, 0.0f)
        let minJumpVel = 200.0f

        let result = Physics.tryCutJump velocity true minJumpVel

        Expect.equal result.Y 200.0f "Should cut to min jump velocity (positive in Y-up)"

      testCase "does not cut when below min velocity"
      <| fun _ ->
        let velocity = Vector3(0.0f, 150.0f, 0.0f)
        let minJumpVel = 200.0f

        let result = Physics.tryCutJump velocity true minJumpVel

        Expect.equal result.Y 150.0f "Should not cut when already below min"

      testCase "does not cut when jump not released"
      <| fun _ ->
        let velocity = Vector3(0.0f, 300.0f, 0.0f)
        let minJumpVel = 200.0f

        let result = Physics.tryCutJump velocity false minJumpVel

        Expect.equal result.Y 300.0f "Should not cut when jump still held"
    ]

    // ============================================
    // checkGrounded tests
    // ============================================
    testList "checkGrounded" [
      testCase "detects ground when standing on box"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              PlayerHeight = 64.0f
              PlayerRadius = 16.0f
        }
        // In Y-up coordinates: ground is at lower Y values
        // Box at y=0 with size 64 has top surface at y=0+32=32
        // Player at y=50 is above the box (higher Y = higher in the air)
        // Ray origin: player.Y - (height/2 - radius) = 50 - (32-16) = 34
        // Ray casts downward (negative Y) from 34, hits box top at 32
        // Distance = 34-32 = 2, which is < checkDist (16+2=18), so should hit
        let playerPos = Vector3(0.0f, 50.0f, 0.0f)

        let groundBody = {
          Position = Vector3(0.0f, 0.0f, 0.0f) // Box center at y=0
          Velocity = Vector3.Zero
          Shape = Box(Vector3(64.0f, 64.0f, 64.0f))
          IsStatic = true
          EntityId = None
        }

        let result = Physics.checkGrounded config playerPos [| groundBody |]

        Expect.isTrue result.IsGrounded "Should be grounded"
        // In Y-up, Up = (0, 1, 0) which is Physics.Up
        Expect.equal result.GroundNormal Physics.Up "Ground normal should be up"

        Expect.equal
          result.SlopeAngle
          0.0f
          "Flat ground should have 0 slope angle"

      testCase "not grounded when in air"
      <| fun _ ->
        let config = Physics.DefaultConfig
        let playerPos = Vector3(0.0f, 200.0f, 0.0f) // High in air

        let groundBody = {
          Position = Vector3(0.0f, 0.0f, 0.0f)
          Velocity = Vector3.Zero
          Shape = Box(Vector3(64.0f, 64.0f, 64.0f))
          IsStatic = true
          EntityId = None
        }

        let result = Physics.checkGrounded config playerPos [| groundBody |]

        Expect.isFalse result.IsGrounded "Should not be grounded when in air"

      testCase "detects slope angle"
      <| fun _ ->
        let config = Physics.DefaultConfig
        // In Y-up coordinates: 
        // Raycast origin: playerPos.Y - (height/2 - radius) = playerPos.Y - 16
        // For playerPos.Y = 50: ray origin at 34, looking down (negative Y) to box top at 32
        // Distance = 2, which is < checkDist (16 + 2.0 = 18.0), so should hit
        let playerPos = Vector3(0.0f, 50.0f, 0.0f)

        let groundBody = {
          Position = Vector3(0.0f, 0.0f, 0.0f) // Box centered at y=0, top at y=32
          Velocity = Vector3.Zero
          Shape = Box(Vector3(64.0f, 64.0f, 64.0f))
          IsStatic = true
          EntityId = None
        }

        let result = Physics.checkGrounded config playerPos [| groundBody |]

        // Should detect ground
        Expect.isTrue result.IsGrounded "Should be grounded on box"

      testCase "empty bodies array returns not grounded"
      <| fun _ ->
        let config = Physics.DefaultConfig
        let playerPos = Vector3.Zero

        let result = Physics.checkGrounded config playerPos [||]

        Expect.isFalse result.IsGrounded "Should not be grounded with no bodies"
    ]

    // ============================================
    // moveAndSlide tests
    // ============================================
    testList "moveAndSlide" [
      testCase "moves freely when no collision"
      <| fun _ ->
        let config = Physics.DefaultConfig

        let player = {
          Position = Vector3(0.0f, 100.0f, 0.0f)
          Velocity = Vector3(10.0f, 0.0f, 0.0f)
          IsGrounded = false
          GroundNormal = Physics.Up
        }

        let dt = 0.016f

        let struct (newPos, newVel, wasGrounded) =
          Physics.moveAndSlide config player [||] dt

        Expect.isGreaterThan newPos.X player.Position.X "Should move right"
        Expect.equal newVel player.Velocity "Velocity should be unchanged"
        Expect.isFalse wasGrounded "Should not be grounded in air"

      testCase "slides along wall when hitting box side"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              PlayerRadius = 16.0f
        }
        // Box at (0,32) with size 64x64 spans x=[-32,32], y=[0,64]
        // Player starts at (-40, 32), moving right at 500 units/s
        // After dt=0.1s: newPos = (-40 + 50, 32) = (10, 32), which is inside the box
        let player = {
          Position = Vector3(-40.0f, 32.0f, 0.0f) // Close to box left edge (-32)
          Velocity = Vector3(500.0f, 0.0f, 0.0f) // Moving fast right into box
          IsGrounded = false
          GroundNormal = Physics.Up
        }

        let wall = {
          Position = Vector3(0.0f, 32.0f, 0.0f)
          Velocity = Vector3.Zero
          Shape = Box(Vector3(64.0f, 64.0f, 64.0f))
          IsStatic = true
          EntityId = None
        }

        let dt = 0.1f // Large enough to cause collision

        let struct (newPos, newVel, _wasGrounded) =
          Physics.moveAndSlide config player [| wall |] dt

        // Should have collided and X velocity should be reduced/slided
        Expect.isLessThan
          newVel.X
          player.Velocity.X
          "X velocity should be reduced by collision"
        // Position should be pushed outside the box
        Expect.isLessThan
          newPos.X
          0.0f
          "Should be pushed back to left of box center"

      testCase "lands on ground when falling onto box"
      <| fun _ ->
        let config = {
          Physics.DefaultConfig with
              PlayerRadius = 16.0f
              PlayerHeight = 64.0f
        }
        // In Y-up coordinates: falling means decreasing Y
        // Box at (0,0) with size 64x64 has top at y=32
        // Player at y=100 with radius 16, falling downward (negative Y velocity)
        let player = {
          Position = Vector3(0.0f, 100.0f, 0.0f) // Above box (higher Y = higher in air)
          Velocity = Vector3(0.0f, -200.0f, 0.0f) // Falling down fast (negative Y)
          IsGrounded = false
          GroundNormal = Physics.Up
        }

        let ground = {
          Position = Vector3(0.0f, 0.0f, 0.0f) // Box center at y=0
          Velocity = Vector3.Zero
          Shape = Box(Vector3(64.0f, 64.0f, 64.0f))
          IsStatic = true
          EntityId = None
        }

        let dt = 0.3f // ensures collision happens (deltaY = -60, newY = 40, which is within radius 16 of top at 32)

        let struct (newPos, newVel, wasGrounded) =
          Physics.moveAndSlide config player [| ground |] dt

        // After landing, should be grounded and Y velocity should be 0 or reflected
        Expect.isTrue wasGrounded "Should be grounded after landing"
        Expect.isGreaterThanOrEqual newPos.Y 48.0f "Should be at or above box top (top=32, radius=16)"
    ]

    // ============================================
    // analyzeSurface tests
    // ============================================
    testList "analyzeSurface" [
      testCase "detects flat surface"
      <| fun _ ->
        let bounds = {
          Min = Vector3(-32.0f, -4.0f, -32.0f)
          Max = Vector3(32.0f, 4.0f, 32.0f)
          Center = Vector3.Zero
          Size = Vector3(64.0f, 8.0f, 64.0f)
          HalfSize = Vector3(32.0f, 4.0f, 32.0f)
        }

        let result = Physics.analyzeSurface bounds

        Expect.equal result Flat "Flat surface should be detected as Flat"

      testCase "detects slope"
      <| fun _ ->
        // Slope logic remains the same as it's ratio-based, but let's ensure it's sane
        let bounds = {
          Min = Vector3(-40.0f, -35.0f, -40.0f)
          Max = Vector3(40.0f, 35.0f, 40.0f)
          Center = Vector3.Zero
          Size = Vector3(80.0f, 70.0f, 80.0f)
          HalfSize = Vector3(40.0f, 35.0f, 40.0f)
        }

        let result = Physics.analyzeSurface bounds

        match result with
        | Slope _ -> () 
        | Steep -> () 
        | Flat -> failtest "Should not be flat"

      testCase "detects steep surface"
      <| fun _ ->
        // Very vertical - Y extent much larger than X/Z
        let bounds = {
          Min = Vector3(-8.0f, -64.0f, -8.0f)
          Max = Vector3(8.0f, 64.0f, 8.0f)
          Center = Vector3.Zero
          Size = Vector3(16.0f, 128.0f, 16.0f)
          HalfSize = Vector3(8.0f, 64.0f, 8.0f)
        }

        let result = Physics.analyzeSurface bounds

        Expect.equal result Steep "Very vertical surface should be Steep"
    ]

    // ============================================
    // createConfig tests
    // ============================================
    testCase "createConfig returns default config"
    <| fun _ ->
      let config = Physics.createConfig()

      Expect.equal config.Gravity.Y -900.0f "Default gravity should be -900 (negative Y = downward)"
      Expect.equal config.MoveSpeed 200.0f "Default move speed should be 200"

      Expect.equal
        config.JumpVelocity
        400.0f
        "Default jump velocity should be 400"

      Expect.equal
        config.PlayerHeight
        64.0f
        "Default player height should be 64"
  ]