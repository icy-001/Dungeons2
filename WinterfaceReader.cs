using System.Drawing;
using System.Text;

namespace Dungeons2
{
    public readonly ref struct WinterfaceReader(RawImageView img)
    {
        public const int WinterfaceWidth = 512;
        public const int WinterfaceHeight = 334;

        public RawImageView Image { get; } = img;

        static Registry Registry => Registry.Instance;

        public readonly Color GetColor(int x, int y) => Image.GetColor(x, y);

        public bool HasWinterface() => Image.IsExactMatch(Registry.WinterfaceMarker, 0, 0) && Image.IsExactMatch(Registry.WinterfaceReady, 334, 299);

        public string ReadField(Field field)
        {
            StringBuilder sb = new();

            int x = FindStartX(field);
            if (x == -1)
                return "";

            while (true)
            {
                bool found = false;
                for (int i = 0; i < field.FontType.NumberImageList.Count; i++)
                {
                    var numberImage = field.FontType.NumberImageList[i];
                    if (Image.IsMatchAlphaColor(numberImage, x, field.Y, field.ForeColor))
                    {
                        found = true;
                        if (i <= 9)
                        {
                            _ = sb.Append(i);
                        }
                        else if (i == 10)   // Plus if small, comma if large. Ignore commas.
                        {
                            if (field.FontType == FontType.SmallFont)
                                _ = sb.Append('+');
                        }
                        else if (i == 11)   // Minus
                        {
                            _ = sb.Append('-');
                        }
                        else if (i == 12)   // Colon
                        {
                            _ = sb.Append(':');
                        }
                        x += numberImage.Width;
                        break;
                    }
                }
                if (!found)
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the fraction of bonus rooms opened according to the bar.
        /// Last non-black pixel at (115, 146) means 0% bonus. At (294, 146) means 100% bonus.
        /// </summary>
        /// <returns>Between 0 and 1.</returns>
        public double GetBonus()
        {
            const int y = 146;
            const int begin = 115, end = 295;
            int low_x = begin, high_x = end;

            static bool pass(in WinterfaceReader @this, int x) => @this.GetColor(x, y).R >= 142;

            if (!pass(this, low_x + 1))
                return 0;
            if (pass(this, high_x - 1))
                return 1;
            while (high_x - low_x > 1)
            {
                int mid = low_x + (high_x - low_x) / 2;
                if (pass(this, mid))
                    low_x = mid;
                else
                    high_x = mid;
            }
            return ((double)low_x - begin + 2) / (end - begin);
        }

        public FloorData Read()
        {
            string floorSizeMod = ReadField(Field.SizeMod);
            string size = floorSizeMod == "+500" ? "Large" : floorSizeMod == "+250" ? "Medium" : "Small";
            int bonusField = ParseInt(ReadField(Field.BonusMod));
            return new FloorData
            {
                FloorNumber = ParseInt(ReadField(Field.Floor)),
                Size = size,
                Bonus = size == "Large" ? (double)bonusField / 360 : GetBonus(),
                MobsKilledMod = ParseInt(ReadField(Field.LevelMod)),
                Boost = ParseInt(ReadField(Field.FloorXPBoost)),
                TotalMod = ParseInt(ReadField(Field.TotalMod)),
                TotalXP = ParseInt(ReadField(Field.FinalXP)),
                Time = ParseTimeSpan(ReadField(Field.Time))
            };
        }

        private int FindStartX(Field field)
        {
            for (int x = field.StartX; x < field.StartX + field.SearchWidth; x++)
            {
                for (int y = field.Y; y < field.Y + field.Height; y++)
                {
                    if (GetColor(x, y) == field.ForeColor)
                        return x;
                }
            }
            return -1;
        }

        static int ParseInt(string s) => int.TryParse(s, out int res) ? res : 0;
        static TimeSpan ParseTimeSpan(string s) => TimeSpan.TryParse(s, out TimeSpan res) ? res : TimeSpan.Zero;
    }
}
