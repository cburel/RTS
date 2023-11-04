using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.GameElements;
using UnityEngine;
using System;

/////////////////////////////////////////////////////////////////////////////
// This is the Moron Agent
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
            Debug.Log("Moron's: " + AgentName);
            Debug.Log("<color=red>CBurel</color>");
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

            switch (this.currentState)
            {
                case PlanningAgent.AgentState.BUILDING_BASE:
                    if (this.myBases.Count <= 0 || this.myRefineries.Count <= 0 || this.myBarracks.Count <= 0)
                    {
                        Debug.Log("<color=red>Stay in BUILDING_BASE</color>");
                        break;
                    }
                    Debug.Log("<color=red>Move to BUILDING_ARMY</color>");
                    this.UpdateState(PlanningAgent.AgentState.BUILDING_ARMY);
                    break;

                case PlanningAgent.AgentState.BUILDING_ARMY:
                    if (this.myBases.Count == 0 || this.myRefineries.Count == 0 || this.myBarracks.Count == 0)
                    {
                        Debug.Log("<color=red>Move to BUILDING_BASE</color>");
                        this.UpdateState(PlanningAgent.AgentState.BUILDING_BASE);
                        break;
                    }
                    if (this.myArchers.Count + this.mySoldiers.Count <= 5 && this.enemyWorkers.Count != 0 && this.mines.Count != 0)
                    {
                        Debug.Log("<color=red>Stay in BUILDING_ARMY</color>");
                        break;
                    }
                    Debug.Log("<color=red>Move to WINNING</color>");
                    this.UpdateState(PlanningAgent.AgentState.WINNING);
                    break;

                case PlanningAgent.AgentState.WINNING:
                    if (this.myArchers.Count + this.mySoldiers.Count >= this.enemyArchers.Count + this.enemySoldiers.Count)
                    {
                        Debug.Log("<color=red>Stay in WINNING</color>");
                        break;
                    }
                    Debug.Log("<color=red>Move to BUILDING_ARMY</color>");
                    this.UpdateState(PlanningAgent.AgentState.BUILDING_ARMY);
                    break;

                default:
                    Debug.Log("<color=red>Default state: BUILDING_BASE</color>");
                    this.UpdateState(PlanningAgent.AgentState.BUILDING_BASE);
                    break;
            }
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update()
        {
            UpdateGameState();
            Debug.Log("<color=green>Current State:</color> " + this.currentState.ToString());

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

                // if we have no base, build one
                if (this.myBases.Count <= 0 && this.Gold >= Constants.COST[UnitType.BASE])
                {
                    this.BuildBuilding(UnitType.BASE);

                    // assume the first base is our main base
                    mainBaseNbr = myBases[0];
                }

                // if we need barracks or refineries, build them
                else if (this.myBarracks.Count < 2 && this.Gold >= Constants.COST[UnitType.BARRACKS]) {
                    this.BuildBuilding(UnitType.BARRACKS);
                }
                else if (this.myRefineries.Count < 1 && (double)this.Gold >= (double)Constants.COST[UnitType.REFINERY]) {
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

        // handles gathering and training
        private void DoWork() {

            // set workers to gather
            foreach (int worker in myWorkers)
            {
                Unit w = GameManager.Instance.GetUnit(worker);
                if (!w.Equals(null) && w.CurrentAction == null && this.mainBaseNbr >= 0 && this.mainMineNbr >= 0)
                {
                    Unit m = GameManager.Instance.GetUnit(this.mainMineNbr);
                    Unit b = GameManager.Instance.GetUnit(this.mainBaseNbr);
                    if (!m.Equals(null) && !b.Equals(null) && b.Health > 0.0)
                        this.Gather(w, m, b);
                }
            }

            // set soldiers and archers to train
            bool trainArcher = false;
            foreach (int barrack in this.myBarracks)
            {
                Unit b = GameManager.Instance.GetUnit(barrack);
                if (!b.Equals(null) && b.IsBuilt && b.CurrentAction == null && this.Gold >= Constants.COST[UnitType.SOLDIER] && !trainArcher)
                {
                    trainArcher = true;
                    this.Train(b, UnitType.SOLDIER);
                }
                if (!b.Equals(null) && b.IsBuilt && b.CurrentAction == null && this.Gold >= Constants.COST[UnitType.ARCHER] && trainArcher)
                {
                    trainArcher = false;
                    this.Train(b, UnitType.ARCHER);
                }
            }

            // set bases to train workers
            foreach (int myBase in this.myBases)
            {
                Unit b = GameManager.Instance.GetUnit(myBase);
                if (!b.Equals(null) && b.IsBuilt && b.CurrentAction == null && this.Gold >= Constants.COST[UnitType.WORKER] && this.myWorkers.Count < 10)
                    this.Train(b, UnitType.WORKER);
            }

        }

        private void UpdateState(PlanningAgent.AgentState newState)
        {
            Debug.Log("<color=green>Exiting State: </color>" + this.currentState.ToString());
            this.currentState = newState;
            Debug.Log("<color=green>Entering State: </color>" + this.currentState.ToString());
        }

        #endregion
    }
}