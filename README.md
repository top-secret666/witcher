# The Witcher — Unity

Dark-fantasy / retro pixel prototype. Continues the spirit of the [Java Chapter 1 prototype](https://github.com/top-secret666/the-witcher), rebuilt in **Unity 6 + URP**.

**Split of work:** scripts live in the repo; scene art, lighting polish, and level layout happen in the Unity Editor.

## Requirements

- Unity **6000.6.4f1** (Unity 6) — project was created with this editor  
- Windows  

Open the folder `D:\HUH\witcher` (or this clone) in Unity Hub → **Add** → open.

## Today: get a dark-fantasy look in 2 clicks

1. Open the project in Unity and wait for scripts to compile.
2. Menu bar → **Witcher → 2. Build Dark Fantasy Demo Scene**
3. Press **Play ▶**

| Control | Action |
|--------|--------|
| WASD | Walk |
| Mouse | Look |
| Shift | Sprint |
| Esc | Unlock cursor |

### Mood

Select `DarkFantasyAtmosphere` in the Hierarchy, or use:

- **Witcher → 3. Apply Mood → Castle Dusk** (purple / blood sunset)
- **Witcher → 3. Apply Mood → Manor Night** (teal moon night)

Reference stills: `Assets/Witcher/Art/Reference/`

### Pixel crunch

- **Witcher → 1. Add Retro Look To URP Renderer** (also runs automatically when building the demo)
- Tweak: `Assets/Settings/PC_Renderer` → **Retro Look** (pixel size, color steps, dither, vignette)

## Project layout

```
Assets/Witcher/
  Art/Reference/     mood images
  Editor/            one-click setup menus
  Scenes/            DarkFantasy_Demo (after menu)
  Scripts/Player/    FPS + held sword
  Scripts/Visuals/   atmosphere + retro look
  Shaders/           RetroLook.shader
```

## Notes

- Demo castle / forest are **blockout primitives** — replace with real meshes whenever you want; keep the lighting + retro pass.
- Game code package namespace: `Witcher.*`
