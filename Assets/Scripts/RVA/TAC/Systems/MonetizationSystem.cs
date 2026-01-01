using UnityEngine;
using System;
using System.Collections.Generic;
using RVA.TAC.Cultural;

namespace RVA.TAC.Monetization
{
    /// <summary>
    /// Ethical monetization system respecting Maldivian economic context
    /// NO predatory mechanics - cosmetic only, no pay-to-win
    /// </summary>
    public class MonetizationSystem : MonoBehaviour
    {
        #region Ethical Principles
        [Header("Ethical Monetization Settings")]
        public bool enableAds = true;
        public bool enableIAP = true;
        public bool enableBattlePass = true;
        public bool respectCulturalValues = true; // No gambling mechanics
        public bool parentGateForPurchases = false;
        public int maxDailyAdViews = 5;
        #endregion

        #region Currency System
        [System.Serializable]
        public class Currency
        {
            public string currencyId;
            public string nameEn;
            public string nameDhivehi;
            public Sprite icon;
            public bool isPremium; // MVR vs Premium
        }
        
        public Currency rufiyaaCurrency;      // MVR - earned in-game
        public Currency premiumCurrency;      // Premium - purchased
        
        private int rufiyaaBalance = 0;
        private int premiumBalance = 0;
        #endregion

        #region In-App Purchases
        [System.Serializable]
        public class IAPProduct
        {
            public string productId;
            public string titleEn;
            public string titleDhivehi;
            public string descriptionEn;
            public string descriptionDhvehi;
            public ProductType type;
            public float priceUSD;
            public int premiumCurrencyAmount;
            public string[] rewardItemIds;
            public bool isConsumable;
            public bool respectCulturalValues = true;
        }
        
        public enum ProductType
        {
            PremiumCurrency,
            CosmeticItem,
            ConvenienceItem,
            ExpansionPack,
            CosmeticBundle
        }
        
        public List<IAPProduct> availableProducts = new List<IAPProduct>();
        private Dictionary<string, IAPProduct> productCatalog = new Dictionary<string, IAPProduct>();
        #endregion

        #region Ad Integration
        [Header("Ad Settings")]
        public float adCooldown = 300f; // 5 minutes between ads
        private float lastAdTime = -999f;
        private int dailyAdViews = 0;
        private System.DateTime lastAdDate;
        
        public enum AdRewardType
        {
            Rufiyaa,
            PremiumCurrency,
            SpeedUp,
            ExtraReward
        }
        #endregion

        # cultural Sensitivity Filters
        private List<string> prohibitedContentKeywords = new List<string>
        {
            "gamble", "casino", "bet", "alcohol", "pork",
            "gambling", "lottery", "raffle"
        };
        
        private Dictionary<string, string> localizedPricePoints = new Dictionary<string, string>();
        #endregion

        private void Start()
        {
            InitializeCurrency();
            InitializeProductCatalog();
            LoadPlayerPurchases();
            
            Debug.Log("[MonetizationSystem] Initialized with ethical constraints");
        }

        private void InitializeCurrency()
        {
            rufiyaaBalance = PlayerPrefs.GetInt("Currency_Rufiyaa", 0);
            premiumBalance = PlayerPrefs.GetInt("Currency_Premium", 0);
            
            // Create currency definitions
            rufiyaaCurrency = new Currency
            {
                currencyId = "MVR",
                nameEn = "Rufiyaa",
                nameDhivehi = "ރުފިޔާ",
                isPremium = false
            };
            
            premiumCurrency = new Currency
            {
                currencyId = "PREMIUM",
                nameEn = "Dhoni Points",
                nameDhivehi = "ދޯނި ޕޮއިންޓުތައް",
                isPremium = true
            };
        }

