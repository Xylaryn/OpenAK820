namespace OpenAK820.Ui;

/// <summary>
/// Translations keyed by the English text, so a missing entry simply shows English.
/// These were written fresh for this project and would benefit from native-speaker review.
/// </summary>
public static class Loc
{
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("zh-Hans", "简体中文"), ("zh-Hant", "繁體中文"), ("uk", "Українська"),
    };

    public static string Current { get; private set; } = "en";

    public static void Set(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            var ui = System.Globalization.CultureInfo.CurrentUICulture;
            code = ui.Name switch
            {
                "zh-CN" or "zh-SG" or "zh-Hans" => "zh-Hans",
                "zh-TW" or "zh-HK" or "zh-MO" or "zh-Hant" => "zh-Hant",
                _ => ui.TwoLetterISOLanguageName == "uk" ? "uk" : "en",
            };
        }
        Current = code;
    }

    public static string T(string english)
    {
        var table = Current switch { "zh-Hans" => Hans, "zh-Hant" => Hant, "uk" => Ukrainian, _ => null };
        return table != null && table.TryGetValue(english, out var text) ? text : english;
    }

    private static readonly Dictionary<string, string> Hans = new()
    {
        ["Home"] = "主页", ["Keys"] = "按键设置", ["Macros"] = "宏管理", ["Lighting Mode"] = "灯光模式", ["User Lighting"] = "自定义灯光",
        ["Real-time Lighting"] = "实时灯光", ["Music Rhythm"] = "音乐律动", ["TFT Screen"] = "彩屏", ["System Settings"] = "系统设置",
        ["OK"] = "确定", ["Cancel"] = "取消", ["Apply"] = "应用", ["Import"] = "导入", ["Export"] = "导出", ["New"] = "新建", ["Rename"] = "重命名", ["Delete"] = "删除",
        ["Write to keyboard"] = "写入键盘", ["Profiles"] = "配置", ["Start"] = "开始", ["Stop"] = "停止", ["Show"] = "显示", ["Exit Program"] = "退出程序",
        ["No keyboard found"] = "未找到键盘", ["Connected by USB"] = "USB 已连接", ["Connected by 2.4 GHz"] = "2.4G 已连接", ["Battery {0}%"] = "电量 {0}%",
        ["Top Layer"] = "顶层", ["Fn Layer"] = "Fn 层", ["Key Layer"] = "按键层", ["Key Function"] = "按键功能", ["Default Key"] = "默认按键", ["Disable Key"] = "禁用按键",
        ["Custom Function"] = "自定义功能", ["Multi-Key"] = "组合键", ["Macro Definition"] = "宏定义", ["Mouse Function"] = "鼠标功能", ["Multimedia"] = "多媒体",
        ["System and Web Keys"] = "系统与网页键", ["Windows Shortcut"] = "Windows 快捷键", ["Open Program"] = "打开程序", ["Open Website"] = "打开网站", ["Send Text"] = "发送文本", ["Switch Profile"] = "切换配置",
        ["Left Click"] = "左键", ["Middle Click"] = "中键", ["Right Click"] = "右键", ["Double Click"] = "双击", ["Scroll Up"] = "向上滚动", ["Scroll Down"] = "向下滚动",
        ["Play/Pause"] = "播放/暂停", ["Previous Track"] = "上一曲", ["Next Track"] = "下一曲", ["Volume +"] = "音量 +", ["Volume -"] = "音量 -", ["Mute"] = "静音",
        ["Show Desktop"] = "显示桌面", ["My Computer"] = "我的电脑", ["Lock Computer"] = "锁定电脑", ["Close Window"] = "关闭窗口", ["Switch Windows"] = "切换窗口", ["Copy"] = "复制", ["Paste"] = "粘贴", ["Cut"] = "剪切",
        ["Calculator"] = "计算器", ["Mail"] = "邮件", ["Homepage"] = "主页",
        ["Macro Manager"] = "宏管理", ["Action Record List"] = "动作记录列表", ["Record Input"] = "录制输入", ["Start Recording"] = "开始录制", ["Stop Recording"] = "停止录制",
        ["Record Input Interval Time"] = "录制输入间隔时间", ["Do Not Record Input Interval Time"] = "不录制输入间隔时间", ["Default Interval"] = "默认间隔", ["Insert"] = "插入", ["Move Up"] = "上移", ["Move Down"] = "下移", ["Modify Delay Time"] = "修改延时",
        ["Key"] = "按键", ["Action"] = "动作", ["Delay(ms)"] = "延时(毫秒)", ["Press"] = "按下", ["Release"] = "释放", ["Play Once"] = "播放一次", ["Play Multiple Times"] = "播放多次", ["Release to stop"] = "松开停止", ["Click to play"] = "单击播放",
        ["Lighting Color"] = "灯光颜色", ["Colorful"] = "彩色", ["Lighting Brightness"] = "灯光亮度", ["Lighting Speed"] = "灯光速度", ["Lighting Direction"] = "灯光方向", ["Up"] = "上", ["Down"] = "下", ["Left"] = "左", ["Right"] = "右",
        ["Static"] = "常亮", ["Breath"] = "呼吸", ["LED Off"] = "关闭灯光", ["Select All"] = "全选", ["Select None"] = "取消选择", ["Effect"] = "效果",
        ["Rhythm"] = "节奏", ["Color"] = "颜色", ["Amplitude"] = "振幅", ["Background Mode"] = "背景模式", ["Background Brightness"] = "背景亮度", ["Rainbow"] = "彩虹", ["Gradient"] = "渐变", ["White"] = "白色", ["Red"] = "红色", ["Orange"] = "橙色", ["Yellow"] = "黄色", ["Green"] = "绿色", ["Cyan"] = "青色", ["Blue"] = "蓝色", ["Purple"] = "紫色", ["Off"] = "关闭",
        ["Frames"] = "帧", ["Editing"] = "编辑", ["Tools"] = "工具", ["Brush"] = "画笔", ["Eraser"] = "橡皮擦", ["Add text"] = "添加文字", ["Grid"] = "网格", ["Preview"] = "预览", ["Upload to keyboard"] = "上传到键盘", ["Time Sync"] = "时间同步", ["Export GIF"] = "导出 GIF", ["Create a new frame"] = "新建帧", ["Copy frame"] = "复制帧", ["Delete frame"] = "删除帧",
        ["Keyboard"] = "键盘", ["Key Response Time"] = "按键响应时间", ["Sleep Time"] = "休眠时间", ["No Sleep"] = "不休眠", ["1min"] = "1 分钟", ["5min"] = "5 分钟", ["30min"] = "30 分钟", ["Fn Switch Function"] = "Fn 切换功能", ["Momentary"] = "按住生效", ["Toggle"] = "切换",
        ["Disable Windows Key"] = "禁用 Windows 键", ["Disable ALT + F4"] = "禁用 ALT + F4", ["Disable ALT + TAB"] = "禁用 ALT + TAB", ["Reset"] = "重置", ["Reset Keyboard"] = "重置键盘", ["Factory Reset"] = "恢复出厂设置", ["Language"] = "语言", ["Automatic Start"] = "开机启动", ["Minimize to Tray, Don't Exit"] = "最小化到托盘，不退出", ["This app"] = "本程序", ["About"] = "关于",
    };

    private static readonly Dictionary<string, string> Hant = new()
    {
        ["Home"] = "首頁", ["Keys"] = "按鍵設定", ["Macros"] = "巨集管理", ["Lighting Mode"] = "燈光模式", ["User Lighting"] = "自訂燈光",
        ["Real-time Lighting"] = "即時燈光", ["Music Rhythm"] = "音樂律動", ["TFT Screen"] = "彩屏", ["System Settings"] = "系統設定",
        ["OK"] = "確定", ["Cancel"] = "取消", ["Apply"] = "套用", ["Import"] = "匯入", ["Export"] = "匯出", ["New"] = "新增", ["Rename"] = "重新命名", ["Delete"] = "刪除",
        ["Write to keyboard"] = "寫入鍵盤", ["Profiles"] = "設定檔", ["Start"] = "開始", ["Stop"] = "停止", ["Show"] = "顯示", ["Exit Program"] = "結束程式",
        ["No keyboard found"] = "找不到鍵盤", ["Connected by USB"] = "USB 已連線", ["Connected by 2.4 GHz"] = "2.4G 已連線", ["Battery {0}%"] = "電量 {0}%",
        ["Top Layer"] = "頂層", ["Fn Layer"] = "Fn 層", ["Key Layer"] = "按鍵層", ["Key Function"] = "按鍵功能", ["Default Key"] = "預設按鍵", ["Disable Key"] = "停用按鍵",
        ["Custom Function"] = "自訂功能", ["Multi-Key"] = "組合鍵", ["Macro Definition"] = "巨集定義", ["Mouse Function"] = "滑鼠功能", ["Multimedia"] = "多媒體",
        ["System and Web Keys"] = "系統與網頁鍵", ["Windows Shortcut"] = "Windows 快速鍵", ["Open Program"] = "開啟程式", ["Open Website"] = "開啟網站", ["Send Text"] = "傳送文字", ["Switch Profile"] = "切換設定檔",
        ["Left Click"] = "左鍵", ["Middle Click"] = "中鍵", ["Right Click"] = "右鍵", ["Double Click"] = "連按兩下", ["Scroll Up"] = "向上捲動", ["Scroll Down"] = "向下捲動",
        ["Play/Pause"] = "播放/暫停", ["Previous Track"] = "上一首", ["Next Track"] = "下一首", ["Volume +"] = "音量 +", ["Volume -"] = "音量 -", ["Mute"] = "靜音",
        ["Show Desktop"] = "顯示桌面", ["My Computer"] = "我的電腦", ["Lock Computer"] = "鎖定電腦", ["Close Window"] = "關閉視窗", ["Switch Windows"] = "切換視窗", ["Copy"] = "複製", ["Paste"] = "貼上", ["Cut"] = "剪下",
        ["Macro Manager"] = "巨集管理", ["Action Record List"] = "動作記錄清單", ["Record Input"] = "錄製輸入", ["Start Recording"] = "開始錄製", ["Stop Recording"] = "停止錄製", ["Insert"] = "插入", ["Move Up"] = "上移", ["Move Down"] = "下移",
        ["Key"] = "按鍵", ["Action"] = "動作", ["Press"] = "按下", ["Release"] = "放開", ["Lighting Color"] = "燈光顏色", ["Colorful"] = "彩色", ["Lighting Brightness"] = "燈光亮度", ["Lighting Speed"] = "燈光速度", ["Lighting Direction"] = "燈光方向",
        ["Select All"] = "全選", ["Select None"] = "取消選取", ["Effect"] = "效果", ["Rhythm"] = "節奏", ["Color"] = "顏色", ["Amplitude"] = "振幅", ["Background Mode"] = "背景模式", ["Background Brightness"] = "背景亮度", ["Off"] = "關閉",
        ["Frames"] = "影格", ["Editing"] = "編輯", ["Tools"] = "工具", ["Brush"] = "畫筆", ["Eraser"] = "橡皮擦", ["Add text"] = "新增文字", ["Grid"] = "格線", ["Preview"] = "預覽", ["Upload to keyboard"] = "上傳到鍵盤", ["Time Sync"] = "時間同步", ["Export GIF"] = "匯出 GIF",
        ["Keyboard"] = "鍵盤", ["Key Response Time"] = "按鍵回應時間", ["Sleep Time"] = "休眠時間", ["No Sleep"] = "不休眠", ["1min"] = "1 分鐘", ["5min"] = "5 分鐘", ["30min"] = "30 分鐘",
        ["Disable Windows Key"] = "停用 Windows 鍵", ["Disable ALT + F4"] = "停用 ALT + F4", ["Disable ALT + TAB"] = "停用 ALT + TAB", ["Reset"] = "重設", ["Reset Keyboard"] = "重設鍵盤", ["Factory Reset"] = "恢復原廠設定", ["Language"] = "語言", ["Automatic Start"] = "開機啟動", ["Minimize to Tray, Don't Exit"] = "最小化到工作列，不結束", ["This app"] = "本程式", ["About"] = "關於",
    };

    private static readonly Dictionary<string, string> Ukrainian = new()
    {
        ["Home"] = "Головна", ["Keys"] = "Клавіші", ["Macros"] = "Макроси", ["Lighting Mode"] = "Режим підсвітки", ["User Lighting"] = "Власна підсвітка",
        ["Real-time Lighting"] = "Підсвітка в реальному часі", ["Music Rhythm"] = "Музичний ритм", ["TFT Screen"] = "Екран", ["System Settings"] = "Налаштування",
        ["OK"] = "Гаразд", ["Cancel"] = "Скасувати", ["Apply"] = "Застосувати", ["Import"] = "Імпорт", ["Export"] = "Експорт", ["New"] = "Новий", ["Rename"] = "Перейменувати", ["Delete"] = "Видалити",
        ["Write to keyboard"] = "Записати на клавіатуру", ["Profiles"] = "Профілі", ["Start"] = "Почати", ["Stop"] = "Зупинити", ["Show"] = "Показати", ["Exit Program"] = "Вийти",
        ["No keyboard found"] = "Клавіатуру не знайдено", ["Connected by USB"] = "Підключено через USB", ["Connected by 2.4 GHz"] = "Підключено через 2.4 ГГц", ["Battery {0}%"] = "Заряд {0}%",
        ["Top Layer"] = "Верхній шар", ["Fn Layer"] = "Шар Fn", ["Key Layer"] = "Шар клавіш", ["Key Function"] = "Функція клавіші", ["Default Key"] = "Типова клавіша", ["Disable Key"] = "Вимкнути клавішу",
        ["Custom Function"] = "Власна функція", ["Multi-Key"] = "Кілька клавіш", ["Macro Definition"] = "Макрос", ["Mouse Function"] = "Функція миші", ["Multimedia"] = "Мультимедіа",
        ["Open Program"] = "Відкрити програму", ["Open Website"] = "Відкрити сайт", ["Send Text"] = "Надіслати текст", ["Switch Profile"] = "Змінити профіль",
        ["Macro Manager"] = "Менеджер макросів", ["Start Recording"] = "Почати запис", ["Stop Recording"] = "Зупинити запис", ["Insert"] = "Вставити", ["Move Up"] = "Вгору", ["Move Down"] = "Вниз",
        ["Key"] = "Клавіша", ["Action"] = "Дія", ["Press"] = "Натиснення", ["Release"] = "Відпускання", ["Lighting Color"] = "Колір підсвітки", ["Colorful"] = "Різнокольоровий", ["Lighting Brightness"] = "Яскравість", ["Lighting Speed"] = "Швидкість", ["Lighting Direction"] = "Напрямок",
        ["Select All"] = "Вибрати все", ["Select None"] = "Зняти вибір", ["Effect"] = "Ефект", ["Rhythm"] = "Ритм", ["Color"] = "Колір", ["Amplitude"] = "Амплітуда", ["Off"] = "Вимк.",
        ["Frames"] = "Кадри", ["Editing"] = "Редагування", ["Tools"] = "Інструменти", ["Brush"] = "Пензель", ["Eraser"] = "Гумка", ["Add text"] = "Додати текст", ["Grid"] = "Сітка", ["Preview"] = "Перегляд", ["Upload to keyboard"] = "Завантажити на клавіатуру", ["Time Sync"] = "Синхронізація часу", ["Export GIF"] = "Експорт GIF",
        ["Keyboard"] = "Клавіатура", ["Key Response Time"] = "Час відгуку клавіш", ["Sleep Time"] = "Час до сну", ["No Sleep"] = "Без сну", ["1min"] = "1 хв", ["5min"] = "5 хв", ["30min"] = "30 хв",
        ["Disable Windows Key"] = "Вимкнути клавішу Windows", ["Disable ALT + F4"] = "Вимкнути ALT + F4", ["Disable ALT + TAB"] = "Вимкнути ALT + TAB", ["Reset"] = "Скидання", ["Reset Keyboard"] = "Скинути клавіатуру", ["Factory Reset"] = "Заводські налаштування", ["Language"] = "Мова", ["Automatic Start"] = "Автозапуск", ["Minimize to Tray, Don't Exit"] = "Згортати в трей, не виходити", ["This app"] = "Ця програма", ["About"] = "Про програму",
    };
}
