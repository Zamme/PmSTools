using System.Text.Json;

namespace PmSTools.Models;

public static class SaveLoadData
{
    public const string PrefixesCountPrefName = "prefixes_count";
    public const string PrefixesPrefsKeyPrefix = "c2bp_";
    public const string ActivePrefixesPrefsKeyPrefix = "c2bap_";
    public const string SeparatorChar = ",";
    public const string LastCodesPrefKey = "last_codes";
    public const string LastPlaceInfoPrefKey = "last_place_info";
    public const string LastPlacesInfoPrefKey = "last_places_info";
    public const string SavedDeliveryRoutePrefKey = "saved_delivery_route";
    public const string SavedDeliveryRoutesPrefKey = "saved_delivery_routes";
    public const int LastPlacesMaxCount = 10;
    
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

    public static void ClearAllSavedCodes()
    {
        Preferences.Remove(SavedCodesPrefsKey);
    }
    
    public static void DeleteCode(string code)
    {
        string codePlusSeparator = code + SeparatorChar;
        string savedCodesString = Preferences.Get(SavedCodesPrefsKey, "");
        savedCodesString = savedCodesString.Replace(codePlusSeparator, "");
        Preferences.Set(SavedCodesPrefsKey, savedCodesString);
    }

    public static List<string> GetSavedCodes()
    {
        List<string> savedCodes = new List<string>();
        if (Preferences.ContainsKey(SavedCodesPrefsKey))
        {
            string savedCodesString = Preferences.Get(SavedCodesPrefsKey, "");
            savedCodes = savedCodesString.Split(SeparatorChar).ToList();
        }

        return savedCodes;
    }

    public static bool IsCodeSaved(string code)
    {
        string codePlusSeparators = code + SeparatorChar;
        bool isCodeSaved = false;
        if (Preferences.ContainsKey(SavedCodesPrefsKey))
        {
            string savedCodesString = Preferences.Get(SavedCodesPrefsKey, "");
            isCodeSaved = savedCodesString.Contains(codePlusSeparators);
        }
        return isCodeSaved;
    }
    
    public static void SaveCode(string code)
    {
        if (!Preferences.ContainsKey(SavedCodesPrefsKey))
        {
            Preferences.Set(SavedCodesPrefsKey, "");
        }

        string savedCodesString = Preferences.Get(SavedCodesPrefsKey, "");
        string newCode = code + SeparatorChar;
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

    public static void SaveLastPlaceInfo(PlaceInfoItem placeInfo)
    {
        if (placeInfo == null)
        {
            return;
        }

        AddLastPlaceInfo(placeInfo);
    }

    public static void AddLastPlaceInfo(PlaceInfoItem placeInfo)
    {
        if (placeInfo == null)
        {
            return;
        }

        List<PlaceInfoItem> places = GetLastPlaceInfos();
        places.Insert(0, placeInfo);

        if (places.Count > LastPlacesMaxCount)
        {
            places = places.Take(LastPlacesMaxCount).ToList();
        }

        string placesJson = JsonSerializer.Serialize(places);
        Preferences.Set(LastPlacesInfoPrefKey, placesJson);

        string lastPlaceJson = JsonSerializer.Serialize(places[0]);
        Preferences.Set(LastPlaceInfoPrefKey, lastPlaceJson);
    }

    public static List<PlaceInfoItem> GetLastPlaceInfos()
    {
        if (Preferences.ContainsKey(LastPlacesInfoPrefKey))
        {
            string json = Preferences.Get(LastPlacesInfoPrefKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    List<PlaceInfoItem>? places = JsonSerializer.Deserialize<List<PlaceInfoItem>>(json);
                    if (places != null)
                    {
                        return places.Where(place => place != null).ToList();
                    }
                }
                catch
                {
                }
            }
        }

        if (TryGetLegacyLastPlaceInfo(out PlaceInfoItem? legacyPlace) && legacyPlace != null)
        {
            return new List<PlaceInfoItem> { legacyPlace };
        }

        return new List<PlaceInfoItem>();
    }

