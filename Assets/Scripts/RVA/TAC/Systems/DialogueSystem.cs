using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RVA.TAC.Cultural;
using RVA.TAC.NPC;

namespace RVA.TAC.Dialogue
{
    /// <summary>
    /// Dynamic dialogue system with Dhivehi language support and reputation-based branching
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        #region Singleton
        private static DialogueSystem instance;
        public static DialogueSystem Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<DialogueSystem>();
                return instance;
            }
        }
        #endregion

        #region Dialogue Data Structure
        [System.Serializable]
        public class DialogueNode
        {
            public string nodeId;
            public string speakerNameEn;
            public string speakerNameDhivehi;
            public string textEn;
            public string textDhivehi;
            public string[] nextNodeIds;
            public DialogueCondition[] conditions;
            public DialogueAction[] onSelectActions;
            public bool isPlayerChoice;
        }
        
        [System.Serializable]
        public class DialogueCondition
        {
            public ConditionType type;
            public string parameter;
            public int requiredValue;
            
            public enum ConditionType
            {
                Reputation,
                MissionComplete,
                ItemOwned,
                TimeOfDay,
                PrayerTime,
                GangAffiliation,
                IslandVisited
            }
        }
        
        [System.Serializable]
        public class DialogueAction
        {
            public ActionType type;
            public string parameter;
            public int value;
            
            public enum ActionType
            {
                GiveItem,
                TakeItem,
                ModifyReputation,
                StartMission,
                CompleteObjective,
                TriggerEvent
            }
        }
        
        private Dictionary<string, DialogueNode> dialogueTrees = new Dictionary<string, DialogueNode>();
        #endregion

        #region Active Dialogue State
        private string currentDialogueId;
        private DialogueNode currentNode;
        private int currentSpeakerId;
        private bool isDialogueActive = false;
        
        private Queue<DialogueNode> dialogueHistory = new Queue<DialogueNode>();
        private const int MAX_HISTORY_NODES = 20;
        #endregion

        # cultural Integration
        private ReputationSystem reputationSystem;
        private PrayerTimeSystem prayerSystem;
        private TimeSystem timeSystem;
        #endregion

        #region Accessibility
        public float textDisplaySpeed = 0.03f; // Per-character delay
        public bool skipFastForward = true;
        private bool isSkipping = false;
        #endregion

        private void Start()
        {
            reputationSystem = ReputationSystem.Instance;
            prayerSystem = PrayerTimeSystem.Instance;
            timeSystem = TimeSystem.Instance;
            
            InitializeSampleDialogues();
        }

        private void InitializeSampleDialogues()
        {
            // Sample: Fisherman NPC dialogue
            var rootNode = new DialogueNode
            {
                nodeId = "FISHERMAN_001_GREETING",
                speakerNameEn = "Khalid the Fisherman",
                speakerNameDhivehi = "މަސްވެރިޔާ ޚާލިދު",
                textEn = "Alifaan! The tuna are running, but those resort boats are in our spot again...",
                textDhivehi = "އަލިފާން! މަސް ހަރުކުރަނީ، އެނޫން ރިސޯޓްތަކުގެ ބޯޓުތައް އަންނަ ސަރަހައްދުގައި...",
                nextNodeIds = new[] { "FISHERMAN_001_RESPONSE_1", "FISHERMAN_001_RESPONSE_2" },
                isPlayerChoice = false
            };
            
            dialogueTrees[rootNode.nodeId] = rootNode;
            
            // Player choice 1: Help
            var helpNode = new DialogueNode
            {
                nodeId = "FISHERMAN_001_RESPONSE_1",
                speakerNameEn = "Player",
                speakerNameDhivehi = "ކުޅިންލަނޑު",
                textEn = "I'll help you defend your fishing grounds.",
                textDhivehi = "މަސް ހަވަރު ރައްކާތެރިކުރުމަށް ހިޔަވަނީ.",
                nextNodeIds = new[] { "FISHERMAN_001_ACCEPT" },
                isPlayerChoice = true,
                onSelectActions = new[]
                {
                    new DialogueAction { type = DialogueAction.ActionType.StartMission, parameter = "FISH_DISPUTE_001" }
                },
                conditions = new[]
                {
                    new DialogueCondition { type = DialogueCondition.ConditionType.Reputation, parameter = "FISHERMEN_GANG", requiredValue = -20 }
                }
            };
            
            dialogueTrees[helpNode.nodeId] = helpNode;
            
            // Player choice 2: Decline
            var declineNode = new DialogueNode
            {
                nodeId = "FISHERMAN_001_RESPONSE_2",
                speakerNameEn = "Player",
                speakerNameDhivehi = "ކުޅިންލަނޑު",
                textEn = "I can't get involved right now.",
                textDhivehi = "މިހާރު ޝާމިލްވެވޭ ގޮތެއް ނެތް.",
                nextNodeIds = new[] { "FISHERMAN_001_DECLINE" },
                isPlayerChoice = true
            };
            
            dialogueTrees[declineNode.nodeId] = declineNode;
            
            Debug.Log($"[DialogueSystem] Initialized with {dialogueTrees.Count} dialogue nodes");
        }

        /// <summary>
        /// Start dialogue with an NPC
        /// </summary>
        public void StartDialogue(string dialogueId, int npcId)
        {
            if (!dialogueTrees.ContainsKey(dialogueId))
            {
                Debug.LogWarning($"[DialogueSystem] Dialogue ID not found: {dialogueId}");
                return;
            }
            
            currentDialogueId = dialogueId;
            currentSpeakerId = npcId;
            isDialogueActive = true;
            
            // Transition audio state
            AudioSystem.Instance?.SetAudioState(AudioSystem.AudioState.Dialogue);
            
            // Show first valid node
            var rootNode = dialogueTrees[dialogueId];
            ShowDialogueNode(rootNode);
            
            Debug.Log($"[DialogueSystem] Started dialogue: {dialogueId}");
        }

        /// <summary>
        /// Display a dialogue node with typewriter effect
        /// </summary>
        private void ShowDialogueNode(DialogueNode node)
        {
            if (node == null) return;
            
            currentNode = node;
            
            // Check conditions first
            if (!MeetsAllConditions(node))
            {
                // Skip to next available node
                var nextNode = FindNextValidNode(node);
                if (nextNode != null)
                {
                    ShowDialogueNode(nextNode);
                    return;
                }
                else
                {
                    EndDialogue();
                    return;
                }
            }
            
            // Display text (supporting both English and Dhivehi)
            string displayText = LocalizationSystem.Instance?.GetCurrentLanguage() == Language.Dhivehi 
                ? node.textDhivehi 
                : node.textEn;
            
            string speakerName = LocalizationSystem.Instance?.GetCurrentLanguage() == Language.Dhivehi
                ? node.speakerNameDhivehi
                : node.speakerNameEn;
            
            // Start typewriter effect
            StartCoroutine(TypewriterEffect(displayText, speakerName, node.isPlayerChoice));
            
            // Show UI
            UIManager.Instance?.ShowDialogueUI(node, speakerName);
            
            // Add to history
            dialogueHistory.Enqueue(node);
            if (dialogueHistory.Count > MAX_HISTORY_NODES)
                dialogueHistory.Dequeue();
        }

        private System.Collections.IEnumerator TypewriterEffect(string text, string speaker, bool isPlayerChoice)
        {
            isSkipping = false;
            float elapsed = 0f;
            int visibleChars = 0;
            
            while (visibleChars < text.Length && !isSkipping)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= textDisplaySpeed)
                {
                    elapsed = 0f;
                    visibleChars++;
                    UIManager.Instance?.UpdateDialogueText(text.Substring(0, visibleChars));
                }
                yield return null;
            }
            
            // Show full text if skipped
            UIManager.Instance?.UpdateDialogueText(text);
            
            // Enable choice buttons if player choice node
            if (isPlayerChoice)
            {
                DisplayChoiceButtons();
            }
        }

        private void DisplayChoiceButtons()
        {
            var choiceNodes = currentNode.nextNodeIds
                .Where(id => dialogueTrees.ContainsKey(id))
                .Select(id => dialogueTrees[id])
                .Where(node => MeetsAllConditions(node))
                .ToList();
            
            UIManager.Instance?.ShowDialogueChoices(choiceNodes);
        }

        /// <summary>
        /// Select a dialogue choice and proceed
        /// </summary>
        public void SelectDialogueChoice(string nextNodeId)
        {
            if (!isDialogueActive || !dialogueTrees.ContainsKey(nextNodeId)) return;
            
            var nextNode = dialogueTrees[nextNodeId];
            
            // Execute any actions attached to this choice
            ExecuteDialogueActions(currentNode);
            
            // Show next node
            ShowDialogueNode(nextNode);
        }

        private void ExecuteDialogueActions(DialogueNode node)
        {
            if (node.onSelectActions == null) return;
            
            foreach (var action in node.onSelectActions)
            {
                ExecuteAction(action);
            }
        }

        private void ExecuteAction(DialogueAction action)
        {
            switch (action.type)
            {
                case DialogueAction.ActionType.GiveItem:
                    InventorySystem.Instance?.AddItem(action.parameter, action.value);
                    break;
                    
                case DialogueAction.ActionType.TakeItem:
                    InventorySystem.Instance?.RemoveItem(action.parameter, action.value);
                    break;
                    
                case DialogueAction.ActionType.ModifyReputation:
                    reputationSystem?.ModifyReputation(int.Parse(action.parameter), action.value);
                    break;
                    
                case DialogueAction.ActionType.StartMission:
                    MissionSystem.Instance?.StartMission(action.parameter);
                    break;
                    
                case DialogueAction.ActionType.CompleteObjective:
                    // Find active mission with this objective
                    foreach (var mission in MissionSystem.Instance?.GetActiveMissions() ?? new List<MissionSystem.ActiveMission>())
                    {
                        MissionSystem.Instance?.CompleteObjective(mission.missionId, action.parameter);
                    }
                    break;
                    
                case DialogueAction.ActionType.TriggerEvent:
                    // Trigger game event
                    GameSceneManager.Instance?.TriggerDialogueEvent(action.parameter);
                    break;
            }
        }

        private bool MeetsAllConditions(DialogueNode node)
        {
            if (node.conditions == null || node.conditions.Length == 0) return true;
            
            foreach (var condition in node.conditions)
            {
                if (!EvaluateCondition(condition)) return false;
            }
            
            return true;
        }

        private bool EvaluateCondition(DialogueCondition condition)
        {
            switch (condition.type)
            {
                case DialogueCondition.ConditionType.Reputation:
                    int rep = reputationSystem?.GetReputation(int.Parse(condition.parameter)) ?? 0;
                    return rep >= condition.requiredValue;
                    
                case DialogueCondition.ConditionType.MissionComplete:
                    return MissionSystem.Instance?.GetMissionHistory().Any(m => m.missionId == condition.parameter && m.state == MissionSystem.MissionState.Completed) ?? false;
                    
                case DialogueCondition.ConditionType.ItemOwned:
                    return InventorySystem.Instance?.HasItem(condition.parameter) ?? false;
                    
                case DialogueCondition.ConditionType.TimeOfDay:
                    float hour = timeSystem?.GetCurrentHour() ?? 12f;
                    return hour >= condition.requiredValue;
                    
                case DialogueCondition.ConditionType.PrayerTime:
                    return prayerSystem?.IsPrayerTimeNow() ?? false;
                    
                case DialogueCondition.ConditionType.GangAffiliation:
                    return reputationSystem?.GetGangAffiliation() == condition.requiredValue;
                    
                case DialogueCondition.ConditionType.IslandVisited:
                    return islandGenerator?.GetDiscoveredIslandCount() >= condition.requiredValue;
                    
                default:
                    return true;
            }
        }

        private DialogueNode FindNextValidNode(DialogueNode node)
        {
            if (node.nextNodeIds == null) return null;
            
            foreach (var nextId in node.nextNodeIds)
            {
                if (dialogueTrees.ContainsKey(nextId))
                {
                    var nextNode = dialogueTrees[nextId];
                    if (MeetsAllConditions(nextNode))
                        return nextNode;
                }
            }
            
            return null;
        }

        public void SkipDialogue()
        {
            if (skipFastForward)
                isSkipping = true;
        }

        public void EndDialogue()
        {
            isDialogueActive = false;
            currentDialogueId = null;
            currentNode = null;
            
            // Revert audio state
            AudioSystem.Instance?.RevertToPreviousState();
            
            UIManager.Instance?.HideDialogueUI();
            
            Debug.Log("[DialogueSystem] Dialogue ended");
        }

        #region Public API
        public bool IsDialogueActive() => isDialogueActive;
        public DialogueNode GetCurrentNode() => currentNode;
        
        public void AddDialogueTree(string rootId, List<DialogueNode> nodes)
        {
            foreach (var node in nodes)
            {
                dialogueTrees[node.nodeId] = node;
            }
        }
        
        public void ClearDialogueHistory()
        {
            dialogueHistory.Clear();
        }
        #endregion
    }
}
