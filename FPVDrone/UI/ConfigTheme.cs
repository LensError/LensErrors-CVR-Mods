using UnityEngine;

namespace FPVDrone
{
    /// <summary>Neutral grey palette. The accent is used only for slider fills and active toggles.</summary>
    internal static class ConfigTheme
    {
        public static readonly Color Panel = new Color(0.082f, 0.086f, 0.094f, 0.97f);
        public static readonly Color Chrome = new Color(0.066f, 0.069f, 0.076f, 1f);
        public static readonly Color Inset = new Color(0.050f, 0.053f, 0.059f, 1f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Hover = new Color(1f, 1f, 1f, 0.045f);
        public static readonly Color Selected = new Color(1f, 1f, 1f, 0.085f);
        public static readonly Color Field = new Color(1f, 1f, 1f, 0.065f);
        public static readonly Color FieldHover = new Color(1f, 1f, 1f, 0.13f);

        public static readonly Color Text = new Color(0.88f, 0.89f, 0.91f, 1f);
        public static readonly Color Dim = new Color(0.55f, 0.57f, 0.61f, 1f);
        public static readonly Color White = Color.white;

        public static readonly Color Accent = new Color(0.40f, 0.65f, 0.90f, 1f);
        public static readonly Color Modified = new Color(0.92f, 0.74f, 0.42f, 1f);
        public static readonly Color Red = new Color(0.90f, 0.42f, 0.42f, 1f);
    }
}
