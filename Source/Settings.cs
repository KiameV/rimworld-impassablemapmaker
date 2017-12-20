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

    public enum ImpassableShape
    {
        Square,
        Round
    }

    public class Settings : ModSettings
    {
        private const int GAP_SIZE = 20;
        private const float DEFAULT_PERCENT_OFFSET = 5f;
        private const int DEFAULT_OPEN_AREA_SIZE = 54;
        private const int DEFAULT_WALLS_SMOOTHNESS = 10;
        private const int DEFAULT_PEREMETER_BUFFER = 6;

        private static Vector2 scrollPosition = Vector2.zero;

        private static float percentOffset = DEFAULT_PERCENT_OFFSET;
        public static float PercentOffset { get { return percentOffset; } }
        public static int OpenAreaSizeX = DEFAULT_OPEN_AREA_SIZE;
        public static int OpenAreaSizeZ = DEFAULT_OPEN_AREA_SIZE;
        public static int MiddleWallSmoothness = 10;
        public static int PeremeterBuffer = DEFAULT_PEREMETER_BUFFER;
        public static bool HasMiddleArea = true;
        public static ImpassableShape shape = ImpassableShape.Square;
        public static bool ScatteredRocks = true;

        public override void ExposeData()
        {
            base.ExposeData();

            string s = shape.ToString();

            Scribe_Values.Look<bool>(ref HasMiddleArea, "ImpassableMapMaker.hasMiddleArea", true, false);
            Scribe_Values.Look<float>(ref percentOffset, "ImpassableMapMaker.percentOffset", DEFAULT_PERCENT_OFFSET, false);
            Scribe_Values.Look<int>(ref OpenAreaSizeX, "ImpassableMapMaker.OpenAreaSizeX", DEFAULT_OPEN_AREA_SIZE, false);
            Scribe_Values.Look<int>(ref OpenAreaSizeZ, "ImpassableMapMaker.OpenAreaSizeZ", DEFAULT_OPEN_AREA_SIZE, false);
            Scribe_Values.Look<int>(ref PeremeterBuffer, "ImpassableMapMaker.PeremeterBuffer", DEFAULT_PEREMETER_BUFFER, false);
            Scribe_Values.Look<int>(ref MiddleWallSmoothness, "ImpassableMapMaker.MakeWallsSmooth", DEFAULT_WALLS_SMOOTHNESS, false);
            Scribe_Values.Look<string>(ref s, "ImpassableMapMaker.Shape", ImpassableShape.Square.ToString(), false);
            Scribe_Values.Look<bool>(ref ScatteredRocks, "ImpassableMapMaker.scatteredRocks", true, false);

            if (Scribe.mode != LoadSaveMode.Saving)
            {
                if (ImpassableShape.Square.ToString().Equals(s))
                {
                    shape = ImpassableShape.Square;
                }
                else if (ImpassableShape.Round.ToString().Equals(s))
                {
                    shape = ImpassableShape.Round;
                }
            }
        }

        public static void DoSettingsWindowContents(Rect rect)
        {
            Rect scroll = new Rect(5f, 45f, 430, rect.height);
            Rect view = new Rect(0, 45, 400, 800);

            Widgets.BeginScrollView(scroll, ref scrollPosition, view, true);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(view);

            ls.Label("ImpassableMapMaker.MountainShape".Translate());
            if (ls.RadioButton("ImpassableMapMaker.ShapeSquare".Translate(), shape == ImpassableShape.Square))
            {
                shape = ImpassableShape.Square;
            }
            if (ls.RadioButton("ImpassableMapMaker.ShapeRound".Translate(), shape == ImpassableShape.Round))
            {
                shape = ImpassableShape.Round;
            }
            ls.Gap(6);
            ls.CheckboxLabeled("ImpassableMapMaker.ScatteredRocks".Translate(), ref ScatteredRocks);
            ls.GapLine(GAP_SIZE);

            ls.CheckboxLabeled("ImpassableMapMaker.HasMiddleArea".Translate(), ref HasMiddleArea);
            if (HasMiddleArea)
            {
                ls.Label("ImpassableMapMaker.OpenAreaMaxOffsetFromMiddle".Translate() + ": " + percentOffset.ToString("N1") + "%");
                percentOffset = ls.Slider(percentOffset, 0, 25);
                if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
                {
                    percentOffset = DEFAULT_PERCENT_OFFSET;
                }
                ls.Gap(GAP_SIZE);

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
                ls.Gap(GAP_SIZE);

                ls.Label("ImpassableMapMaker.MiddleOpeningWallSmoothnes".Translate());
                ls.Label("<< " + "ImpassableMapMaker.Smooth".Translate() + " -- " + "ImpassableMapMaker.Rough".Translate() + " >>");
                MiddleWallSmoothness = (int)ls.Slider(MiddleWallSmoothness, 0, 20);
                if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
                {
                    MiddleWallSmoothness = 10;
                }
                ls.GapLine(GAP_SIZE);
            }

            if (shape == ImpassableShape.Square)
            {
                ls.Label("ImpassableMapMaker.EdgeBuffer".Translate() + ": " + PeremeterBuffer.ToString());
                ls.Label("<< " + "ImpassableMapMaker.Smaller".Translate() + " -- " + "ImpassableMapMaker.Larger".Translate() + " >>");
                PeremeterBuffer = (int)ls.Slider(PeremeterBuffer, 3, 30);
                if (ls.ButtonText("ImpassableMapMaker.Default".Translate()))
                {
                    PeremeterBuffer = DEFAULT_PEREMETER_BUFFER;
                }
                ls.Label("ImpassableMapMaker.EdgeBufferWarning".Translate());
            }
            
            ls.End();
        }
    }
}