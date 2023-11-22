using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.GameElements;
using UnityEngine;
using System;

/////////////////////////////////////////////////////////////////////////////
// This is Celeste Burel's Agent
/////////////////////////////////////////////////////////////////////////////

namespace GameManager
{
    ///<summary>Planning Agent is the over-head planner that decided where
    /// individual units go and what tasks they perform.  Low-level 
    /// AI is handled by other classes (like pathfinding).
    ///</summary> 
    public class PlanningAgent : Agent
    {
        private const int MAX_NBR_WORKERS = 20;
        private PlanningAgent.AgentState currentState;
        private const int maxWorkers = 15;
        private const int minWorkers = 5;
        private const int minTroops = 7;
        private const int maxArchers = 10;
        private const int minArchers = 5;
        private const int maxSoldiers = 10;
        private const int minSoldiers = 5;
        private const int maxTroops = maxArchers + maxSoldiers;
        private const int maxBases = 1;
        private const int maxBarracks = 2;
        private const int maxRefineries = 1;

        // used for learn method
        private int LEARN_MAX_WORKERS = maxWorkers;
        private int LEARN_MIN_WORKERS = minWorkers;
        private int LEARN_MAX_ARCHERS = maxArchers;
        private int LEARN_MIN_ARCHERS = minArchers;
        private int LEARN_MAX_SOLDIERS = maxSoldiers;
        private int LEARN_MIN_SOLDIERS = minSoldiers;
        private int LEARN_MAX_TROOPS = maxTroops;
        private int LEARN_MAX_BASES = maxBases;
        private int LEARN_MAX_BARRACKS = maxBarracks;
        private int LEARN_MAX_REFINERIES = maxRefineries;

        #region Private Data

        ///////////////////////////////////////////////////////////////////////
        // Handy short-cuts for pulling all of the relevant data that you
        // might use for each decision.  Feel free to add your own.
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The agent's possible states
        /// </summary>
        private enum AgentState
        {
            BUILDING_BASE,
            BUILDING_ARMY,
            WINNING,
            DEFENDING,
            RECOVERING
        }

        /// <summary>
        /// The enemy's agent number
        /// </summary>
        private int enemyAgentNbr { get; set; }

        /// <summary>
        /// My primary mine number
        /// </summary>
        private int mainMineNbr { get; set; }

        /// <summary>
        /// My primary base number
        /// </summary>
        private int mainBaseNbr { get; set; }

        /// <summary>
        /// List of all the mines on the map
        /// </summary>
        private List<int> mines { get; set; }

        /// <summary>
        /// List of all of my workers
        /// </summary>
        private List<int> myWorkers { get; set; }

        /// <summary>
        /// List of all of my soldiers
        /// </summary>
        private List<int> mySoldiers { get; set; }

        /// <summary>
        /// List of all of my archers
        /// </summary>
        private List<int> myArchers { get; set; }

        /// <summary>
        /// List of all of my bases
        /// </summary>
        private List<int> myBases { get; set; }

        /// <summary>
        /// List of all of my barracks
        /// </summary>
        private List<int> myBarracks { get; set; }

        /// <summary>
        /// List of all of my refineries
        /// </summary>
        private List<int> myRefineries { get; set; }

        /// <summary>
        /// List of the enemy's workers
        /// </summary>
        private List<int> enemyWorkers { get; set; }

        /// <summary>
        /// List of the enemy's soldiers
        /// </summary>
        private List<int> enemySoldiers { get; set; }

        /// <summary>
        /// List of enemy's archers
        /// </summary>
        private List<int> enemyArchers { get; set; }

        /// <summary>
        /// List of the enemy's bases
        /// </summary>
        private List<int> enemyBases { get; set; }

        /// <summary>
        /// List of the enemy's barracks
        /// </summary>
        private List<int> enemyBarracks { get; set; }

        /// <summary>
        /// List of the enemy's refineries
        /// </summary>
        private List<int> enemyRefineries { get; set; }

        /// <summary>
        /// List of the possible build positions for a 3x3 unit
        /// </summary>
        private List<Vector3Int> buildPositions { get; set; }

        /// <summary>
        /// Finds all of the possible build locations for a specific UnitType.
        /// Currently, all structures are 3x3, so these positions can be reused
        /// for all structures (Base, Barracks, Refinery)
        /// Run this once at the beginning of the game and have a list of
        /// locations that you can use to reduce later computation.  When you
        /// need a location for a build-site, simply pull one off of this list,
        /// determine if it is still buildable, determine if you want to use it
        /// (perhaps it is too far away or too close or not close enough to a mine),
        /// and then simply remove it from the list and build on it!
        /// This method is called from the Awake() method to run only once at the
        /// beginning of the game.
        /// </summary>
        /// <param name="unitType">the type of unit you want to build</param>
        public void FindProspectiveBuildPositions(UnitType unitType)
        {
            // For the entire map
            for (int i = 0; i < GameManager.Instance.MapSize.x; ++i)
            {
                for (int j = 0; j < GameManager.Instance.MapSize.y; ++j)
                {
                    // Construct a new point near gridPosition
                    Vector3Int testGridPosition = new Vector3Int(i, j, 0);

                    // Test if that position can be used to build the unit
                    if (Utility.IsValidGridLocation(testGridPosition)
                        && GameManager.Instance.IsBoundedAreaBuildable(unitType, testGridPosition))
                    {
                        // If this position is buildable, add it to the list
                        buildPositions.Add(testGridPosition);
                    }
                }
            }
        }

