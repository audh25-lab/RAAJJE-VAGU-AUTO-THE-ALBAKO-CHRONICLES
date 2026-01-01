// InventorySystem.cs - RVACONT-004
// Maldives-specific items: fishing gear, cultural artifacts, Dhivehi terms
// Mobile-optimized grid UI backend

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace RVA.TAC.GAMEPLAY
{
    [BurstCompile]
    public partial struct InventorySystem : ISystem
    {
        private const int INVENTORY_WIDTH = 8; // Mobile-friendly grid
        private const int INVENTORY_HEIGHT = 6;
        private const int MAX_STACK_SIZE = 99;

        private ComponentLookup<InventoryComponent> inventoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            inventoryLookup = state.GetComponentLookup<InventoryComponent>();
            state.RequireForUpdate<InventoryComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            inventoryLookup.Update(ref state);
            
            // Process item pickup (auto-pickup in range)
            var pickupJob = new ItemPickupJob
            {
                Inventories = inventoryLookup,
                DeltaTime = SystemAPI.Time.DeltaTime,
                AutoPickupRange = 2.5f
            };
            pickupJob.ScheduleParallel();
            
            // Update item timers (spoilage for fish)
            var spoilageJob = new ItemSpoilageJob
            {
                Inventories = inventoryLookup,
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            spoilageJob.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ItemPickupJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<InventoryComponent> Inventories;
            public float DeltaTime;
            public float AutoPickupRange;

            void Execute(Entity entity, ref LocalTransform transform, in PlayerTagComponent player)
            {
                if (!Inventories.HasComponent(entity)) return;
                
                var inventory = Inventories[entity];
                
                // Auto-pickup nearby items (mobile-friendly)
                foreach (var (itemTransform, itemEntity) in 
                    SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ItemComponent>().WithEntityAccess())
                {
                    float distance = math.distance(transform.Position, itemTransform.ValueRO.Position);
                    if (distance < AutoPickupRange)
                    {
                        // Attempt to pickup item
                        if (TryAddItemToInventory(ref inventory, itemEntity))
                        {
                            // Mark item for destruction (collected)
                            SystemAPI.AddComponent<Disabled>(itemEntity);
                        }
                    }
                }
                
                Inventories[entity] = inventory;
            }

            private bool TryAddItemToInventory(ref InventoryComponent inventory, Entity item)
            {
                var itemComponent = SystemAPI.GetComponent<ItemComponent>(item);
                
                // Find slot (stackable check)
                for (int i = 0; i < inventory.ItemCount; i++)
                {
                    if (inventory.ItemIds[i] == itemComponent.ItemId && 
                        inventory.StackCounts[i] < MAX_STACK_SIZE)
                    {
                        inventory.StackCounts[i]++;
                        return true;
                    }
                }
                
                // Find empty slot
                for (int i = 0; i < MAX_INVENTORY_SLOTS; i++)
                {
                    if (inventory.ItemIds[i] == ItemId.None)
                    {
                        inventory.ItemIds[i] = itemComponent.ItemId;
                        inventory.StackCounts[i] = 1;
                        inventory.ItemCount++;
                        return true;
                    }
                }
                
                return false; // Inventory full
            }
        }

        [BurstCompile]
        partial struct ItemSpoilageJob : IJobEntity
        {
            public ComponentLookup<InventoryComponent> Inventories;
            public float DeltaTime;

            void Execute(Entity entity)
            {
                if (!Inventories.HasComponent(entity)) return;
                
                var inventory = Inventories[entity];
                
                for (int i = 0; i < inventory.ItemCount; i++)
                {
                    var itemData = GetItemData(inventory.ItemIds[i]);
                    if (itemData.Perishable)
                    {
                        inventory.SpoilageTimers[i] += DeltaTime;
                        
                        // Fish spoils in 2 minutes (game time)
                        if (inventory.SpoilageTimers[i] > 120f) 
                        {
                            // Item becomes spoiled fish
                            inventory.ItemIds[i] = ItemId.SpoiledFish;
                            inventory.StackCounts[i] = 1;
                        }
                    }
                }
                
                Inventories[entity] = inventory;
            }

            private ItemData GetItemData(ItemId itemId)
            {
                // Item database lookup
                return itemId switch
                {
                    ItemId.Tuna => new ItemData { Value = 150, Perishable = true },
                    ItemId.ReefFish => new ItemData { Value = 80, Perishable = true },
                    ItemId.BoduberuDrum => new ItemData { Value = 500, Perishable = false },
                    ItemId.DhivehiDictionary => new ItemData { Value = 300, Perishable = false },
                    _ => new ItemData { Value = 0, Perishable = false }
                };
            }
        }

        // Inventory UI query (for mobile UI system)
        public static InventorySlotData[] GetInventorySlots(in InventoryComponent inventory)
        {
            var slots = new InventorySlotData[MAX_INVENTORY_SLOTS];
            for (int i = 0; i < MAX_INVENTORY_SLOTS; i++)
            {
                slots[i] = new InventorySlotData
                {
                    ItemId = inventory.ItemIds[i],
                    StackCount = inventory.StackCounts[i],
                    IsEmpty = inventory.ItemIds[i] == ItemId.None
                };
            }
            return slots;
        }
    }

    [BurstCompile]
    public struct InventoryComponent : IComponentData
    {
        public const int MAX_INVENTORY_SLOTS = 48; // 8x6 grid
        
        public FixedList64Bytes<ItemId> ItemIds;
        public FixedList64Bytes<byte> StackCounts;
        public FixedList64Bytes<float> SpoilageTimers;
        public int ItemCount;
        public int RufiyaaCurrency; // Maldivian currency
        public int LaariCurrency; // Sub-unit (1 Rufiyaa = 100 Laari)
    }

    [BurstCompile]
    public struct ItemComponent : IComponentData
    {
        public ItemId ItemId;
        public float3 WorldPosition;
        public bool AutoPickup;
    }

    [BurstCompile]
    public struct ItemData
    {
        public int Value; // in Laari
        public bool Perishable;
        public bool IsCulturalArtifact;
    }

    public enum ItemId : ushort
    {
        None = 0,
        Tuna = 1, // Maldives staple
        ReefFish = 2,
        Octopus = 3,
        Dhiggaa = 4, // Harpoon
        FishingNet = 5,
        BoduberuDrum = 6,
        Coconut = 7,
        DhivehiDictionary = 8, // Language learning
        PrayerCap = 9, // Cultural item
        MaldivianFlag = 10, // Patriotism mechanic
        SpoiledFish = 999, // Degraded item
    }

    public struct InventorySlotData
    {
        public ItemId ItemId;
        public byte StackCount;
        public bool IsEmpty;
    }
}