        private void InitializeProductCatalog()
        {
            // Premium currency packs (ethical: no gambling, direct purchase)
            AddProduct(new IAPProduct
            {
                productId = "premium_100",
                titleEn = "Small Dhoni Points Pack",
                titleDhivehi = "ކުޑަ ދޯނި ޕޮއިންޓް ޕެކް",
                descriptionEn = "100 Dhoni Points for cosmetic items",
                descriptionDhvehi = "ކޮސްމެޓިކް ސާމާނުތަކަށް 100 ދޯނި ޕޮއިންޓް",
                type = ProductType.PremiumCurrency,
                priceUSD = 0.99f,
                premiumCurrencyAmount = 100,
                isConsumable = true,
                respectCulturalValues = true
            });
            
            AddProduct(new IAPProduct
            {
                productId = "premium_500",
                titleEn = "Large Dhoni Points Pack",
                titleDhivehi = "ބޮޑު ދޯނި ޕޮއިންޓް ޕެކް",
                descriptionEn = "500 Dhoni Points + 50 bonus",
                descriptionDhvehi = "500 ދޯނި ޕޮއިންޓް + 50 ބޯނަސް",
                type = ProductType.PremiumCurrency,
                priceUSD = 4.99f,
                premiumCurrencyAmount = 550,
                isConsumable = true,
                respectCulturalValues = true
            });
            
            // Cosmetic items (boats, costumes)
            AddProduct(new IAPProduct
            {
                productId = "cosmetic_dhoni_premium",
                titleEn = "Premium Dhoni Skin",
                titleDhivehi = "ޕްރީމިއަމް ދޯނި ސްކިން",
                descriptionEn = "Luxurious traditional dhoni appearance",
                descriptionDhvehi = "ސައްހަ ދޯނިކޮޅެއްގެ ހިދުމަތްތެރި ސްކިން",
                type = ProductType.CosmeticItem,
                priceUSD = 2.99f,
                rewardItemIds = new[] { "SKIN_DHONI_PREMIUM" },
                isConsumable = false,
                respectCulturalValues = true
            });
            
            // Convenience items (non-pay-to-win)
            AddProduct(new IAPProduct
            {
                productId = "convenience_map_pack",
                titleEn = "Island Explorer Pack",
                titleDhivehi = "ޖަޒީރާ ހޯދުންވެރި ޕެކް",
                descriptionEn = "Reveals all 41 islands on map (cosmetic only)",
                descriptionDhvehi = "ހުރިހާ 41 ޖަޒީރައެއް މެޕުގައި ދައްކައިފަ (ކޮސްމެޓިކް ހެއްޔެވެ.)",
                type = ProductType.ConvenienceItem,
                priceUSD = 1.99f,
                isConsumable = false,
                respectCulturalValues = true
            });
            
            Debug.Log($"[MonetizationSystem] Initialized with {productCatalog.Count} products");
        }

        private void AddProduct(IAPProduct product)
        {
            // Validate for cultural sensitivity
            if (!product.respectCulturalValues)
            {
                Debug.LogWarning($"[MonetizationSystem] Product blocked for cultural insensitivity: {product.productId}");
                return;
            }
            
            // Check for prohibited content
            if (ContainsProhibitedContent(product))
            {
                Debug.LogWarning($"[MonetizationSystem] Product contains prohibited content: {product.productId}");
                return;
            }
            
            productCatalog[product.productId] = product;
            availableProducts.Add(product);
        }

        private bool ContainsProhibitedContent(IAPProduct product)
        {
            string combinedText = (product.titleEn + product.descriptionEn).ToLower();
            return prohibitedContentKeywords.Any(keyword => combinedText.Contains(keyword));
        }

        /// <summary>
        /// Attempt to purchase a product
        /// </summary>
        public void PurchaseProduct(string productId)
        {
            if (!productCatalog.ContainsKey(productId))
            {
                Debug.LogWarning($"[MonetizationSystem] Product not found: {productId}");
                return;
            }
            
            var product = productCatalog[productId];
            
            // Show parent gate if enabled
            if (parentGateForPurchases && !ShowParentGate())
            {
                Debug.Log("[MonetizationSystem] Purchase blocked by parent gate");
                return;
            }
            
            // In production: integrate with platform store (App Store, Google Play)
            // For now: simulate purchase
            
            Debug.Log($"[MonetizationSystem] Initiating purchase: {product.titleEn} (${product.priceUSD})");
            
            // Simulate purchase success
            SimulatePurchaseSuccess(product);
        }

        private void SimulatePurchaseSuccess(IAPProduct product)
        {
            // Grant rewards
            if (product.premiumCurrencyAmount > 0)
            {
                AddPremiumCurrency(product.premiumCurrencyAmount);
            }
            
            if (product.rewardItemIds != null)
            {
                foreach (var itemId in product.rewardItemIds)
                {
                    InventorySystem.Instance?.AddItem(itemId);
                }
            }
            
            // Log purchase for analytics
            AnalyticsSystem.Instance?.LogPurchase(product.productId, product.priceUSD, product.type.ToString());
            
            // Show confirmation
            UIManager.Instance?.ShowPurchaseConfirmation(product);
            
            Debug.Log($"[MonetizationSystem] Purchase successful: {product.productId}");
            
            // Save purchase state
            SavePurchase(product.productId);
        }

        private bool ShowParentGate()
        {
            // Simple math question for parent gate
            int a = Random.Range(10, 100);
            int b = Random.Range(10, 100);
            int expected = a + b;
            
            // In production: Show UI dialog
            // For now: auto-pass in development
            #if UNITY_EDITOR
            return true;
            #else
            // Show actual parent gate UI
            return UIManager.Instance?.ShowParentGate(a, b, expected) ?? true;
            #endif
        }

