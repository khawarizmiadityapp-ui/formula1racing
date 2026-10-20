# APEX V12 — Baku-Inspired Street Circuit Asset Pack

Procedural low-poly OBJ/MTL assets for Unity 6. The layout is inspired by the character of a waterfront street circuit and tight old-city corners in Baku. It is a fictional playable layout, not an official survey-accurate or FIA-approved replica.

## Contents

- `Baku_StreetCircuit_Road.obj` — main continuous asphalt ribbon, about 16 m wide.
- `Baku_StreetCircuit_Curbs.obj` — alternating red/white curb strips.
- `Baku_StreetCircuit_Markings.obj` — edge lines, starting-grid slots, checkered start/finish stripe, pit lane markings.
- `Baku_StreetCircuit_Barriers.obj` — segmented impact barriers with concrete / dark / turquoise panels.
- `Baku_StreetCircuit_PitLane.obj` — connected pit lane ribbon beside the main straight.
- `Baku_StreetCircuit_Ground.obj` — surrounding land plane.
- `Baku_StreetCircuit_Water.obj` and `Baku_StreetCircuit_Promenade.obj` — waterfront environment.
- `Baku_StreetCircuit_Environment.obj` — pit building, garage doors, grandstands, city buildings, windows, lighting masts, and old-city-inspired landmark.
- `Baku_StreetCircuit_StartGantry.obj` — start/finish structure.
- `Baku_StreetCircuit_Route.json` — control points, approximate length and setup metadata.
- `Editor/BakuCircuitSetup.cs` — one-click scene setup utility.

## Install in Unity 6

1. Unzip the package.
2. Copy the entire `BakuCircuitPack` folder into the project's `Assets/` folder. The resulting location should be `Assets/BakuCircuitPack/`. Do not copy only the OBJ files; keep the `Editor` folder and `Models` folder together.
3. Wait for Unity to finish importing the OBJ/MTL files. If materials do not appear, select the model and use the Materials section in the Model Importer to import materials; reassign compatible URP/Lit materials if your render pipeline needs them.
4. Open the target scene. In the top menu, choose **APEX V12 > Build Baku-Inspired Circuit**.
5. The utility creates a parent named `APEX_BakuCircuit_Generated` at world origin and assigns static MeshColliders to road, pit lane, ground, curb, and barrier models as appropriate.
6. Inspect the scene from above. The main straight runs approximately along +X at Z = -500. Place the player car around `( -340, 0.4, -500 )`, facing +X (Unity yaw approximately 90 degrees if the car model's forward axis is +Z). Confirm the actual model's forward axis before final placement.
7. Disable the earlier duplicate circuit objects if the scene already contains a track; avoid overlapping road meshes and colliders. Save the scene.

## Important integration notes

- Imported OBJ model scale assumes approximate meters and Y-up. Keep model transforms at local position `(0,0,0)`, rotation `(0,0,0)`, scale `(1,1,1)` unless inspection proves the importer axis conversion requires adjustment.
- Road collider belongs on the road mesh only. Markings are visual overlays, not drive surfaces.
- Generated barriers are static meshes. Do not attach Rigidbody components to non-convex MeshColliders on track environment objects.
- If collision is expensive on target Android hardware, keep the main road as one MeshCollider and replace detailed barrier meshes with a simpler collider proxy.
- The included installer is an Editor script. It appears only after Unity compiles the script successfully.
- This pack contains geometry and material definitions only; it does not include a fully baked lighting setup or a ready-made Scene asset.

## Suggested player-car start

- Position: `( -340, 0.45, -500 )`
- Main straight direction: `+X`
- Track width: about `16 m` plus curbs
- Approximate generated centerline length: see `Models/Baku_StreetCircuit_Route.json`

## Troubleshooting

**Pink models:** assign URP/Lit materials in the material slots; do not change the project's render pipeline merely to correct material colors.

**Car falls through road:** verify the imported road model has a MeshFilter and a MeshCollider, and ensure the car's Rigidbody begins above the road.

**Existing road overlaps:** disable or remove duplicate previous track meshes before driving.

**Installer menu missing:** check the Console for Editor-script compilation errors and confirm the folder is `Assets/BakuCircuitPack/Editor/BakuCircuitSetup.cs`.
