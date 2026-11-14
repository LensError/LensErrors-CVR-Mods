using System;
using MelonLoader;
using UnityEngine;

namespace FPVDrone.Spout
{
    public class SpoutManager : IDisposable
    {
        private SpoutSender _sender;

        public string SenderName { get; private set; } = "CVR_FPVDrone";
        public bool IsSpoutActive => _sender != null && _sender.IsActive;
        public event Action<bool> OnSpoutStateChanged;

        public SpoutManager(string senderName = "CVR_FPVDrone")
        {
            SenderName = string.IsNullOrEmpty(senderName) ? "CVR_FPVDrone" : senderName;
        }

        public void SetSenderName(string newName)
        {
            if (string.IsNullOrEmpty(newName) || newName == SenderName)
            {
                return;
            }

            bool wasActive = IsSpoutActive;
            if (wasActive)
            {
                StopSpout();
            }

            SenderName = newName;

            if (wasActive)
            {
                StartSpout();
            }
        }

        public bool StartSpout(int initialWidth = 1920, int initialHeight = 1080)
        {
            if (_sender == null)
            {
                _sender = new SpoutSender(SenderName, initialWidth, initialHeight);
            }

            bool started = _sender.Start();
            OnSpoutStateChanged?.Invoke(IsSpoutActive);
            return started;
        }

        public void StopSpout()
        {
            if (_sender != null)
            {
                _sender.Stop();
                _sender.Dispose();
                _sender = null;
            }

            OnSpoutStateChanged?.Invoke(false);
        }

        public void ToggleSpout()
        {
            if (IsSpoutActive)
            {
                StopSpout();
            }
            else
            {
                StartSpout();
            }
        }

        public void Send(RenderTexture target)
        {
            if (_sender != null && _sender.IsActive && target != null)
            {
                _sender.Send(target);
            }
        }

        public void Dispose()
        {
            StopSpout();
        }
    }
}

