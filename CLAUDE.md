# Дворовый хоккей 3D — заметки для Claude

В репозитории две версии одной игры:
- `index.html`, `js/`, `css/`: браузерная версия (Three.js, онлайн через PeerJS);
- `unity/`: Unity-версия (Built-in RP, всё создаётся кодом, сеть по UDP). Подробности в `unity/README.md`.

Общайся с пользователем по-русски.

## Как увидеть и проверить Unity-версию

1. Найди Unity Editor (версия 6000.x или 2022.3), установленный через Unity Hub:
   - Windows: `C:\Program Files\Unity\Hub\Editor\<версия>\Editor\Unity.exe`
   - macOS: `/Applications/Unity/Hub/Editor/<версия>/Unity.app/Contents/MacOS/Unity`
   - Linux: `~/Unity/Hub/Editor/<версия>/Editor/Unity`
   Если редактор открыт с этим проектом, закрой его или попроси пользователя закрыть: в batchmode
   два экземпляра на один проект не запускаются.
2. Сборка (заодно проверяет компиляцию):
   ```
   <Unity> -batchmode -quit -projectPath unity -executeMethod Hockey.EditorTools.HockeyBuild.Build -logFile unity/Logs/build.log
   ```
   Ошибки компиляции ищи в `unity/Logs/build.log` (строки `error CS`). Игра собирается в `unity/Build/`.
3. Автотест: игра сама проходит сценарий и сохраняет скриншоты в `unity/Autotest/`:
   ```
   unity/Build/Hockey.exe -autotest -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile unity/Logs/player.log
   ```
   (macOS: `unity/Build/Hockey.app/Contents/MacOS/<имя> ...`, Linux: `unity/Build/Hockey.x86_64 ...`)
   Процесс завершится сам примерно через 40 секунд. Потом посмотри PNG в `unity/Autotest/` (меню, вбрасывание,
   катание, бросок, крупные планы игрока и вратаря, вид сверху, сплит-скрин) и прочитай
   `unity/Autotest/autotest.log` (состояние матча, FPS, ошибки).
   Не запускай с `-nographics` и `-batchmode`: тогда нечего снимать.
4. Альтернатива без сборки: в редакторе меню **Hockey → Автотест (Play)**, результат в той же папке.

Сценарий автотеста лежит в `unity/Assets/Hockey/Scripts/HockeyGame.Autotest.cs`. Добавляй туда шаги,
если нужно проверить что-то новое.

## Как проверить браузерную версию

`python3 -m http.server 8000` в корне, потом открыть http://localhost:8000 (можно через Playwright).

## Устройство

Физика и ИИ ботов: `js/sim.js` и порт `unity/Assets/Hockey/Scripts/Sim/Sim.cs`. Правки баланса
делай в обоих файлах. `Sim.cs` и `Net.cs` не зависят от UnityEngine, их можно проверить обычным `dotnet`.
