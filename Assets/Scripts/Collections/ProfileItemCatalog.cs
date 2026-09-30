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
    /// «Клуб садівника», назавжди (Shop/ShopService). "rose" — чесний плейсхолдер «Скоро».
    /// Поки немає окремого арту, аватари — гілочка, тонована кольором tint.
    /// </summary>
    public static class ProfileItemCatalog
    {
        public const string StarterAvatarId = "sprout";
        public const string StarterFrameId = "grey";

        public static readonly AvatarDef[] Avatars =
        {
            new AvatarDef("sprout", "Паросток", "#DDE8C8", null),
            new AvatarDef("cactus", "Кактус", "#A7CE73", "Колекція «Сукуленти»"),
            new AvatarDef("leaf", "Листок", "#7FC98B", "Колекція «Зелене листя»"),
            new AvatarDef("leg", "Орхідея", "#E4C77E", "Колекція «Рідкісні красуні»"),
            // «Лікар рослин» повернеться разом із хворобами (STG 2).
            new AvatarDef("doctor", "Лікар", "#9FB2E6",
                PlantBreeding.Garden.PlantAilments.Enabled ? "Колекція «Лікар рослин»" : "Скоро"),
            new AvatarDef("can", "Золота лійка", "#B9A6E6", "Колекція «Садівник-ветеран»"),
            new AvatarDef("rose", "Троянда", "#E7A6B4", "Скоро"),
            new AvatarDef("sun", "Сонце", "#F3D98B", "Клуб садівника в Крамниці"),
        };

        // grad — колір кільця рамки (тонується на sprig-бейджі/hero-кільці).
        public static readonly FrameDef[] Frames =
        {
            new FrameDef("grey", "Сіра", "#5C6653", null),
            new FrameDef("green", "Зелена", "#A7CE73", null),
            new FrameDef("gold", "Золота", "#E4C77E", null),
            new FrameDef("floral", "Квіткова", "#E7A6B4", "Колекція «Квіти на підвіконні»"),
            new FrameDef("dew", "Роса", "#9FD8E6", "Колекція «Ідеальний догляд»"),
            new FrameDef("marble", "Мармурова", "#EEEAE3", "Колекція горщиків"),
            new FrameDef("spark", "Іскриста", "#B9A6E6", "Колекція «Ботанік»"),
            new FrameDef("club", "Клуб", "#EDD592", "Клуб садівника в Крамниці"),
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

        public AvatarDef(string id, string label, string tintHex, string source)
        {
            this.id = id;
            this.label = label;
            this.tintHex = tintHex;
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
