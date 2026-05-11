namespace CVROPENAI
{
    static class KokoroVoices
    {
        internal static readonly string[] Languages =
        {
            "American English", "British English", "Spanish", "French",
            "Hindi", "Italian", "Japanese", "Portuguese", "Chinese",
        };

        internal static readonly string[][] VoicesByLanguage =
        {
            // American English (af_ / am_)
            new[] {
                "af_alloy", "af_aoede", "af_bella", "af_heart", "af_jadzia", "af_jessica",
                "af_kore", "af_nicole", "af_nova", "af_river", "af_sarah", "af_sky",
                "af_v0", "af_v0bella", "af_v0irulan", "af_v0nicole", "af_v0sarah", "af_v0sky",
                "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael",
                "am_onyx", "am_puck", "am_santa", "am_v0adam", "am_v0gurney", "am_v0michael",
            },
            // British English (bf_ / bm_)
            new[] {
                "bf_alice", "bf_emma", "bf_lily", "bf_v0emma", "bf_v0isabella",
                "bm_daniel", "bm_fable", "bm_george", "bm_lewis", "bm_v0george", "bm_v0lewis",
            },
            // Spanish (ef_ / em_)
            new[] { "ef_dora", "em_alex", "em_santa" },
            // French (ff_)
            new[] { "ff_siwis" },
            // Hindi (hf_ / hm_)
            new[] { "hf_alpha", "hf_beta", "hm_omega", "hm_psi" },
            // Italian (if_ / im_)
            new[] { "if_sara", "im_nicola" },
            // Japanese (jf_ / jm_)
            new[] { "jf_alpha", "jf_gongitsune", "jf_nezumi", "jf_tebukuro", "jm_kumo" },
            // Portuguese (pf_ / pm_)
            new[] { "pf_dora", "pm_alex", "pm_santa" },
            // Chinese (zf_ / zm_)
            new[] { "zf_xiaobei", "zf_xiaoni", "zf_xiaoxiao", "zf_xiaoyi", "zm_yunjian", "zm_yunxi", "zm_yunxia", "zm_yunyang" },
        };
    }
}