        /// <summary>
        /// Show rewarded video ad
        /// </summary>
        public void ShowRewardedAd(AdRewardType rewardType)
        {
            if (!enableAds)
            {
                Debug.Log("[MonetizationSystem] Ads disabled");
                return;
            }
            
            // Check cooldown
            if (Time.time - lastAdTime < adCooldown)
            {
                float remaining = adCooldown - (Time.time - lastAdTime);
                UIManager.Instance?.ShowMessage($"Ad cooldown: {remaining:F0} seconds remaining");
                return;
            }
            
            // Check daily limit
            if (dailyAdViews >= maxDailyAdViews && System.DateTime.Today == lastAdDate)
            {
                UIManager.Instance?.ShowMessage("Daily ad limit reached");
                return;
            }
            
            // Reset daily counter if new day
            if (System.DateTime.Today != lastAdDate)
            {
                dailyAdViews = 0;
                lastAdDate = System.DateTime.Today;
            }
            
            Debug.Log($"[MonetizationSystem] Showing rewarded ad for: {rewardType}");
            
            // In production: Show actual ad
            // For now: simulate ad completion
            
            SimulateAdCompletion(rewardType);
        }

        private void SimulateAdCompletion(AdRewardType rewardType)
        {
            dailyAdViews++;
            lastAdTime = Time.time;
            
            // Grant reward
            switch (rewardType)
            {
                case AdRewardType.Rufiyaa:
                    AddRufiyaa(100);
                    break;
                    
                case AdRewardType.PremiumCurrency:
                    AddPremiumCurrency(5);
                    break;
                    
                case AdRewardType.SpeedUp:
                    // Apply 2x speed boost for 10 minutes
                    TimeSystem.Instance?.ApplySpeedBoost(1200f, 2f);
                    break;
                    
                case AdRewardType.ExtraReward:
                    // Double next mission reward
                    MissionSystem.Instance?.SetNextMissionRewardMultiplier(2f);
                    break;
            }
            
            // Log ad view
            AnalyticsSystem.Instance?.LogAdView(rewardType.ToString());
            
            Debug.Log($"[MonetizationSystem] Ad completed, reward: {rewardType}");
        }

        #region Currency Management
        public void AddRufiyaa(int amount)
        {
            rufiyaaBalance += amount;
            PlayerPrefs.SetInt("Currency_Rufiyaa", rufiyaaBalance);
            
            UIManager.Instance?.UpdateCurrencyDisplay();
            
            Debug.Log($"[MonetizationSystem] Added {amount} Rufiyaa. Balance: {rufiyaaBalance}");
        }

        public bool SpendRufiyaa(int amount)
        {
            if (rufiyaaBalance >= amount)
            {
                rufiyaaBalance -= amount;
                PlayerPrefs.SetInt("Currency_Rufiyaa", rufiyaaBalance);
                
                UIManager.Instance?.UpdateCurrencyDisplay();
                
                Debug.Log($"[MonetizationSystem] Spent {amount} Rufiyaa. Balance: {rufiyaaBalance}");
                return true;
            }
            
            Debug.LogWarning($"[MonetizationSystem] Insufficient Rufiyaa. Required: {amount}, Balance: {rufiyaaBalance}");
            return false;
        }

        public void AddPremiumCurrency(int amount)
        {
            premiumBalance += amount;
            PlayerPrefs.SetInt("Currency_Premium", premiumBalance);
            
            UIManager.Instance?.UpdateCurrencyDisplay();
            
            Debug.Log($"[MonetizationSystem] Added {amount} Premium. Balance: {premiumBalance}");
        }

        public bool SpendPremiumCurrency(int amount)
        {
            if (premiumBalance >= amount)
            {
                premiumBalance -= amount;
                PlayerPrefs.SetInt("Currency_Premium", premiumBalance);
                
                UIManager.Instance?.UpdateCurrencyDisplay();
                
                Debug.Log($"[MonetizationSystem] Spent {amount} Premium. Balance: {premiumBalance}");
                return true;
            }
            
            Debug.LogWarning($"[MonetizationSystem] Insufficient Premium. Required: {amount}, Balance: {premiumBalance}");
            
            // Offer to purchase premium
            UIManager.Instance?.ShowPremiumPurchasePrompt();
            
            return false;
        }

        public int GetRufiyaaBalance() => rufiyaaBalance;
        public int GetPremiumBalance() => premiumBalance;
        #endregion

        #region Purchase Persistence
        private void SavePurchase(string productId)
        {
            string key = $"Purchase_{productId}";
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            
            Debug.Log($"[MonetizationSystem] Purchase saved: {productId}");
        }
        
        private void LoadPlayerPurchases()
        {
            // In production: validate with platform receipt
            // For now: load from PlayerPrefs
            
            Debug.Log("[MonetizationSystem] Loaded purchase history");
        }
        
        public bool HasPurchased(string productId)
        {
            return PlayerPrefs.GetInt($"Purchase_{productId}", 0) == 1;
        }
        #endregion

        #region Public API
        public List<IAPProduct> GetAvailableProducts() => availableProducts;
        public IAPProduct GetProduct(string productId) => productCatalog.ContainsKey(productId) ? productCatalog[productId] : null;
        
        public bool CanShowAd()
        {
            return enableAds && 
                   Time.time - lastAdTime >= adCooldown && 
                   (dailyAdViews < maxDailyAdViews || System.DateTime.Today != lastAdDate);
        }
        
        public int GetDailyAdViews() => dailyAdViews;
        public int GetMaxDailyAdViews() => maxDailyAdViews;
        #endregion
    }
}
