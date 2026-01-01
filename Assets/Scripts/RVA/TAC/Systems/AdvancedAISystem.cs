using UnityEngine;
using System.Collections.Generic;

public class AdvancedAISystem : MonoBehaviour
{
    public static AdvancedAISystem Instance { get; private set; }

    private List<AIAgent> agents = new List<AIAgent>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void RegisterAgent(AIAgent agent)
    {
        if (!agents.Contains(agent))
        {
            agents.Add(agent);
        }
    }

    public void UnregisterAgent(AIAgent agent)
    {
        if (agents.Contains(agent))
        {
            agents.Remove(agent);
        }
    }

    private void Update()
    {
        foreach (AIAgent agent in agents)
        {
            agent.UpdateState();
        }
    }
}

public abstract class AIAgent : MonoBehaviour
{
    public enum AIState
    {
        Idle,
        Patrol,
        React,
        Attack,
        Flee
    }

    public AIState currentState = AIState.Idle;

    protected virtual void Start()
    {
        AdvancedAISystem.Instance.RegisterAgent(this);
    }

    protected virtual void OnDestroy()
    {
        if (AdvancedAISystem.Instance != null)
        {
            AdvancedAISystem.Instance.UnregisterAgent(this);
        }
    }

    public void UpdateState()
    {
        switch (currentState)
        {
            case AIState.Idle:
                Idle();
                break;
            case AIState.Patrol:
                Patrol();
                break;
            case AIState.React:
                React();
                break;
            case AIState.Attack:
                Attack();
                break;
            case AIState.Flee:
                Flee();
                break;
        }
    }

    protected abstract void Idle();
    protected abstract void Patrol();
    protected abstract void React();
    protected abstract void Attack();
    protected abstract void Flee();
}
