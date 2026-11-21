using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class Palette
{
    public Color Background, Surface, Text, Muted, Accent, AccentText, Border, Input, Good, Alert;
}

internal static class Appearance
{
    private static Color Hex(string value) { return ColorTranslator.FromHtml(value); }

    private static readonly Palette Light = new Palette {
        Background = Hex("#F3F5F7"), Surface = Color.White, Text = Hex("#192937"),
        Muted = Hex("#485F70"), Accent = Hex("#234B6A"), AccentText = Color.White,
        Border = Hex("#CBD5DB"), Input = Hex("#F9FAFB"), Good = Hex("#1B694D"), Alert = Hex("#98461E")
    };
    private static readonly Palette Dark = new Palette {
        Background = Hex("#18222C"), Surface = Hex("#253544"), Text = Hex("#E5E9ED"),
        Muted = Hex("#BAC9D5"), Accent = Hex("#A5CEE9"), AccentText = Hex("#102C3D"),
        Border = Hex("#4D6273"), Input = Hex("#1C2B38"), Good = Hex("#8AD7B0"), Alert = Hex("#F2B491")
    };

    public static Palette Colors(string theme)
    {
        if (SystemInformation.HighContrast)
            return new Palette {
                Background = SystemColors.Window, Surface = SystemColors.Window, Text = SystemColors.WindowText,
                Muted = SystemColors.WindowText, Accent = SystemColors.Highlight, AccentText = SystemColors.HighlightText,
                Border = SystemColors.WindowText, Input = SystemColors.Window, Good = SystemColors.WindowText, Alert = SystemColors.WindowText
            };
        if (theme == "dark") return Dark;
        if (theme == "light") return Light;
        const string personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        try {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(personalize)) {
                if (key != null && Object.Equals(key.GetValue("AppsUseLightTheme", 1), 0)) return Dark;
            }
        }
        catch (System.Security.SecurityException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.IO.IOException) { }
        return Light;
    }
}

