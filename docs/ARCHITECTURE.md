# Архитектура сцены (Unity)

Даже маленький блок-аут замка собираем **как настоящий Unity-проект**, а не кучу кубов в корне Hierarchy.

## Принципы

1. **Данные ≠ логика** — цвета/туман/свет в `ScriptableObject` (`AtmosphereProfile`), не в `if (mood == …)`.
2. **Префабы ≠ сцена** — куски замка живут в `Prefabs/`, сцена только **инстансит** их.
3. **Иерархия по ролям** — пустые корни-организаторы, без мешей у корня сцены.
4. **Один объект — одна ответственность** — Atmosphere не на замке, меч не в Environment.
5. **Материалы — ассеты** — `Art/Materials/*.mat`, не `new Material()` на лету в сцене.

## Hierarchy (канон)

```
_Systems
  Lighting
    Directional Light
  Atmosphere          ← DarkFantasyAtmosphere + AtmosphereProfile
_Environment
  Ground
    PF_Ground
    PF_Path
  Forest
    PF_Pine (×N)
  Castle             ← PF_Castle (составной префаб) + CastleLandmark
_Gameplay
  Player             ← PF_Player (камера + View_SilverSword)
```

## Папки

```
Assets/Witcher/
  Art/Materials/          shared materials
  Art/Reference/          mood refs
  Data/Atmosphere/        ScriptableObjects
  Prefabs/
    Environment/Castle/   PF_CastleTower, Keep, Wing, Roof, Gate, Stairs, PF_Castle
    Environment/Nature/   PF_Pine
    Player/               PF_Player
    Systems/              PF_Atmosphere
  Scenes/                 DarkFantasy_Demo
  Scripts/
    Core/                 SceneRoots
    Data/                 AtmosphereProfile
    Environment/          CastleLandmark
    Player/               FPS, HeldWeapon
    Visuals/              Atmosphere, RetroLook
  Editor/                 ArchitectureBuilder, DarkFantasySetup
```

## Как пользоваться в редакторе

1. **Witcher → Architecture → 1. Generate Materials + Profiles + Prefabs**
2. **Witcher → Architecture → 2. Rebuild Demo Scene From Prefabs**
3. Правишь **префаб** `PF_Castle` / модули — сцена обновится у инстансов
4. Mood: перетащи другой `Atmosphere_*.asset` в `Atmosphere.profile` или меню **Architecture → 3**

## Что делать тебе (Editor)

| Задача | Где |
|--------|-----|
| Сдвинуть замок | Hierarchy → `_Environment/Castle` |
| Изменить форму башни | Project → `PF_CastleTower` (Open Prefab) |
| Новый кусок стены | дублируй модуль → сохрани префаб → вложи в `PF_Castle` |
| Свет/туман | `Data/Atmosphere/Atmosphere_*.asset` |
| Пиксель | `PC_Renderer` → Retro Look |

## Чего избегать

- Не оставляй «сиротские» Cube в корне Hierarchy
- Не копируй цвета света в трёх скриптах — только в Profile
- Не ломай вложенность: viewmodel меча только под камерой игрока
