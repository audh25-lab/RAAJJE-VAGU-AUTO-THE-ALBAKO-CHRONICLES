using UnityEngine;
using System.Collections.Generic;
using RVA.TAC.Cultural;
using RVA.TAC.Input;

namespace RVA.TAC.Tutorial
{
    /// <summary>
    /// Culturally-aware tutorial system that adapts to player's knowledge of Maldivian customs
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        #region Tutorial State
        public enum TutorialPhase
        {
            NotStarted,
            Movement,
            Interaction,
            Fishing,
            PrayerTimes,
            Combat,
            Vehicles,
            Economy,
            Gangs,
            Completed
        }
        
        private TutorialPhase currentPhase = TutorialPhase.NotStarted;
        private Dictionary<TutorialPhase, bool> completedPhases = new Dictionary<TutorialPhase, bool>();
        private Dictionary<TutorialPhase, float> phaseProgress = new Dictionary<TutorialPhase, float>();
        #endregion

        # cultural Awareness
        [Header("Cultural Sensitivity")]
        public bool assumeLocalKnowledge = false; // If true, skips basic cultural tutorials
        public bool skipAllTutorials = false;
        
        private bool playerKnowsPrayerTimes = false;
        private bool playerKnowsBoduberu = false;
        private bool playerKnowsFishing = false;
        #endregion

        #region Tutorial Triggers
        private bool hasMoved = false;
        private bool hasInteracted = false;
        private bool hasFished = false;
        private bool hasPrayed = false;
        private bool hasFought = false;
        private bool hasDriven = false;
        private bool hasEarnedMoney = false;
        private bool hasMetGang = false;
        #endregion

        #region UI & Prompts
        private struct TutorialPrompt
        {
            public string key;
            public string textEn;
            public string textDhivehi;
            public TutorialPhase phase;
            public string inputAction;
            public float displayDuration;
        }
        
        private List<TutorialPrompt> tutorialPrompts = new List<TutorialPrompt>();
        private TutorialPrompt? activePrompt = null;
        private float promptTimer = 0f;
        #endregion

        private void Awake()
        {
            InitializePhases();
            LoadTutorialPrompts();
        }

        private void InitializePhases()
        {
            foreach (TutorialPhase phase in System.Enum.GetValues(typeof(TutorialPhase)))
            {
                completedPhases[phase] = false;
                phaseProgress[phase] = 0f;
            }
        }

        private void LoadTutorialPrompts()
        {
            // Movement
            tutorialPrompts.Add(new TutorialPrompt
            {
                key = "MOVE_BASIC",
                textEn = "Use the virtual joystick to move around the island",
                textDhivehi = "ޖަސްޓިކް ކިނަފެނާ ބޭނުންކޮށްގެން ޖަޖިންގައި ހިނގާށް",
                phase = TutorialPhase.Movement,
                inputAction = "Move",
                displayDuration = 5f
            });
            
            // Prayer times (culturally important)
            tutorialPrompts.Add(new TutorialPrompt
            {
                key = "PRAYER_INTRO",
                textEn = "Prayer times are important in Maldives. Watch for the adhan call",
                textDhivehi = "ނަމާދުގެ ވަގުތު މުހިންމު. އަޟާނުގެ ގޯސްތަކަށް ސަމާލުވޭ",
                phase = TutorialPhase.PrayerTimes,
                inputAction = "",
                displayDuration = 7f
            });
            
            // Fishing
            tutorialPrompts.Add(new TutorialPrompt
            {
                key = "FISHING_BASIC",
                textEn = "Tap the fishing button when near ocean to catch tuna",
                textDhivehi = "ބަނޑުގެ ކުރިމާތީ ހުންނަ ނަމަ މަސް ހިއްލާނުން ބުންޓަން ދައްކާށް",
                phase = TutorialPhase.Fishing,
                inputAction = "Fish",
                displayDuration = 4f
            });
            
            // Gang system
            tutorialPrompts.Add(new TutorialPrompt
            {
                key = "GANGS_INTRO",
                textEn = "There are 83 gangs in the Maldives. Your reputation affects their behavior",
                textDhivehi = "ދިވެހިރާއްޖޭގައި 83 ޖަންގު އެބަ. ތިޔައްގެ ނަންބަރު އޮޅުވާލައި ދަނީ",
                phase = TutorialPhase.Gangs,
                inputAction = "",
                displayDuration = 6f
            });
        }

