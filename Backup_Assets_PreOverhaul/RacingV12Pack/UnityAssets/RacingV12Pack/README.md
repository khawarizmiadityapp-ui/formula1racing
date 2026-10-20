# APEX V12 — Unity Racing Asset Pack (Prototype V1)

A procedural stylized racing-game starter pack containing a fictional circuit, original low-poly formula-style car meshes, synthesized prototype engine audio, and basic Unity C# scripts. The optional black/silver/turquoise car is **Mercedes-inspired fan-game styling**, not an official Mercedes-AMG Petronas or Formula 1 asset.

## What's included

### 3D models (`Models/`)
- `Apex_V12_Formula_Car.obj` + `.mtl` — original generic low-poly formula chassis, wings, cockpit, halo, helmet, suspension arms and diffuser.
- `Apex_Formula_Wheel_Assembly.obj` + `.mtl` — matching generic wheel assembly; import once and duplicate for the four wheel positions.
- `Mercedes_AMG_F1_Inspired_Car.obj` + `.mtl` — black body, silver mechanical accents and turquoise livery variant, based on original procedural geometry.
- `Mercedes_F1_Wheel_Assembly.obj` + `.mtl` — dark slick wheel with a turquoise hub accent.
- `Mercedes_F1_Livery_Details.obj` + `.mtl` — separate turquoise nose/wing/sidepod accents and a generic three-spoke emblem overlay. Parent this under the Mercedes-inspired car root at local position `(0, 0, 0)` and scale `(1, 1, 1)`.
- `Apex_Circuit_Road.obj` + `.mtl` — fictional asphalt circuit and connected pit lane, separated for a road collider.
- `Apex_Circuit_Details.obj` + `.mtl` — curbs, modular barriers, start/finish stripe and tire stacks.
- `Apex_Circuit_Environment.obj` + `.mtl` — pit structures, grandstand, start-light gantry, trees and light poles.
- `Apex_Circuit_Ground.obj` + `.mtl` — separate grass ground plane.

### Audio (`Audio/`)
Includes three synthesized engine loops (idle/mid/high), a rev-up effect, gear-shift click, turbo wastegate hiss and tire scrub sound. These are procedural placeholders to prototype game audio, **not authentic F1 recordings**.

### Unity scripts (`Scripts/`)
- `FormulaCarArcadeController.cs` — basic keyboard arcade driving.
- `FormulaWheelVisuals.cs` — simple visual wheel spin and front steering.
- `V12EngineAudio.cs` — basic layered engine audio blending and effects.
- `RacingCameraFollow.cs` — smooth chase-camera follow.

## Import and assemble in Unity

1. Extract the ZIP.
2. Copy `UnityAssets/RacingV12Pack` into your Unity project's `Assets/` folder. Keep `Models`, `Audio`, `Scripts` and `Docs` together.
3. Select each `.obj` and begin with import scale `1`. Coordinates use Y-up and approximate one Unity unit per metre. Keep each `.mtl` alongside its OBJ. Depending on Unity version and render pipeline, you may need to create/extract materials manually and assign Standard or URP/Lit shaders.
4. Drag `Apex_Circuit_Ground`, `Apex_Circuit_Road`, `Apex_Circuit_Details` and `Apex_Circuit_Environment` into the scene. Keep transforms at `(0, 0, 0)` because the meshes share one layout.
5. Add a **MeshCollider** to `Apex_Circuit_Road` and assign the imported road mesh. As the road is static, leave this collider non-convex. Add colliders to barriers only where needed. Do not use a non-convex MeshCollider on a car Rigidbody.
6. Choose one car chassis. For the Mercedes-inspired version, drag `Mercedes_AMG_F1_Inspired_Car` into the scene, then parent `Mercedes_F1_Livery_Details` underneath it at local position `(0, 0, 0)`, rotation `(0, 0, 0)` and scale `(1, 1, 1)`. The chassis front faces `+Z`. Add a Rigidbody and a simple BoxCollider (or a few primitive colliders) to the car root. Place its root near Y=`0.02` and verify the wheel height against the road.
7. Add four wheel child objects using `Mercedes_F1_Wheel_Assembly` for the Mercedes-inspired car, or `Apex_Formula_Wheel_Assembly` for the generic car. Set scale `(1, 1, 1)` and positions: FrontLeft `(-1.02, 0.40, 1.46)`, FrontRight `(1.02, 0.40, 1.46)`, RearLeft `(-1.02, 0.40, -1.48)`, RearRight `(1.02, 0.40, -1.48)`. Add `FormulaWheelVisuals` to the car root and assign all four wheel transforms. These are visual wheels; use simple colliders if wheel physics is required.
8. Add `FormulaCarArcadeController` to the car root. Default controls: `W/S` or Up/Down to accelerate/reverse, `A/D` or Left/Right to steer, and `Space` to brake. If using the new Input System only, set **Active Input Handling** to **Both** or **Input Manager (Old)** in Player settings.
9. For engine audio, create four AudioSources on the car root: Idle, Mid, High and Effects. Assign the three loop clips to the first three sources and wire those sources into `V12EngineAudio`. Assign the gear-shift and turbo wastegate clips as effects. Use Spatial Blend `1` for positional sound and tune volume by ear.
10. Add a Camera, attach `RacingCameraFollow`, and set the car root as its `target`.
11. Press Play. Tune Rigidbody mass, acceleration, grip, audio levels and camera offset to match the handling feel you want.

## Prototype limitations

The models are low-poly starter geometry, not production CAD or a scan of a real car. UV/PBR texture maps, detailed suspension animation, full tire physics, damage, race rules, HUD, menus and production-ready camera tuning are not included. Wheel spin/steering are visual approximations. Trackside meshes are mostly visual, so add collision shapes deliberately. OBJ/MTL material import may need manual setup in Unity. The circuit is fictional and is not a reproduction of a real venue.

## Use and branding

The included procedural meshes and generated placeholder audio may be modified for personal or commercial game projects. The Mercedes-inspired variant is an unofficial fan-game style reference, not endorsed by or affiliated with Mercedes-Benz, Mercedes-AMG Petronas, or Formula 1. No official team mark or recorded third-party engine audio is included.
