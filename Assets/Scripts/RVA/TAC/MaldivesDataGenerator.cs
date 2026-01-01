using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVA.TAC {
    public static class MaldivesDataGenerator {
        private static System.Random rng = new System.Random(42); // Deterministic seed
        
        public class WorldData {
            public Island[] islands;
            public Gang[] gangs;
            public BuildingType[] buildings;
            public VehicleType[] vehicles;
            public FloraType[] flora;
        }
        
        [Serializable]
        public class Island {
            public string id;
            public string nameEn;
            public string nameDhivehi;
            public Vector2 position;
            public int size; // 1=small, 5=large
            public bool isResort;
            public string dominantGangId;
            public string[] buildingIds;
            public float prayerTimeOffset; // Minutes from Male time
        }
        
        [Serializable]
        public class Gang {
            public int id;
            public string nameEn;
            public string nameDhivehi;
            public string territoryIslandId;
            public int reputation;
            public GangType type;
            public Color color;
        }
        
        public enum GangType { Fishermen, Political, Criminal, Environmental, Religious, Youth }
        
        [Serializable]
        public class BuildingType {
            public string id;
            public string nameEn;
            public string nameDhivehi;
            public BuildingCategory category;
            public int cost;
            public bool isEnterable;
        }
        
        public enum BuildingCategory { Mosque, House, Shop, Resort, Government, FishingHut }
        
        [Serializable]
        public class VehicleType {
            public string id;
            public string nameEn;
            public VehicleCategory category;
            public float speed;
            public int capacity;
        }
        
        public enum VehicleCategory { Dhoni, Speedboat, Bicycle, Truck }
        
        [Serializable]
        public class FloraType {
            public string id;
            public string nameEn;
            public string nameDhivehi;
            public bool isTropical;
        }
        
        public static WorldData GenerateCompleteMaldivesWorld() {
            var data = new WorldData {
                islands = Generate41Islands(),
                gangs = Generate83Gangs(),
                buildings = Generate70Buildings(),
                vehicles = Generate40Vehicles(),
                flora = Generate12Flora()
            };
            return data;
        }
        
        static Island[] Generate41Islands() {
            var islands = new Island[41];
            string[] realIslandNames = {
                "Male", "Hulhumale", "Villingili", "Maafushi", "Thulusdhoo",
                "Guraidhoo", "Gulhi", "Guraidhoo", "Fulidhoo", "Keyodhoo",
                "Rakeedhoo", "Felidhoo", "Keyodhoo", "Thinadhoo", "Kudahuvadhoo",
                "Eydhafushi", "Kulhudhuffushi", "Nolhivaram", "Manadhoo", "Magoodhoo",
                "Milandhoo", "Funadhoo", "Foakaidhoo", "Naifaru", "Lhohi",
                "Komandoo", "Kanditheemu", "Maaungoodhoo", "Manadhoo", "Velidhoo",
                "Holhudhoo", "Alifushi", "Maduvvaree", "Angolhitheemu", "Rasmaadhoo",
                "Kandholhudhoo", "Kanditheemu", "Kudafaree", "Dheburidheytherey", "Maduvvaree"
            };
            
            for (int i = 0; i < 41; i++) {
                islands[i] = new Island {
                    id = $"ISLAND_{i:D3}",
                    nameEn = i < realIslandNames.Length ? realIslandNames[i] : $"Island {i}",
                    nameDhvehi = $"ޖަޒީރާ {i}",
                    position = new Vector2(
                        (float)(Math.Cos(i * 2 * Math.PI / 41) * 50 + rng.NextDouble() * 10),
                        (float)(Math.Sin(i * 2 * Math.PI / 41) * 50 + rng.NextDouble() * 10)
                    ),
                    size = rng.Next(1, 6),
                    isResort = rng.NextDouble() > 0.7,
                    dominantGangId = $"GANG_{rng.Next(0, 83):D3}",
                    buildingIds = new string[0], // Populate later
                    prayerTimeOffset = (float)(rng.NextDouble() * 30 - 15) // ±15 minutes
                };
            }
            return islands;
        }
        
        static Gang[] Generate83Gangs() {
            var gangs = new Gang[83];
            string[] gangNames = {"Bodu", "Kuda", "Fen", "Rah", "Vela", "Dhon", "Hura", "Madi", "Fushi", "Rai"};
            
            for (int i = 0; i < 83; i++) {
                gangs[i] = new Gang {
                    id = i,
                    nameEn = $"{gangNames[i % 10]} Gang {i}",
                    nameDhivehi = $"ޖަންގު {i}",
                    territoryIslandId = $"ISLAND_{rng.Next(0, 41):D3}",
                    reputation = rng.Next(-100, 101),
                    type = (GangType)rng.Next(0, 6),
                    color = new Color(
                        (float)rng.NextDouble(),
                        (float)rng.NextDouble(),
                        (float)rng.NextDouble()
                    )
                };
            }
            return gangs;
        }
        
        static BuildingType[] Generate70Buildings() {
            var buildings = new BuildingType[70];
            string[] prefixes = {"Traditional", "Modern", "Coastal", "Fishing", "Community"};
            string[] suffixes = {"Mosque", "House", "Shop", "Dock", "Hall"};
            
            for (int i = 0; i < 70; i++) {
                var category = (BuildingCategory)(i % 6);
                buildings[i] = new BuildingType {
                    id = $"BUILDING_{i:D3}",
                    nameEn = $"{prefixes[i % 5]} {suffixes[i % 5]} {i}",
                    nameDhivehi = $"އިމާރާތް {i}",
                    category = category,
                    cost = rng.Next(100, 10000),
                    isEnterable = category != BuildingCategory.FishingHut
                };
            }
            return buildings;
        }
        
        static VehicleType[] Generate40Vehicles() {
            var vehicles = new VehicleType[40];
            string[] dhoniNames = {"Bokkura", "Feru", "Handhu", "Gaa", "Mundu"};
            
            for (int i = 0; i < 40; i++) {
                bool isDhoni = i < 20;
                vehicles[i] = new VehicleType {
                    id = $"VEHICLE_{i:D3}",
                    nameEn = isDhoni ? $"Dhoni '{dhoniNames[i % 5]}'" : $"Speedboat {i}",
                    category = isDhoni ? VehicleCategory.Dhoni : VehicleCategory.Speedboat,
                    speed = isDhoni ? rng.Next(10, 20) : rng.Next(30, 50),
                    capacity = rng.Next(2, 12)
                };
            }
            return vehicles;
        }
        
        static FloraType[] Generate12Flora() {
            var flora = new FloraType[12];
            string[] plantNames = {"Palm Tree", "Mangrove", "Hibiscus", "Banyan", "Seagrass", "Coral"};
            
            for (int i = 0; i < 12; i++) {
                flora[i] = new FloraType {
                    id = $"FLORA_{i:D2}",
                    nameEn = plantNames[i % 6],
                    nameDhivehi = $"ގަސް {i}",
                    isTropical = true
                };
            }
            return flora;
        }
        
        // Utility: Shuffle arrays for variety
        public static void Shuffle<T>(T[] array) {
            int n = array.Length;
            while (n > 1) {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }
}
