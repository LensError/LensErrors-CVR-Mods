using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;

namespace FPVDrone.Spout
{
    public enum KlakEventId
    {
        UpdateSender = 0,
        UpdateReceiver = 1,
        CloseSender = 2,
        CloseReceiver = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KlakEventData
    {
        public IntPtr instancePointer;
        public IntPtr texturePointer;

        public KlakEventData(IntPtr instance, IntPtr texture)
        {
            instancePointer = instance;
            texturePointer = texture;
        }
    }

    public static class SpoutInterop
    {
        private const string DllName = "KlakSpout";
        private static bool? _isAvailable = null;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport(DllName, EntryPoint = "GetRenderEventCallback")]
        public static extern IntPtr GetRenderEventCallback();

        [DllImport(DllName, EntryPoint = "CreateSender", CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateSender(string name, int width, int height);

        public static bool CheckAvailability()
        {
            if (_isAvailable.HasValue)
            {
                return _isAvailable.Value;
            }

            try
            {
                string gameRoot = Directory.GetCurrentDirectory();
                string pluginsDir = Path.Combine(gameRoot, "ChilloutVR_Data", "Plugins");
                string targetPath = Path.Combine(pluginsDir, DllName + ".dll");

                // Ensure KlakSpout.dll is present in ChilloutVR_Data/Plugins/
                if (!File.Exists(targetPath))
                {
                    if (!Directory.Exists(pluginsDir))
                    {
                        Directory.CreateDirectory(pluginsDir);
                    }

                    Assembly asm = Assembly.GetExecutingAssembly();
                    using Stream stream = asm.GetManifestResourceStream("FPVDrone.resources.KlakSpout.dll");
                    if (stream != null)
                    {
                        using FileStream fileStream = File.Create(targetPath);
                        stream.CopyTo(fileStream);
                        MelonLogger.Msg($"[Spout] Extracted bundled KlakSpout.dll to {targetPath}");
                    }
                }

                // Explicitly load native library so P/Invoke resolves properly from plugins folder
                IntPtr hMod = LoadLibrary(targetPath);
                if (hMod != IntPtr.Zero)
                {
                    MelonLogger.Msg($"[Spout] KlakSpout native library successfully loaded from {targetPath}");
                    _isAvailable = true;
                    return true;
                }
                else
                {
                    int err = Marshal.GetLastWin32Error();
                    MelonLogger.Warning($"[Spout] LoadLibrary failed for {targetPath} (Win32 error {err})");
                    _isAvailable = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Spout] KlakSpout check failed: {ex.Message}");
                _isAvailable = false;
                return false;
            }
        }
    }
}

