// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// RVACONT-006: Batch 6 - Localization System
// Dhivehi (dv) primary support with cultural context preservation

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using TMPro;

namespace RVA.TAC.Localization
{
    public class LocalizationSystem : MonoBehaviour
    {
        private static LocalizationSystem _instance;
        public static LocalizationSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<LocalizationSystem>();
                return _instance;
            }
        }
        
        // Supported languages: Dhivehi (primary), English (fallback)
        public enum Language { dv, en }
        
        [Header("Current Language")]
        [SerializeField] private Language currentLanguage = Language.dv;
        public Language CurrentLanguage => currentLanguage;
        
        // In-memory translation database (no local files)
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        
        // Dhivehi-specific constants
        private const string RTL_MARK = "\u200F"; // Right-to-left mark
        private const string THAANA_RANGE_START = "\u0780";
        private const string THAANA_RANGE_END = "\u07BF";
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeTranslations();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Initialize all translations procedurally
        /// CRITICAL: NO EXTERNAL FILES - ALL DATA GENERATED AT RUNTIME
        /// </summary>
        void InitializeTranslations()
        {
            // Dhivehi translations (primary)
            AddTranslation("game_title", "ރާޖްޖެ ވަގުއް އޯޓޯ", "dv");
            AddTranslation("start_game", "މެޗު ފެށުން", "dv");
            AddTranslation("settings", "ސެޓިންސް", "dv");
            AddTranslation("prayer_time_fajr", "ފަޖްރު", "dv");
            AddTranslation("prayer_time_dhuhr", "ދުހުރު", "dv");
            AddTranslation("prayer_time_asr", "ައަސުރު", "dv");
            AddTranslation("prayer_time_maghrib", "މަރުރިބު", "dv");
            AddTranslation("prayer_time_isha", "އިޝާ", "dv");
            AddTranslation("prayer_time_active", "ނަމާދު ވަގުރުވެފައި", "dv");
            AddTranslation("please_return_after_prayer", "ނަމާދު ނިމުނުތަނާ ފުރަތަމަ އަންނަން", "dv");
            AddTranslation("gang_rival_alert", "ގެންގުން މީހުން ފެނުނު!", "dv");
            AddTranslation("police_warning", "ފުލުހުންގެ ލޯނުފެތޭ!", "dv");
            AddTranslation("fishing_catch", "މާސް ހިފުނީ!", "dv");
            AddTranslation("boat_docked", "ބޯޓު ދިވެށްޓައިލީ", "dv");
            AddTranslation("health_low", "ސިއްހު ދުވަލު ކުޑަވެއްޖެ", "dv");
            AddTranslation("stamina_depleted", "ހަށިގަނޑު ހިއްވަނީ ހުއްޓެވެ", "dv");
            
            // English translations (fallback)
            AddTranslation("game_title", "RAAJJE VAGU AUTO", "en");
            AddTranslation("start_game", "Start Game", "en");
            AddTranslation("settings", "Settings", "en");
            AddTranslation("prayer_time_fajr", "Fajr", "en");
            AddTranslation("prayer_time_dhuhr", "Dhuhr", "en");
            AddTranslation("prayer_time_asr", "Asr", "en");
            AddTranslation("prayer_time_maghrib", "Maghrib", "en");
            AddTranslation("prayer_time_isha", "Isha", "en");
            AddTranslation("prayer_time_active", "Prayer Time Active", "en");
            AddTranslation("please_return_after_prayer", "Please return after prayer", "en");
            AddTranslation("gang_rival_alert", "Rival gang spotted!", "en");
            AddTranslation("police_warning", "Police attention!", "en");
            AddTranslation("fishing_catch", "Fish caught!", "en");
            AddTranslation("boat_docked", "Boat docked", "en");
            AddTranslation("health_low", "Health is low", "en");
            AddTranslation("stamina_depleted", "Stamina depleted", "en");
            
            // Game-specific terms
            AddTranslation("island_hulhumale", "ހުޅުމާލެ", "dv");
            AddTranslation("island_hulhumale", "Hulhumalé", "en");
            AddTranslation("island_male", "މާލެ", "dv");
            AddTranslation("island_male", "Malé", "en");
            AddTranslation("vehicle_dhoani", "ދޯނި", "dv");
            AddTranslation("vehicle_dhoani", "Dhoni Boat", "en");
            AddTranslation("vehicle_bajaj", "ބަޖާޖު", "dv");
            AddTranslation("vehicle_bajaj", "Bajaj", "en");
            
            Debug.Log($"Localization initialized: {translations.Count} entries");
        }
        
        void AddTranslation(string key, string value, string langCode)
        {
            string compositeKey = $"{key}_{langCode}";
            if (!translations.ContainsKey(compositeKey))
            {
                translations.Add(compositeKey, value);
            }
        }
        
        /// <summary>
        /// Get localized string with fallback to English
        /// </summary>
        public string GetLocalizedString(string key)
        {
            string langCode = currentLanguage.ToString();
            string compositeKey = $"{key}_{langCode}";
            
            if (translations.TryGetValue(compositeKey, out string value))
            {
                return ApplyTextDirection(value);
            }
            
            // Fallback to English
            string englishKey = $"{key}_en";
            if (translations.TryGetValue(englishKey, out string englishValue))
            {
                Debug.LogWarning($"Missing translation for {key} in {langCode}, using English fallback");
                return englishValue;
            }
            
            Debug.LogError($"No translation found for key: {key}");
            return $"[{key}]";
        }
        
        /// <summary>
        /// Applies RTL formatting for Dhivehi text
        /// </summary>
        string ApplyTextDirection(string text)
        {
            if (currentLanguage == Language.dv)
            {
                // Wrap in RTL markers and ensure proper Thaana rendering
                return RTL_MARK + text + RTL_MARK;
            }
            return text;
        }
        
        /// <summary>
        /// Changes language at runtime with UI refresh
        /// </summary>
        public void SetLanguage(Language newLanguage)
        {
            currentLanguage = newLanguage;
            
            // Notify all TMPro components to refresh
            TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (var text in allTexts)
            {
                // Trigger text refresh (assumes text keys are stored elsewhere)
                if (text.gameObject.activeInHierarchy)
                {
                    text.SetVerticesDirty();
                }
            }
            
            // Re-apply safe area for RTL
            if (newLanguage == Language.dv)
            {
                UIManager.Instance?.ApplySafeArea();
            }
            
            Debug.Log($"Language changed to: {newLanguage}");
        }
        
        /// <summary>
        /// Detects if string contains Thaana characters
        /// </summary>
        public bool IsDhivehiText(string text)
        {
            foreach (char c in text)
            {
                if (c >= '\u0780' && c <= '\u07BF')
                    return true;
            }
            return false;
        }
    }
}
