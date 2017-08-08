using UnityEngine;
using Verse;

namespace ImpassableMapMaker
{
    public class SettingsController : Mod
    {
        public SettingsController(ModContentPack content) : base(content)
        {
            base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
        {
            return "Impassable Map Maker";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }

    public class Settings : ModSettings
    {
        private const float DEFAULT_PERCENT_OFFSET = 5f;
        private static float percentOffset = DEFAULT_PERCENT_OFFSET;
        public static float PercentOffset { get { return percentOffset; } }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look<float>(ref percentOffset, "ImpassableMapMaker.percentOffset", DEFAULT_PERCENT_OFFSET, true);
        }

        public static void DoSettingsWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(5f, 45f, 400f, 32f), "Open Area Max Offset From Middle");
            percentOffset = Widgets.HorizontalSlider(new Rect(5f, 85f, 100f, 32f), percentOffset, 0, 25);
            Widgets.Label(new Rect(110f, 85f, 40f, 32f), percentOffset.ToString("N1") + "%");
            if (Widgets.ButtonText(new Rect(5f, 135f, 60f, 32f), "Default"))
            {
                percentOffset = DEFAULT_PERCENT_OFFSET;
            }
        }
    }
}