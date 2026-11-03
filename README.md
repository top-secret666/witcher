# The Witcher — Unity

Dark-fantasy / retro pixel prototype. Continues the spirit of the [Java Chapter 1 prototype](https://github.com/top-secret666/the-witcher), rebuilt in **Unity 6 + URP**.

**Split of work:** scripts live in the repo; scene art, lighting polish, and level layout happen in the Unity Editor.

## Requirements

- Unity **6000.6.4f1** (Unity 6) — project was created with this editor  
- Windows  

Open the folder `D:\HUH\witcher` (or this clone) in Unity Hub → **Add** → open.

## Today: architecture-first demo

1. Open in Unity, wait for compile.
2. **Witcher → Architecture → 1. Generate Materials + Profiles + Prefabs**
3. **Witcher → Architecture → 2. Rebuild Demo Scene From Prefabs**
4. **Play ▶**

| Control | Action |
|--------|--------|
| WASD | Walk |
| Mouse | Look |
| Shift | Sprint |
| Esc | Unlock cursor |

Docs: `docs/ARCHITECTURE.md`, `docs/EDITOR_TODAY.md`

### Mood / pixel

- Atmosphere: `Assets/Witcher/Data/Atmosphere/*.asset` or **Architecture → 3**
- Pixel: `Assets/Settings/PC_Renderer` → **Retro Look**

## Layout

```
Assets/Witcher/
  Art/Materials/     shared mats
  Data/Atmosphere/   ScriptableObject moods
  Prefabs/           Castle modules, Pine, Player, Atmosphere
  Scenes/            DarkFantasy_Demo
  Scripts/           Core, Data, Environment, Player, Visuals
  Editor/            ArchitectureBuilder
  Shaders/           RetroLook
```

Namespaces: `Witcher.Core`, `Witcher.Data`, `Witcher.Environment`, `Witcher.Player`, `Witcher.Visuals`