internal static class Texts
{
    private static readonly Dictionary<string, string[]> Entries = new Dictionary<string, string[]>(StringComparer.Ordinal) {
        // Each row is keyed by the English source, followed by Ukrainian and Russian.
        { "Controls", new[] { "Керування", "Управление" } },
        { "Settings", new[] { "Налаштування", "Настройки" } },
        { "Startup", new[] { "Запуск", "Запуск" } },
        { "Start when I sign in to Windows", new[] { "Запускати під час входу у Windows", "Запускать при входе в Windows" } },
        { "Start in the system tray", new[] { "Запускати у згорнутому вигляді в треї", "Начинать в трее" } },
        { "Windows startup settings are unavailable.", new[] { "Налаштування автозапуску Windows недоступні.", "Настройки автозапуска Windows недоступны." } },
        { "Windows startup could not be updated.", new[] { "Не вдалося змінити налаштування автозапуску Windows.", "Не удалось изменить настройки автозапуска Windows." } },
        { "Connected", new[] { "Підключено", "Подключён" } },
        { "Waiting for microphone", new[] { "Очікування мікрофона", "Ожидание микрофона" } },
        { "Control enabled", new[] { "Керування ввімкнено", "Управление включено" } },
        { "Control paused", new[] { "Керування призупинено", "Управление на паузе" } },
        { "Active profile", new[] { "Активний профіль", "Активный профиль" } },
        { "Add profile", new[] { "Додати профіль", "Добавить профиль" } },
        { "Edit profile", new[] { "Змінити профіль", "Изменить профиль" } },
        { "Microphone button", new[] { "Кнопка мікрофона", "Кнопка микрофона" } },
        { "Check button", new[] { "Перевірити кнопку", "Проверить кнопку" } },
        { "Press the button on your AKG Ara within 30 seconds.", new[] { "Натисніть кнопку на AKG Ara протягом 30 секунд.", "Нажмите кнопку на AKG Ara в течение 30 секунд." } },
        { "Activate the target window, then press the microphone button within 30 seconds.", new[] { "Перейдіть у вікно вибраної програми й натисніть кнопку мікрофона протягом 30 секунд.", "Перейдите в окно выбранной программы и нажмите кнопку микрофона в течение 30 секунд." } },
        { "Physical button", new[] { "Фізична кнопка", "Физическая кнопка" } },
        { "Target application", new[] { "Вибрана програма", "Выбранная программа" } },
        { "Keyboard shortcut", new[] { "Комбінація клавіш", "Сочетание клавиш" } },
        { "Waiting for a press", new[] { "Очікування натискання", "Ожидание нажатия" } },
        { "Not checked", new[] { "Ще не перевірено", "Ещё не проверено" } },
        { "Received", new[] { "Отримано", "Получено" } },
        { "Running", new[] { "Запущено", "Запущена" } },
        { "Not running", new[] { "Не запущено", "Не запущена" } },
        { "Status unavailable", new[] { "Не вдалося визначити стан", "Не удалось определить состояние" } },
        { "Not sent", new[] { "Не надіслано", "Не отправлено" } },
        { "No press received within 30 seconds.", new[] { "За 30 секунд натискання не виявлено.", "За 30 секунд нажатие не обнаружено." } },
        { "Microphone disconnected.", new[] { "Мікрофон від’єднано.", "Микрофон отключён." } },
        { "Check the result in the target application. Press again to repeat.", new[] { "Перевірте результат у програмі. Натисніть кнопку ще раз, щоб повторити команду.", "Проверьте результат в программе. Нажмите кнопку ещё раз, чтобы повторить команду." } },
        { "Connect the AKG Ara and try again.", new[] { "Підключіть AKG Ara і повторіть перевірку.", "Подключите AKG Ara и повторите проверку." } },
        { "The selected profile changed.", new[] { "Вибрано інший профіль.", "Выбран другой профиль." } },
        { "Record", new[] { "Записати", "Записать" } },
        { "Reset", new[] { "Скинути", "Сбросить" } },
        { "Shortcut reset", new[] { "Початкову комбінацію відновлено", "Сочетание по умолчанию восстановлено" } },
        { "Test", new[] { "Перевірити", "Проверить" } },
        { "Press a shortcut…", new[] { "Натисніть комбінацію клавіш…", "Нажмите сочетание клавиш…" } },
        { "Press Esc to cancel", new[] { "Esc — скасувати", "Esc — отменить" } },
        { "Knob rotation", new[] { "Обертання ручки", "Вращение ручки" } },
        { "Volume up", new[] { "Збільшення гучності", "Увеличение громкости" } },
        { "Volume down", new[] { "Зменшення гучності", "Уменьшение громкости" } },
        { "Send shortcuts on rotation", new[] { "Надсилати комбінації під час обертання", "Отправлять сочетания при вращении" } },
        { "Headphone volume still follows the knob.", new[] { "Ручка також регулює гучність навушників.", "Ручка также регулирует громкость наушников." } },
        { "Clockwise", new[] { "За годинниковою стрілкою", "По часовой стрелке" } },
        { "Counterclockwise", new[] { "Проти годинникової стрілки", "Против часовой стрелки" } },
        { "Direction 1", new[] { "Напрямок 1", "Направление 1" } },
        { "Direction 2", new[] { "Напрямок 2", "Направление 2" } },
        { "Identify clockwise turn", new[] { "Визначити напрямок", "Определить направление" } },
        { "Turn the knob clockwise one step.", new[] { "Поверніть ручку на один крок за годинниковою стрілкою.", "Поверните ручку на один шаг по часовой стрелке." } },
        { "Clockwise direction saved.", new[] { "Напрямок обертання збережено.", "Направление вращения сохранено." } },
        { "Last action", new[] { "Остання дія", "Последнее действие" } },
        { "Ready", new[] { "Готово", "Готово" } },
        { "Press", new[] { "Натискання", "Нажатие" } },
        { "Turn", new[] { "Поворот", "Поворот" } },
        { "Windows rejected the shortcut.", new[] { "Windows відхилила комбінацію клавіш.", "Windows отклонила сочетание клавиш." } },
        { "Sent", new[] { "Надіслано", "Отправлено" } },
        { "The target application is not running.", new[] { "Вибрану програму не запущено.", "Выбранная программа не запущена." } },
        { "The target window is not active.", new[] { "Вікно вибраної програми неактивне.", "Окно выбранной программы неактивно." } },
        { "Shortcut is unassigned.", new[] { "Комбінацію клавіш не призначено.", "Сочетание клавиш не назначено." } },
        { "No shortcut", new[] { "Без комбінації", "Без сочетания" } },
        { "Language", new[] { "Мова", "Язык" } },
        { "Theme", new[] { "Тема", "Тема" } },
        { "Follow Windows", new[] { "Як у Windows", "Как в Windows" } },
        { "Light", new[] { "Світла", "Светлая" } },
        { "Dark", new[] { "Темна", "Тёмная" } },
        { "Only send when the target window is active", new[] { "Лише коли вікно вибраної програми активне", "Только когда окно выбранной программы активно" } },
        { "For background control, assign this shortcut as a global shortcut in the target application.", new[] { "Для роботи у фоні призначте цю комбінацію як глобальну в налаштуваннях вибраної програми.", "Для работы в фоне назначьте это сочетание как глобальное в настройках выбранной программы." } },
        { "Minimize to tray", new[] { "Згорнути в трей", "Свернуть в трей" } },
        { "Open", new[] { "Відкрити", "Открыть" } },
        { "Pause control", new[] { "Призупинити керування", "Приостановить управление" } },
        { "Enable control", new[] { "Увімкнути керування", "Включить управление" } },
        { "Exit", new[] { "Вийти", "Выход" } },
        { "Profile name", new[] { "Назва профілю", "Название профиля" } },
        { "Application process", new[] { "Процес програми", "Процесс программы" } },
        { "Save", new[] { "Зберегти", "Сохранить" } },
        { "Cancel", new[] { "Скасувати", "Отмена" } },
        { "Enter a profile name and process.", new[] { "Вкажіть назву профілю та процес програми.", "Укажите название профиля и процесс программы." } },
        { "This profile name already exists.", new[] { "Профіль із такою назвою вже існує.", "Профиль с таким названием уже есть." } },
        { "This shortcut is already assigned", new[] { "Цю комбінацію вже призначено", "Это сочетание уже назначено" } },
        { "Settings could not be saved.", new[] { "Не вдалося зберегти налаштування.", "Не удалось сохранить настройки." } },
        { "Switch to the target window within 3 seconds.", new[] { "Протягом 3 секунд перейдіть у вікно вибраної програми.", "В течение 3 секунд переключитесь на окно выбранной программы." } },
        { "Test shortcut", new[] { "Перевірити комбінацію", "Проверить сочетание" } },
        { "Click to enable or pause control", new[] { "Увімкнути або призупинити керування", "Включить или приостановить управление" } },
        { "Clear", new[] { "Очистити", "Очистить" } },
        { "A shortcut was received.", new[] { "Отримано комбінацію клавіш.", "Сочетание клавиш получено." } },
        { "Direction calibration", new[] { "Визначення напрямку", "Определение направления" } },
        { "Close", new[] { "Закрити", "Закрыть" } },
        { "Microphone", new[] { "Мікрофон", "Микрофон" } },
    };

    public static string Get(string language, string key)
    {
        if (String.IsNullOrEmpty(language) || language == "system" || language == "auto")
            language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        string[] translations;
        if ((language == "uk" || language == "ru") && Entries.TryGetValue(key, out translations))
            return translations[language == "uk" ? 0 : 1];
        return key;
    }
}
