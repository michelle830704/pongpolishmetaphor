# Reef Rally — Ocean Pong (C# Aquarium View)

A 3D underwater presentation of the supplied Pong, with orange and blue fish paddles and a pearl ball. This revision has a front-facing aquarium camera, rounded fish bodies with large 3D eyes and fins, coloured stones and coral, sand, and a moving underwater light backdrop. The style is stylized 3D rather than photorealistic. The fish, kelp and rock meshes are solid 3D adaptations of Kenney's CC0 Fish Pack artwork. Kenney's original pack is 2D; the included models have front, back and side geometry, thickness and vertex-color materials, rather than image billboards.

The included `PREVIEW.png` is an actual rendered screenshot.

## Open and play

1. Extract this ZIP fully.
2. Open **Godot 4.5 .NET** (the C# edition). Install the **.NET 8 SDK** if it is missing.
3. Import `project.godot` in this folder.
4. Allow assets to import, click **Build**, then press **F5**.
5. Connect one game controller. The left stick moves the left paddle; the right stick moves the right paddle. This is the teacher's original control scheme. Keyboard paddle controls were not added.
6. Move a paddle as the ball hits it to trigger the original smash.
7. Press **P**, or click the checkbox, to switch the entire ocean/polish layer on or off. Press **R** to restart the rally.

The ocean is created when the game runs. Its visual world is rotated upright to face the camera; the original hidden X/Z gameplay calculations remain unchanged. The editor scene initially shows the original Pong. The project does not require external asset downloads.

## Assignment implementation

| Requirement | Implementation |
|---|---|
| Same mechanics | Original movement, stick input, speeds, wall bounce, collision, smash multiplier and loss/reset calculations retained. C# changes only notify the presentation layer. |
| Runtime on/off | P or checkbox switches scenery, fish, pearl, effects, added audio, camera presentation and environment. Original geometry and camera return when off. |
| Consistent metaphor | Kenney fish, rocks and kelp adapted into 3D meshes; sand arena, pearl, bubbles and aquatic colors. |
| Ball squash/stretch | Outgoing impact direction orients a visual squash/stretch pulse which interpolates back to normal. |
| Paddle squash/stretch | Fish visual children squash/stretch after impacts. The original paddle scales used for collision never change. |
| Restart staging | Camera pulls back for a wider view at each rally restart, then smoothly returns to play framing. Gameplay continues during the presentation. |
| Smash exaggeration | Larger deformation, more bubbles, stronger particle velocity, distinct audio and camera shake. |
| Impact audio | Original synthesized watery pop sounds for wall and paddle impacts. |
| Start/stop audio | Start cue on initial serve/restart; stop cue when a point ends. No pause or new round-delay rule is added. |
| Camera following | Gentle movement toward the ball. |
| Smash shake | Brief decaying camera shake. |
| Impact particles | One-shot 3D bubble particles at paddle and wall impacts. |
| Paddle secondary action | Bubble puff in the opposite direction on movement start/reversal; subtle swimming motion. |
| Extra polish | Kelp sway, themed HUD, visual tally of the original loss events. No win condition added. |

Freeze frame is optional and is not implemented, so gameplay timing remains unchanged. R invokes the existing InitMatch method; it does not reset the visual tally.

## Files to study

- `W0041E/Laboratory1/Lab3_GameFeel_Pong/Scripts/PongLogic.cs`: original rules plus presentation notifications.
- `Ocean/OceanPolish.cs`: all theme, effects, audio, camera, UI and switch behavior.
- `Ocean/Source/PongLogic.original.txt`: untouched supplied logic for comparison.
- `Ocean/Models`: volumetric model vertex/color data loaded through SurfaceTool as ArrayMesh resources.
- `Ocean/Source`: the original selected Kenney artwork and its CC0 license.
- All gameplay, presentation and test scripts are C#. No GDScript or Python scripts are included. Models and sounds are already supplied.

## Validation

- C# project compiled successfully with Godot.NET.Sdk 4.5.0 and .NET 8.
- Actual game scene ran headlessly without runtime errors.
- Integration checks passed for source transforms, on/off restoration, restart notification and smash effect triggering.
- Model generation and effect lifecycle checks passed.
- Removing the added presentation-only C# lines reproduces the original logic, ignoring whitespace.
- The revised aquarium scene was rendered with the Compatibility renderer and its screenshot inspected.
- A controller playtest and listening check still need to be completed on your computer. Automated checks do not assess game feel or sound quality.

Optional checks using Godot .NET from a terminal:

```
godot --headless --path . res://Ocean/Tests/ProjectCheck.tscn
```

## Before submission

Complete `PLAYTEST_NOTES.txt` after an actual playtest. Push this project to your repository and add your repository URL, Git username and final commit hash to those notes. The original ZIP's commit is not the commit for this modified project.

Asset credits and modification details are in `ASSET_CREDITS.txt`.
