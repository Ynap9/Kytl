namespace ktl.be.shared.Constants.Documents
{
    public static class StampImageConstants
    {
        public static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg" };

        public const long MaxBytes = 5 * 1024 * 1024;

        public const int DarknessDefault = 140;
        public const int DarknessMin = 40;
        public const int DarknessMax = 250;
        public const double DarknessBrightnessFactor = 0.5;
        public const double DarknessMinBrightness = 0.5;

        public const int StrokeDefault = 100;
        public const int StrokeMin = 0;
        public const int StrokeMax = 200;
        public const double StrokeRadiusRatio = 0.35 / 158d;
        public const int StrokeDirections = 8;

        public const string ColorPattern = "^#[0-9A-Fa-f]{6}$";
    }
}
