using System;
using System.Collections.Generic;
using ABI.CCK.Components;
using ABI_RC.Core.Player;
using UnityEngine;

namespace FPVDrone
{
    /// <summary>
    /// Discovers, binds, and drives a player-spawned CVRSpawnable prop from DronePhysics
    /// and syncs FPV_Size, FPV_Armed, and FPV_Throttle parameters over CVR's native prop network.
    /// </summary>
    public class DronePropSync
    {
        public const float MinSizeMm = 65f;
        public const float MaxSizeMm = 400f;

        private CVRSpawnable _bound;
        private Rigidbody _rb;
        private bool _origKinematic;
        private bool _hasRb;

        private int _idxSize = -1;
        private int _idxArmed = -1;
        private int _idxThrottle = -1;

        private float _lastSize = float.NaN;
        private float _lastArmed = float.NaN;
        private float _lastThrottle = float.NaN;
        private Vector3 _lastPos;
        private Quaternion _lastRot;
        private float _autoScanCooldown;
        private float _lastSpawnAttemptTime = -999f;

        private readonly List<(Renderer renderer, int originalLayer, bool isDiscard)> _cachedRendererLayers = new List<(Renderer, int, bool)>();
        private readonly List<(AudioSource source, bool originalMute)> _cachedAudioMutes = new List<(AudioSource, bool)>();
        private readonly List<(Collider collider, bool originalEnabled)> _cachedColliders = new List<(Collider, bool)>();
        private bool _discardHidden = false;
        private bool _lastDebugHearPropAudio = false;

        public CVRSpawnable BoundSpawnable => _bound != null ? _bound : null;
        public bool IsBound => _bound != null;

        public string BoundName => _bound != null ? _bound.gameObject.name : "None";

        public static List<CVRSpawnable> FindMySpawnables()
        {
            var result = new List<CVRSpawnable>();
            CVRSpawnable[] all = UnityEngine.Object.FindObjectsOfType<CVRSpawnable>();
            for (int i = 0; i < all.Length; i++)
            {
                CVRSpawnable s = all[i];
                if (s != null && s.SpawnedByMe)
                {
                    result.Add(s);
                }
            }
            return result;
        }

