using UnityEngine;

namespace FPVDrone
{
    public class DroneAudioEngine
    {
        private GameObject _audioEmitterObject;
        private readonly AudioSource[] _motorSources = new AudioSource[4];
        private AudioSource _statorWhineSource;
        private AudioSource _propWindAudioSource;
        private AudioSource _bladeRipAudioSource;
        private AudioSource _escBeepSource;
        private AudioSource _impactAudioSource;

        // 4 decorrelated audio clips for the 4 physical motors to prevent coherent comb-filtering / flanging
        private readonly AudioClip[] _bladePassClips = new AudioClip[4];
        private AudioClip _statorWhineClip;
        private AudioClip _propWindClip;
        private AudioClip _bladeRipClip;
        private AudioClip _escInitClip;
        private AudioClip _impactClip;
        private AudioClip _armBeepClip;
        private AudioClip _disarmBeepClip;
        private AudioClip _lowBattBeepClip;
        private float _lowBattBeepTimer = 0.0f;

        // Acoustic heterodyne micro-detuning per motor [0: Rear-Right, 1: Front-Right, 2: Rear-Left, 3: Front-Left]
        private static readonly float[] DetuneFactors = new float[] { 1.003f, 0.997f, 1.002f, 0.998f };

        private readonly float[] _motorPitches = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        private readonly float[] _motorVolumes = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };

        private float _statorVolume = 0.0f;
        private float _propWindPitch = 1.0f;
        private float _propWindVolume = 0.0f;
        private float _bladeRipPitch = 1.0f;
        private float _currentBladeRipVolume = 0.0f;
        private float _masterFade = 0.0f;
        private float _clipBaseF0 = 600.0f;
        private float _cachedMinDist = 1.0f;

        public void Initialize()
        {
            if (_audioEmitterObject != null)
                return;

            _audioEmitterObject = new GameObject("FPVDrone_AudioEmitter");
            Object.DontDestroyOnLoad(_audioEmitterObject);

            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();
            _clipBaseF0 = ComputeClipBaseF0(hw);
            _cachedMinDist = hw.AudioMinDistance;

            for (int i = 0; i < 4; i++)
            {
                _bladePassClips[i] = GenerateBladePassClip(i, 1337 + i * 7919, _clipBaseF0);
            }

            _statorWhineClip = GenerateStatorWhineClip(_clipBaseF0);
            _propWindClip = GeneratePropWindClip();
            _bladeRipClip = GenerateBladeRipClip();
            _escInitClip = GenerateEscInitClip();
            _impactClip = GenerateImpactClip();
            _armBeepClip = GenerateToneSequenceClip("FPV_ArmBeep", new[] { 1850f, 2450f }, 0.075f, 0.025f);
            _disarmBeepClip = GenerateToneSequenceClip("FPV_DisarmBeep", new[] { 2450f, 1650f }, 0.075f, 0.025f);
            _lowBattBeepClip = GenerateToneSequenceClip("FPV_LowBattBeep", new[] { 2950f, 2950f }, 0.060f, 0.045f);

            // 1. 4-Motor Acoustic Array (3D Spatialized with spread and physical Doppler)
            for (int i = 0; i < 4; i++)
            {
                AudioSource src = _audioEmitterObject.AddComponent<AudioSource>();
                src.clip = _bladePassClips[i];
                src.loop = true;
                src.spatialBlend = 1.0f;
                src.dopplerLevel = 1.0f;
                src.spread = 35.0f;
                src.playOnAwake = false;
                _motorSources[i] = src;
            }

            // 2. High-Frequency Brushless Stator Commutation Layer
            _statorWhineSource = _audioEmitterObject.AddComponent<AudioSource>();
            _statorWhineSource.clip = _statorWhineClip;
            _statorWhineSource.loop = true;
            _statorWhineSource.spatialBlend = 1.0f;
            _statorWhineSource.dopplerLevel = 1.0f;
            _statorWhineSource.spread = 20.0f;
            _statorWhineSource.playOnAwake = false;

            // 3. Propeller Inflow Wind and Slipstream Rush
            _propWindAudioSource = _audioEmitterObject.AddComponent<AudioSource>();
            _propWindAudioSource.clip = _propWindClip;
            _propWindAudioSource.loop = true;
            _propWindAudioSource.spatialBlend = 1.0f;
            _propWindAudioSource.dopplerLevel = 1.0f;
            _propWindAudioSource.spread = 60.0f;
            _propWindAudioSource.playOnAwake = false;

            // 4. Aerodynamic Prop Rip / Blade Stall Flutter
            _bladeRipAudioSource = _audioEmitterObject.AddComponent<AudioSource>();
            _bladeRipAudioSource.clip = _bladeRipClip;
            _bladeRipAudioSource.loop = true;
            _bladeRipAudioSource.spatialBlend = 1.0f;
            _bladeRipAudioSource.dopplerLevel = 1.0f;
            _bladeRipAudioSource.spread = 45.0f;
            _bladeRipAudioSource.playOnAwake = false;

            // 5. ESC Beeper & BLHeli Initialization Chimes
            _escBeepSource = _audioEmitterObject.AddComponent<AudioSource>();
            _escBeepSource.clip = _escInitClip;
            _escBeepSource.loop = false;
            _escBeepSource.spatialBlend = 1.0f;
            _escBeepSource.dopplerLevel = 1.0f;
            _escBeepSource.playOnAwake = false;

            // 6. Prop Strike / Frame Collision Impacts
            _impactAudioSource = _audioEmitterObject.AddComponent<AudioSource>();
            _impactAudioSource.clip = _impactClip;
            _impactAudioSource.loop = false;
            _impactAudioSource.spatialBlend = 1.0f;
            _impactAudioSource.dopplerLevel = 1.0f;
            _impactAudioSource.playOnAwake = false;

            ApplyPresetRolloff(hw);
        }

