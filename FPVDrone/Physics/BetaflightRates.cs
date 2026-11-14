using UnityEngine;

namespace FPVDrone
{
    public static class BetaflightRates
    {
        public const float MaxRateDegPerSec = 1998.0f;

        public static float CalculateRate(float stickInput, float rcRate, float superRate, float expo)
        {
            float clamped = Mathf.Clamp(stickInput, -1.0f, 1.0f);
            float absStick = Mathf.Abs(clamped);
            if (absStick < 0.0001f)
                return 0.0f;

            // Betaflight polynomial expo (rc.c applyBetaflightRates):
            // rcCommandf = rcCommandf * |rc|^3 * expo + rcCommandf * (1 - expo)
            float stickCubed = absStick * absStick * absStick;
            float expoStick = clamped * stickCubed * expo + clamped * (1.0f - expo);

            // SuperRate denominator (uses raw stick magnitude, clamped like Betaflight)
            float superFactor = Mathf.Clamp(1.0f - (absStick * superRate), 0.01f, 1.0f);

            // Commanded angular rate in degrees per second, capped at Betaflight SETPOINT_RATE_LIMIT
            float rate = (200.0f * rcRate * expoStick) / superFactor;
            return Mathf.Clamp(rate, -MaxRateDegPerSec, MaxRateDegPerSec);
        }

        public static float EvaluateAxisRate(float stickInput, int axis)
        {
            if (Settings.RateSystem.Value == RateType.Actual)
            {
                switch (axis)
                {
                    case 0: return CalculateActualRate(stickInput, Settings.RollCenterSens.Value, Settings.RollMaxRate.Value, Settings.RollActualExpo.Value);
                    case 1: return CalculateActualRate(stickInput, Settings.PitchCenterSens.Value, Settings.PitchMaxRate.Value, Settings.PitchActualExpo.Value);
                    default: return CalculateActualRate(stickInput, Settings.YawCenterSens.Value, Settings.YawMaxRate.Value, Settings.YawActualExpo.Value);
                }
            }

            switch (axis)
            {
                case 0: return CalculateRate(stickInput, Settings.RcRate.Value, Settings.SuperRate.Value, Settings.Expo.Value);
                case 1: return CalculateRate(stickInput, Settings.PitchRcRate.Value, Settings.PitchSuperRate.Value, Settings.PitchExpo.Value);
                default: return CalculateRate(stickInput, Settings.YawRcRate.Value, Settings.YawSuperRate.Value, Settings.YawExpo.Value);
            }
        }

        public static float GetAxisMaxRate(int axis)
        {
            return EvaluateAxisRate(1.0f, axis);
        }

        public static float GetMaxRate(float rcRate, float superRate, float expo)
        {
            return CalculateRate(1.0f, rcRate, superRate, expo);
        }

        /// <summary>
        /// Betaflight 4.2+ Actual Rates (rc.c applyActualRates).
        /// Center sensitivity sets exact slope at stick=0; maxRate sets exact output at stick=1.
        /// </summary>
        public static float CalculateActualRate(float stickInput, float centerSensitivity, float maxRate, float expo)
        {
            float clamped = Mathf.Clamp(stickInput, -1.0f, 1.0f);
            float absStick = Mathf.Abs(clamped);
            if (absStick < 0.0001f)
                return 0.0f;

            float expof = absStick * (Mathf.Pow(absStick, 5.0f) * expo + absStick * (1.0f - expo));
            float stickMovement = Mathf.Max(0.0f, maxRate - centerSensitivity);
            float rate = clamped * centerSensitivity + Mathf.Sign(clamped) * stickMovement * expof;
            return Mathf.Clamp(rate, -MaxRateDegPerSec, MaxRateDegPerSec);
        }

        /// <summary>
        /// Betaflight rcLookupThrottle curve (throttle_mid + throttle_expo) with throttle_limit.
        /// </summary>
        public static float CalculateThrottle(float input01, float throttleMid, float throttleExpo, float limitPercent, ThrottleLimitType limitType)
        {
            float x = Mathf.Clamp01(input01);
            float mid = Mathf.Clamp(throttleMid, 0.05f, 0.95f);
            float expo = Mathf.Clamp01(throttleExpo);

            float tmp = x - mid;
            float y = tmp > 0.0f ? (1.0f - mid) : mid;
            if (y < 0.001f) y = 0.001f;
            float norm = tmp / y;
            float curved = Mathf.Clamp01(mid + tmp * (1.0f - expo + expo * (norm * norm)));

            float limit = Mathf.Clamp(limitPercent / 100.0f, 0.25f, 1.0f);
            switch (limitType)
            {
                case ThrottleLimitType.Scale:
                    return curved * limit;
                case ThrottleLimitType.Clip:
                    return Mathf.Min(curved, limit);
                case ThrottleLimitType.Off:
                default:
                    return curved;
            }
        }
    }
}

