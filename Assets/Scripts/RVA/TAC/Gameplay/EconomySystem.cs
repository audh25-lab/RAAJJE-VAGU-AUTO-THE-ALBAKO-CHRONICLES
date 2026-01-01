// EconomySystem.cs - RVACONT-004
// Maldivian economy: Fishing + Tourism dual model
// Currency: Rufiyaa (MVR) - 1 USD ≈ 15.4 MVR (game balanced)

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RVA.TAC.GAMEPLAY
{
    [BurstCompile]
    public partial struct EconomySystem : ISystem
    {
        private const int FISH_PRICE_BASE = 150; // Laari (1.5 Rufiyaa)
        private const int TOURISM_MULTIPLIER = 3; // Tourism pays more
        private const float PRAYER_TIME_BONUS = 1.2f; // Good deeds during prayer

        private EntityQuery playerQuery;
        private ComponentLookup<EconomyComponent> economyLookup;
        private ComponentLookup<InventoryComponent> inventoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            economyLookup = state.GetComponentLookup<EconomyComponent>();
            inventoryLookup = state.GetComponentLookup<InventoryComponent>();
            
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTagComponent, InventoryComponent>()
                .Build();
            
            state.RequireForUpdate(playerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            economyLookup.Update(ref state);
            inventoryLookup.Update(ref state);
            
            // Update market prices (dynamic based on weather/prayer)
            var marketJob = new MarketPriceUpdateJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                WeatherSystem = SystemAPI.GetSingleton<WeatherComponent>(),
                PrayerTime = SystemAPI.GetSingleton<PrayerTimeComponent>(),
                CurrentTime = SystemAPI.Time.ElapsedTime
            };
            marketJob.ScheduleParallel();
            
            // Process transactions (fishing sales, tourism)
            ProcessTransactions(ref state);
        }

        [BurstCompile]
        partial struct MarketPriceUpdateJob : IJobEntity
        {
            [ReadOnly] public WeatherComponent WeatherSystem;
            [ReadOnly] public PrayerTimeComponent PrayerTime;
            public float CurrentTime;
            public float DeltaTime;

            void Execute(ref EconomyComponent economy)
            {
                // Dynamic fish pricing
                float priceMultiplier = 1f;
                
                // Bad weather = less fish = higher prices
                if (WeatherSystem.CurrentWeather == WeatherType.MonsoonHeavy || 
                    WeatherSystem.CurrentWeather == WeatherType.Storm)
                {
                    priceMultiplier += 0.5f;
                }
                
                // Prayer time bonus for ethical fishing
                if (PrayerTime.IsPrayerTimeActive)
                {
                    priceMultiplier *= PRAYER_TIME_BONUS;
                }
                
                // Tourism demand cycle (day/night)
                float tourismDemand = math.sin(CurrentTime * 0.1f) * 0.5f + 0.5f;
                economy.TourismDemandMultiplier = 1f + tourismDemand;
                
                economy.CurrentFishPrice = (int)(FISH_PRICE_BASE * priceMultiplier);
            }
        }

        private void ProcessTransactions(ref SystemState state)
        {
            // Process all active transactions
            foreach (var (transaction, entity) in 
                SystemAPI.Query<RefRO<TransactionComponent>>().WithEntityAccess())
            {
                switch (transaction.ValueRO.TransactionType)
                {
                    case TransactionType.SellFish:
                        ProcessFishSale(entity, transaction.ValueRO);
                        break;
                    case TransactionType.TourismService:
                        ProcessTourismTransaction(entity, transaction.ValueRO);
                        break;
                    case TransactionType.PurchaseItem:
                        ProcessPurchase(entity, transaction.ValueRO);
                        break;
                }
                
                // Mark transaction complete
                state.EntityManager.RemoveComponent<TransactionComponent>(entity);
            }
        }

        private void ProcessFishSale(Entity seller, TransactionComponent transaction)
        {
            if (!inventoryLookup.HasComponent(seller)) return;
            
            var inventory = inventoryLookup[seller];
            int totalFish = 0;
            
            // Count fish in inventory
            for (int i = 0; i < inventory.ItemCount; i++)
            {
                if (inventory.ItemIds[i] == ItemId.Tuna || 
                    inventory.ItemIds[i] == ItemId.ReefFish)
                {
                    totalFish += inventory.StackCounts[i];
                    // Clear sold fish
                    inventory.ItemIds[i] = ItemId.None;
                    inventory.StackCounts[i] = 0;
                }
            }
            
            if (totalFish > 0)
            {
                // Calculate earnings
                var economy = SystemAPI.GetComponent<EconomyComponent>(seller);
                int earnings = totalFish * economy.CurrentFishPrice;
                
                // Apply prayer time bonus
                if (SystemAPI.GetComponent<PrayerTimeComponent>(seller).IsPrayerTimeActive)
                {
                    earnings = (int)(earnings * PRAYER_TIME_BONUS);
                }
                
                // Convert to Rufiyaa/Laari
                inventory.RufiyaaCurrency += earnings / 100;
                inventory.LaariCurrency += earnings % 100;
                
                // Reputation boost for legal fishing
                if (SystemAPI.HasComponent<ReputationComponent>(seller))
                {
                    var rep = SystemAPI.GetComponent<ReputationComponent>(seller);
                    rep.FishermenFaction += totalFish * 2;
                    SystemAPI.SetComponent(seller, rep);
                }
                
                inventoryLookup[seller] = inventory;
            }
        }

        private void ProcessTourismTransaction(Entity serviceProvider, TransactionComponent transaction)
        {
            // Tourism activities: diving tours, Boduberu performances
            var economy = SystemAPI.GetComponent<EconomyComponent>(serviceProvider);
            int basePrice = transaction.Amount * TOURISM_MULTIPLIER;
            
            // Bonus for cultural authenticity
            if (transaction.IsCulturalActivity && 
                SystemAPI.HasComponent<BoduberuSkillComponent>(serviceProvider))
            {
                basePrice *= 2;
            }
            
            // Diving bonus during clear weather
            if (transaction.IsDivingActivity)
            {
                var weather = SystemAPI.GetComponent<WeatherComponent>(serviceProvider);
                if (weather.CurrentWeather == WeatherType.Clear)
                {
                    basePrice = (int)(basePrice * 1.3f);
                }
            }
            
            // Award money
            if (inventoryLookup.HasComponent(serviceProvider))
            {
                var inventory = inventoryLookup[serviceProvider];
                inventory.RufiyaaCurrency += basePrice / 100;
                inventory.LaariCurrency += basePrice % 100;
                inventoryLookup[serviceProvider] = inventory;
            }
        }

        private void ProcessPurchase(Entity buyer, TransactionComponent transaction)
        {
            if (!inventoryLookup.HasComponent(buyer)) return;
            
            var inventory = inventoryLookup[buyer];
            int totalCost = transaction.Amount;
            
            // Check funds
            int totalLaari = inventory.RufiyaaCurrency * 100 + inventory.LaariCurrency;
            if (totalLaari >= totalCost)
            {
                // Deduct money
                totalLaari -= totalCost;
                inventory.RufiyaaCurrency = totalLaari / 100;
                inventory.LaariCurrency = totalLaari % 100;
                
                // Add item
                // (Item addition logic would go here)
                
                inventoryLookup[buyer] = inventory;
            }
        }
    }

    [BurstCompile]
    public struct EconomyComponent : IComponentData
    {
        public int CurrentFishPrice; // In Laari
        public float TourismDemandMultiplier;
        public int DailyExpenses; // Living costs in Maldives
        public int ReputationImpact; // Economic reputation
    }

    [BurstCompile]
    public struct TransactionComponent : IComponentData
    {
        public TransactionType TransactionType;
        public int Amount;
        public bool IsCulturalActivity;
        public bool IsDivingActivity;
        public Entity TargetEntity;
    }

    public enum TransactionType : byte
    {
        None = 0,
        SellFish = 1,
        TourismService = 2,
        PurchaseItem = 3,
        PayFine = 4, // Police system
        GangPayment = 5,
        Charity = 6, // Cultural - Zakat
    }
}
