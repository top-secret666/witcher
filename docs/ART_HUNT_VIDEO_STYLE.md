# Что искать / генерить — стиль как в savekit-видео

Направление: **день, яркий stylized / toon**, не ночной ведьмак.

Клади файлы сюда:
`Assets/Witcher/Art/Textures/`  
Имена лучше как в таблице (тогда потом легко прикрутить меню Apply).

---

## 1. Что ГЕНЕРИТЬ (AI: Midjourney / SD / ChatGPT Images)

Генери картинки как **tileable texture** или **sky**, не целые сцены.

### Обязательный минимум (6 штук)

| Имя файла | Что это | Промпт (англ., копируй) |
|-----------|---------|-------------------------|
| `T_Sky_Day.png` | небо + chunky clouds | `hand painted stylized sky, bright saturated blue, chunky white clouds, anime background, seamless optional, flat shading, no people --tile` |
| `T_Stone_Cream.png` | стены замка | `seamless hand painted cream stone brick wall texture, stylized toon, soft peach beige, game texture 1024, flat colors` |
| `T_Roof_Red.png` | красные конусные крыши | `seamless dark red clay roof tiles, stylized fantasy, simple shapes, game texture` |
| `T_Cobble.png` | булыжная тропа | `seamless stylized cobblestone path, warm brown gray, bright blue puddle highlights optional, toon game texture` |
| `T_Hedge.png` | живая изгородь / листва | `seamless stylized hedge leaves, vibrant green, painterly blocks of color, game foliage texture` |
| `T_Carpet_Red.png` | ковёр в коридоре (позже) | `seamless ornate red carpet with gold pattern, stylized fantasy, flat colors, game texture` |

### Если хочешь ещё красивее (+4)

| Имя | Промпт-ключ |
|-----|-------------|
| `T_Wheat.png` | `seamless golden wheat field top-down stylized` |
| `T_Bark.png` | `seamless stylized tree bark brown toon` |
| `T_Ivy.png` | `seamless green ivy on wall stylized alpha` |
| `T_Flower_Pink.png` | `stylized pink flower cluster sprite transparent background` |

**Настройки генерации:** квадрат 1024×1024, стиль flat / painterly / game texture, **без фотореализма**.

---

## 2. Что ИСКАТЬ готовое (бесплатно)

Ищи: `seamless`, `tileable`, `stylized`, `hand painted`, `toon`.

| Сайт | Что брать |
|------|-----------|
| [itch.io](https://itch.io/game-assets/free) | пакеты `stylized texture`, `hand painted` |
| [Kenney.nl](https://kenney.nl/assets) | простые tile-текстуры (быстрый старт) |
| [OpenGameArt](https://opengameart.org) | `stone seamless`, `cobblestone` |
| [Poly Haven](https://polyhaven.com/textures) только если потом упростишь цвет — лучше не фото | |

Фильтр лицензии: **CC0** или коммерция OK.

---

## 3. Что НЕ нужно сейчас

- 4K PBR (Normal/Roughness/Metal) — для toon лишнее  
- Фотореалистичный мрамор / кожа  
- Модель коня (позже)  
- Ночные purple sky из старого референса  

---

## 4. Когда файлы лягут в папку

Напиши мне «текстуры готовы» или кинь скрин Project.  
Я подключу материалы + дневной свет/toon-настроение в сцену, ты крутишь в редакторе.