        private void Start()
        {
            if (skipAllTutorials)
            {
                CompleteAllTutorials();
                return;
            }
            
            // Start with movement tutorial
            StartPhase(TutorialPhase.Movement);
        }

        private void Update()
        {
            if (skipAllTutorials) return;
            
            UpdatePhaseProgress();
            CheckTutorialTriggers();
            
            if (activePrompt.HasValue)
            {
                promptTimer += Time.deltaTime;
                if (promptTimer >= activePrompt.Value.displayDuration)
                {
                    HideCurrentPrompt();
                }
            }
        }

        private void CheckTutorialTriggers()
        {
            // Movement detection
            if (!hasMoved && Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
            {
                hasMoved = true;
                MarkPhaseComplete(TutorialPhase.Movement);
                StartPhase(TutorialPhase.Interaction);
            }
            
            // Interaction detection
            if (!hasInteracted && TouchInputSystem.Instance?.GetTapCount() > 1)
            {
                hasInteracted = true;
                MarkPhaseComplete(TutorialPhase.Interaction);
            }
            
            // Combat detection
            if (!hasFought && CombatSystem.Instance?.IsInCombat() == true)
            {
                hasFought = true;
                StartPhase(TutorialPhase.Combat);
            }
            
            // Auto-detect cultural knowledge
            if (assumeLocalKnowledge)
            {
                playerKnowsPrayerTimes = true;
                playerKnowsBoduberu = true;
                playerKnowsFishing = true;
            }
        }

        private void UpdatePhaseProgress()
        {
            foreach (var phase in phaseProgress.Keys.ToList())
            {
                if (completedPhases[phase]) continue;
                
                phaseProgress[phase] = CalculatePhaseProgress(phase);
                
                if (phaseProgress[phase] >= 1.0f)
                {
                    MarkPhaseComplete(phase);
                }
            }
        }

        private float CalculatePhaseProgress(TutorialPhase phase)
        {
            return phase switch
            {
                TutorialPhase.Movement => hasMoved ? 1f : 0f,
                TutorialPhase.Interaction => hasInteracted ? 1f : 0f,
                TutorialPhase.Fishing => hasFished ? 1f : 0f,
                TutorialPhase.PrayerTimes => hasPrayed ? 1f : (playerKnowsPrayerTimes ? 0.5f : 0f),
                TutorialPhase.Combat => hasFought ? 1f : 0f,
                TutorialPhase.Vehicles => hasDriven ? 1f : 0f,
                TutorialPhase.Economy => hasEarnedMoney ? 1f : 0f,
                TutorialPhase.Gangs => hasMetGang ? 1f : 0f,
                TutorialPhase.Completed => 1f,
                _ => 0f
            };
        }

        public void StartPhase(TutorialPhase phase)
        {
            if (completedPhases[phase] || currentPhase == phase) return;
            
            currentPhase = phase;
            
            // Show tutorial prompt for this phase
            var prompt = tutorialPrompts.FirstOrDefault(p => p.phase == phase);
            if (prompt.key != null)
            {
                ShowPrompt(prompt);
            }
            
            Debug.Log($"[TutorialSystem] Started phase: {phase}");
            
            // Special handling for prayer tutorial (culturally sensitive)
            if (phase == TutorialPhase.PrayerTimes && playerKnowsPrayerTimes)
            {
                MarkPhaseComplete(phase);
                StartPhase(TutorialPhase.Fishing);
            }
        }

        private void ShowPrompt(TutorialPrompt prompt)
        {
            activePrompt = prompt;
            promptTimer = 0f;
            
            string text = LocalizationSystem.Instance?.GetCurrentLanguage() == Language.Dhivehi 
                ? prompt.textDhivehi 
                : prompt.textEn;
            
            UIManager.Instance?.ShowTutorialPrompt(text, prompt.inputAction);
            
            // Show visual highlight for input action if it's a control
            if (!string.IsNullOrEmpty(prompt.inputAction))
            {
                HighlightControl(prompt.inputAction);
            }
        }

        private void HideCurrentPrompt()
        {
            activePrompt = null;
            UIManager.Instance?.HideTutorialPrompt();
        }

        private void HighlightControl(string inputAction)
        {
            // Tell UI manager to highlight a specific control
            UIManager.Instance?.HighlightControl(inputAction);
        }

        public void MarkPhaseComplete(TutorialPhase phase)
        {
            if (completedPhases[phase]) return;
            
            completedPhases[phase] = true;
            phaseProgress[phase] = 1f;
            
            Debug.Log($"[TutorialSystem] Completed phase: {phase}");
            
            // Unlock achievement
            AchievementSystem.Instance?.UnlockAchievement($"TUTORIAL_{phase}_COMPLETE");
            
            // Determine next phase
            TutorialPhase nextPhase = GetNextPhase(phase);
            if (nextPhase != TutorialPhase.Completed)
            {
                StartPhase(nextPhase);
            }
            else
            {
                CompleteAllTutorials();
            }
        }

        private TutorialPhase GetNextPhase(TutorialPhase current)
        {
            return current switch
            {
                TutorialPhase.Movement => TutorialPhase.Interaction,
                TutorialPhase.Interaction => playerKnowsFishing ? TutorialPhase.PrayerTimes : TutorialPhase.Fishing,
                TutorialPhase.Fishing => TutorialPhase.PrayerTimes,
                TutorialPhase.PrayerTimes => TutorialPhase.Combat,
                TutorialPhase.Combat => TutorialPhase.Vehicles,
                TutorialPhase.Vehicles => TutorialPhase.Economy,
                TutorialPhase.Economy => TutorialPhase.Gangs,
                TutorialPhase.Gangs => TutorialPhase.Completed,
                _ => TutorialPhase.Completed
            };
        }

        public void CompleteAllTutorials()
        {
            foreach (TutorialPhase phase in System.Enum.GetValues(typeof(TutorialPhase)))
            {
                completedPhases[phase] = true;
                phaseProgress[phase] = 1f;
            }
            
            currentPhase = TutorialPhase.Completed;
            
            Debug.Log("[TutorialSystem] All tutorials completed");
            
            // Grant completion reward
            EconomySystem.Instance?.AddFunds(500);
            AchievementSystem.Instance?.UnlockAchievement("TUTORIAL_MASTER");
            
            UIManager.Instance?.ShowTutorialComplete();
        }

        #region Cultural Knowledge Detection
        /// <summary>
        /// Called when player demonstrates knowledge of Maldivian customs
        /// </summary>
        public void RegisterCulturalKnowledge(string knowledgeType)
        {
            switch (knowledgeType)
            {
                case "PrayerTimes":
                    playerKnowsPrayerTimes = true;
                    if (currentPhase == TutorialPhase.PrayerTimes)
                        MarkPhaseComplete(TutorialPhase.PrayerTimes);
                    break;
                    
                case "Boduberu":
                    playerKnowsBoduberu = true;
                    break;
                    
                case "Fishing":
                    playerKnowsFishing = true;
                    if (currentPhase == TutorialPhase.Fishing)
                        MarkPhaseComplete(TutorialPhase.Fishing);
                    break;
            }
        }
        #endregion

        #region Event Handlers (called by other systems)
        public void OnPlayerFished()
        {
            hasFished = true;
            RegisterCulturalKnowledge("Fishing");
        }
        
        public void OnPlayerPrayed()
        {
            hasPrayed = true;
            RegisterCulturalKnowledge("PrayerTimes");
        }
        
        public void OnPlayerDroveVehicle()
        {
            hasDriven = true;
        }
        
        public void OnPlayerEarnedMoney(int amount)
        {
            hasEarnedMoney = true;
        }
        
        public void OnPlayerMetGang(int gangId)
        {
            hasMetGang = true;
        }
        
        public void OnPlayerFought()
        {
            hasFought = true;
        }
        #endregion

        #region Public API
        public TutorialPhase GetCurrentPhase() => currentPhase;
        public float GetPhaseProgress(TutorialPhase phase) => phaseProgress[phase];
        public bool IsPhaseComplete(TutorialPhase phase) => completedPhases[phase];
        public bool AreAllTutorialsComplete() => currentPhase == TutorialPhase.Completed;
        
        public void SkipTutorialPhase(TutorialPhase phase)
        {
            MarkPhaseComplete(phase);
        }
        
        public void ResetTutorials()
        {
            InitializePhases();
            currentPhase = TutorialPhase.NotStarted;
            
            hasMoved = false;
            hasInteracted = false;
            hasFished = false;
            hasPrayed = false;
            hasFought = false;
            hasDriven = false;
            hasEarnedMoney = false;
            hasMetGang = false;
            
            StartPhase(TutorialPhase.Movement);
        }
        #endregion
    }
}
