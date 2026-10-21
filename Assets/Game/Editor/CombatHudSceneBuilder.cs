using Roguelike.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>场景 HUD 缺失时创建可编辑布局；不覆盖已有布局。</summary>
    internal sealed class CombatHudSceneBuilder
    {
        private Font font;
        private RectTransform bottom;
        private RectTransform titleRect, hintRect;
        private Image healthFill;
        private Text healthText, counterText, clockText, detailText, resultText;
        private GameObject deathOverlay;
        private Button restartButton;
        private readonly Color ink = new Color(0.84f, 0.88f, 0.87f);
        private readonly Color gold = new Color(0.77f, 0.66f, 0.39f);

        public CombatHud Build(Transform parent, Font assetFont)
        {
            font = assetFont;
            var gameObject = new GameObject("Combat HUD", typeof(RectTransform));
            var transform = gameObject.transform;
            transform.SetParent(parent, false);
            var hud = gameObject.AddComponent<CombatHud>();
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var title = Panel("Title", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, 72), new Vector2(24, -20), new Vector2(0, 1), new Color(0.035f, 0.052f, 0.057f, 0.93f));
            Label("Name", title, "万 人 围 城", 26, gold, new Vector2(18, -6), new Vector2(270, 38));
            Label("Subtitle", title, "GAME  /  SURVIVE THE SWARM", 12, ink, new Vector2(20, -44), new Vector2(270, 20));
            var hints = Panel("Controls", transform, Vector2.one, Vector2.one, new Vector2(380, 62), new Vector2(-24, -22), Vector2.one, new Color(0, 0, 0, 0.55f));
            titleRect = title;
            hintRect = hints;
            Label("Hints", hints, "右键点击 / 按住移动    ·    滚轮缩放\n自动攻击最近敌人    ·    F5 重开", 16, ink, new Vector2(16, -8), new Vector2(360, 48));

            bottom = Panel("Bottom HUD", transform, Vector2.zero, Vector2.right, new Vector2(0, 174), Vector2.zero, new Vector2(0.5f, 0), new Color(0.028f, 0.044f, 0.05f, 0.98f));
            var rule = Panel("Gold edge", bottom, Vector2.up, Vector2.one, new Vector2(0, 3), Vector2.zero, new Vector2(0.5f, 1), gold);
            var hero = Panel("Hero", bottom, Vector2.zero, Vector2.zero, new Vector2(420, 150), new Vector2(28, 12), Vector2.zero, new Color(0.065f, 0.10f, 0.12f));
            Label("HeroName", hero, "先锋  /  VANGUARD", 23, ink, new Vector2(18, -10), new Vector2(390, 34));
            var hp = Panel("Health", hero, Vector2.up, Vector2.up, new Vector2(380, 27), new Vector2(18, -52), Vector2.up, new Color(0.02f, 0.035f, 0.04f));
            healthFill = Panel("Fill", hp, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.16f, 0.57f, 0.36f)).GetComponent<Image>();
            healthText = Label("HP", hp, "生命  300 / 300", 17, Color.white, Vector2.zero, new Vector2(380, 27));
            healthText.alignment = TextAnchor.MiddleCenter;
            detailText = Label("Weapon", hero, "追踪弹 · 自动攻击    射程 20m\n移动中持续开火", 16, ink, new Vector2(18, -93), new Vector2(390, 44));

            var skills = Panel("Abilities", bottom, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(500, 146), new Vector2(0, 12), new Vector2(0.5f, 0), Color.clear);
            Label("AbilityTitle", skills, "寒冰主动技能", 16, gold, new Vector2(0, -2), new Vector2(500, 28)).alignment = TextAnchor.MiddleCenter;
            string[] keys = { "Q", "W", "E", "R" };
            for (int i = 0; i < keys.Length; i++)
            {
                var slot = Panel("Skill " + keys[i], skills, Vector2.up, Vector2.up, new Vector2(90, 90), new Vector2(38 + i * 112, -38), Vector2.up, new Color(0.075f, 0.095f, 0.105f));
                Label("Key", slot, keys[i], 25, gold, new Vector2(0, -9), new Vector2(90, 38)).alignment = TextAnchor.MiddleCenter;
                Label("Status", slot, "就绪", 14, new Color(0.45f, 0.5f, 0.51f), new Vector2(0, -52), new Vector2(90, 24)).alignment = TextAnchor.MiddleCenter;
            }
            var stats = Panel("Battle stats", bottom, Vector2.right, Vector2.right, new Vector2(380, 148), new Vector2(-26, 12), Vector2.right, new Color(0.065f, 0.10f, 0.12f));
            clockText = Label("Clock", stats, "00:00     -- FPS", 22, gold, new Vector2(18, -8), new Vector2(345, 33));
            counterText = Label("Counters", stats, "存活敌人   10,000\n击杀   0       弹体   0", 18, ink, new Vector2(18, -51), new Vector2(345, 84));

            deathOverlay = Panel("Defeat", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.01f, 0.02f, 0.025f, 0.85f)).gameObject;
            var card = Panel("Result", deathOverlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(540, 280), Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.045f, 0.065f, 0.075f));
            Label("DefeatTitle", card, "战斗结束", 38, gold, new Vector2(0, -24), new Vector2(540, 60)).alignment = TextAnchor.MiddleCenter;
            resultText = Label("ResultText", card, "生存 0.0 秒    ·    击杀 0", 22, ink, new Vector2(0, -103), new Vector2(540, 48));
            resultText.alignment = TextAnchor.MiddleCenter;
            var restart = Panel("Restart", card, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(260, 56), new Vector2(0, 28), new Vector2(0.5f, 0), new Color(0.16f, 0.36f, 0.30f));
            var button = restart.gameObject.AddComponent<Button>();
            button.targetGraphic = restart.GetComponent<Image>();
            restartButton = button;
            Label("RestartLabel", restart, "重新开始  /  F5", 21, Color.white, Vector2.zero, new Vector2(260, 56)).alignment = TextAnchor.MiddleCenter;
            deathOverlay.SetActive(false);
            var serialized = new SerializedObject(hud);
            Bind(serialized, "bottom", bottom); Bind(serialized, "titleRect", titleRect);
            Bind(serialized, "hintRect", hintRect); Bind(serialized, "healthFill", healthFill);
            Bind(serialized, "healthText", healthText); Bind(serialized, "counterText", counterText);
            Bind(serialized, "clockText", clockText); Bind(serialized, "detailText", detailText);
            Bind(serialized, "resultText", resultText); Bind(serialized, "deathOverlay", deathOverlay);
            Bind(serialized, "restartButton", restartButton);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        private static void Bind(SerializedObject serialized, string name, Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private RectTransform Panel(string label, Transform parent, Vector2 min, Vector2 max,
            Vector2 size, Vector2 position, Vector2 pivot, Color color)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = color;
            return rect;
        }

        private Text Label(string name, Transform parent, string value, int size, Color color, Vector2 position, Vector2 bounds)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = position; rect.sizeDelta = bounds;
            var text = obj.GetComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color; text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

    }
}
