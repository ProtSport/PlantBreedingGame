namespace PlantBreeding.Collections
{
    /// <summary>
    /// Статичний каталог предметів кастомізації профілю (ТЗ «Персонаж»,
    /// 2026-08). Джерело правди "чи є в гравця предмет" —
    /// PlayerData.ownedAvatarIds/ownedFrameIds, не цей каталог; тут лише опис
    /// усіх МОЖЛИВИХ предметів + звідки їх узяти.
    ///
    /// Звідки беруться: стартовий аватар "sprout" і рамка "grey"; рамки
    /// green/gold — за рівнем престижу (GameManager.SyncPrestigeFrames); решту
    /// видають колекції Дендрарію (CollectionCatalog, нагорода забирається в
    /// Дендрарії). Рамка "club" і аватар "sun" — з першою покупкою підписки
    /// «Клуб садівника», назавжди (Shop/ShopService). Троянда, Соняшник, Бджілка, Метелик, Боровик
    /// і рамки Ранкова/Вереснева/Лозова/Садова — нагороди «Завдань тижня» (поки «Скоро»).
    /// Малюнки — ProfileArt (Resources/Profile).
    /// </summary>
    public static class ProfileItemCatalog
    {
        public const string StarterAvatarId = "sprout";
        public const string StarterFrameId = "grey";

        /// <summary>Нагороди скрині «Завдань тижня» (Economy/WeeklyTasks.WeeklyRewardOrder).</summary>
        public const string WeeklySource = "Завдання тижня";

        // Малюнки — Assets/Resources/Profile/avatars/avatar-<art>.png і frames/frame-<art>.png
        // (оригінали — папка «Аватариірамки»). id — ключ у збереженнях, тому старі id не міняємо.
        public static readonly AvatarDef[] Avatars =
        {
            new AvatarDef("sprout", "Паросток", "#DDE8C8", null, "sprout"),
            new AvatarDef("cactus", "Кактус", "#A7CE73", "Колекція «Сукуленти»", "cactus"),
            new AvatarDef("leaf", "Листок", "#7FC98B", "Колекція «Зелене листя»", "monstera"),
            new AvatarDef("leg", "Орхідея", "#E4C77E", "Колекція «Рідкісні красуні»", "orchid"),
            // «Лікар рослин» повернеться разом із хворобами (STG 2).
            new AvatarDef("doctor", "Лікар", "#9FB2E6",
                PlantBreeding.Garden.PlantAilments.Enabled ? "Колекція «Лікар рослин»" : "Скоро", "doctor"),
            new AvatarDef("can", "Золота лійка", "#B9A6E6", "Колекція «Садівник-ветеран»", "goldencan"),
            new AvatarDef("sun", "Сонце", "#F3D98B", "Клуб садівника в Крамниці", "sun"),
            new AvatarDef("rose", "Троянда", "#E7A6B4", WeeklySource, "rose"),
            new AvatarDef("sunflower", "Соняшник", "#F3C94B", WeeklySource, "sunflower"),
            new AvatarDef("bee", "Бджілка", "#F2C14E", WeeklySource, "bee"),
            new AvatarDef("butterfly", "Метелик", "#E8924A", WeeklySource, "butterfly"),
            new AvatarDef("porcini", "Боровик", "#B07A4A", WeeklySource, "porcini"),
        };

        // colorHex — колір підпису обраної рамки.
        public static readonly FrameDef[] Frames =
        {
            new FrameDef("grey", "Сіра", "#9AA592", null, "gray"),
            new FrameDef("green", "Зелена", "#A7CE73", null, "green"),
            new FrameDef("gold", "Золота", "#E4C77E", null, "gold"),
            new FrameDef("floral", "Квіткова", "#E7A6B4", "Колекція «Квіти на підвіконні»", "floral"),
            new FrameDef("dew", "Роса", "#9FD8E6", "Колекція «Ідеальний догляд»", "dew"),
            new FrameDef("marble", "Мармурова", "#EEEAE3", "Колекція горщиків", "marble"),
            new FrameDef("spark", "Іскриста", "#B9A6E6", "Колекція «Ботанік»", "sparkle"),
            new FrameDef("club", "Клуб", "#EDD592", "Клуб садівника в Крамниці", "club"),
            new FrameDef("morning", "Ранкова", "#F2B48A", WeeklySource, "morning"),
            new FrameDef("september", "Вереснева", "#E0884A", WeeklySource, "september"),
            new FrameDef("vine", "Лозова", "#C9A06A", WeeklySource, "vine"),
            new FrameDef("garden", "Садова", "#E6E0CC", WeeklySource, "garden"),
        };

        public static AvatarDef FindAvatar(string id)
        {
            foreach (var a in Avatars) if (a.id == id) return a;
            return null;
        }

        public static FrameDef FindFrame(string id)
        {
            foreach (var f in Frames) if (f.id == id) return f;
            return null;
        }
    }

    /// <summary>Один можливий аватар. source == null → реально досяжний зараз (не locked-плейсхолдер).</summary>
    public class AvatarDef
    {
        public readonly string id;
        public readonly string label;
        public readonly string tintHex;
        public readonly string source;
        /// <summary>Файл малюнка: Resources/Profile/avatars/avatar-&lt;art&gt;.</summary>
        public readonly string art;

        public AvatarDef(string id, string label, string tintHex, string source, string art)
        {
            this.id = id;
            this.label = label;
            this.tintHex = tintHex;
            this.source = source;
            this.art = art;
        }
    }

    /// <summary>Одна можлива рамка профілю. source == null → реально досяжна зараз.</summary>
    public class FrameDef
    {
        public readonly string id;
        public readonly string label;
        public readonly string colorHex;
        public readonly string source;
        /// <summary>Файл малюнка: Resources/Profile/frames/frame-&lt;art&gt;.</summary>
        public readonly string art;

        public FrameDef(string id, string label, string colorHex, string source, string art)
        {
            this.id = id;
            this.label = label;
            this.colorHex = colorHex;
            this.source = source;
            this.art = art;
        }
    }

    /// <summary>
    /// Малюнки аватарів і рамок (PNG 512×512 у Resources/Profile). Рамка — кільце з прозорим
    /// центром ~70% ширини; аватар — повне коло, тож у рамці він має розмір AvatarInFrame.
    /// </summary>
    public static class ProfileArt
    {
        /// <summary>Розмір аватара відносно рамки, у якій він стоїть (трохи заходить під кільце).</summary>
        public const float AvatarInFrame = 0.74f;

        private static readonly System.Collections.Generic.Dictionary<string, UnityEngine.Sprite> Cache =
            new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();

        public static UnityEngine.Sprite Avatar(string avatarId)
        {
            var def = ProfileItemCatalog.FindAvatar(avatarId) ?? ProfileItemCatalog.FindAvatar(ProfileItemCatalog.StarterAvatarId);
            return Load("Profile/avatars/avatar-" + def.art);
        }

        public static UnityEngine.Sprite Frame(string frameId)
        {
            var def = ProfileItemCatalog.FindFrame(frameId) ?? ProfileItemCatalog.FindFrame(ProfileItemCatalog.StarterFrameId);
            return Load("Profile/frames/frame-" + def.art);
        }

        private static UnityEngine.Sprite Load(string path)
        {
            if (!Cache.TryGetValue(path, out var sprite))
            {
                sprite = UnityEngine.Resources.Load<UnityEngine.Sprite>(path);
                Cache[path] = sprite;
            }
            return sprite;
        }
    }
}
