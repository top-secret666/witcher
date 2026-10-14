# Сегодня в редакторе (dark fantasy, минимум)

Ты работаешь только в Unity Editor. Скрипты уже лежат в `Assets/Witcher/`.

## Шаги

1. Открой проект `D:\HUH\witcher` в Unity Hub (Unity **6000.6.4f1**).
2. Дождись компиляции (консоль без красных ошибок).
3. Меню сверху: **Witcher → 2. Build Dark Fantasy Demo Scene**
4. Нажми **Play ▶** и походи по сцене.

## Что крутить для красоты

| Где | Что |
|-----|-----|
| Hierarchy → `DarkFantasyAtmosphere` | Mood: **Castle Dusk** или **Manor Night** |
| `Assets/Settings/PC_Renderer` → Retro Look | Pixel Size, Color Steps, Dither, Vignette |
| Hierarchy → `Castle_Blockout` | Сдвинь/масштабируй замок |
| Hierarchy → `Forest` | Добавь деревья (дублируй Pine) |
| Main Camera | По желанию добавь компонент `SimplePixelCamera` для ещё более «хрустящего» пикселя |

## Референсы

`Assets/Witcher/Art/Reference/mood_castle_dusk.png`  
`Assets/Witcher/Art/Reference/mood_manor_night.png`

## Разделение труда

- **Ты:** свет, композиция, ассеты, Terrain, постэффект-числа в инспекторе  
- **Скрипты:** FPS, меч, атмосфера, retro-pass, меню сборки сцены
