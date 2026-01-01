// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// Batch 8: Cultural Systems - RVACONT-008
// IslamicCalendar.cs - Hijri calendar integration for cultural events

using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace RVA.TAC.Cultural
{
    [System.Serializable]
    public struct IslamicDate
    {
        public int Year;
        public int Month;      // 1-12
        public int Day;        // 1-30 (varies)
        public string MonthName;
    }

    [BurstCompile]
    public class IslamicCalendar : MonoBehaviour
    {
        #region Singleton & References
        public static IslamicCalendar Instance { get; private set; }
        private TimeSystem timeSystem;
        
        [Header("Current Islamic Date")]
        public IslamicDate CurrentIslamicDate;
        
        [Header("Configuration")]
        public bool Use AstronomicalCalculation = true;
        public float NewMoonThreshold = 0.5f; // Visibility threshold
        #endregion

        #region Islamic Calendar Constants
        // Average lunar month = 29.53059 days
        private const float LUNAR_MONTH_DAYS = 29.53059f;
        private const float LUNAR_YEAR_DAYS = 354.36708f; // 12 lunar months
        
        // Epoch: July 16, 622 CE (Julian) = Days since 1/1/1
        private const double ISLAMIC_EPOCH = 1948440.5;
        
        private static readonly string[] MONTH_NAMES = {
            "Muharram", "Safar", "Rabi al-Awwal", "Rabi al-Thani",
            "Jumada al-Awwal", "Jumada al-Thani", "Rajab", "Shaban",
            "Ramadan", "Shawwal", "Dhul Qadah", "Dhul Hijjah"
        };
        
        private static readonly int[] MONTH_DAYS = {
            30, 29, 30, 29, 30, 29, 30, 29, 30, 29, 30, 29  // Base pattern (30/29 alternating)
        };
        #endregion

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            timeSystem = TimeSystem.Instance;
            if (timeSystem == null)
            {
                Debug.LogError("[IslamicCalendar] TimeSystem not found!");
                return;
            }

            CalculateIslamicDate();
        }

        void Update()
        {
            // Update daily
            if (timeSystem != null && math.floor(timeSystem.CurrentDay) != math.floor(timeSystem.CurrentDay - Time.deltaTime / (24f * 3600f)))
            {
                CalculateIslamicDate();
            }
        }

        #region Islamic Date Calculation
        public void CalculateIslamicDate()
        {
            if (timeSystem == null) return;
            
            double julianDay = GetJulianDay(timeSystem.CurrentDay);
            CurrentIslamicDate = JulianToIslamic(julianDay);
        }

        private double GetJulianDay(float gameDay)
        {
            // Base date: Start from 2024/01/01 as game start
            System.DateTime baseDate = new System.DateTime(2024, 1, 1);
            System.DateTime currentDate = baseDate.AddDays(gameDay);
            
            return DateToJulianDay(currentDate.Year, currentDate.Month, currentDate.Day);
        }

        private double DateToJulianDay(int year, int month, int day)
        {
            if (month <= 2)
            {
                year -= 1;
                month += 12;
            }
            
            int A = year / 100;
            int B = 2 - A + (A / 4);
            
            return math.floor(365.25 * (year + 4716)) + math.floor(30.6001 * (month + 1)) + 
                   day + B - 1524.5;
        }

        private IslamicDate JulianToIslamic(double julianDay)
        {
            double islamicDay = julianDay - ISLAMIC_EPOCH;
            int estimatedYear = (int)(islamicDay / LUNAR_YEAR_DAYS);
            
            // Search forward for correct year
            int year = estimatedYear - 2;
            double yearStart = GetIslamicYearStart(year);
            
            while (yearStart > islamicDay && year > 0)
            {
                year--;
                yearStart = GetIslamicYearStart(year);
            }
            
            while (GetIslamicYearStart(year + 1) <= islamicDay)
            {
                year++;
            }
            
            // Find month and day
            double daysIntoYear = islamicDay - GetIslamicYearStart(year);
            int[] monthLengths = GetIslamicMonthLengths(year);
            
            int month = 0;
            while (month < 12 && daysIntoYear >= monthLengths[month])
            {
                daysIntoYear -= monthLengths[month];
                month++;
            }
            
            return new IslamicDate
            {
                Year = year,
                Month = month + 1, // 1-based
                Day = (int)daysIntoYear + 1,
                MonthName = month < MONTH_NAMES.Length ? MONTH_NAMES[month] : "Unknown"
            };
        }

        private double GetIslamicYearStart(int year)
        {
            // Simplified astronomical calculation for mobile performance
            return year * LUNAR_YEAR_DAYS + year / 30f; // Include leap year cycles
        }

        private int[] GetIslamicMonthLengths(int year)
        {
            int[] lengths = (int[])MONTH_DAYS.Clone();
            
            // Adjust for leap years (Hijri calendar has 11 leap years in 30-year cycle)
            if (IsIslamicLeapYear(year))
            {
                lengths[11] = 30; // Dhul Hijjah becomes 30 days in leap year
            }
            
            // Astronomical adjustment for month visibility
            if (UseAstronomicalCalculation)
            {
                AdjustForMoonVisibility(year, lengths);
            }
            
            return lengths;
        }

        private bool IsIslamicLeapYear(int year)
        {
            // 30-year cycle: years 2, 5, 7, 10, 13, 15, 18, 21, 24, 26, 29 are leap
            return ((11 * year + 14) % 30) < 11;
        }

        private void AdjustForMoonVisibility(int year, int[] lengths)
        {
            // Based on lunar conjunction and visibility criteria
            float lunarConjunctionOffset = (year * 11.01f) % 1f; // Approximate conjunction cycle
            
            if (lunarConjunctionOffset > NewMoonThreshold)
            {
                // Month might be 30 days if moon is visible
                for (int i = 0; i < 12; i++)
                {
                    if (lengths[i] == 29 && lunarConjunctionOffset > (0.5f + i * 0.04f))
                    {
                        lengths[i] = 30;
                        break;
                    }
                }
            }
        }
        #endregion

        #region Special Islamic Events
        public bool IsRamadan()
        {
            return CurrentIslamicDate.Month == 9; // Ramadan is 9th month
        }

        public bool IsEidAlFitr()
        {
            return CurrentIslamicDate.Month == 10 && CurrentIslamicDate.Day <= 3; // Shawwal first 3 days
        }

        public bool IsEidAlAdha()
        {
            return CurrentIslamicDate.Month == 12 && CurrentIslamicDate.Day >= 10 && CurrentIslamicDate.Day <= 13;
        }

        public bool IsSacredMonth()
        {
            // Muharram, Rajab, Dhul Qadah, Dhul Hijjah
            return CurrentIslamicDate.Month == 1 || CurrentIslamicDate.Month == 7 || 
                   CurrentIslamicDate.Month == 11 || CurrentIslamicDate.Month == 12;
        }

        public int GetDaysUntilRamadan()
        {
            if (IsRamadan()) return 0;
            
            int daysInYear = IsIslamicLeapYear(CurrentIslamicDate.Year) ? 355 : 354;
            int currentDayOfYear = GetDayOfYear(CurrentIslamicDate);
            int ramadanStart = GetDayOfYear(new IslamicDate { Year = CurrentIslamicDate.Year, Month = 9, Day = 1 });
            
            if (currentDayOfYear < ramadanStart)
            {
                return ramadanStart - currentDayOfYear;
            }
            else
            {
                return daysInYear - currentDayOfYear + ramadanStart;
            }
        }

        private int GetDayOfYear(IslamicDate date)
        {
            int day = 0;
            int[] lengths = GetIslamicMonthLengths(date.Year);
            for (int i = 0; i < date.Month - 1; i++)
            {
                day += lengths[i];
            }
            return day + date.Day;
        }
        #endregion

        #region String Formatting
        public string GetFormattedIslamicDate()
        {
            return $"{CurrentIslamicDate.Day} {CurrentIslamicDate.MonthName} {CurrentIslamicDate.Year} AH";
        }

        public string GetIslamicDateShort()
        {
            return $"{CurrentIslamicDate.Day}/{CurrentIslamicDate.Month}/{CurrentIslamicDate.Year}";
        }
        #endregion

        #region Debug & Validation
        [ContextMenu("Test Islamic Calendar")]
        public void TestCalendar()
        {
            CalculateIslamicDate();
            Debug.Log($"[IslamicCalendar] Current Date: {GetFormattedIslamicDate()}");
            Debug.Log($"[IslamicCalendar] Ramadan: {IsRamadan()}, Eid Al-Fitr: {IsEidAlFitr()}");
            Debug.Log($"[IslamicCalendar] Days until Ramadan: {GetDaysUntilRamadan()}");
        }
        #endregion
    }

    #region Burst-Optimized Date Calculation
    [BurstCompile]
    public struct IslamicDateCalculationJob : IJob
    {
        [ReadOnly] public float gameDay;
        [WriteOnly] public NativeArray<int> result; // Year, Month, Day

        public void Execute()
        {
            // Simplified calculation for burst compilation
            const float lunarMonth = 29.53059f;
            const float lunarYear = 354.36708f;
            
            float totalIslamicDays = gameDay * 0.97f; // Approximate conversion
            int year = (int)(totalIslamicDays / lunarYear) + 1445; // Base year 1445 AH
            
            float daysInYear = totalIslamicDays % lunarYear;
            int month = 0;
            float accumulatedDays = 0;
            
            while (month < 12 && accumulatedDays <= daysInYear)
            {
                float monthLength = (month % 2 == 0) ? 30f : 29f;
                accumulatedDays += monthLength;
                month++;
            }
            
            int day = (int)(daysInYear - (accumulatedDays - ((month % 2 == 0) ? 30f : 29f))) + 1;
            
            result[0] = year;
            result[1] = math.max(1, math.min(12, month));
            result[2] = math.max(1, math.min(30, day));
        }
    }
    #endregion
}
