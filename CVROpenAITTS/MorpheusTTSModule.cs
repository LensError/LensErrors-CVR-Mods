namespace CVROPENAI
{
    static class MorpheusVoices
    {
        internal static readonly string[] Languages =
        {
            "English", "French", "German", "Korean", "Hindi", "Mandarin", "Spanish", "Italian",
        };

        internal static readonly string[][] VoicesByLanguage =
        {
            new[] { "tara", "leah", "jess", "leo", "dan", "mia", "zac", "zoe" },      // English
            new[] { "pierre", "amelie", "marie" },                                      // French
            new[] { "jana", "thomas", "max" },                                          // German
            new[] { "유나", "준서" },                                                   // Korean
            new[] { "ऋतिका" },                                                         // Hindi
            new[] { "长乐", "白芷" },                                                   // Mandarin
            new[] { "javi", "sergio", "maria" },                                        // Spanish
            new[] { "pietro", "giulia", "carlo" },                                      // Italian
        };
    }
}
