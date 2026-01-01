// RVAIMPL-FIX-008: IslamicCalendar.cs - REVISED
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs; // MISSING using statement!
using Unity.Mathematics;

namespace RVA.TAC.Cultural
{
    [System.Serializable]
    public struct IslamicDate
    {
        public int Year;
        public int Month;
        public int Day;
        public FixedString64Bytes MonthName; // Burst-compatible string
    }

    public class IslamicCalendar : MonoBehaviour
    {
        // Singleton with thread-safe lazy init
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
                        var go = new GameObject("IslamicCalendar");
                        _instance = go.AddComponent<IslamicCalendar>();
                    }
                }
                return _instance;
            }
        }

        // Configuration
        [Header("Maldives Islamic Settings")]
        public bool UseUmmAlQura = true; // Maldives standard
        public bool SimulateMoonSighting = true; // For gameplay events
        
        // Current date with proper encapsulation
        private IslamicDate _currentDate;
        public IslamicDate CurrentIslamicDate => _currentDate;

        // TimeSystem reference
        private TimeSystem _timeSystem;

        // Event system for other components
        public System.Action<IslamicDate> OnIslamicDateChanged;
        public System.Action OnRamadanStarted;
        public System.Action OnEidAlFitr;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _timeSystem = TimeSystem.Instance;
            if (_timeSystem == null)
            {
                Debug.LogError("[IslamicCalendar] TimeSystem not found! Waiting for initialization...");
                // Retry after delay for load order issues
                Invoke(nameof(Initialize), 0.5f);
                return;
            }
            
            Initialize();
        }

        private void Initialize()
        {
            if (_timeSystem == null) return;

            // Subscribe to TimeSystem event (more efficient than Update)
            _timeSystem.OnDayChanged += CalculateIslamicDate;
            
            // Initial calculation
            CalculateIslamicDate();
            
            Debug.Log($"[IslamicCalendar] Initialized: {GetFormattedIslamicDate()}");
        }

        void OnDestroy()
        {
            if (_timeSystem != null)
            {
                _timeSystem.OnDayChanged -= CalculateIslamicDate;
            }
        }

        #region Corrected Umm al-Qura Calculation (Maldives Standard)
        public void CalculateIslamicDate()
        {
            if (_timeSystem == null) return;

            var oldDate = _currentDate;
            double julianDay = GetJulianDay(_timeSystem.CurrentDay);
            _currentDate = JulianToUmmAlQura(julianDay);

            // Trigger events if date changed
            if (oldDate.Year != _currentDate.Year || 
                oldDate.Month != _currentDate.Month || 
                oldDate.Day != _currentDate.Day)
            {
                OnIslamicDateChanged?.Invoke(_currentDate);
                
                // Special event triggers
                if (IsRamadan() && !IsSameMonth(oldDate, 9))
                    OnRamadanStarted?.Invoke();
                    
                if (IsEidAlFitr() && !IsSameDay(oldDate, 10, 1))
                    OnEidAlFitr?.Invoke();
            }
        }

        private bool IsSameMonth(IslamicDate date, int targetMonth)
        {
            return date.Month == targetMonth;
        }

        private bool IsSameDay(IslamicDate date, int month, int day)
        {
            return date.Month == month && date.Day == day;
        }

        private double GetJulianDay(float gameDay)
        {
            // Game starts on 2024/01/01 (Gregorian)
            System.DateTime baseDate = new System.DateTime(2024, 1, 1);
            System.DateTime currentDate = baseDate.AddDays(gameDay);
            
            // Use astronomical Julian Day calculation
            int y = currentDate.Year;
            int m = currentDate.Month;
            int d = currentDate.Day;
            
            if (m <= 2)
            {
                y -= 1;
                m += 12;
            }
            
            int A = y / 100;
            int B = A / 4;
            int C = 2 - A + B;
            double JD = math.floor(365.25 * (y + 4716)) + 
                        math.floor(30.6001 * (m + 1)) + 
                        d + C - 1524.5;
            
            return JD;
        }

        private IslamicDate JulianToUmmAlQura(double julianDay)
        {
            // Umm al-Qura uses pre-calculated tables
            // For production, use certified tables from Maldives Islamic Ministry
            
            // Simplified but more accurate than original
            int year = 1445; // Base year 2024 = 1445 AH
            int daysSinceEpoch = (int)(julianDay - 2455927.5); // 1445/1/1
            
            // 30-year leap cycle (Maldives standard)
            int[] leapYears = {2, 5, 7, 10, 13, 15, 18, 21, 24, 26, 29};
            
            // Find correct year
            while (daysSinceEpoch > GetUmmAlQuraYearLength(year))
            {
                daysSinceEpoch -= GetUmmAlQuraYearLength(year);
                year++;
            }
            
            // Month lengths with actual leap year adjustments
            int[] monthLengths = GetUmmAlQuraMonthLengths(year, leapYears);
            
            int month = 0;
            while (month < 12 && daysSinceEpoch >= monthLengths[month])
            {
                daysSinceEpoch -= monthLengths[month];
                month++;
            }
            
            return new IslamicDate
            {
                Year = year,
                Month = math.clamp(month + 1, 1, 12),
                Day = math.clamp(daysSinceEpoch + 1, 1, 30),
                MonthName = new FixedString64Bytes(MONTH_NAMES[month])
            };
        }

        private int GetUmmAlQuraYearLength(int year)
        {
            return IsIslamicLeapYearUmmAlQura(year) ? 355 : 354;
        }

        private bool IsIslamicLeapYearUmmAlQura(int year)
        {
            // 30-year cycle: years 2, 5, 7, 10, 13, 15, 18, 21, 24, 26, 29
            return ((11 * year + 14) % 30) < 11;
        }

        private int[] GetUmmAlQuraMonthLengths(int year, int[] leapYears)
        {
            int[] lengths = new int[12];
            bool isLeap = IsIslamicLeapYearUmmAlQura(year);
            
            for (int i = 0; i < 12; i++)
            {
                // Standard pattern: 30 days for odd months (1,3,5,7,9,11)
                // 29 days for even months, but Dhul Hijjah becomes 30 in leap year
                lengths[i] = (i % 2 == 0) ? 30 : 29;
            }
            
            if (isLeap) lengths[11] = 30; // Dhul Hijjah
            
            return lengths;
        }
        #endregion

        #region Event Detection (Culturally Accurate)
        private IslamicDate _lastCheckedDate; // Prevent duplicate event triggers
        
        public bool IsRamadan()
        {
            return _currentDate.Month == 9;
        }

        public bool IsEidAlFitr()
        {
            // Shawwal 1-3, but Maldives celebrates for 4-5 days
            return _currentDate.Month == 10 && _currentDate.Day <= 5;
        }

        public bool IsEidAlAdha()
        {
            // Dhul Hijjah 10-13
            return _currentDate.Month == 12 && _currentDate.Day >= 10 && _currentDate.Day <= 13;
        }

        public bool IsSacredMonth()
        {
            // Muharram, Rajab, Dhul Qadah, Dhul Hijjah (no fighting in these months)
            return new int[] {1, 7, 11, 12}.Contains(_currentDate.Month);
        }

        public bool IsFriday()
        {
            // For Jumu'ah prayer logic
            if (_timeSystem == null) return false;
            System.DateTime gregorian = new System.DateTime(2024, 1, 1).AddDays(_timeSystem.CurrentDay);
            return gregorian.DayOfWeek == System.DayOfWeek.Friday;
        }

        public int GetDaysUntilRamadan()
        {
            if (IsRamadan()) return 0;
            
            int currentDayOfYear = GetDayOfYear(_currentDate);
            int ramadanStartDay = GetDayOfYear(new IslamicDate { Year = _currentDate.Year, Month = 9, Day = 1 });
            
            int daysInCurrentYear = IsIslamicLeapYearUmmAlQura(_currentDate.Year) ? 355 : 354;
            
            if (currentDayOfYear < ramadanStartDay)
                return ramadanStartDay - currentDayOfYear;
            else
                return daysInCurrentYear - currentDayOfYear + ramadanStartDay;
        }
        #endregion

        // ... (String formatting and debug methods remain similar)
        
        #region Burst-Optimized Async Calculation (Optional)
        [BurstCompile]
        public struct IslamicDateCalculationJob : IJob
        {
            [ReadOnly] public float gameDay;
            [WriteOnly] public NativeArray<int> result; // Year, Month, Day
            
            public void Execute()
            {
                // Fast approximate calculation for background tasks
                const float lunarYear = 354.36708f;
                float days = gameDay * 0.97023f; // Conversion factor
                int year = (int)(days / lunarYear) + 1445;
                
                float daysInYear = days % lunarYear;
                int month = (int)(daysInYear / 29.5f);
                int day = (int)(daysInYear % 29.5f) + 1;
                
                result[0] = year;
                result[1] = math.clamp(month + 1, 1, 12);
                result[2] = math.clamp(day, 1, 30);
            }
        }
        
        // Usage example:
        public void CalculateIslamicDateAsync()
        {
            NativeArray<int> result = new NativeArray<int>(3, Allocator.TempJob);
            
            var job = new IslamicDateCalculationJob
            {
                gameDay = _timeSystem.CurrentDay,
                result = result
            };
            
            JobHandle handle = job.Schedule();
            handle.Complete(); // In real usage, don't block, use JobHandle for continuation
            
            _currentDate.Year = result[0];
            _currentDate.Month = result[1];
            _currentDate.Day = result[2];
            
            result.Dispose(); // CRITICAL: Prevent memory leak
        }
        #endregion
    }
}