        public static bool IsDroneProp(CVRSpawnable s)
        {
            if (s == null || s.syncValues == null)
                return false;

            for (int i = 0; i < s.syncValues.Count; i++)
            {
                string n = s.syncValues[i]?.name;
                if (string.IsNullOrEmpty(n))
                    continue;

                if (n.Equals("FPV_Size", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("FPV_Armed", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("FPV_Throttle", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TrySpawnProp(Vector3 spawnPos, bool ignoreCooldown = false)
        {
            if (_bound != null)
                return true;

            string guid = Settings.DronePropGuid?.Value?.Trim();
            if (string.IsNullOrEmpty(guid))
                return false;

            float now = Time.unscaledTime;
            if (!ignoreCooldown && now - _lastSpawnAttemptTime < 10f)
                return false;

            var player = PlayerSetup.Instance;
            if (player == null)
                return false;

            _lastSpawnAttemptTime = now;
            try
            {
                player.SpawnProp(guid, spawnPos);
                MelonLoader.MelonLogger.Msg($"[FPVDrone] Requested prop spawn for GUID '{guid}'.");
                return true;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[FPVDrone] Failed to spawn prop '{guid}': {ex.Message}");
                return false;
            }
        }

        public bool TryAutoBind(bool fallbackToLatestIfNoFpvParams = false)
        {
            if (_bound != null)
                return true;

            List<CVRSpawnable> mine = FindMySpawnables();
            for (int i = mine.Count - 1; i >= 0; i--)
            {
                if (IsDroneProp(mine[i]))
                {
                    Bind(mine[i]);
                    return true;
                }
            }

            if (fallbackToLatestIfNoFpvParams && mine.Count > 0)
            {
                Bind(mine[mine.Count - 1]);
                return true;
            }

            return false;
        }

        public void Bind(CVRSpawnable spawnable)
        {
            if (spawnable == null)
                return;

            if (_bound == spawnable)
                return;

            Unbind();

            _bound = spawnable;
            _idxSize = FindSyncIndex(spawnable, "FPV_Size");
            _idxArmed = FindSyncIndex(spawnable, "FPV_Armed");
            _idxThrottle = FindSyncIndex(spawnable, "FPV_Throttle");

            _rb = spawnable.GetComponent<Rigidbody>();
            _hasRb = _rb != null;
            if (_hasRb)
            {
                _origKinematic = _rb.isKinematic;
                _rb.isKinematic = true;
            }

            // Route prop renderers to DroneMeshLayer ("Discard" renderers only move to DroneDiscardLayer while armed)
            _cachedRendererLayers.Clear();
            _discardHidden = false;
            Renderer[] renderers = spawnable.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                bool isDiscard = r.gameObject.name.IndexOf("Discard", StringComparison.OrdinalIgnoreCase) >= 0;
                _cachedRendererLayers.Add((r, r.gameObject.layer, isDiscard));
                if (!isDiscard)
                {
                    r.gameObject.layer = DroneMeshRenderer.DroneMeshLayer;
                }
            }

            // Mute prop's generic AudioSource locally unless DebugHearPropAudio is enabled
            _cachedAudioMutes.Clear();
            _lastDebugHearPropAudio = Settings.DebugHearPropAudio.Value;
            AudioSource[] audioSources = spawnable.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource src = audioSources[i];
                if (src == null) continue;
                _cachedAudioMutes.Add((src, src.mute));
                src.mute = !_lastDebugHearPropAudio && true;
            }

            // Disable all colliders on the prop while bound so DronePhysics never box-casts into its own prop
            _cachedColliders.Clear();
            Collider[] colliders = spawnable.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null) continue;
                _cachedColliders.Add((c, c.enabled));
                c.enabled = false;
            }

            _lastSize = float.NaN;
            _lastArmed = float.NaN;
            _lastThrottle = float.NaN;
            _lastPos = spawnable.transform.position;
            _lastRot = spawnable.transform.rotation;

            MelonLoader.MelonLogger.Msg($"[FPVDrone] Bound prop '{spawnable.gameObject.name}' (Size={_idxSize}, Armed={_idxArmed}, Throttle={_idxThrottle})");
        }

        public void Unbind()
        {
            if (_bound == null)
            {
                _cachedRendererLayers.Clear();
                _rb = null;
                _hasRb = false;
                return;
            }

            try
            {
                if (_idxArmed >= 0) _bound.SetValue(_idxArmed, 0f);
                if (_idxThrottle >= 0) _bound.SetValue(_idxThrottle, 0f);
                _bound.ForceUpdate();
            }
            catch { }

            if (_hasRb && _rb != null)
            {
                _rb.isKinematic = _origKinematic;
            }

            for (int i = 0; i < _cachedRendererLayers.Count; i++)
            {
                var (r, origLayer, _) = _cachedRendererLayers[i];
                if (r != null)
                {
                    r.gameObject.layer = origLayer;
                }
            }
            _cachedRendererLayers.Clear();
            _discardHidden = false;

            for (int i = 0; i < _cachedAudioMutes.Count; i++)
            {
                var (src, origMute) = _cachedAudioMutes[i];
                if (src != null)
                {
                    src.mute = origMute;
                }
            }
            _cachedAudioMutes.Clear();

            for (int i = 0; i < _cachedColliders.Count; i++)
            {
                var (c, origEnabled) = _cachedColliders[i];
                if (c != null)
                {
                    c.enabled = origEnabled;
                }
            }
            _cachedColliders.Clear();

            MelonLoader.MelonLogger.Msg($"[FPVDrone] Unbound prop '{_bound.gameObject.name}'");
            _bound = null;
            _rb = null;
            _hasRb = false;
            _idxSize = -1;
            _idxArmed = -1;
            _idxThrottle = -1;
        }

        public bool UpdateSync(Vector3 pos, Quaternion rot, bool isArmed, float throttle01, float wheelbaseMm, float dt)
        {
            if (_bound == null)
            {
                if (Settings.AutoBindDroneProp.Value)
                {
                    _autoScanCooldown -= dt;
                    if (_autoScanCooldown <= 0f)
                    {
                        _autoScanCooldown = 1.0f;
                        TryAutoBind(false);
                    }
                }

                if (_bound == null)
                    return false;
            }

            // Toggle "Discard" renderers: hidden from FPV/Spout only while armed
            if (_discardHidden != isArmed)
            {
                _discardHidden = isArmed;
                for (int i = 0; i < _cachedRendererLayers.Count; i++)
                {
                    var (r, origLayer, isDiscard) = _cachedRendererLayers[i];
                    if (r != null && isDiscard)
                    {
                        r.gameObject.layer = isArmed ? DroneMeshRenderer.DroneDiscardLayer : origLayer;
                    }
                }
            }

            // Sync debug prop audio mute toggle
            bool hearProp = Settings.DebugHearPropAudio.Value;
            if (_lastDebugHearPropAudio != hearProp)
            {
                _lastDebugHearPropAudio = hearProp;
                for (int i = 0; i < _cachedAudioMutes.Count; i++)
                {
                    var (src, origMute) = _cachedAudioMutes[i];
                    if (src != null)
                    {
                        src.mute = hearProp ? origMute : true;
                    }
                }
            }

            // Ensure kinematic stays forced while bound
            if (_hasRb && _rb != null && !_rb.isKinematic)
            {
                _rb.isKinematic = true;
            }

            bool transformChanged = (_lastPos - pos).sqrMagnitude > 1e-7f || Quaternion.Angle(_lastRot, rot) > 0.05f;
            _bound.transform.SetPositionAndRotation(pos, rot);
            _lastPos = pos;
            _lastRot = rot;

            float sizeNorm = Mathf.Clamp01(Mathf.InverseLerp(MinSizeMm, MaxSizeMm, wheelbaseMm));
            float armedVal = isArmed ? 1f : 0f;
            float throttleVal = isArmed ? Mathf.Clamp01(throttle01) : 0f;

            bool valueChanged = false;
            if (_idxSize >= 0 && (float.IsNaN(_lastSize) || Mathf.Abs(sizeNorm - _lastSize) > 0.002f))
            {
                _bound.SetValue(_idxSize, sizeNorm);
                _lastSize = sizeNorm;
                valueChanged = true;
            }

            if (_idxArmed >= 0 && (float.IsNaN(_lastArmed) || Mathf.Abs(armedVal - _lastArmed) > 0.01f))
            {
                _bound.SetValue(_idxArmed, armedVal);
                _lastArmed = armedVal;
                valueChanged = true;
            }

            if (_idxThrottle >= 0 && (float.IsNaN(_lastThrottle) || Mathf.Abs(throttleVal - _lastThrottle) > 0.005f))
            {
                _bound.SetValue(_idxThrottle, throttleVal);
                _lastThrottle = throttleVal;
                valueChanged = true;
            }

            if (isArmed || transformChanged || valueChanged)
            {
                _bound.ForceUpdate();
            }

            return true;
        }

        private static int FindSyncIndex(CVRSpawnable s, string paramName)
        {
            if (s.syncValues == null)
                return -1;

            for (int i = 0; i < s.syncValues.Count; i++)
            {
                if (string.Equals(s.syncValues[i]?.name, paramName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}

