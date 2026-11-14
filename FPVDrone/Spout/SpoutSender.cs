using System;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPVDrone.Spout
{
    public class SpoutSender : IDisposable
    {
        public string SenderName { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsActive => _nativeSenderPtr != IntPtr.Zero;

        private IntPtr _nativeSenderPtr = IntPtr.Zero;
        private KlakEventData[] _eventDataArray;
        private GCHandle _eventDataHandle;
        private CommandBuffer _cmdBuffer;
        private RenderTexture _flipBuffer;

        public SpoutSender(string senderName, int initialWidth, int initialHeight)
        {
            SenderName = string.IsNullOrEmpty(senderName) ? "CVR_FPVDrone" : senderName;
            Width = initialWidth;
            Height = initialHeight;
        }

        public bool Start()
        {
            if (IsActive)
            {
                return true;
            }

            if (!SpoutInterop.CheckAvailability())
            {
                MelonLogger.Warning("[Spout] KlakSpout.dll was not found or failed to initialize.");
                return false;
            }

            try
            {
                _nativeSenderPtr = SpoutInterop.CreateSender(SenderName, Width, Height);
                if (_nativeSenderPtr == IntPtr.Zero)
                {
                    MelonLogger.Error($"[Spout] KlakSpout failed to create sender '{SenderName}' ({Width}x{Height})");
                    return false;
                }

                _eventDataArray = new KlakEventData[1];
                _eventDataArray[0] = new KlakEventData(_nativeSenderPtr, IntPtr.Zero);
                _eventDataHandle = GCHandle.Alloc(_eventDataArray, GCHandleType.Pinned);

                _cmdBuffer = new CommandBuffer { name = "KlakSpout_FPVDroneSenderUpdate" };

                MelonLogger.Msg($"[Spout] Started KlakSpout sender '{SenderName}' ({Width}x{Height})");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Spout] Error starting KlakSpout sender: {ex.Message}");
                Stop();
                return false;
            }
        }

        public void Send(RenderTexture target)
        {
            if (!IsActive || target == null)
            {
                return;
            }

            int currentWidth = target.width;
            int currentHeight = target.height;

            if (currentWidth != Width || currentHeight != Height)
            {
                Width = currentWidth;
                Height = currentHeight;
                RestartSender();
            }

            if (_nativeSenderPtr == IntPtr.Zero || !_eventDataHandle.IsAllocated)
            {
                return;
            }

            if (_flipBuffer == null || _flipBuffer.width != currentWidth || _flipBuffer.height != currentHeight)
            {
                if (_flipBuffer != null)
                {
                    _flipBuffer.Release();
                    UnityEngine.Object.Destroy(_flipBuffer);
                }
                _flipBuffer = new RenderTexture(currentWidth, currentHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "KlakSpout_FPVFlipBuffer",
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _flipBuffer.Create();
            }

            // Invert vertically so Spout2 texture matches standard Direct3D UV convention.
            // In Linear color space, ensure sRGB write is active so linear render output is properly gamma-corrected for OBS display!
            bool prevSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(target, _flipBuffer, new Vector2(1, -1), new Vector2(0, 1));
            }
            finally
            {
                GL.sRGBWrite = prevSrgb;
            }

            IntPtr nativeTexPtr = _flipBuffer.GetNativeTexturePtr();
            if (nativeTexPtr == IntPtr.Zero)
            {
                return;
            }

            _eventDataArray[0].instancePointer = _nativeSenderPtr;
            _eventDataArray[0].texturePointer = nativeTexPtr;

            _cmdBuffer.Clear();
            _cmdBuffer.IssuePluginEventAndData(
                SpoutInterop.GetRenderEventCallback(),
                (int)KlakEventId.UpdateSender,
                _eventDataHandle.AddrOfPinnedObject());

            Graphics.ExecuteCommandBuffer(_cmdBuffer);
        }

        private void RestartSender()
        {
            Stop();
            Start();
        }

        public void Stop()
        {
            if (_flipBuffer != null)
            {
                _flipBuffer.Release();
                UnityEngine.Object.Destroy(_flipBuffer);
                _flipBuffer = null;
            }

            if (_nativeSenderPtr != IntPtr.Zero)
            {
                try
                {
                    if (_eventDataHandle.IsAllocated && _cmdBuffer != null)
                    {
                        _eventDataArray[0].instancePointer = _nativeSenderPtr;
                        _eventDataArray[0].texturePointer = IntPtr.Zero;

                        _cmdBuffer.Clear();
                        _cmdBuffer.IssuePluginEventAndData(
                            SpoutInterop.GetRenderEventCallback(),
                            (int)KlakEventId.CloseSender,
                            _eventDataHandle.AddrOfPinnedObject());

                        Graphics.ExecuteCommandBuffer(_cmdBuffer);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Spout] Error issuing CloseSender: {ex.Message}");
                }

                _nativeSenderPtr = IntPtr.Zero;
                MelonLogger.Msg($"[Spout] Stopped KlakSpout sender '{SenderName}'");
            }

            if (_eventDataHandle.IsAllocated)
            {
                _eventDataHandle.Free();
            }

            if (_cmdBuffer != null)
            {
                _cmdBuffer.Release();
                _cmdBuffer = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