        private static float ComputeClipBaseF0(DroneHardwareSpec hw)
        {
            return Mathf.Clamp(hw.MaxBladePassF0 * 0.42f, 180.0f, 1600.0f);
        }

        private static void ConfigureRolloff(AudioSource src, float minDist, float maxDist)
        {
            if (src == null) return;

            minDist = Mathf.Max(0.1f, minDist);
            maxDist = Mathf.Max(minDist + 1.0f, maxDist);

            src.rolloffMode = AudioRolloffMode.Custom;
            src.minDistance = minDist;
            src.maxDistance = maxDist;

            float minT = minDist / maxDist;
            float[] sampleTs = { 0.0f, minT, minT + (1.0f - minT) * 0.08f, minT + (1.0f - minT) * 0.22f, minT + (1.0f - minT) * 0.45f, minT + (1.0f - minT) * 0.72f, 1.0f };
            Keyframe[] keys = new Keyframe[sampleTs.Length];

            for (int i = 0; i < sampleTs.Length; i++)
            {
                float t = sampleTs[i];
                float val;
                if (t <= minT)
                {
                    val = 1.0f;
                }
                else if (t >= 1.0f)
                {
                    val = 0.0f;
                }
                else
                {
                    float dist = t * maxDist;
                    float norm = (t - minT) / (1.0f - minT);
                    // Inverse-distance decay with smooth cosine-squared tail to hit 0 cleanly at maxDist
                    float inv = minDist / dist;
                    float tail = Mathf.Cos(norm * Mathf.PI * 0.5f);
                    val = Mathf.Clamp01(inv * tail);
                }
                keys[i] = new Keyframe(t, val);
            }

            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0.0f);
            }
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
        }

        private void ApplyPresetRolloff(DroneHardwareSpec hw)
        {
            float minD = hw.AudioMinDistance;
            float maxD = hw.AudioMaxDistance;

            for (int i = 0; i < 4; i++)
            {
                ConfigureRolloff(_motorSources[i], minD, maxD);
            }
            ConfigureRolloff(_statorWhineSource, minD * 0.85f, maxD * 0.75f);
            ConfigureRolloff(_propWindAudioSource, minD * 0.90f, maxD * 0.70f);
            ConfigureRolloff(_bladeRipAudioSource, minD * 1.10f, maxD * 0.95f);
            ConfigureRolloff(_escBeepSource, minD * 0.80f, maxD * 0.45f);
            ConfigureRolloff(_impactAudioSource, minD * 0.90f, maxD * 0.65f);
        }

        public void PlayImpactSound(float impactSpeed)
        {
            if (_impactAudioSource == null || _impactClip == null)
                return;

            float intensity = Mathf.Clamp01(impactSpeed / 10.0f);
            if (intensity < 0.12f)
                return;

            _impactAudioSource.pitch = UnityEngine.Random.Range(0.85f, 1.25f);
            _impactAudioSource.PlayOneShot(_impactClip, intensity * Settings.MotorVolume.Value * 0.55f);
        }

        public void ReloadHardwareClips(DroneHardwareSpec hw)
        {
            if (_audioEmitterObject == null)
                return;

            _clipBaseF0 = ComputeClipBaseF0(hw);
            _cachedMinDist = hw.AudioMinDistance;

            for (int i = 0; i < 4; i++)
            {
                if (_bladePassClips[i] != null) Object.Destroy(_bladePassClips[i]);
                _bladePassClips[i] = GenerateBladePassClip(i, 1337 + i * 7919, _clipBaseF0);
                if (_motorSources[i] != null) _motorSources[i].clip = _bladePassClips[i];
            }

            if (_statorWhineClip != null) Object.Destroy(_statorWhineClip);
            _statorWhineClip = GenerateStatorWhineClip(_clipBaseF0);
            if (_statorWhineSource != null) _statorWhineSource.clip = _statorWhineClip;

            ApplyPresetRolloff(hw);
        }

        public void PlayEscInitBeeps()
        {
            if (_escBeepSource != null && _escInitClip != null)
            {
                _escBeepSource.volume = Settings.MotorVolume.Value * 0.45f;
                _escBeepSource.pitch = 1.0f;
                _escBeepSource.Play();
            }
        }

        public void PlayArmBeep()
        {
            if (_escBeepSource != null && _armBeepClip != null)
            {
                _escBeepSource.pitch = 1.0f;
                _escBeepSource.PlayOneShot(_armBeepClip, Settings.MotorVolume.Value * 0.45f);
            }
        }

        public void PlayDisarmBeep()
        {
            if (_escBeepSource != null && _disarmBeepClip != null)
            {
                _escBeepSource.pitch = 1.0f;
                _escBeepSource.PlayOneShot(_disarmBeepClip, Settings.MotorVolume.Value * 0.45f);
            }
        }

        public void UpdateLowBatteryBeeper(float dt, float batteryVoltage, int cells, bool isArmed)
        {
            if (!isArmed || _escBeepSource == null || _lowBattBeepClip == null)
            {
                _lowBattBeepTimer = 0.0f;
                return;
            }
            float cellVolt = batteryVoltage / Mathf.Max(1, cells);
            if (cellVolt < 3.45f)
            {
                _lowBattBeepTimer -= dt;
                if (_lowBattBeepTimer <= 0.0f)
                {
                    _escBeepSource.pitch = 1.0f;
                    _escBeepSource.PlayOneShot(_lowBattBeepClip, Settings.MotorVolume.Value * 0.42f);
                    _lowBattBeepTimer = cellVolt < 3.25f ? 0.85f : 1.65f;
                }
            }
            else
            {
                _lowBattBeepTimer = 0.0f;
            }
        }

        private static AudioClip GenerateToneSequenceClip(string name, float[] freqs, float toneDur, float gapDur)
        {
            const int sampleRate = 44100;
            float step = toneDur + gapDur;
            int totalSamples = Mathf.Max(1, (int)(sampleRate * (freqs.Length * step)));
            float[] samples = new float[totalSamples];

            for (int tIdx = 0; tIdx < freqs.Length; tIdx++)
            {
                int start = (int)(tIdx * step * sampleRate);
                int len = (int)(toneDur * sampleRate);
                float f = freqs[tIdx];
                for (int i = 0; i < len && (start + i) < totalSamples; i++)
                {
                    float u = (float)i / len;
                    float env = Mathf.Sin(u * Mathf.PI);
                    float t = (float)i / sampleRate;
                    // Piezo buzzer timbre (fundamental + subtle 3rd harmonic)
                    samples[start + i] = (Mathf.Sin(Mathf.PI * 2f * f * t) * 0.78f + Mathf.Sin(Mathf.PI * 6f * f * t) * 0.22f) * env * 0.65f;
                }
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void UpdateAudio(
            Vector3 dronePosition,
            Quaternion droneRotation,
            float[] motorThrottles,
            float[] motorRpms,
            float collectiveRpmPercent,
            Vector3 localAngularRatesDeg,
            float speedMps,
            float gForceLoad,
            float batteryCurrentAmps,
            bool isInPropWash,
            bool isArmed)
        {
            if (_audioEmitterObject == null || _motorSources[0] == null)
                return;

            _audioEmitterObject.transform.SetPositionAndRotation(dronePosition, droneRotation);
            float dt = Time.deltaTime;
            float masterVol = Settings.MotorVolume.Value * 0.42f;

            if (!isArmed)
            {
                // Smooth rotational inertia spin-down
                float fadeLerp = 1.0f - Mathf.Exp(-dt * 2.5f);
                _masterFade = Mathf.Lerp(_masterFade, 0.0f, fadeLerp);
                _currentBladeRipVolume = Mathf.Lerp(_currentBladeRipVolume, 0.0f, 1.0f - Mathf.Exp(-dt * 14.0f));

                for (int i = 0; i < 4; i++)
                {
                    _motorPitches[i] = Mathf.Lerp(_motorPitches[i], 0.10f, fadeLerp);
                    _motorSources[i].pitch = _motorPitches[i];
                    _motorSources[i].volume = _motorVolumes[i] * _masterFade * masterVol;
                }

                _statorWhineSource.pitch = Mathf.Lerp(_statorWhineSource.pitch, 0.10f, fadeLerp);
                _statorWhineSource.volume = _statorVolume * _masterFade * masterVol;
                _propWindAudioSource.volume = _propWindVolume * _masterFade * masterVol;
                _bladeRipAudioSource.volume = _currentBladeRipVolume * _masterFade * masterVol;

                if (_masterFade < 0.005f && _motorSources[0].isPlaying)
                {
                    for (int i = 0; i < 4; i++) _motorSources[i].Stop();
                    _statorWhineSource.Stop();
                    _propWindAudioSource.Stop();
                    _bladeRipAudioSource.Stop();
                }
                return;
            }

            _masterFade = Mathf.Lerp(_masterFade, 1.0f, 1.0f - Mathf.Exp(-dt * 14.0f));

            if (!_motorSources[0].isPlaying)
            {
                for (int i = 0; i < 4; i++) _motorSources[i].Play();
                _statorWhineSource.Play();
                _propWindAudioSource.Play();
                _bladeRipAudioSource.Play();
            }

            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();
            float desiredBaseF0 = ComputeClipBaseF0(hw);
            if (Mathf.Abs(desiredBaseF0 - _clipBaseF0) / Mathf.Max(1.0f, _clipBaseF0) > 0.12f ||
                Mathf.Abs(hw.AudioMinDistance - _cachedMinDist) > 0.15f)
            {
                ReloadHardwareClips(hw);
                for (int i = 0; i < 4; i++) _motorSources[i].Play();
                _statorWhineSource.Play();
            }

            float collectiveThrottle = Mathf.Clamp01(collectiveRpmPercent / 100.0f);
            float pitchSmooth = 1.0f - Mathf.Exp(-dt / 0.045f);
            float volSmooth = 1.0f - Mathf.Exp(-dt / 0.035f);

            // 1. 4-Motor Acoustic Array driven by physical Blade Pass Frequency = (RPM / 60) * Blades
            for (int i = 0; i < 4; i++)
            {
                float mThrottle = (motorThrottles != null && i < motorThrottles.Length) ? motorThrottles[i] : collectiveThrottle;
                mThrottle = Mathf.Clamp01(mThrottle);

                float rpm = (motorRpms != null && i < motorRpms.Length) ? motorRpms[i] : (mThrottle * hw.MaxLoadedRpm);
                float bpfHz = (rpm / 60.0f) * hw.PropBlades;
                float targetMotorPitch = (bpfHz / Mathf.Max(50.0f, _clipBaseF0)) * DetuneFactors[i];

                // Rotational inertia smoothing prevents stepping and zipper noise
                _motorPitches[i] = Mathf.Lerp(_motorPitches[i], targetMotorPitch, pitchSmooth);
                _motorSources[i].pitch = Mathf.Clamp(_motorPitches[i], 0.12f, 2.85f);

                // Acoustic power curve (dynamic expansion from quiet idle to punchout)
                float volPower = Mathf.Pow(mThrottle, 1.55f);
                float targetMotorVol = Mathf.Lerp(0.035f, 0.72f, volPower);
                _motorVolumes[i] = Mathf.Lerp(_motorVolumes[i], targetMotorVol, volSmooth);
                _motorSources[i].volume = _motorVolumes[i] * _masterFade * masterVol;
            }

            // 2. High-Frequency Brushless Stator Commutation Layer (subtle 7/3 ratio to blade-pass)
            float avgMotorPitch = (_motorPitches[0] + _motorPitches[1] + _motorPitches[2] + _motorPitches[3]) * 0.25f;
            _statorWhineSource.pitch = Mathf.Clamp(avgMotorPitch, 0.12f, 2.85f);

            float currentFactor = Mathf.Clamp01(batteryCurrentAmps / Mathf.Max(10.0f, hw.PeakCurrentAmps));
            float targetStatorVol = Mathf.Lerp(0.01f, 0.18f, Mathf.Pow(currentFactor, 1.4f));
            _statorVolume = Mathf.Lerp(_statorVolume, targetStatorVol, volSmooth);
            _statorWhineSource.volume = _statorVolume * _masterFade * masterVol;

            // 3. Inflow Wind and Prop Wash Buffeting
            float windFactor = Mathf.Clamp01((speedMps / 36.0f) * 0.65f + collectiveThrottle * 0.35f);
            float targetWindPitch = Mathf.Lerp(0.82f, 1.60f, windFactor);
            _propWindPitch = Mathf.Lerp(_propWindPitch, targetWindPitch, pitchSmooth);
            _propWindAudioSource.pitch = _propWindPitch;

            // Prop wash descent creates choppy airflow flutter
            float washFlutter = isInPropWash ? (1.0f + 0.20f * Mathf.Sin(Time.time * 45.0f)) : 1.0f;
            float targetWindVol = windFactor * 0.35f * washFlutter;
            _propWindVolume = Mathf.Lerp(_propWindVolume, targetWindVol, volSmooth);
            _propWindAudioSource.volume = _propWindVolume * _masterFade * masterVol;

            // 4. Aerodynamic Prop Rip ("Blade Tear" / "Vortex Stall")
            float angSpeed = localAngularRatesDeg.magnitude;
            float angIntensity = Mathf.Clamp01((angSpeed - 320.0f) / 480.0f);
            float gIntensity = (gForceLoad > 2.2f && angSpeed > 180.0f && speedMps > 8.0f) ? Mathf.Clamp01((gForceLoad - 2.2f) / 3.5f) : 0.0f;
            float yawIntensity = Mathf.Clamp01((Mathf.Abs(localAngularRatesDeg.y) - 280.0f) / 400.0f);
            float washRipIntensity = isInPropWash ? 0.25f : 0.0f;

            float ripIntensity = Mathf.Max(Mathf.Max(angIntensity, gIntensity), Mathf.Max(yawIntensity, washRipIntensity));

            if (Settings.EnableBladeSlapAudio.Value && ripIntensity > 0.03f)
            {
                _currentBladeRipVolume = Mathf.Lerp(_currentBladeRipVolume, ripIntensity * 0.65f, volSmooth);
                float maxThrottle = (motorThrottles != null && motorThrottles.Length >= 4) ?
                    Mathf.Max(Mathf.Max(motorThrottles[0], motorThrottles[1]), Mathf.Max(motorThrottles[2], motorThrottles[3])) :
                    collectiveThrottle;

                float ripScale = Mathf.Clamp(hw.MaxBladePassF0 / 1600.0f, 0.60f, 1.85f);
                float targetRipPitch = Mathf.Lerp(1.0f, 2.0f, maxThrottle) * ripScale;
                _bladeRipPitch = Mathf.Lerp(_bladeRipPitch, targetRipPitch, pitchSmooth);
                _bladeRipAudioSource.pitch = _bladeRipPitch;
            }
            else
            {
                _currentBladeRipVolume = Mathf.Lerp(_currentBladeRipVolume, 0.0f, 1.0f - Mathf.Exp(-dt * 14.0f));
            }

            _bladeRipAudioSource.volume = _currentBladeRipVolume * _masterFade * masterVol;
        }

        public void Destroy()
        {
            if (_audioEmitterObject != null)
            {
                Object.Destroy(_audioEmitterObject);
                _audioEmitterObject = null;
            }
            for (int i = 0; i < 4; i++)
            {
                if (_bladePassClips[i] != null)
                {
                    Object.Destroy(_bladePassClips[i]);
                    _bladePassClips[i] = null;
                }
            }
            if (_statorWhineClip != null)
            {
                Object.Destroy(_statorWhineClip);
                _statorWhineClip = null;
            }
            if (_propWindClip != null)
            {
                Object.Destroy(_propWindClip);
                _propWindClip = null;
            }
            if (_bladeRipClip != null)
            {
                Object.Destroy(_bladeRipClip);
                _bladeRipClip = null;
            }
            if (_escInitClip != null)
            {
                Object.Destroy(_escInitClip);
                _escInitClip = null;
            }
            if (_impactClip != null)
            {
                Object.Destroy(_impactClip);
                _impactClip = null;
            }
            if (_armBeepClip != null) { Object.Destroy(_armBeepClip); _armBeepClip = null; }
            if (_disarmBeepClip != null) { Object.Destroy(_disarmBeepClip); _disarmBeepClip = null; }
            if (_lowBattBeepClip != null) { Object.Destroy(_lowBattBeepClip); _lowBattBeepClip = null; }
        }

        private AudioClip GenerateBladePassClip(int motorIndex, int seed, float baseF0)
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int totalSamples = (int)(sampleRate * duration);
            int fadeSamples = 512;
            int genSamples = totalSamples + fadeSamples;
            float[] samples = new float[totalSamples];
            float[] raw = new float[genSamples];

            // Fundamental aerodynamic frequency of tri-blade prop at reference pitch 1.0
            float f0 = baseF0;
            float twoPi = Mathf.PI * 2.0f;
            System.Random rand = new System.Random(seed);

            // Independent randomized harmonic phases for each motor to guarantee phase incoherence
            float[] harmonicPhases = new float[8];
            for (int k = 0; k < 8; k++)
            {
                harmonicPhases[k] = (float)(rand.NextDouble() * twoPi);
            }

            float b0 = 0.0f;
            float b1 = 0.0f;

            for (int i = 0; i < genSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Tri-blade aerodynamic passing phase: 3 pressure waves per shaft revolution
                float phase = (t * f0) % 1.0f;
                // Asymmetric compression impulse (steep leading edge, exponential suction decay)
                float pulse = Mathf.Exp(-12.0f * phase) * Mathf.Sin(twoPi * phase);

                // Harmonic series with 1x shaft sub-fundamental and decorrelated motor phases
                float wave = pulse * 0.30f;
                wave += Mathf.Sin(twoPi * (f0 / 3.0f) * t + harmonicPhases[0]) * 0.22f;
                wave += Mathf.Sin(twoPi * f0 * t + harmonicPhases[0]) * 0.30f;
                wave += Mathf.Sin(twoPi * (f0 * 2.0f) * t + harmonicPhases[1]) * 0.18f;
                wave += Mathf.Sin(twoPi * (f0 * 3.0f) * t + harmonicPhases[2]) * 0.11f;
                wave += Mathf.Sin(twoPi * (f0 * 4.0f) * t + harmonicPhases[3]) * 0.06f;
                wave += Mathf.Sin(twoPi * (f0 * 5.0f) * t + harmonicPhases[4]) * 0.035f;
                wave += Mathf.Sin(twoPi * (f0 * 6.0f) * t + harmonicPhases[5]) * 0.020f;
                wave += Mathf.Sin(twoPi * (f0 * 7.0f) * t + harmonicPhases[6]) * 0.012f;
                wave += Mathf.Sin(twoPi * (f0 * 8.0f) * t + harmonicPhases[7]) * 0.008f;

                // Independent blade surface pink turbulence
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                b0 = 0.99f * b0 + white * 0.05f;
                b1 = 0.95f * b1 + white * 0.15f;
                float pink = (b0 + b1) * 0.5f;

                wave += pink * pulse * 0.12f;

                raw[i] = wave;
            }

            // Proper lookahead seamless loop crossfade
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < fadeSamples)
                {
                    float blend = (float)i / fadeSamples;
                    samples[i] = Mathf.Lerp(raw[totalSamples + i], raw[i], blend);
                }
                else
                {
                    samples[i] = raw[i];
                }
            }

            // Normalize peak to 0.90
            float maxAbs = 0.001f;
            for (int i = 0; i < totalSamples; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > maxAbs) maxAbs = abs;
            }
            float norm = 0.90f / maxAbs;
            for (int i = 0; i < totalSamples; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * norm, -1.0f, 1.0f);
            }

            AudioClip clip = AudioClip.Create($"FPV_Blade_Pass_M{motorIndex}", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateStatorWhineClip(float baseF0)
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Clean 14-pole brushless stator commutation fundamental: 7/3 ratio to tri-blade pass
            // For baseF0 = 600Hz: fCarrier = 1400Hz (700 cycles in 0.5s exact). Zero loop click!
            float fCarrier = baseF0 * (7.0f / 3.0f);
            float twoPi = Mathf.PI * 2.0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Clean harmonic series representing stator commutation
                float s1 = Mathf.Sin(twoPi * fCarrier * t);
                float s2 = Mathf.Sin(twoPi * (fCarrier * 2.0f) * t) * 0.25f;
                float s3 = Mathf.Sin(twoPi * (fCarrier * 3.0f) * t) * 0.08f;

                // Soft saturation gives smooth metallic timbre without harsh synthetic robot tones
                float raw = s1 + s2 + s3;
                float saturated = (float)System.Math.Tanh(raw * 0.80);

                samples[i] = saturated * 0.24f;
            }

            AudioClip clip = AudioClip.Create("FPV_Stator_Whine_Clean", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateImpactClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(8888);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-22.0f * t);
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                float lowThump = Mathf.Sin(2.0f * Mathf.PI * 95.0f * t) * Mathf.Exp(-12.0f * t);
                samples[i] = Mathf.Clamp((white * 0.75f + lowThump * 0.65f) * env, -1.0f, 1.0f);
            }

            AudioClip clip = AudioClip.Create("FPV_Prop_Strike", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateBladeRipClip()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int totalSamples = (int)(sampleRate * duration);
            int fadeSamples = 512;
            int genSamples = totalSamples + fadeSamples;
            float[] samples = new float[totalSamples];
            float[] raw = new float[genSamples];

            // Aerodynamic prop rip: violent blade-tip vortex stall flutter at 165 Hz base
            float ripFreq = 165.0f;
            float twoPi = Mathf.PI * 2.0f;
            System.Random rand = new System.Random(4096);

            for (int i = 0; i < genSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Aerodynamic vortex separation pulse
                float s = Mathf.Sin(twoPi * ripFreq * t);
                float pulse = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 6.0f);

                // Broadband turbulent tear noise (filtered)
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                float subHarmonic = Mathf.Sin(twoPi * (ripFreq * 2.0f) * t) * 0.35f;

                float tear = pulse * (noise * 0.75f + subHarmonic);
                raw[i] = Mathf.Clamp(tear * 0.70f, -1.0f, 1.0f);
            }

            // Proper lookahead seamless loop crossfade
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < fadeSamples)
                {
                    float blend = (float)i / fadeSamples;
                    samples[i] = Mathf.Lerp(raw[totalSamples + i], raw[i], blend);
                }
                else
                {
                    samples[i] = raw[i];
                }
            }

            AudioClip clip = AudioClip.Create("FPV_Blade_Rip_Synthetic", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateEscInitClip()
        {
            int sampleRate = 44100;
            float totalDuration = 0.85f;
            int totalSamples = (int)(sampleRate * totalDuration);
            float[] samples = new float[totalSamples];

            (float start, float duration, float freq)[] tones = new[]
            {
                (0.00f, 0.085f, 440.0f),
                (0.11f, 0.085f, 554.37f),
                (0.22f, 0.085f, 659.25f),
                (0.38f, 0.100f, 880.0f),
                (0.50f, 0.180f, 1108.7f)
            };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0.0f;

                foreach (var tone in tones)
                {
                    if (t >= tone.start && t < tone.start + tone.duration)
                    {
                        float toneTime = t - tone.start;
                        float phase = toneTime * tone.freq * Mathf.PI * 2.0f;
                        float wave = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 3.0f);
                        float env = Mathf.Sin((toneTime / tone.duration) * Mathf.PI);
                        sample = wave * env * 0.8f;
                        break;
                    }
                }

                samples[i] = sample;
            }

            AudioClip clip = AudioClip.Create("FPV_ESC_Init_BLHeli", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GeneratePropWindClip()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int totalSamples = (int)(sampleRate * duration);
            int fadeSamples = 512;
            int genSamples = totalSamples + fadeSamples;
            float[] samples = new float[totalSamples];
            float[] raw = new float[genSamples];

            float b0 = 0.0f;
            float b1 = 0.0f;
            float b2 = 0.0f;
            System.Random rand = new System.Random(42);

            for (int i = 0; i < genSamples; i++)
            {
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                b0 = 0.99765f * b0 + white * 0.0990460f;
                b1 = 0.96300f * b1 + white * 0.2965164f;
                b2 = 0.57000f * b2 + white * 1.0526913f;
                float pink = (b0 + b1 + b2 + white * 0.1848f) * 0.18f;
                raw[i] = Mathf.Clamp(pink, -1.0f, 1.0f);
            }

            // Proper lookahead seamless loop crossfade
            for (int i = 0; i < totalSamples; i++)
            {
                if (i < fadeSamples)
                {
                    float blend = (float)i / fadeSamples;
                    samples[i] = Mathf.Lerp(raw[totalSamples + i], raw[i], blend);
                }
                else
                {
                    samples[i] = raw[i];
                }
            }

            AudioClip clip = AudioClip.Create("FPV_PropWind_Synthetic", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