        /// <summary>
        /// Build a building
        /// </summary>
        /// <param name="unitType"></param>
        public void BuildBuilding(UnitType unitType)
        {
            // For each worker
            foreach (int worker in myWorkers)
            {
                // Grab the unit we need for this function
                Unit unit = GameManager.Instance.GetUnit(worker);

                // Make sure this unit actually exists and we have enough gold
                if (unit != null && Gold >= Constants.COST[unitType])
                {
                    // Find the closest build position to this worker's position (DUMB) and 
                    // build the base there
                    foreach (Vector3Int toBuild in buildPositions)
                    {
                        if (GameManager.Instance.IsBoundedAreaBuildable(unitType, toBuild))
                        {
                            Debug.Log("<color=green>Building!</color>");
                            Build(unit, toBuild, unitType);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attack the enemy
        /// </summary>
        /// <param name="myTroops"></param>
        public void AttackEnemy(List<int> myTroops)
        {
            if (myTroops.Count > 3)
            {
                // For each of my troops in this collection
                foreach (int troopNbr in myTroops)
                {
                    // If this troop is idle, give him something to attack
                    Unit troopUnit = GameManager.Instance.GetUnit(troopNbr);
                    if (troopUnit.CurrentAction == UnitAction.IDLE)
                    {
                        Debug.Log("<color=green>Attacking!</color>");
                        // If there are archers to attack
                        if (enemyArchers.Count > 0)
                        {
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyArchers[UnityEngine.Random.Range(0, enemyArchers.Count)]));
                        }
                        // If there are soldiers to attack
                        else if (enemySoldiers.Count > 0)
                        {
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemySoldiers[UnityEngine.Random.Range(0, enemySoldiers.Count)]));
                        }
                        // If there are workers to attack
                        else if (enemyWorkers.Count > 0)
                        {
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyWorkers[UnityEngine.Random.Range(0, enemyWorkers.Count)]));
                        }
                        // If there are bases to attack
                        else if (enemyBases.Count > 0)
                        {
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyBases[UnityEngine.Random.Range(0, enemyBases.Count)]));
                        }
                        // If there are barracks to attack
                        else if (enemyBarracks.Count > 0)
                        {
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyBarracks[UnityEngine.Random.Range(0, enemyBarracks.Count)]));
                        }
                        // If there are refineries to attack
                        else if (enemyRefineries.Count > 0)
                        {
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyRefineries[UnityEngine.Random.Range(0, enemyRefineries.Count)]));
                        }
                    }
                }
            }
            else if (myTroops.Count > 0)
            {
                // Find a good rally point
                Vector3Int rallyPoint = Vector3Int.zero;
                foreach (Vector3Int toBuild in buildPositions)
                {
                    if (GameManager.Instance.IsBoundedAreaBuildable(UnitType.BASE, toBuild))
                    {
                        rallyPoint = toBuild;
                        // For each of my troops in this collection
                        foreach (int troopNbr in myTroops)
                        {
                            // If this troop is idle, give him something to attack
                            Unit troopUnit = GameManager.Instance.GetUnit(troopNbr);
                            if (troopUnit.CurrentAction == UnitAction.IDLE)
                            {
                                Move(troopUnit, rallyPoint);
                            }
                        }
                        break;
                    }
                }
            }
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Called at the end of each round before remaining units are
        /// destroyed to allow the agent to observe the "win/loss" state
        /// </summary>
        public override void Learn()
        {
            Debug.Log("Nbr Wins: " + AgentNbrWins);

            //Debug.Log("PlanningAgent::Learn");
            Log("value 1");
            Log("value 2");
            Log("value 3a, 3b");
            Log("value 4");
        }

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds. 
        /// </summary>
        public override void InitializeMatch()
        {
            Debug.Log("Celeste's: " + AgentName);
            //Debug.Log("PlanningAgent::InitializeMatch");
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// There are multiple rounds in a single match between two agents.
        /// </summary>
        public override void InitializeRound()
        {
            //Debug.Log("PlanningAgent::InitializeRound");
            buildPositions = new List<Vector3Int>();

            FindProspectiveBuildPositions(UnitType.BASE);

            // Set the main mine and base to "non-existent"
            mainMineNbr = -1;
            mainBaseNbr = -1;

            // Initialize all of the unit lists
            mines = new List<int>();

            myWorkers = new List<int>();
            mySoldiers = new List<int>();
            myArchers = new List<int>();
            myBases = new List<int>();
            myBarracks = new List<int>();
            myRefineries = new List<int>();

            enemyWorkers = new List<int>();
            enemySoldiers = new List<int>();
            enemyArchers = new List<int>();
            enemyBases = new List<int>();
            enemyBarracks = new List<int>();
            enemyRefineries = new List<int>();
        }

        /// <summary>
        /// Updates the game state for the Agent - called once per frame for GameManager
        /// Pulls all of the agents from the game and identifies who they belong to
        /// </summary>
        public void UpdateGameState()
        {
            // Update the common resources
            mines = GameManager.Instance.GetUnitNbrsOfType(UnitType.MINE);

            // Update all of my unitNbrs
            myWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, AgentNbr);
            mySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, AgentNbr);
            myArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, AgentNbr);
            myBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, AgentNbr);
            myBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, AgentNbr);
            myRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, AgentNbr);

            // Update the enemy agents & unitNbrs
            List<int> enemyAgentNbrs = GameManager.Instance.GetEnemyAgentNbrs(AgentNbr);
            if (enemyAgentNbrs.Any())
            {
                enemyAgentNbr = enemyAgentNbrs[0];
                enemyWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, enemyAgentNbr);
                enemySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, enemyAgentNbr);
                enemyArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, enemyAgentNbr);
                enemyBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, enemyAgentNbr);
                enemyBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, enemyAgentNbr);
                enemyRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, enemyAgentNbr);
                Debug.Log("<color=red>Enemy gold</color>: " + GameManager.Instance.GetAgent(enemyAgentNbr).Gold);
            }
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update()
        {
            UpdateGameState();
            Debug.Log("<color=green>Current State:</color> " + this.currentState.ToString());

            // state machine //
            int troopsCount = this.mySoldiers.Count + this.myArchers.Count;
            int structureCount = this.myBases.Count + this.myBarracks.Count + this.myRefineries.Count;
            float shouldAttack = Mathf.Clamp(structureCount - 1, 0, 1) * Mathf.Clamp(troopsCount - minTroops, 0, 1);
            Debug.Log("shouldAttack: " + shouldAttack.ToString());
            float shouldBuildArmy = Mathf.Clamp(structureCount - 3, 0, 1) * Mathf.Clamp(minTroops - troopsCount, 0, 1);
            Debug.Log("shouldBuildArmy: " + shouldBuildArmy.ToString());

            if (this.myBases.Count == 0 && this.currentState != PlanningAgent.AgentState.BUILDING_BASE)
            {
                this.mainBaseNbr = -1;
                this.UpdateState(PlanningAgent.AgentState.BUILDING_BASE);
            }
            else if (shouldBuildArmy == 1.0)
            {
                this.UpdateState(PlanningAgent.AgentState.BUILDING_ARMY);
            }
            else if (shouldAttack == 1.0)
            {
                this.UpdateState(PlanningAgent.AgentState.WINNING);
            }
            // end state machine //


            if (this.currentState == PlanningAgent.AgentState.BUILDING_BASE)
            {
                // if mines exist
                if (mines.Count > 0)
                {
                    // if we have no main mine
                    if (this.mainMineNbr == -1)
                    {
                        // find mine with health, set it as main mine
                        for (int i = 0; i < this.mines.Count; ++i)
                        {
                            if (GameManager.Instance.GetUnit(this.mines[i]).Health > 0)
                            {
                                this.mainMineNbr = this.mines[i];
                            }
                        }
                    }
                }

                // otherwise, no mines exist
                else
                {
                    mainMineNbr = -1;
                }

                // if we have a base
                if (myBases.Count > 0)
                {
                    // assume the first base is our main base
                    mainBaseNbr = myBases[0];
                }

                float shouldBuildBase = Mathf.Clamp(maxBases - this.myBases.Count, 0, 1) * Mathf.Clamp((float)this.Gold - Constants.COST[UnitType.BASE], 0.0f, 1f);
                Debug.Log("shouldBuildBase:" + shouldBuildBase.ToString());
                float shouldBuildBarracks = Mathf.Clamp(maxBarracks - this.myBarracks.Count, 0, 1) * Mathf.Clamp((float)this.Gold - Constants.COST[UnitType.BARRACKS], 0.0f, 1f);
                Debug.Log("shouldBuildBarracks:" + shouldBuildBarracks.ToString());
                float shouldBuildRefinery = Mathf.Clamp(maxRefineries - this.myRefineries.Count, 0, 1) * Mathf.Clamp((float)this.Gold - Constants.COST[UnitType.REFINERY], 0.0f, 1f);
                Debug.Log("shouldBuildRefinery:" + shouldBuildRefinery.ToString());

                // if we have no base, build one
                if (shouldBuildBase == 1.0)
                {
                    this.BuildBuilding(UnitType.BASE);
                }

                // if we need barracks or refineries and have the appropriate dependency, build them
                else if (shouldBuildBarracks == 1.0 && this.myBases.Count > 0) {
                    this.BuildBuilding(UnitType.BARRACKS);
                }
                else if (shouldBuildRefinery == 1.0 && this.myBarracks.Count > 0) {
                    this.BuildBuilding(UnitType.REFINERY);
                }

                this.DoWork();
            }

            if (this.currentState == PlanningAgent.AgentState.BUILDING_ARMY)
            {
                this.DoWork();
            }


            if (this.currentState == PlanningAgent.AgentState.WINNING)
            {
                AttackEnemy(mySoldiers);
                AttackEnemy(myArchers);
                this.DoWork();
            }

        }

        /// <summary>
        /// handles gathering and training
        /// </summary>
        private void DoWork() {

            // set workers to gather
            foreach (int worker in myWorkers)
            {
                Unit w = GameManager.Instance.GetUnit(worker);
                float shouldGather = (!w.Equals(null) ? 1 : 0) * (w.CurrentAction == UnitAction.IDLE ? 1 : 0) * Mathf.Clamp(this.mainBaseNbr + 1, 0, 1) * Mathf.Clamp(this.mainMineNbr + 1, 0, 1);
                if (shouldGather == 1.0)
                {
                    Unit m = GameManager.Instance.GetUnit(this.mainMineNbr);
                    Unit b = GameManager.Instance.GetUnit(this.mainBaseNbr);
                    if (!m.Equals(null) && !b.Equals(null) && b.Health > 0.0)
                        this.Gather(w, m, b);
                }
            }

            // set soldiers and archers to train
            foreach (int barrack in this.myBarracks)
            {
                Unit b = GameManager.Instance.GetUnit(barrack);

                // if we have at least one soldier, save gold to build refinery.
                // if we have no soldiers, train one.
                if (mySoldiers.Count < minSoldiers || myArchers.Count < minArchers || myRefineries.Count > 0) {
                    float trainArcher = (!b.Equals(null) ? 1 : 0) * (b.IsBuilt ? 1 : 0) * (b.CurrentAction == UnitAction.IDLE ? 1 : 0) * (Mathf.Clamp(maxArchers - myArchers.Count, 0, 1)) * Mathf.Clamp((float)this.Gold - Constants.COST[UnitType.ARCHER], 0.0f, 1f);
                    Debug.Log("trainArcher:" + trainArcher.ToString() + ", <color=red>numArchers: </color>" + myArchers.Count.ToString());
                    if (trainArcher == 1.0)
                    {
                        this.Train(b, UnitType.ARCHER);
                    }

                    float trainSoldier = (!b.Equals(null) ? 1 : 0) * (b.IsBuilt ? 1 : 0) * (b.CurrentAction == UnitAction.IDLE ? 1 : 0) * (Mathf.Clamp(maxSoldiers - mySoldiers.Count, 0, 1)) * Mathf.Clamp((float)this.Gold - Constants.COST[UnitType.SOLDIER], 0.0f, 1f);
                    Debug.Log("trainSoldier:" + trainSoldier.ToString());
                    if (trainSoldier == 1.0)
                    {
                        this.Train(b, UnitType.SOLDIER);
                    }
                }
            }

            // set bases to train workers
            foreach (int myBase in this.myBases)
            {
                if (myWorkers.Count < minWorkers || (mySoldiers.Count >= maxSoldiers && myArchers.Count >= maxArchers)) {
                    Unit b = GameManager.Instance.GetUnit(myBase);
                    float trainWorker = (!b.Equals(null) ? 1 : 0) * (b.CurrentAction == UnitAction.IDLE ? 1 : 0) * Mathf.Clamp((float)this.Gold - Constants.COST[UnitType.WORKER], 0.0f, 1f) * Mathf.Clamp(maxWorkers - myWorkers.Count, 0, 1);
                    Debug.Log("trainWorker:" + trainWorker.ToString());
                    if (trainWorker == 1.0)
                    {
                        this.Train(b, UnitType.WORKER);
                    }
                }
            }

        }

        /// <summary>
        /// prints state of state machine
        /// </summary>
        /// <param name="newState"></param>
        private void UpdateState(PlanningAgent.AgentState newState)
        {
            Debug.Log("<color=green>Exiting State: </color>" + this.currentState.ToString());
            this.currentState = newState;
            Debug.Log("<color=green>Entering State: </color>" + this.currentState.ToString());
        }

        #endregion
    }
}