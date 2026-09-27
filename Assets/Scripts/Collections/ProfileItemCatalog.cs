namespace PlantBreeding.Collections
{
    /// <summary>
    /// Статичний каталог предметів кастомізації профілю (ТЗ «Персонаж»,
    /// 2026-08) — дзеркалить CollectionCatalog.cs. Джерело правди "чи є в
    /// гравця предмет" — PlayerData.ownedAvatarIds/ownedFrameIds, не цей
    /// каталог; тут лише опис усіх МОЖЛИВИХ предметів + звідки їх узяти.
    ///
    /// Реально розблоковані зараз: стартовий аватар "sprout" і рамки
    /// grey/green/gold (авто-видаються за рівнем престижу, GameManager.
    /// SyncPrestigeFrames). Решта — чесний плейсхолдер із мокапу
    /// (Персонаж/export/profile-screen.html): locked із source, куди
    /// насправді ведуть Завдання тижня, яких ще немає в грі.
    /// </summary>
    public static class ProfileItemCatalog
    {
        public const string StarterAvatarId = "sprout";
        public const string StarterFrameId = "grey";

        public static readonly AvatarDef[] Avatars =
        {
            new AvatarDef("sprout", null),
            new AvatarDef("rose", "Скоро"),
            new AvatarDef("sun", "Скоро"),
            new AvatarDef("w3", "Завдання тижня 3"),
            new AvatarDef("w4", "Завдання тижня 4"),
            new AvatarDef("w5", "Завдання тижня 5"),
            new AvatarDef("leg", "Легендарна колекція"),
            new AvatarDef("w7", "Завдання тижня 7"),
        };

        // grad — колір кільця рамки (тонується на sprig-бейджі/hero-кільці).
        public static readonly FrameDef[] Frames =
        {
            new FrameDef("grey", "Сіра", "#5C6653", null),
            new FrameDef("green", "Зелена", "#A7CE73", null),
            new FrameDef("gold", "Золота", "#E4C77E", null),
            new FrameDef("spark", "Іскриста", "#B9A6E6", "Завдання тижня 6"),
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
        public readonly string source;

        public AvatarDef(string id, string source)
        {
            this.id = id;
            this.source = source;
        }
    }

    /// <summary>Одна можлива рамка профілю. source == null → реально досяжна зараз.</summary>
    public class FrameDef
    {
        public readonly string id;
        public readonly string label;
        public readonly string colorHex;
        public readonly string source;

        public FrameDef(string id, string label, string colorHex, string source)
        {
            this.id = id;
            this.label = label;
            this.colorHex = colorHex;
            this.source = source;
        }
    }
}