    public static void ClearLastPlaceInfos()
    {
        Preferences.Remove(LastPlacesInfoPrefKey);
        Preferences.Remove(LastPlaceInfoPrefKey);
    }

    private static bool TryGetLegacyLastPlaceInfo(out PlaceInfoItem? placeInfo)
    {
        placeInfo = null;
        if (!Preferences.ContainsKey(LastPlaceInfoPrefKey))
        {
            return false;
        }

        string json = Preferences.Get(LastPlaceInfoPrefKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            placeInfo = JsonSerializer.Deserialize<PlaceInfoItem>(json);
            return placeInfo != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetLastPlaceInfo(out PlaceInfoItem? placeInfo)
    {
        placeInfo = GetLastPlaceInfos().FirstOrDefault();
        return placeInfo != null;
    }

    public static void SaveDeliveryRoutes(IEnumerable<DeliveryRoute> routes)
    {
        if (routes == null)
            return;

        var persistedRoutes = routes
            .Select(route => new DeliveryRouteSnapshot
            {
                Name = route.Name,
                Stops = route.Stops.Select(BuildPersistedStop).ToList()
            })
            .ToList();

        var routesJson = JsonSerializer.Serialize(persistedRoutes);
        Preferences.Set(SavedDeliveryRoutesPrefKey, routesJson);
    }

    public static bool TryGetSavedDeliveryRoutes(out List<DeliveryRoute> routes)
    {
        routes = new List<DeliveryRoute>();

        if (Preferences.ContainsKey(SavedDeliveryRoutesPrefKey))
        {
            var routesJson = Preferences.Get(SavedDeliveryRoutesPrefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(routesJson))
                return false;

            try
            {
                var savedRoutes = JsonSerializer.Deserialize<List<DeliveryRouteSnapshot>>(routesJson);
                if (savedRoutes == null || savedRoutes.Count == 0)
                    return false;

                foreach (var savedRoute in savedRoutes)
                {
                    var restoredRoute = new DeliveryRoute();
                    restoredRoute.Name = string.IsNullOrWhiteSpace(savedRoute.Name) ? null : savedRoute.Name.Trim();
                    foreach (var stop in savedRoute.Stops.Where(stop => stop != null))
                    {
                        stop.IsExpanded = false;
                        restoredRoute.AddStop(stop);
                    }

                    restoredRoute.RenumberStops();
                    routes.Add(restoredRoute);
                }

                return routes.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        if (!Preferences.ContainsKey(SavedDeliveryRoutePrefKey))
            return false;

        var legacyRouteJson = Preferences.Get(SavedDeliveryRoutePrefKey, string.Empty);
        if (string.IsNullOrWhiteSpace(legacyRouteJson))
            return false;

        try
        {
            var savedStops = JsonSerializer.Deserialize<List<DeliveryRouteStop>>(legacyRouteJson);
            if (savedStops == null || savedStops.Count == 0)
                return false;

            var restoredRoute = new DeliveryRoute();
            foreach (var stop in savedStops.Where(stop => stop != null))
            {
                stop.IsExpanded = false;
                restoredRoute.AddStop(stop);
            }

            restoredRoute.RenumberStops();
            routes.Add(restoredRoute);
            SaveDeliveryRoutes(routes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void ClearSavedDeliveryRoute()
    {
        Preferences.Remove(SavedDeliveryRoutePrefKey);
        Preferences.Remove(SavedDeliveryRoutesPrefKey);
    }

    private static DeliveryRouteStop BuildPersistedStop(DeliveryRouteStop stop)
    {
        return new DeliveryRouteStop
        {
            Order = stop.Order,
            Name = stop.Name,
            StreetName = stop.StreetName,
            StreetNumber = stop.StreetNumber,
            PostalCode = stop.PostalCode,
            City = stop.City,
            Country = stop.Country,
            Latitude = stop.Latitude,
            Longitude = stop.Longitude,
            IsExpanded = false
        };
    }

    private sealed class DeliveryRouteSnapshot
    {
        public string? Name { get; set; }
        public List<DeliveryRouteStop> Stops { get; set; } = new List<DeliveryRouteStop>();
    }

}