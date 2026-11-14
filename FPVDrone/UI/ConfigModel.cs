using System;
using MelonLoader;
using UnityEngine;

namespace FPVDrone
{
    public enum RowKind
    {
        Section,
        Float,
        Bool,
        Choice,
        Bind,
        Action,
        Info,
        Graph,
        TextInput
    }

    /// <summary>
    /// One declarative configurator row. Drawing, mouse, keyboard and gamepad input
    /// all operate on this object, so a setting is defined exactly once.
    /// </summary>
    public class ConfigRow
    {
        public RowKind Kind;
        public string Label = "";
        public string Desc = "";
        public Func<bool> Visible;

        // Float
        public Func<float> GetFloat;
        public Action<float> SetFloat;
        public float Step = 0.1f, Min, Max = 1f, DefaultFloat;
        public Func<float, string> Format;

        // Bool
        public Func<bool> GetBool;
        public Action ToggleBool;
        public bool DefaultBool;
        public string OnText = "ENABLED", OffText = "DISABLED";

        // Choice / Bind / Info
        public Func<string> Text;
        public Action<int> Cycle;

        // Bind
        public DroneAction BindAction;
        public bool BindRadio;

        // Action
        public Action Invoke;
        public bool Danger;

        // TextInput
        public Func<string> GetText;
        public Action<string> SetText;
        public string Placeholder = "";

        // Shared
        public Func<bool> IsModified;
        public Action Reset;

        public bool IsSelectable
        {
            get { return Kind != RowKind.Section && Kind != RowKind.Info && Kind != RowKind.Graph; }
        }

        public bool IsShown()
        {
            return Visible == null || Visible();
        }
    }

    internal static class Rows
    {
        public static ConfigRow Section(string title, Func<bool> visible = null)
        {
            return new ConfigRow { Kind = RowKind.Section, Label = title, Visible = visible };
        }

        public static ConfigRow Graph()
        {
            return new ConfigRow { Kind = RowKind.Graph, Label = "Rate Curves" };
        }

        public static ConfigRow Info(string label, Func<string> text, Func<bool> visible = null)
        {
            return new ConfigRow { Kind = RowKind.Info, Label = label, Text = text, Visible = visible };
        }

        public static ConfigRow Float(string label, MelonPreferences_Entry<float> e, float step, float min, float max,
            Func<float, string> fmt, string desc, Func<bool> visible = null)
        {
            return new ConfigRow
            {
                Kind = RowKind.Float,
                Label = label,
                Desc = desc,
                Visible = visible,
                GetFloat = () => e.Value,
                SetFloat = v => e.Value = v,
                Step = step,
                Min = min,
                Max = max,
                DefaultFloat = e.DefaultValue,
                Format = fmt,
                IsModified = () => Mathf.Abs(e.Value - e.DefaultValue) > 0.0001f,
                Reset = () => e.Value = e.DefaultValue
            };
        }

        public static ConfigRow Int(string label, MelonPreferences_Entry<int> e, int min, int max,
            Func<float, string> fmt, string desc, Func<bool> visible = null)
        {
            return new ConfigRow
            {
                Kind = RowKind.Float,
                Label = label,
                Desc = desc,
                Visible = visible,
                GetFloat = () => e.Value,
                SetFloat = v => e.Value = Mathf.Clamp(Mathf.RoundToInt(v), min, max),
                Step = 1f,
                Min = min,
                Max = max,
                DefaultFloat = e.DefaultValue,
                Format = fmt,
                IsModified = () => e.Value != e.DefaultValue,
                Reset = () => e.Value = e.DefaultValue
            };
        }

        public static ConfigRow Bool(string label, MelonPreferences_Entry<bool> e, string desc, string on = "ENABLED",
            string off = "DISABLED", Func<bool> visible = null)
        {
            return new ConfigRow
            {
                Kind = RowKind.Bool,
                Label = label,
                Desc = desc,
                Visible = visible,
                GetBool = () => e.Value,
                ToggleBool = () => e.Value = !e.Value,
                DefaultBool = e.DefaultValue,
                OnText = on,
                OffText = off,
                IsModified = () => e.Value != e.DefaultValue,
                Reset = () => e.Value = e.DefaultValue
            };
        }

        public static ConfigRow Toggle(string label, Func<bool> get, Action toggle, string desc, string on, string off)
        {
            return new ConfigRow
            {
                Kind = RowKind.Bool,
                Label = label,
                Desc = desc,
                GetBool = get,
                ToggleBool = toggle,
                OnText = on,
                OffText = off
            };
        }

        public static ConfigRow Choice<T>(string label, MelonPreferences_Entry<T> e, Func<T, string> name, string desc,
            Func<bool> visible = null) where T : struct, IComparable
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            return new ConfigRow
            {
                Kind = RowKind.Choice,
                Label = label,
                Desc = desc,
                Visible = visible,
                Text = () => name(e.Value),
                Cycle = dir =>
                {
                    int idx = Array.IndexOf(values, e.Value);
                    e.Value = values[(idx + dir + values.Length) % values.Length];
                },
                IsModified = () => !e.Value.Equals(e.DefaultValue),
                Reset = () => e.Value = e.DefaultValue
            };
        }

        public static ConfigRow Choice(string label, Func<string> text, Action<int> cycle, string desc)
        {
            return new ConfigRow { Kind = RowKind.Choice, Label = label, Desc = desc, Text = text, Cycle = cycle };
        }

        public static ConfigRow Bind(string label, DroneAction action, bool radio, Func<string> text, string desc,
            Func<bool> visible = null)
        {
            return new ConfigRow
            {
                Kind = RowKind.Bind,
                Label = label,
                Desc = desc,
                Visible = visible,
                BindAction = action,
                BindRadio = radio,
                Text = text
            };
        }

        public static ConfigRow Action(string label, Action invoke, string desc, bool danger = false, Func<bool> visible = null)
        {
            return new ConfigRow { Kind = RowKind.Action, Label = label, Desc = desc, Invoke = invoke, Danger = danger, Visible = visible };
        }

        public static ConfigRow TextInput(string label, Func<string> get, Action<string> set, string placeholder, string desc, Func<bool> visible = null)
        {
            return new ConfigRow
            {
                Kind = RowKind.TextInput,
                Label = label,
                Desc = desc,
                GetText = get,
                SetText = set,
                Placeholder = placeholder,
                Visible = visible
            };
        }
    }
}

