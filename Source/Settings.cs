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
        private const int DEFAULT_OPEN_AREA_SIZE = 54;
        private const int DEFAULT_WALLS_SMOOTHNESS = 10;
        private const int DEFAULT_PEREMETER_BUFFER = 6;

        private static float percentOffset = DEFAULT_PERCENT_OFFSET;
        public static float PercentOffset { get { return percentOffset; } }
        public static int OpenAreaSizeX = DEFAULT_OPEN_AREA_SIZE;
        public static int OpenAreaSizeZ = DEFAULT_OPEN_AREA_SIZE;
        public static int MiddleWallSmoothness = 10;
        public static int PeremeterBuffer = DEFAULT_PEREMETER_BUFFER;
        public static bool HasMiddleArea = true;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look<bool>(ref HasMiddleArea, "ImpassableMapMaker.hasMiddleArea", true, false);
            Scribe_Values.Look<float>(ref percentOffset, "ImpassableMapMaker.percentOffset", DEFAULT_PERCENT_OFFSET, true);
            Scribe_Values.Look<int>(ref OpenAreaSizeX, "ImpassableMapMaker.OpenAreaSizeX", DEFAULT_OPEN_AREA_SIZE, false);
            Scribe_Values.Look<int>(ref OpenAreaSizeZ, "ImpassableMapMaker.OpenAreaSizeZ", DEFAULT_OPEN_AREA_SIZE, false);
            Scribe_Values.Look<int>(ref PeremeterBuffer, "ImpassableMapMaker.PeremeterBuffer", DEFAULT_PEREMETER_BUFFER, false);
            Scribe_Values.Look<int>(ref MiddleWallSmoothness, "ImpassableMapMaker.MakeWallsSmooth", DEFAULT_WALLS_SMOOTHNESS, false);
        }

        public static void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(new Rect(5f, 45f, 400f, 600f));

            ls.CheckboxLabeled("ImpassableMapMaker.HasMiddleArea".Translate(), ref HasMiddleArea);
            if (HasMiddleArea)
            {
                ls.Label("ImpassableMapMaker.OpenAreaMaxOffsetFromMiddle".Translate() + ": " + percentOffset.ToString("N1") + "%");
                percentOffset = ls.Slider(percentOffset, 0, 25);
                if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
                {
                    percentOffset = DEFAULT_PERCENT_OFFSET;
                }
            }
            ls.GapLine();


            ls.Label("ImpassableMapMaker.EdgeBuffer".Translate() + ": " + PeremeterBuffer.ToString());
            ls.Label("<< " + "ImpassableMapMaker.Smaller".Translate() + " -- " + "ImpassableMapMaker.Larger".Translate() + " >>");
            PeremeterBuffer = (int)ls.Slider(PeremeterBuffer, 3, 12);
            if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
            {
                PeremeterBuffer = DEFAULT_PEREMETER_BUFFER;
            }
            ls.Label("ImpassableMapMaker.EdgeBufferWarning".Translate());
            ls.GapLine();


            ls.Label("ImpassableMapMaker.OpenAreaSize".Translate());
            ls.Label("ImpassableMapMaker.Width".Translate() + ": " + OpenAreaSizeZ);
            OpenAreaSizeZ = (int)ls.Slider(OpenAreaSizeZ, 40, 75);
            ls.Label("ImpassableMapMaker.Height".Translate() + ": " + OpenAreaSizeX);
            OpenAreaSizeX = (int)ls.Slider(OpenAreaSizeX, 40, 75);
            if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
            {
                OpenAreaSizeX = DEFAULT_OPEN_AREA_SIZE;
                OpenAreaSizeZ = DEFAULT_OPEN_AREA_SIZE;
            }
            ls.GapLine();


            ls.Label("ImpassableMapMaker.MiddleOpeningWallSmoothnes".Translate());
            ls.Label("<< " + "ImpassableMapMaker.Smooth".Translate() + " -- " + "ImpassableMapMaker.Rough".Translate() + " >>");
            MiddleWallSmoothness = (int)ls.Slider(MiddleWallSmoothness, 0, 20);
            if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
            {
                MiddleWallSmoothness = 10;
            }
            ls.End();
        }
    }
}