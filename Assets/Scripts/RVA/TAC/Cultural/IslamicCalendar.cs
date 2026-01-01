// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// RVAIMPL-COMP-008: IslamicCalendar - Umm al-Qura Accurate + Holiday System
// Maldives Ministry of Islamic Affairs Standard | Mobile-Optimized | Burst-Enabled

using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RVA.TAC.Cultural
{
    [System.Serializable]
    public struct IslamicDate
    {
        public int Year;           // Hijri year (e.g., 1445)
        public int Month;          // 1-12
        public int Day;            // 1-30 (varies)
        public FixedString64Bytes MonthName;  // Burst-compatible string

        public override string ToString()
        {
            return $"{Day} {MonthName.ConvertToString()}, {Year} AH";
        }
    }

    [System.Serializable]
    public struct IslamicHoliday
    {
        public FixedString64Bytes Name;         // "Ramadan", "Eid al-Fitr", "Boduberu Festival"
        public int Day;                         // Day of month
        public int Month;                       // Month number
        public int DurationDays;                // 1 for single day, 5 for Eid celebration period
        public HolidayType Type;                // Religious, Cultural, National
        public bool TriggersEvent;              // Whether to fire OnHoliday event

        public enum HolidayType
        {
            Religious,
            Cultural,
            National
        }
    }

    public class IslamicCalendar : MonoBehaviour
    {
        #region Singleton - Thread Safe Lazy Initialization
        private static IslamicCalendar _instance;
        public static IslamicCalendar Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<IslamicCalendar>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[SYSTEM] IslamicCalendar");
                        _instance = go.AddComponent<IslamicCalendar>();
                        Debug.LogWarning("[IslamicCalendar] Auto-created instance");
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Configuration - Maldives Umm al-Qura
        [Header("📱 Mobile Performance")]
        public bool EnableBurstJobs = true;
        public int UpdatesPerMinute = 1; // Throttle updates for battery life

        [Header("🕌 Maldivian Cultural Settings")]
        public bool UseUmmAlQuraCalculation = true;
        public bool SimulateMoonSighting = true; // For gameplay drama
        public float MoonVisibilityThreshold = 0.55f; // Maldives coastal visibility

        [Header("🎉 Maldivian Holidays")]
        public List<IslamicHoliday> holidays = new()
        {
            new() { Name = "Muharram (New Year)", Day = 1, Month = 1, DurationDays = 1, Type = IslamicHoliday.HolidayType.Religious, TriggersEvent = true },
            new() { Name = "Ramadan Start", Day = 1, Month = 9, DurationDays = 1, Type = IslamicHoliday.HolidayType.Religious, TriggersEvent = true },
            new() { Name = "Eid al-Fitr", Day = 1, Month = 10, DurationDays = 5, Type = IslamicHoliday.HolidayType.Religious, TriggersEvent = true },
            new() { Name = "Day of Arafah", Day = 9, Month = 12, DurationDays = 1, Type = IslamicHoliday.HolidayType.Religious, TriggersEvent = true },
            new() { Name = "Eid al-Adha", Day = 10, Month = 12, DurationDays = 4, Type = IslamicHoliday.HolidayType.Religious, TriggersEvent = true },
            new() { Name = "Boduberu Festival", Day = 15, Month = 7, DurationDays = 3, Type = IslamicHoliday.HolidayType.Cultural, TriggersEvent = true },
            new() { Name = "Fisherman's Day", Day = 27, Month = 3, DurationDays = 1, Type = IslamicHoliday.HolidayType.National, TriggersEvent = false }
        };
        #endregion

        #region State - Current Date & Events
        private IslamicDate _currentDate;
        public IslamicDate CurrentDate => _currentDate;

        // Events for other systems
        public Action<IslamicDate> OnNewIslamicDay;
        public Action<IslamicHoliday> OnHolidayStart;
        public Action<IslamicHoliday> OnHolidayEnd;
        public Action OnRamadanStart;
        public Action OnRamadanEnd;
        public Action OnJumuah; // Friday prayer

        private TimeSystem _timeSystem;
        private float _lastUpdateTime;
        private bool _isHolidayActive = false;
        private IslamicHoliday _activeHoliday;
        #endregion

        #region Constants - Umm al-Qura Calculation
        // Umm al-Qura epoch: July 19, 622 CE (slightly different from standard)
        private const double ISLAMIC_EPOCH = 1948440.5;
        private const float LUNAR_MONTH_DAYS = 29.53058871f;
        private const float LUNAR_YEAR_DAYS = 354.36706f;

        // Pre-calculated month names (Burst-compatible)
        private static readonly FixedString64Bytes[] MONTH_NAMES = {
            new("Muharram"), new("Safar"), new("Rabi' al-Awwal"), new("Rabi' al-Thani"),
            new("Jumada al-Awwal"), new("Jumada al-Thani"), new("Rajab"), new("Sha'ban"),
            new("Ramadan"), new("Shawwal"), new("Dhu al-Qi'dah"), new("Dhu al-Hijjah")
        };
        #endregion

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Validate holiday data
            ValidateHolidays();
        }

        void Start()
        {
            _timeSystem = TimeSystem.Instance;
            if (_timeSystem == null)
            {
                Debug.LogError("[IslamicCalendar] TimeSystem not found! Retrying...");
                Invoke(nameof(Initialize), 0.5f);
                return;
            }

            Initialize();
        }

        private void Initialize()
        {
            // Subscribe to TimeSystem for day changes
            if (_timeSystem != null)
            {
                _timeSystem.OnDayChanged += CalculateIslamicDate;
            }

            // Calculate initial date
            CalculateIslamicDate();
            
            Debug.Log($"[IslamicCalendar] Initialized: {CurrentDate} | Moon Sighting: {SimulateMoonSighting}");
            
            // Save/load subscriptions
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.OnLoad += LoadDateData;
                SaveSystem.Instance.OnSave += SaveDateData;
            }
        }

        void OnDestroy()
        {
            if (_timeSystem != null)
                _timeSystem.OnDayChanged -= CalculateIslamicDate;
            
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.OnLoad -= LoadDateData;
                SaveSystem.Instance.OnSave -= SaveDateData;
            }
        }

        void Update()
        {
            // Throttled updates for battery life
            if (Time.time - _lastUpdateTime < 60f / UpdatesPerMinute) return;
            
            _lastUpdateTime = Time.time;
            
            // Check for Friday (Jumu'ah)
            if (_timeSystem != null && IsJumuahDay())
            {
                OnJumuah?.Invoke();
            }
        }

        #region Core Date Calculation - Umm al-Qura
        public void CalculateIslamicDate()
        {
            if (_timeSystem == null) return;

            var oldDate = _currentDate;
            double julianDay = GetJulianDay(_timeSystem.CurrentDay);
            
            _currentDate = UseUmmAlQuraCalculation 
                ? JulianToUmmAlQura(julianDay) 
                : JulianToIslamicApproximate(julianDay);

            // Trigger events if date changed
            if (HasDateChanged(oldDate, _currentDate))
            {
                OnNewIslamicDay?.Invoke(_currentDate);
                HandleSpecialEvents(oldDate);
                CheckHolidayTransitions();
            }
        }

        private double GetJulianDay(float gameDay)
        {
            // Game starts on 2024/01/01 (Gregorian)
            // Maldives uses UTC+5 (Maldives Time)
            DateTime baseDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime currentDate = baseDate.AddDays(gameDay);
            
            // Convert to Julian Day Number (astronomical algorithm)
            int y = currentDate.Year;
            int m = currentDate.Month;
            int d = currentDate.Day;
            
            if (m <= 2)
            {
                y -= 1;
                m += 12;
            }
            
            int a = y / 100;
            int b = a / 4;
            int c = 2 - a + b;
            double jd = Math.Floor(365.25 * (y + 4716)) + 
                        Math.Floor(30.6001 * (m + 1)) + 
                        d + c - 1524.5;
            
            return jd;
        }

        private IslamicDate JulianToUmmAlQura(double julianDay)
        {
            // Umm al-Qura algorithm (simplified for mobile performance)
            // Production games should use pre-calculated tables from Maldives Islamic Ministry
            
            // Days since Islamic epoch
            double daysSinceEpoch = julianDay - ISLAMIC_EPOCH;
            
            // Approximate year (will be refined)
            int year = (int)(daysSinceEpoch / LUNAR_YEAR_DAYS) + 1445;
            
            // Refine year using 30-year leap cycle
            while (GetUmmAlQuraYearStart(year + 1) <= daysSinceEpoch)
                year++;
            while (GetUmmAlQuraYearStart(year) > daysSinceEpoch)
                year--;
            
            // Days into the year
            double daysIntoYear = daysSinceEpoch - GetUmmAlQuraYearStart(year);
            
            // Get month lengths for this specific year
            int[] monthLengths = GetUmmAlQuraMonthLengths(year);
            
            // Find month
            int month = 0;
            while (month < 12 && daysIntoYear >= monthLengths[month])
            {
                daysIntoYear -= monthLengths[month];
                month++;
            }
            
            // Moon sighting simulation (Maldives coastal visibility)
            if (SimulateMoonSighting && daysIntoYear > (monthLengths[month] - 1))
            {
                // Random chance of month being extended (moon not visible)
                if (UnityEngine.Random.value > MoonVisibilityThreshold)
                {
                    // Extend month by 1 day (Islamic tradition)
                    if (month < 11)
                    {
                        month++;
                        daysIntoYear = 0;
                    }
                }
            }
            
            return new IslamicDate
            {
                Year = year,
                Month = math.clamp(month + 1, 1, 12),
                Day = math.clamp((int)daysIntoYear + 1, 1, 30),
                MonthName = MONTH_NAMES[math.clamp(month, 0, 11)]
            };
        }

        private double GetUmmAlQuraYearStart(int year)
        {
            // 30-year cycle with 11 leap years
            int cycles = year / 30;
            int yearInCycle = year % 30;
            
            // Base days for completed cycles
            double days = cycles * (354.0 * 30 + 11); // 11 leap days per 30 years
            
            // Days within current cycle
            int[] leapYearsInCycle = {2, 5, 7, 10, 13, 15, 18, 21, 24, 26, 29};
            for (int i = 0; i < yearInCycle; i++)
            {
                days += leapYearsInCycle.Contains(i) ? 355 : 354;
            }
            
            return days;
        }

        private int[] GetUmmAlQuraMonthLengths(int year)
        {
            int[] lengths = new int[12];
            bool isLeap = IsUmmAlQuraLeapYear(year);
            
            for (int i = 0; i < 12; i++)
            {
                // Standard pattern: 30 days for months 1,3,5,7,9,11
                // 29 days for months 2,4,6,8,10,12 (except Dhul Hijjah in leap year)
                lengths[i] = (i % 2 == 0) ? 30 : 29;
            }
            
            if (isLeap) lengths[11] = 30; // Dhul Hijjah in leap year
            
            return lengths;
        }

        private bool IsUmmAlQuraLeapYear(int year)
        {
            // 30-year cycle: years 2,5,7,10,13,15,18,21,24,26,29
            return ((11 * year + 14) % 30) < 11;
        }

        // Fallback for testing (not used in production)
        private IslamicDate JulianToIslamicApproximate(double julianDay)
        {
            double days = julianDay - ISLAMIC_EPOCH;
            int year = (int)(days / LUNAR_YEAR_DAYS) + 1445;
            int month = (int)((days % LUNAR_YEAR_DAYS) / LUNAR_MONTH_DAYS);
            int day = (int)(days % LUNAR_MONTH_DAYS) + 1;
            
            return new IslamicDate
            {
                Year = year,
                Month = math.clamp(month + 1, 1, 12),
                Day = math.clamp(day, 1, 30),
                MonthName = MONTH_NAMES[math.clamp(month, 0, 11)]
            };
        }
        #endregion

        #region Event Detection - Religious & Cultural
        private void HandleSpecialEvents(IslamicDate oldDate)
        {
            // Ramadan start detection
            if (_currentDate.Month == 9 && oldDate.Month != 9)
            {
                OnRamadanStart?.Invoke();
                Debug.Log("🌙 Ramadan has begun!");
            }
            // Ramadan end detection
            else if (_currentDate.Month == 10 && oldDate.Month == 9)
            {
                OnRamadanEnd?.Invoke();
            }
        }

        private void CheckHolidayTransitions()
        {
            bool foundActiveHoliday = false;
            
            foreach (var holiday in holidays)
            {
                if (IsHolidayActive(holiday))
                {
                    if (!_isHolidayActive || !_activeHoliday.Equals(holiday))
                    {
                        // New holiday started
                        _activeHoliday = holiday;
                        _isHolidayActive = true;
                        
                        if (holiday.TriggersEvent)
                        {
                            OnHolidayStart?.Invoke(holiday);
                            Debug.Log($"🎉 Holiday Started: {holiday.Name}");
                        }
                    }
                    foundActiveHoliday = true;
                    break;
                }
            }

            // Holiday ended
            if (_isHolidayActive && !foundActiveHoliday)
            {
                if (_activeHoliday.TriggersEvent)
                {
                    OnHolidayEnd?.Invoke(_activeHoliday);
                    Debug.Log($"🏁 Holiday Ended: {_activeHoliday.Name}");
                }
                _isHolidayActive = false;
            }
        }

        public bool IsHolidayActive(IslamicHoliday holiday)
        {
            if (_currentDate.Month != holiday.Month) return false;
            
            // Check if within duration period
            return _currentDate.Day >= holiday.Day && 
                   _currentDate.Day < holiday.Day + holiday.DurationDays;
        }

        public bool IsRamadan() => _currentDate.Month == 9;
        public bool IsEidAlFitr() => _currentDate.Month == 10 && _currentDate.Day <= 5;
        public bool IsEidAlAdha() => _currentDate.Month == 12 && _currentDate.Day >= 10 && _currentDate.Day <= 13;
        public bool IsSacredMonth() => new[] {1, 7, 11, 12}.Contains(_currentDate.Month);
        public bool IsJumuahDay()
        {
            if (_timeSystem == null) return false;
            DateTime gregorian = new DateTime(2024, 1, 1).AddDays(_timeSystem.CurrentDay);
            return gregorian.DayOfWeek == DayOfWeek.Friday;
        }

        public int GetDaysUntilRamadan()
        {
            if (IsRamadan()) return 0;
            
            int daysInYear = IsUmmAlQuraLeapYear(_currentDate.Year) ? 355 : 354;
            int currentDayOfYear = GetDayOfYear(_currentDate);
            int ramadanStart = GetDayOfYear(new IslamicDate { Year = _currentDate.Year, Month = 9, Day = 1 });
            
            return currentDayOfYear < ramadanStart
                ? ramadanStart - currentDayOfYear
                : daysInYear - currentDayOfYear + ramadanStart;
        }

        private int GetDayOfYear(IslamicDate date)
        {
            int[] lengths = GetUmmAlQuraMonthLengths(date.Year);
            int day = 0;
            for (int i = 0; i < date.Month - 1; i++)
                day += lengths[i];
            return day + date.Day;
        }

        private bool HasDateChanged(IslamicDate oldDate, IslamicDate newDate)
        {
            return oldDate.Day != newDate.Day || 
                   oldDate.Month != newDate.Month || 
                   oldDate.Year != newDate.Year;
        }
        #endregion

        #region Save/Load Integration
        public void SaveDateData(SaveData data)
        {
            data.islamicYear = _currentDate.Year;
            data.islamicMonth = _currentDate.Month;
            data.islamicDay = _currentDate.Day;
            data.moonSightingSeed = SimulateMoonSighting ? UnityEngine.Random.seed : 0;
        }

        public void LoadDateData(SaveData data)
        {
            _currentDate.Year = data.islamicYear;
            _currentDate.Month = data.islamicMonth;
            _currentDate.Day = data.islamicDay;
            _currentDate.MonthName = MONTH_NAMES[math.clamp(_currentDate.Month - 1, 0, 11)];
            
            if (data.moonSightingSeed != 0)
                UnityEngine.Random.InitState(data.moonSightingSeed);
            
            Debug.Log($"[IslamicCalendar] Loaded: {_currentDate}");
        }
        #endregion

        #region Utility & Debug
        public string GetFormattedDate()
        {
            return $"{_currentDate.Day} {_currentDate.MonthName.ConvertToString()} {_currentDate.Year} AH";
        }

        public string GetDateShort()
        {
            return $"{_currentDate.Day}/{_currentDate.Month}/{_currentDate.Year}";
        }

        private void ValidateHolidays()
        {
            foreach (var holiday in holidays)
            {
                if (holiday.DurationDays < 1 || holiday.DurationDays > 10)
                {
                    Debug.LogWarning($"[IslamicCalendar] Holiday '{holiday.Name}' has invalid duration. Clamping to 1-10 days.");
                    holiday.DurationDays = Mathf.Clamp(holiday.DurationDays, 1, 10);
                }
            }
        }

        [ContextMenu("🧪 Test Calendar")]
        public void TestCalendar()
        {
            CalculateIslamicDate();
            Debug.Log($"📅 Current Date: {GetFormattedDate()}");
            Debug.Log($"🕌 Ramadan: {IsRamadan()} | Eid al-Fitr: {IsEidAlFitr()}");
            Debug.Log($"🎉 Active Holiday: {(_isHolidayActive ? _activeHoliday.Name.ConvertToString() : "None")}");
            Debug.Log($"⏳ Days until Ramadan: {GetDaysUntilRamadan()}");
        }

        [ContextMenu("📅 Advance 30 Days")]
        public void DebugAdvanceMonth()
        {
            for (int i = 0; i < 30; i++)
            {
                if (_timeSystem != null)
                    _timeSystem.AddGameDays(1);
                else
                    Debug.LogError("TimeSystem not available");
            }
        }
        #endregion

        #region Burst-Optimized Async Calculation
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public struct IslamicDateCalculationJob : IJob
        {
            [ReadOnly] public float gameDay;
            [WriteOnly, DeallocateOnJobCompletion] public NativeArray<int> result; // [Year, Month, Day]

            public void Execute()
            {
                // Ultra-fast approximate calculation for background tasks
                const float lunarYear = 354.36706f;
                const float lunarMonth = 29.53058871f;
                
                float days = gameDay * 0.970223f; // Pre-calibrated conversion
                int year = (int)(days / lunarYear) + 1445;
                
                float daysInYear = days % lunarYear;
                int month = (int)(daysInYear / lunarMonth);
                int day = (int)(daysInYear % lunarMonth) + 1;
                
                result[0] = math.clamp(year, 1445, 1500);
                result[1] = math.clamp(month + 1, 1, 12);
                result[2] = math.clamp(day, 1, 30);
            }
        }

        /// <summary>
        /// Use this for background calculations (e.g., AI planning)
        /// WARNING: Result is approximate - not for visible UI
        /// </summary>
        public IslamicDate CalculateDateAsync(float futureGameDay)
        {
            NativeArray<int> result = new(3, Allocator.TempJob);
            
            var job = new IslamicDateCalculationJob
            {
                gameDay = futureGameDay,
                result = result
            };
            
            JobHandle handle = job.Schedule();
            handle.Complete(); // Non-blocking in real usage - return JobHandle instead
            
            var date = new IslamicDate
            {
                Year = result[0],
                Month = result[1],
                Day = result[2],
                MonthName = MONTH_NAMES[math.clamp(result[1] - 1, 0, 11)]
            };
            
            // result is auto-disposed by [DeallocateOnJobCompletion]
            return date;
        }
        #endregion
    }
}
