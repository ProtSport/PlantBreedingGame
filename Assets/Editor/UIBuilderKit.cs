using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using PlantBreeding.UI;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Спільні хелпери для Editor-білдерів екранів (HomeScreenBuilder,
    /// PlantingScreenBuilder): процедурні спрайти (заокруглення/кола/градієнти),
    /// шрифти TMP, дрібні UI-примітиви. Тримає один набір згенерованих
    /// асетів в Assets/Art/Generated, щоб екрани не дублювали графіку.
    /// </summary>
    public static class UIBuilderKit
    {
        public const string GenDir = "Assets/Art/Generated";
        public const string SvgDir = "Assets/Art/SVG";
        public const string FontDir = "Assets/Art/Fonts";

        public static TMP_FontAsset FontHead { get; private set; } // Cormorant Garamond
        public static TMP_FontAsset FontUi { get; private set; }   // Manrope

        // ── Палітра (tokens.css) ─────────────────────────────────────────
        public static readonly Color ColText     = Hex("#F2ECDF");
        public static readonly Color ColTextMut  = Hex("#8C9683");
        public static readonly Color ColSoft     = Hex("#93A088");
        public static readonly Color ColGreen    = Hex("#A7CE73");
        public static readonly Color ColGreenBr  = Hex("#B5E67A");
        public static readonly Color ColGold     = Hex("#C9A65A");
        public static readonly Color ColGoldLt   = Hex("#E4C77E");
        public static readonly Color ColBlue     = Hex("#8FD0E6");
        public static readonly Color ColWarn     = Hex("#E7A66B");
        public static readonly Color ColChipGrow = Hex("#C6D0E8");
        public static readonly Color ColChipOk   = Hex("#CDEBA0");
        public static readonly Color ColChipSick = Hex("#EDB98A");

        /// <summary>Генерує спільні спрайти/шрифти, якщо їх ще немає (ідемпотентно).</summary>
        public static void EnsureGenerated()
        {
            AssetDatabase.Refresh();
            GenerateSprites();
            FontHead = GetOrCreateFont("CormorantGaramond-VF.ttf", "CormorantGaramond SDF");
            FontUi = GetOrCreateFont("Manrope-VF.ttf", "Manrope SDF");
        }

        // ════════════════════════════════════════════════════════════════
        //  ГЕНЕРАЦІЯ СПРАЙТІВ
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Ідемпотентно: якщо спрайт з такою назвою вже існує — НЕ перегенеровує
        /// (інакше видалення+створення асета на тому ж шляху міняє fileID сабасета
        /// і ламає всі вже збережені посилання на нього в сценах/префабах —
        /// а EnsureGenerated() викликається з кількох білдерів поспіль).
        /// </summary>
        static void GenerateSprites()
        {
            Directory.CreateDirectory(GenDir);
            If("rounded-24", () => RoundedRect("rounded-24", 64, 64, 24, true, 0));
            If("rounded-24-line", () => RoundedRect("rounded-24-line", 64, 64, 24, false, 1.5f));
            If("rounded-20", () => RoundedRect("rounded-20", 56, 56, 20, true, 0));
            If("rounded-20-line", () => RoundedRect("rounded-20-line", 56, 56, 20, false, 1.5f));
            If("rounded-16", () => RoundedRect("rounded-16", 48, 48, 16, true, 0));
            If("rounded-16-line", () => RoundedRect("rounded-16-line", 48, 48, 16, false, 1.5f));
            If("rounded-12", () => RoundedRect("rounded-12", 40, 40, 13, true, 0));
            If("rounded-12-line", () => RoundedRect("rounded-12-line", 40, 40, 13, false, 1.5f));
            If("pill-16", () => RoundedRect("pill-16", 48, 32, 16, true, 0));
            If("pill-16-line", () => RoundedRect("pill-16-line", 48, 32, 16, false, 1.5f));
            If("pill-10", () => RoundedRect("pill-10", 36, 22, 11, true, 0));
            If("pill-10-line", () => RoundedRect("pill-10-line", 36, 22, 11, false, 1.5f));
            If("pill-tiny", () => RoundedRect("pill-tiny", 12, 6, 3, true, 0));
            If("circle-fill", () => CircleSprite("circle-fill", 64, true, 0));
            If("circle-line", () => CircleSprite("circle-line", 64, false, 1.5f));
            If("glow", () => GlowSprite("glow", 256));
            If("bg-gradient", () => BackgroundSprite("bg-gradient"));
            If("chevron-left", () => ChevronSprite("chevron-left", 32));
            If("pot-glyph", () => PotGlyphSprite("pot-glyph", 160, 120));
            If("icon-bolt", () => BoltSprite("icon-bolt", 48));
            If("icon-heart", () => HeartSprite("icon-heart", 48));
            If("icon-flask", () => FlaskSprite("icon-flask", 48));
            If("icon-star", () => StarSprite("icon-star", 48));
            If("icon-check", () => CheckSprite("icon-check", 32));
            If("sprig", () => SprigSprite("sprig", 64));
            AssetDatabase.SaveAssets();
        }

        static void If(string name, System.Action generate)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>($"{GenDir}/{name}.asset") != null) return;
            generate();
        }

        static void SaveSprite(string name, Texture2D tex, Vector4 border)
        {
            string path = $"{GenDir}/{name}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);

            tex.name = name;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            sp.name = name;
            AssetDatabase.AddObjectToAsset(sp, tex);
        }

        static void RoundedRect(string name, int w, int h, float r, bool fill, float stroke)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - w / 2f, py = y + 0.5f - h / 2f;
                float qx = Mathf.Abs(px) - (w / 2f - r), qy = Mathf.Abs(py) - (h / 2f - r);
                float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude
                          + Mathf.Min(Mathf.Max(qx, qy), 0) - r + 1f;
                float a = fill
                    ? Mathf.Clamp01(0.5f - d)
                    : Mathf.Clamp01(stroke / 2f + 0.5f - Mathf.Abs(d));
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            float bx = Mathf.Min(r + 2, w / 2f);
            float by = Mathf.Min(r + 2, h / 2f);
            SaveSprite(name, tex, new Vector4(bx, by, bx, by));
        }

        static void CircleSprite(string name, int size, bool fill, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float R = size / 2f - 1.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude - R;
                float a = fill ? Mathf.Clamp01(0.5f - d) : Mathf.Clamp01(stroke / 2f + 0.5f - Mathf.Abs(d));
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            SaveSprite(name, tex, Vector4.zero);
        }

        static void GlowSprite(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float t = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - t), 2.2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            SaveSprite(name, tex, Vector4.zero);
        }

        static void BackgroundSprite(string name)
        {
            // radial-gradient(130% 90% at 50% -5%, #22301C 0%, #151D12 46%, #0D120A 100%)
            int w = 195, h = 422;
            Color c0 = Hex("#22301C"), c1 = Hex("#151D12"), c2 = Hex("#0D120A");
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = 0.5f * w, cy = 1.05f * h;
            float rx = 1.30f * w, ry = 0.90f * h;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                float t = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = t < 0.46f ? Color.Lerp(c0, c1, t / 0.46f) : Color.Lerp(c1, c2, (t - 0.46f) / 0.54f);
                c.a = 1;
                tex.SetPixel(x, y, c);
            }
            SaveSprite(name, tex, Vector4.zero);
        }

        static void ChevronSprite(string name, int size)
        {
            // Проста "<"-стрілка з двох діагональних відрізків, товщина ~2px.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1, 1, 1, 0));

            float cx = size * 0.42f, cy = size * 0.5f, len = size * 0.28f, thick = size * 0.09f;
            DrawSegment(tex, size, cx + len, cy + len, cx, cy, thick);
            DrawSegment(tex, size, cx, cy, cx + len, cy - len, thick);
            SaveSprite(name, tex, Vector4.zero);
        }

        static void DrawSegment(Texture2D tex, int size, float x0, float y0, float x1, float y1, float thickness)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = DistanceToSegment(x + 0.5f, y + 0.5f, x0, y0, x1, y1);
                float a = Mathf.Clamp01(thickness / 2f + 0.5f - d);
                if (a <= 0f) continue;
                var existing = tex.GetPixel(x, y);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(existing.a, a)));
            }
        }

        static float DistanceToSegment(float px, float py, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float lenSq = dx * dx + dy * dy;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / lenSq) : 0f;
            float projX = x0 + t * dx, projY = y0 + t * dy;
            return new Vector2(px - projX, py - projY).magnitude;
        }

        /// <summary>
        /// Спільний силует горщика (трапеція + вінце) з вертикальним затіненням,
        /// запеченим у RGB. Тонується кольором конкретного горщика через Image.color
        /// (множення), тому один спрайт покриває всі 4 горщики без окремого арту.
        /// </summary>
        static void PotGlyphSprite(string name, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = w / 2f;
            float bodyBottomY = h * 0.08f;
            float bodyTopY = h * 0.62f;
            float rimTopY = h * 0.80f;
            float bodyBottomHalfW = w * 0.20f;
            float bodyTopHalfW = w * 0.30f;
            float rimHalfW = w * 0.36f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float alpha = 0f, shade = 1f;

                if (fy <= bodyTopY)
                {
                    float t = Mathf.Clamp01((fy - bodyBottomY) / (bodyTopY - bodyBottomY));
                    float halfW = Mathf.Lerp(bodyBottomHalfW, bodyTopHalfW, t);
                    float edge = halfW - Mathf.Abs(fx - cx);
                    float bottomEdge = fy - bodyBottomY;
                    alpha = Mathf.Clamp01(edge + 0.5f) * Mathf.Clamp01(bottomEdge + 0.5f);
                    shade = Mathf.Lerp(0.60f, 0.86f, t);
                }
                else if (fy <= rimTopY)
                {
                    float t = Mathf.Clamp01((fy - bodyTopY) / (rimTopY - bodyTopY));
                    float edge = rimHalfW - Mathf.Abs(fx - cx);
                    alpha = Mathf.Clamp01(edge + 0.5f);
                    shade = Mathf.Lerp(0.90f, 1.0f, t);
                }

                tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
            SaveSprite(name, tex, Vector4.zero);
        }

        /// <summary>
        /// Спільний растеризатор для дрібних піктограм Лабораторії (bolt/heart/flask):
        /// біла альфа-маска в 24×24-просторі (як viewBox React-макету), тонується
        /// через Image.color під колір гілки — той самий підхід, що вже
        /// використовують RoundedRect/CircleSprite/ChevronSprite вище.
        /// </summary>
        static void ShapeSprite(string name, int size, Func<float, float, bool> insideFn)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            const int S = 3;
            float unitsPerPixel = 24f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hit = 0f;
                for (int sy = 0; sy < S; sy++)
                for (int sx = 0; sx < S; sx++)
                {
                    float px = (x + (sx + 0.5f) / S) * unitsPerPixel;
                    float py = 24f - (y + (sy + 0.5f) / S) * unitsPerPixel; // SVG Y росте вниз, текстура — вгору
                    if (insideFn(px, py)) hit += 1f;
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, hit / (S * S)));
            }
            SaveSprite(name, tex, Vector4.zero);
        }

        static bool PointInPolygon(float px, float py, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > py) != (poly[j].y > py) &&
                    px < (poly[j].x - poly[i].x) * (py - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // Блискавка: "M13 2 L4 14 H11 L10 22 L20 9 H13 Z" з lab-research.html.
        static void BoltSprite(string name, int size)
        {
            var poly = new[]
            {
                new Vector2(13, 2), new Vector2(4, 14), new Vector2(11, 14),
                new Vector2(10, 22), new Vector2(20, 9), new Vector2(13, 9),
            };
            ShapeSprite(name, size, (px, py) => PointInPolygon(px, py, poly));
        }

        // Зірка: "M12 3 L14.6 9 L21 9.5 L16 13.8 L17.6 20 L12 16.5 L6.4 20 L8 13.8 L3 9.5 L9.4 9 Z" з home-screen-components.html (TasksButton).
        static void StarSprite(string name, int size)
        {
            var poly = new[]
            {
                new Vector2(12, 3), new Vector2(14.6f, 9), new Vector2(21, 9.5f), new Vector2(16, 13.8f),
                new Vector2(17.6f, 20), new Vector2(12, 16.5f), new Vector2(6.4f, 20), new Vector2(8, 13.8f),
                new Vector2(3, 9.5f), new Vector2(9.4f, 9),
            };
            ShapeSprite(name, size, (px, py) => PointInPolygon(px, py, poly));
        }

        // Серце: два кола (верхні частки) + трикутник (нижнє вістря) — стилізоване наближення.
        static void HeartSprite(string name, int size)
        {
            var c1 = new Vector2(8.3f, 15.2f);
            var c2 = new Vector2(15.7f, 15.2f);
            const float r = 5.2f;
            var tri = new[] { new Vector2(3f, 14.5f), new Vector2(21f, 14.5f), new Vector2(12f, 2f) };
            ShapeSprite(name, size, (px, py) =>
                (new Vector2(px, py) - c1).magnitude <= r ||
                (new Vector2(px, py) - c2).magnitude <= r ||
                PointInPolygon(px, py, tri));
        }

        // Колба: спрощений силует (пряма шийка + конус тіла) з горизонтальною рискою вінця.
        static void FlaskSprite(string name, int size)
        {
            var body = new[]
            {
                new Vector2(10, 3), new Vector2(10, 9), new Vector2(5, 19), new Vector2(7, 21),
                new Vector2(17, 21), new Vector2(19, 19), new Vector2(14, 9), new Vector2(14, 3),
            };
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            const int S = 3;
            float unitsPerPixel = 24f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hit = 0f;
                for (int sy = 0; sy < S; sy++)
                for (int sx = 0; sx < S; sx++)
                {
                    float px = (x + (sx + 0.5f) / S) * unitsPerPixel;
                    float py = 24f - (y + (sy + 0.5f) / S) * unitsPerPixel;
                    if (PointInPolygon(px, py, body)) hit += 1f;
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, hit / (S * S)));
            }
            // Рисочка вінця "M9 3 H15" (у текстурних пікселях, з поправкою на переворот Y).
            DrawSegment(tex, size, 9 / unitsPerPixel, size - 3 / unitsPerPixel, 15 / unitsPerPixel, size - 3 / unitsPerPixel, 1.6f / unitsPerPixel);
            SaveSprite(name, tex, Vector4.zero);
        }

        /// <summary>
        /// Стилізована гілочка рослини (стебло + 4 листки-краплі) — біла
        /// альфа-маска в 60×60-просторі (як viewBox &lt;Sprig&gt; з макету
        /// dex-collections.html), тонується через Image.color під колір рідкості
        /// слота. Один спрайт покриває всі рідкості (green/gold/rose/blue/violet)
        /// без окремого арту — той самий підхід, що PotGlyphSprite/BoltSprite.
        /// </summary>
        static void SprigSprite(string name, int size)
        {
            // Стебло (viewBox-координати, y росте вниз як у SVG): M30 52 → M30 24.
            var stemA = new Vector2(30, 52);
            var stemB = new Vector2(30, 24);
            const float stemThick = 3.4f;

            // Листки: (база на стеблі, кут повороту °, масштаб) — з <Leaf> макету.
            var leaves = new[]
            {
                (baseP: new Vector2(30, 40), angle: -36f, scale: 0.5f),
                (baseP: new Vector2(30, 36), angle: 34f, scale: 0.54f),
                (baseP: new Vector2(30, 28), angle: -16f, scale: 0.58f),
                (baseP: new Vector2(30, 26), angle: 12f, scale: 0.5f),
            };

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            const int S = 3;
            float unitsPerPixel = 60f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hit = 0f;
                for (int sy = 0; sy < S; sy++)
                for (int sx = 0; sx < S; sx++)
                {
                    float px = (x + (sx + 0.5f) / S) * unitsPerPixel;
                    // Текстура має y-up, SVG-координати стебла/листків — y-down:
                    // svgY = 60 - texY. p — точка вже в SVG-просторі (y-down).
                    float svgY = 60f - (y + (sy + 0.5f) / S) * unitsPerPixel;
                    var p = new Vector2(px, svgY);

                    bool inside = DistanceToSegment(p.x, p.y, stemA.x, stemA.y, stemB.x, stemB.y) <= stemThick / 2f;
                    if (!inside)
                    {
                        foreach (var lf in leaves)
                        {
                            if (PointInLeaf(p, lf.baseP, lf.angle, lf.scale)) { inside = true; break; }
                        }
                    }
                    if (inside) hit += 1f;
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, hit / (S * S)));
            }
            SaveSprite(name, tex, Vector4.zero);
        }

        // Листок-крапля як еліпс: центр зсунуто від бази вздовж напрямку повороту,
        // точку переводимо в локальні координати листка й перевіряємо еліпс.
        static bool PointInLeaf(Vector2 p, Vector2 baseP, float angleDeg, float scale)
        {
            float halfH = 23f * scale;   // листок тягнеться на 0..-46 (половина 23)
            float halfW = 8.5f * scale;
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            // центр = base + Rot(angle)·(0, -halfH) у SVG-просторі (y-down, поворот за годинниковою)
            var center = new Vector2(baseP.x - (-halfH) * sin, baseP.y + (-halfH) * cos);
            var d = p - center;
            // локальні координати (обертання назад)
            float lx = d.x * cos + d.y * sin;
            float ly = -d.x * sin + d.y * cos;
            return (lx * lx) / (halfW * halfW) + (ly * ly) / (halfH * halfH) <= 1f;
        }

        // Галочка: "M6 12 L10 16 L18 7" — два сегменти, як ChevronSprite вище.
        static void CheckSprite(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1, 1, 1, 0));

            float s = size / 24f, thick = 2.6f * s;
            DrawSegment(tex, size, 6 * s, size - 12 * s, 10 * s, size - 16 * s, thick);
            DrawSegment(tex, size, 10 * s, size - 16 * s, 18 * s, size - 7 * s, thick);
            SaveSprite(name, tex, Vector4.zero);
        }

        // ════════════════════════════════════════════════════════════════
        //  ШРИФТИ
        // ════════════════════════════════════════════════════════════════
        static TMP_FontAsset GetOrCreateFont(string ttfFile, string assetName)
        {
            string path = $"{FontDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{ttfFile}");
            if (font == null)
            {
                Debug.LogWarning($"[PlantBreeding] Не знайдено {ttfFile} — використовую стандартний шрифт TMP.");
                return TMP_Settings.defaultFontAsset;
            }
            var fa = TMP_FontAsset.CreateFontAsset(font, 60, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fa == null) return TMP_Settings.defaultFontAsset;
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, path);
            fa.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
            {
                fa.atlasTextures[0].name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ════════════════════════════════════════════════════════════════
        //  ДРІБНІ ХЕЛПЕРИ
        // ════════════════════════════════════════════════════════════════
        public static Sprite LoadSprite(string name, string dir = GenDir)
        {
            string ext = dir == SvgDir ? "svg" : "asset";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.{ext}");
            if (sp == null) Debug.LogWarning($"[PlantBreeding] Спрайт не знайдено: {dir}/{name}.{ext}");
            return sp;
        }

        public static Image MakeImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI MakeLabel(string name, Transform parent, string text,
            TMP_FontAsset font, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Пігулка валюти (іконка + число) — однаковий вигляд на всіх екранах.</summary>
        public static TMP_Text MakeCurrencyPill(Transform parent, string name, string icon, string value)
        {
            var pill = MakeImage(name, parent, LoadSprite("pill-16"), Rgba(Color.white, 0.055f), Image.Type.Sliced);
            var line = MakeImage("Border", pill.transform, LoadSprite("pill-16-line"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            var hl = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(7, 13, 6, 6);
            hl.spacing = 7; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var ico = MakeImage("Icon", pill.transform, LoadSprite(icon, SvgDir), Color.white, Image.Type.Simple);
            ico.preserveAspect = true;
            ico.gameObject.AddComponent<LayoutElement>().preferredWidth = 18;
            ico.GetComponent<LayoutElement>().preferredHeight = 18;

            var txt = MakeLabel("Value", pill.transform, value, FontUi, 14, Hex("#EBE4D6"), FontStyles.Bold);
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            return txt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ════════════════════════════════════════════════════════════════
        //  ЄДИНА ШАПКА ВСІХ ЕКРАНІВ
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Однакова шапка для Саду, Посадки, Лабораторії, Дендрарію й Профілю:
        /// рядок (y=-44, висота 42) [назад?] [★ завдання] [⚙ налаштування] … [монети] [кристали] [аватар]
        /// і під ним (y=-104) рядок «РІВЕНЬ N · xp/next XP». Оживляє її HeaderView.
        /// withBack — для екранів без нижнього меню (Посадка, Профіль).
        /// </summary>
        public static HeaderView BuildStandardHeader(Transform root, bool withBack, out Button backButton)
        {
            var row = new GameObject("HeaderRow", typeof(RectTransform));
            row.transform.SetParent(root, false);
            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = new Vector2(0, 1); rowRt.anchorMax = new Vector2(1, 1);
            rowRt.pivot = new Vector2(0.5f, 1);
            rowRt.anchoredPosition = new Vector2(0, -44);
            rowRt.sizeDelta = new Vector2(-40, 42);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            // З «назад» у рядку 6 елементів — щільніше, щоб вмістилось у 350 px.
            hl.spacing = withBack ? 6 : 9; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            backButton = withBack ? MakeBackButton(row.transform) : null;
            var tasksButton = MakeTasksButton(row.transform, out var badgeGo, out var badgeCount);
            var settingsButton = MakeSettingsButton(row.transform);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(row.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1;

            var coinsLabel = MakeCurrencyPill(row.transform, "CoinPill", "icon-coin", "0");
            var gemsLabel = MakeCurrencyPill(row.transform, "GemPill", "icon-gem", "0");
            MakeAvatarButton(row.transform);

            // Пілюля кристалів — кнопка (Крамниця).
            var gemPill = gemsLabel.transform.parent.gameObject;
            var gemImg = gemPill.GetComponent<Image>();
            gemImg.raycastTarget = true;
            var gemBtn = gemPill.AddComponent<Button>();
            gemBtn.targetGraphic = gemImg;

            var level = MakeLabel("LevelLine", root, "РІВЕНЬ 1", FontUi, 12, ColTextMut, FontStyles.Bold);
            level.characterSpacing = 6;
            Place(level.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -104), new Vector2(300, 16));
            level.alignment = TextAlignmentOptions.BottomLeft;
            level.textWrappingMode = TextWrappingModes.NoWrap;

            var view = row.AddComponent<HeaderView>();
            view.coinsLabel = coinsLabel;
            view.gemsLabel = gemsLabel;
            view.levelLabel = level;
            view.tasksButton = tasksButton;
            view.tasksBadge = badgeGo;
            view.tasksBadgeLabel = badgeCount;
            view.gemsButton = gemBtn;
            view.settingsButton = settingsButton;
            return view;
        }

        /// <summary>Кнопка-шестірня «Налаштування» (тема, мова) — друга зліва, макет «Світла-Темна_тема_Мови».</summary>
        static Button MakeSettingsButton(Transform parent)
        {
            var go = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 42; le.preferredHeight = 42;
            var bg = go.GetComponent<Image>();
            bg.sprite = LoadSprite("rounded-12"); bg.type = Image.Type.Sliced;
            bg.color = Rgba(Color.white, 0.055f);
            var line = MakeImage("Border", go.transform, LoadSprite("rounded-12-line"), Rgba(Color.white, 0.12f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            var gear = MakeImage("Gear", go.transform, LoadSprite("icon-settings", SvgDir), Color.white, Image.Type.Simple);
            gear.preserveAspect = true;
            Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(21, 21));
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            return btn;
        }

        /// <summary>Квадратна кнопка «назад» зі стрілкою — перша в рядку шапки.</summary>
        static Button MakeBackButton(Transform parent)
        {
            var backGO = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            backGO.transform.SetParent(parent, false);
            backGO.GetComponent<LayoutElement>().preferredWidth = 42;
            backGO.GetComponent<LayoutElement>().preferredHeight = 42;
            var backImg = backGO.GetComponent<Image>();
            backImg.sprite = LoadSprite("rounded-12"); backImg.type = Image.Type.Sliced; backImg.color = Rgba(Color.white, 0.055f);
            var backLine = MakeImage("Border", backGO.transform, LoadSprite("rounded-12-line"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(backLine.rectTransform);
            var chevron = MakeImage("Chevron", backGO.transform, LoadSprite("chevron-left"), Hex("#C9CFC1"), Image.Type.Simple);
            Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var btn = backGO.GetComponent<Button>();
            btn.targetGraphic = backImg;
            return btn;
        }

        /// <summary>
        /// Кругла кнопка-аватар — точка входу в «Профіль» (ТЗ «Персонаж»).
        /// Кільце тонується кольором рівня престижу рантайм-компонентом
        /// ProfileEntryButtonView (grey→green→gold→spark, GameManager.GetPrestigeTier()).
        /// </summary>
        static void MakeAvatarButton(Transform parent)
        {
            var btnGo = new GameObject("AvatarButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(parent, false);
            var le = btnGo.GetComponent<LayoutElement>();
            le.preferredWidth = 42; le.preferredHeight = 42;
            var ring = btnGo.GetComponent<Image>();
            ring.sprite = LoadSprite("circle-line");
            ring.color = Hex("#5C6653");
            ring.raycastTarget = true;

            var face = MakeImage("Face", btnGo.transform, LoadSprite("circle-fill"), Hex("#1C2414"), Image.Type.Simple);
            Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));

            var icon = MakeImage("Sprig", btnGo.transform, LoadSprite("sprig"), Hex("#DDE8C8"), Image.Type.Simple);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));

            var nav = btnGo.AddComponent<SceneNavButton>();
            nav.action = SceneNavButton.NavAction.OpenAdditive;
            nav.sceneName = "Profile";
            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = ring;

            var view = btnGo.AddComponent<ProfileEntryButtonView>();
            view.ringImage = ring;

            var dot = MakeImage("OnlineDot", btnGo.transform, LoadSprite("circle-fill"), ColGreenBr, Image.Type.Simple);
            Place(dot.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-1, -1), new Vector2(12, 12));
            var dotLine = MakeImage("Border", dot.transform, LoadSprite("circle-line"), Hex("#12180D"), Image.Type.Simple);
            Stretch(dotLine.rectTransform);
        }

        /// <summary>
        /// Кнопка-зірка «Завдання дня» (ліворуч у шапці, home-screen-components.html).
        /// Відкриває панель EconomyOverlayView (нагорода за вхід + 3 щоденні
        /// завдання + скриня). Бейдж вмикає HeaderView, коли є незабрані нагороди.
        /// </summary>
        static Button MakeTasksButton(Transform parent, out GameObject badgeGo, out TMP_Text badgeCount)
        {
            var btnGo = new GameObject("TasksButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(parent, false);
            var le = btnGo.GetComponent<LayoutElement>();
            le.preferredWidth = 42; le.preferredHeight = 42;
            var bg = btnGo.GetComponent<Image>();
            bg.sprite = LoadSprite("rounded-12"); bg.type = Image.Type.Sliced;
            bg.color = Rgba(ColGoldLt, 0.14f);
            bg.raycastTarget = true;
            var line = MakeImage("Border", btnGo.transform, LoadSprite("rounded-12-line"), Rgba(ColGoldLt, 0.34f), Image.Type.Sliced);
            Stretch(line.rectTransform);

            var star = MakeImage("Star", btnGo.transform, LoadSprite("icon-star"), ColGold, Image.Type.Simple);
            star.preserveAspect = true;
            Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

            var badge = MakeImage("Badge", btnGo.transform, LoadSprite("circle-fill"), Hex("#D9744F"), Image.Type.Simple);
            Place(badge.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-5, -5), new Vector2(19, 19));
            var badgeLine = MakeImage("Border", badge.transform, LoadSprite("circle-line"), Hex("#12180D"), Image.Type.Simple);
            Stretch(badgeLine.rectTransform);
            var badgeLabel = MakeLabel("Count", badge.transform, "", FontUi, 11, Color.white, FontStyles.Bold);
            Stretch(badgeLabel.rectTransform);
            badgeLabel.alignment = TextAlignmentOptions.Center;
            badge.gameObject.SetActive(false);
            badgeGo = badge.gameObject;
            badgeCount = badgeLabel;

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = bg;
            return btn;
        }
    }
}
