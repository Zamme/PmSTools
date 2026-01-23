namespace PmSTools.Models;

public static class SaveLoadData
{
    public const string PrefixesCountPrefName = "prefixes_count";
    public const string PrefixesPrefsKeyPrefix = "c2bp_";
    public const string ActivePrefixesPrefsKeyPrefix = "c2bap_";

    public const string LastCodesPrefKey = "last_codes";
    
    /*
    public const string SavedCodesCountPrefName = "saved_codes_count";
    */
    public const string SavedCodesPrefsKey = "saved_codes";
    

    public static void SavePrefixesPrefs(List<string> prefixes)
    {
        int prefixesCount = prefixes.Count;
        Preferences.Set(PrefixesCountPrefName, prefixesCount);
        int prefixCounter = -1;
        foreach (string prefix in prefixes)
        {
            prefixCounter++;
            string prefixKey = PrefixesPrefsKeyPrefix + prefixCounter.ToString();
            Preferences.Set(prefixKey, prefix);
        }
    }
    
    public static void SaveActivePrefixesPrefs(List<bool> activePrefixes)
    {
        int prefixCounter = -1;
        foreach (bool activePrefix in activePrefixes)
        {
            prefixCounter++;
            string activePrefixKey = ActivePrefixesPrefsKeyPrefix + prefixCounter.ToString();
            Preferences.Set(activePrefixKey, activePrefix);
        }
    }

    public static void DeleteCode(string code)
    {
        string codePlusSeparator = code + ",";
        string savedCodesString = Preferences.Get(SavedCodesPrefsKey, "");
        savedCodesString = savedCodesString.Replace(codePlusSeparator, "");
        Preferences.Set(SavedCodesPrefsKey, savedCodesString);
    }
    
    public static void SaveCode(string code)
    {
        if (!Preferences.ContainsKey(SavedCodesPrefsKey))
        {
            Preferences.Set(SavedCodesPrefsKey, "");
        }

        string savedCodesString = Preferences.Get(SavedCodesPrefsKey, "");
        string newCode = code + ",";
        if (!savedCodesString.Contains(newCode))
        {
            savedCodesString += newCode;
            Preferences.Set(SavedCodesPrefsKey, savedCodesString);
        }
    }
    
    /*public static void SaveBarcodesPrefs(List<string> barcodes)
    {
        if (!Preferences.ContainsKey(SavedCodesCountPrefName))
        {
            Preferences.Set(SavedCodesCountPrefName, 0);
        }
        
        int savedCodesCount = Preferences.Get(SavedCodesCountPrefName, 0);
        int barcodeCounter = savedCodesCount;
        foreach (var barcode in barcodes)
        {
            barcodeCounter++;
            string barcodeKey = SavedCodesPrefsKeyPrefix + barcodeCounter.ToString();
            Preferences.Set(barcodeKey, barcode);
        }
    }*/

    public static void CleanActivePrefixesPrefs()
    {
        int prefsCount = Preferences.Get(PrefixesCountPrefName, 0);
        if (prefsCount > 0)
        {
            for (int count = 0; count < prefsCount; count++)
            {
                string key = $"{ActivePrefixesPrefsKeyPrefix}{count}";
                if (Preferences.ContainsKey(key))
                {
                    Preferences.Remove(key);
                }
            }
        }
    }

    public static void CleanPrefixesPrefs()
    {
        int prefsCount = Preferences.Get(PrefixesCountPrefName, 0);
        if (prefsCount > 0)
        {
            for (int count = 0; count < prefsCount; count++)
            {
                string key = $"{PrefixesPrefsKeyPrefix}{count}";
                if (Preferences.ContainsKey(key))
                {
                    Preferences.Remove(key);
                }
            }
        }
    }

}