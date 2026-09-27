# Розведення рослин — стартовий Unity-проект (MVP)

Цей проект — скелет, згенерований за планом розробки з GDD. Містить готову
кодову архітектуру екрану "Мій сад" (розділ 5.1 документа) і чекає на:
1. Відкриття в Unity Hub
2. Побудову самої сцени (Canvas + Prefab) — вручну або через Claude + Unity MCP
3. Підключення реальних спрайтів рослин

## Структура

```
Assets/
  Scripts/
    Core/     — GameManager, PlayerData, GameEvents (глобальний стан і події)
    Save/     — SaveSystem (локальне JSON-збереження)
    Garden/   — PlotState, PlotSlot, PlantData, GardenManager (логіка саду)
    UI/       — PlotSlotView, CurrencyBarView (візуальний шар)
  Scenes/     — сюди покладеш MainGarden.unity (створюється в редакторі)
  Prefabs/    — сюди покладеш PlotSlot.prefab
  Art/Sprites/— спрайти рослин, горщиків, іконок
  Resources/Plants/ — асети PlantData (ScriptableObject)
Packages/manifest.json — залежності (2D, TextMeshPro, URP, Input System)
ProjectSettings/ProjectVersion.txt — версія Unity (6000.3.0f1 LTS)
```

## Крок 1 — Встановлення Unity

1. Встанови **Unity Hub**: https://unity.com/download
2. У Hub → Installs → Install Editor → обери **Unity 6.3 LTS** (6000.3.x)
   при встановленні онови модулі **Android Build Support** та **iOS Build Support**
3. Hub → Open → вкажи цю папку (`PlantBreedingGame`) — Hub запропонує
   встановити версію з ProjectVersion.txt, якщо її ще нема

## Крок 2 — Перевірка пакетів

Після відкриття Unity автоматично підтягне пакети з `Packages/manifest.json`
(2D Sprite, TextMeshPro, URP, Input System). Якщо TextMeshPro попросить
імпортувати TMP Essentials — погодься (Window → TextMeshPro → Import TMP Essential Resources).

## Крок 3 — Побудова сцени "Мій сад" (перший екран)

> **Автоматизовано:** у проєкті є `Assets/Editor/HomeScreenBuilder.cs`.
> Після відкриття проєкту (і імпорту TMP Essentials) запусти меню
> **PlantBreeding → Зібрати екран «Мій сад»** — сцена `Assets/Scenes/MainGarden.unity`
> збереться за дизайном автоматично (тільки вигляд, без логіки).
> Ручні кроки нижче лишаються як довідка. План розробки: `docs/DEV-PLAN.md`.

Код уже готовий, залишається зібрати сцену в редакторі:

1. `File → New Scene` → 2D-шаблон → зберегти як `Assets/Scenes/MainGarden.unity`
2. Створити `Canvas` (Screen Space – Overlay) + `EventSystem`
3. У Canvas створити:
   - `TopBar` (Panel) з полями `coinsLabel`, `gemsLabel`, `levelLabel` (TMP_Text) → повісити `CurrencyBarView`
   - `GridParent` (порожній `GameObject`, зверху — `Grid Layout Group`, 3 колонки)
4. Створити Prefab `PlotSlot`:
   - `Button` + дочірні `Image` (plantImage), `Image` (statusIconImage),
     `Image` Filled/Radial360 (growthRing), `TMP_Text` (stateLabel)
   - Повісити скрипт `PlotSlotView`, перетягнути посилання на ці елементи
   - Перетягнути в `Assets/Prefabs/PlotSlot.prefab`
5. Створити порожній `GameObject` `GardenManager` на сцені → повісити скрипт
   `GardenManager` → у полі `Plot View Prefab` вказати `PlotSlot.prefab`,
   у `Grid Parent` — `GridParent` з кроку 3
6. Створити порожній `GameObject` `GameManager` → повісити скрипт `GameManager`
7. (Опційно для тесту) `Assets → Create → PlantBreeding → Plant Data` →
   заповнити `growTimeSeconds` невеликим числом (напр. 30) для швидкого тесту
   → перетягнути в поле `Default Plant For Testing` у `GardenManager`
8. Натиснути Play — перша грядка засадиться тестовою рослиною автоматично,
   через `growTimeSeconds` стане "Готово!"

Саме цю рутинну збірку сцени (кроки 3–7) зручно доручити Claude через
Unity MCP — дивись інструкцію нижче.

## Крок 4 — Підключення Unity MCP (щоб Claude міг керувати редактором)

Unity MCP дозволяє агенту (Claude Code / Claude Desktop) напряму створювати
GameObject'и, компонувати UI, редагувати скрипти й читати консоль Unity —
без копіювання коду вручну між чатом і редактором.

Є два варіанти. Для інді-розробки без підписки Unity AI рекомендую **варіант A**.

### Варіант A — MCP for Unity (безкоштовний, MIT-ліцензія, від CoplayDev)

1. У Unity: `Window → Package Manager → + → Add package from git URL`
   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
   ```
2. Після імпорту: `Window → MCP for Unity → Configure All Detected Clients`
   — це саме пропише конфіг для Claude Code / Claude Desktop / Cursor, якщо
   вони встановлені на цьому комп'ютері
3. Перезапусти Claude Code/Desktop, щоб він підхопив нового MCP-сервера
4. У Unity відкриється вікно підтвердження нового підключення при першому
   зверненні агента — натисни **Accept**
5. Перевірка: напиши агенту "Прочитай консоль Unity і опиши помилки" —
   якщо підключено правильно, він викличе інструмент читання консолі

Вимоги: Unity 2021.3 LTS+ (у нас 6.3 LTS — підходить), Python 3.10+ (через `uv`).

### Варіант B — офіційний Unity MCP (пакет AI Assistant, потрібна підписка Unity)

1. `Window → Package Manager` → встанови пакет **AI Assistant**
2. `Edit → Project Settings → AI → Unity MCP` → перевір, що **Unity Bridge**
   показує **Running** (зелений індикатор)
3. Розгорни розділ **Integrations** → обери клієнта (Claude Code/Desktop) →
   **Configure** — конфіг пропишеться автоматично
4. Якщо клієнта нема у списку — додай вручну шлях до relay-бінарника
   (`~/.unity/relay/…`, точний шлях під ОС показаний на тій самій сторінці
   налаштувань) у конфіг MCP-клієнта
5. При першому підключенні підтверди клієнта в **Pending Connections**

### Що просити Claude зробити через MCP (приклади)

- "Створи Canvas зі Screen Space Overlay і додай TopBar-панель з трьома TMP_Text полями"
- "Створи Prefab PlotSlot за описом у PlotSlotView.cs і збережи в Assets/Prefabs"
- "Прочитай консоль Unity і виправ помилки компіляції"
- "Додай Grid Layout Group на GridParent, 3 колонки, spacing 12"

Агент бачить структуру сцени й консоль напряму — не потрібно вручну
копіювати помилки компіляції в чат.

## Наступні кроки (після робочого першого екрану)

Дивись розділи 2, 7 GDD-документа: далі йде Firebase Auth + синхронізація
стану саду, базова генетика (2 гени), екран лабораторії схрещування.
