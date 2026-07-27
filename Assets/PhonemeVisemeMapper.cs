public static class PhonemeVisemeMapper
{
    public static Viseme ToViseme(string phoneme)
    {
        if (string.IsNullOrWhiteSpace(phoneme))
        {
            return Viseme.Silence;
        }

        phoneme = RemoveStressMarker(phoneme.Trim().ToUpperInvariant());

        return phoneme switch
        {
            // Closed-lip sounds.
            "P" or "B" or "M"
                => Viseme.Explosive,

            // Lower lip touching upper teeth.
            "F" or "V"
                => Viseme.DentalLip,

            // Wide/front vowels.
            "IY" or "IH" or "EY" or "EH" or "AE"
                => Viseme.Wide,

            // Open vowels.
            "AA" or "AH" or "AY" or "AW"
                => Viseme.Open,

            // Rounded vowels.
            "OW" or "OY" or "UH" or "UW" or "W"
                => Viseme.Rounded,

            // Consonants involving teeth/tongue.
            "TH" or "DH" or "CH" or "JH" or "SH" or "ZH"
                => Viseme.Affricate,

            // General consonant mouth shape.
            "T" or "D" or "K" or "G" or
            "N" or "NG" or "L" or "R" or
            "S" or "Z" or "HH" or "Y"
                => Viseme.Tight,

            "SIL" or "SP" or "PAUSE"
                => Viseme.Silence,

            _ => Viseme.Tight
        };
    }

    private static string RemoveStressMarker(string phoneme)
    {
        return phoneme.TrimEnd('0', '1', '2');
    }
}