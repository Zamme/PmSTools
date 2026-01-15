namespace PmSTools.Models;

public static class SaveLoadData
{
    public const string PrefixesCountPrefName = "prefixes_count";
    public const string PrefixesPrefsKeyPrefix = "c2bp_";
    public const string ActivePrefixesPrefsKeyPrefix = "c2bap_";

    public const string LastCodesPrefKey = "last_codes";
    

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
        foreach (bool prefix in activePrefixes)
        {
            prefixCounter++;
            string prefixKey = ActivePrefixesPrefsKeyPrefix + prefixCounter.ToString();
            Preferences.Set(prefixKey, prefix);
        }
    }

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