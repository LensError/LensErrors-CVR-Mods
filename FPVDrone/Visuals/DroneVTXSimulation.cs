using UnityEngine;

namespace FPVDrone
{
    public class DroneVTXSimulation
    {
        public float RssiDbm { get; private set; } = -40.0f;
        public int LinkQualityPercent { get; private set; } = 99;
        public float StaticIntensity01 { get; private set; } = 0.0f;
        public bool IsSignalLost { get; private set; } = false;
        public float VoltageFlickerFactor { get; private set; } = 1.0f;

        private float _noiseOffset = 0.0f;
        // Layer mask excludes UI (5), PlayerLocal (8), PlayerClone (9), UIInternal (15)
        private static readonly int ObstacleMask = ~((1 << 5) | (1 << 8) | (1 << 9) | (1 << 15));
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        public void UpdateSimulation(Vector3 dronePos, Vector3 pilotPos, float batteryVoltage, float dt)
        {
            if (dt <= 0.0001f) dt = 0.016f;

            _noiseOffset += dt * 14.0f;

            // 1. Calculate distance path loss at 5.8 GHz
            float distance = Vector3.Distance(dronePos, pilotPos);
            distance = Mathf.Max(1.0f, distance);

            // VTX power to dBm: 25mW = 14dBm, 200mW = 23dBm, 800mW = 29dBm, 1W = 30dBm
            float powerMw = Mathf.Max(25.0f, Settings.VtxPowerMw.Value);
            float txPowerDbm = 10.0f * Mathf.Log10(powerMw);

            // Free space path loss: calibrated so 800mW provides clean video out to 150m+ in open air
            float pathLoss = 20.0f * Mathf.Log10(distance) + 40.0f;
            float rawRssi = txPowerDbm - pathLoss;

            // 2. Multi-obstacle penetration attenuation with zero-allocation non-alloc raycast
            Vector3 toPilot = pilotPos - dronePos;
            int hitCount = Physics.RaycastNonAlloc(dronePos, toPilot.normalized, _hitBuffer, distance, ObstacleMask, QueryTriggerInteraction.Ignore);
            int obstacleCount = 0;

            if (hitCount > 0)
            {
                // In-place sort by distance without heap allocation
                for (int i = 0; i < hitCount - 1; i++)
                {
                    for (int j = i + 1; j < hitCount; j++)
                    {
                        if (_hitBuffer[j].distance < _hitBuffer[i].distance)
                        {
                            var tmp = _hitBuffer[i];
                            _hitBuffer[i] = _hitBuffer[j];
                            _hitBuffer[j] = tmp;
                        }
                    }
                }

                float lastHitDist = -10.0f;
                for (int i = 0; i < hitCount; i++)
                {
                    if (_hitBuffer[i].distance - lastHitDist > 0.45f)
                    {
                        obstacleCount++;
                        lastHitDist = _hitBuffer[i].distance;
                    }
                }
            }

            // Each solid wall drops signal by 5.5 dBm (capped at 32 dBm max occlusion loss)
            float obstacleAttenuation = Mathf.Min(32.0f, obstacleCount * 5.5f);
            float totalRssi = rawRssi - obstacleAttenuation;

            // Noise floor is around -105 dBm, strong signal is -35 dBm
            RssiDbm = Mathf.Clamp(totalRssi, -105.0f, -30.0f);

            // Link Quality (0 - 100%)
            float normalizedSignal = Mathf.InverseLerp(-100.0f, -50.0f, RssiDbm);
            LinkQualityPercent = Mathf.RoundToInt(normalizedSignal * 100.0f);

            // 3. Static Intensity (0.0 = clean video, 1.0 = total white static/snow)
            if (!Settings.EnableVtxStatic.Value || Settings.VtxNoiseStrength.Value <= 0.001f)
            {
                StaticIntensity01 = 0.0f;
                IsSignalLost = false;
            }
            else
            {
                float noiseMult = Mathf.Clamp(Settings.VtxNoiseStrength.Value, 0.0f, 2.5f);

                if (RssiDbm > -80.0f)
                {
                    StaticIntensity01 = 0.0f;
                    IsSignalLost = false;
                }
                else if (RssiDbm > -92.0f)
                {
                    // Mild static / subtle horizontal noise bands
                    float t = Mathf.InverseLerp(-80.0f, -92.0f, RssiDbm);
                    StaticIntensity01 = Mathf.Clamp01(t * 0.28f * noiseMult);
                    IsSignalLost = false;
                }
                else if (RssiDbm > -100.0f)
                {
                    // Moderate to heavy snow and analog scanlines
                    float t = Mathf.InverseLerp(-92.0f, -100.0f, RssiDbm);
                    StaticIntensity01 = Mathf.Clamp01((0.28f + t * 0.55f) * noiseMult);
                    IsSignalLost = false;
                }
                else
                {
                    // Total signal loss / heavy snow
                    StaticIntensity01 = Mathf.Clamp01(1.0f * noiseMult);
                    IsSignalLost = true;
                }
            }

            // 4. Battery Voltage Sag Flicker (per-cell voltage)
            float cellVoltage = batteryVoltage / Mathf.Max(1, Settings.BatteryCells.Value);
            if (Settings.EnableVoltageSag.Value && cellVoltage < 3.63f)
            {
                float sagSeverity = Mathf.InverseLerp(3.63f, 3.25f, cellVoltage);
                float flicker = (Mathf.Sin(Time.time * 35.0f) * 0.5f + 0.5f) * sagSeverity * 0.25f;
                VoltageFlickerFactor = 1.0f - flicker;
            }
            else
            {
                VoltageFlickerFactor = 1.0f;
            }
        }
    }
}

