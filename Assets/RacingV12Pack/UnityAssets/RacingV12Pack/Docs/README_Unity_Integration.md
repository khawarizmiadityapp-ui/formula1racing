# APEX V12 — Unity Racing Asset Pack (Prototype V1)

An original, procedural, stylized racing-game starter pack: a generic single-seater formula car, fictional circuit, trackside props, synthesized V12-turbo-inspired audio, and basic Unity scripts. It is not affiliated with Formula 1 or a real racing team.

## What is included
- `Models/Apex_V12_Formula_Car.obj` + `.mtl` — low-poly formula chassis/body, wings, cockpit, halo, helmet, suspension arms and diffuser. Wheels are separate for animation.
- `Models/Apex_Formula_Wheel_Assembly.obj` + `.mtl` — one wheel/rim assembly centered at local origin; duplicate it four times under the chassis.
- `Models/Apex_Circuit_Road.obj` + `.mtl` — main asphalt loop and connected pit lane, separated as the road collider mesh.
- `Models/Apex_Circuit_Details.obj` + `.mtl` — curbs, modular barriers, start/finish checker stripe and tire stacks.
- `Models/Apex_Circuit_Environment.obj` + `.mtl` — pit garage, grandstand, start-light gantry, trees and light poles.
- `Models/Apex_Circuit_Ground.obj` + `.mtl` — separate grass ground plane.
- `Audio/` — three engine loops (idle / mid / high), rev-up, gear-shift, turbo wastegate, and tire-scrub one-shots.
- `Scripts/FormulaCarArcadeController.cs` — basic keyboard arcade driving.
- `Scripts/FormulaWheelVisuals.cs` — simple visual wheel rotation / front steering.
- `Scripts/V12EngineAudio.cs` — basic engine-layer crossfade and effects.
- `Scripts/RacingCameraFollow.cs` — smooth chase camera.
- `Docs/ASSET_MANIFEST.csv` — full file list.

## Import and assemble in Unity
1. Extract the ZIP.
2. Copy `UnityAssets/RacingV12Pack` into your Unity project's `Assets/` folder (keep Models, Audio, Scripts and Docs together).
3. Select each `.obj` model and start with import scale `1`. The meshes use Y-up and an approximate scale of one Unity unit per meter. Keep each `.mtl` beside its OBJ; depending on Unity version/render pipeline, you may need to extract or recreate materials with Standard / URP Lit shaders.
4. Drag `Apex_Circuit_Ground`, `Apex_Circuit_Road`, `Apex_Circuit_Details` and `Apex_Circuit_Environment` into the scene. Keep their transforms at `(0,0,0)` because they share one world layout.
5. Add a **MeshCollider** to `Apex_Circuit_Road` and assign its imported mesh. For a static road, leave the collider non-convex. Add colliders to details only where you need collision. Never put a non-convex MeshCollider on the Rigidbody car.
6. Drag `Apex_V12_Formula_Car` into the scene. Add a Rigidbody and a simple BoxCollider (or a few primitive colliders) to the root. The chassis front faces `+Z`. Place its root near Y=`0.02` so the wheel bottoms sit on the road.
7. Add four child objects using `Apex_Formula_Wheel_Assembly` as the mesh, scale `(1,1,1)`: FrontLeft `(-1.02, 0.40, 1.46)`, FrontRight `(1.02, 0.40, 1.46)`, RearLeft `(-1.02, 0.40, -1.48)`, RearRight `(1.02, 0.40, -1.48)`. Add `FormulaWheelVisuals` to the car root and assign all four transforms. These are visual wheels; use simplified colliders if needed for your physics design.
8. Add `FormulaCarArcadeController` to the car root. Controls use Unity's classic Input Manager: `W/S` or up/down arrows to accelerate/reverse, `A/D` or left/right arrows to steer, `Space` to brake. If your project is set to the new Input System only, go to **Edit > Project Settings > Player > Active Input Handling** and select **Both** or **Input Manager (Old)**.
9. For engine audio, create four AudioSources on the car root: Idle, Mid, High and Effects. Assign the three engine loop WAVs to the first three sources. Assign shift and wastegate clips in the `V12EngineAudio` component; assign each source reference in the Inspector. The script starts loop playback. Set 3D Spatial Blend to `1` for positional audio and tune volume levels to taste.
10. Add a Camera, attach `RacingCameraFollow` to it, and assign the car root to `target`. Tune the offset in the Inspector.
11. Press Play and tune Rigidbody mass, acceleration, grip, audio levels and camera offset for your game feel. The scripts are starter examples, not a full racing-physics simulation.

## Audio note
The WAV files are original **procedurally synthesized placeholders**, intended to evoke the high-revving character of a multi-cylinder turbo racing engine. They are **not real F1/V12 recordings** and are not an authentic motorsport sound library. Use them to prototype the game and replace them with licensed recordings or your own foley later if desired.

## Prototype limitations
- The car and environment use low-poly procedural geometry. UV maps, PBR texture sets, LODs, detailed suspension animation, full tire physics, race rules, HUD, menus, and a production camera rig are not included.
- Wheel spin and front steering scripts are basic visual approximations, not a tire / suspension simulation.
- Trackside barrier and structure meshes are visual starter geometry; build collision shapes deliberately in Unity.
- The circuit is fictional and compact, not a reproduction of a real-world track.
- OBJ/MTL materials are basic and may need recreating with Unity Standard / URP Lit shaders.

## License
The original generated files may be used and modified in personal or commercial projects. No real team marks, manufacturer logos, official F1 branding, or third-party audio recordings are included. Provided as-is without warranty.
