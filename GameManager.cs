using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using Random = UnityEngine.Random;

namespace GamePlay {

    public class GameManager : MonoBehaviour, ITurnManagerCallbacks {

        #region Custom Fields

        public enum GameMode {
            classic, modern
        }

        public SpaceManager[,] gameBoard = new SpaceManager[8, 8];
        public static GameManager instance;
        public static GameMode gameMode;

        [Header("Set-up References")]
        [SerializeField] private GameObject[] unitPrefabs = null;
        [SerializeField] private RectTransform unitParent = null;
        [SerializeField] private GameObject boardPrefab = null;
        [SerializeField] private RectTransform boardParent = null;
        [Header("HUD References")]
        [SerializeField] private TextMeshProUGUI turnDisplayerText = null;
        [SerializeField] private GameObject menuButton = null;
        [SerializeField] private Button suggestMoveButton = null;
        [SerializeField] private GameObject returnToMainButton = null;
        public UnitExplainer unitExplainer = null;
        [Header("Behaviour Controllers")]
        [SerializeField] private TurnManager turnManager = null;
        public MiniMaxController miniMaxController;
        [SerializeField] private AIController aiController = null;

        public readonly List<GamePiece> allUnits = new List<GamePiece>();

        private Vector2Int[] suggestedMoves = new Vector2Int[2];

        private readonly int[,] classicArmy = new int[3, 8] {
            { 0, 1, 0, 1, 0, 1, 0, 1 },
            { 6, 0, 3, 0, 6, 0, 3, 0 },
            { 0, 7, 0, 5, 0, 7, 0, 4 }
        };

        [HideInInspector] public bool suggestedMovesHighlighted = false;

        [HideInInspector] public bool gameOver = false;

        #endregion

        #region Public Properties

        public static bool MyTurn => instance.turnManager.myTurn;

        public IEnumerable<GamePiece> FriendlyUnits => allUnits.Where(x => x.friendly);

        public IEnumerable<GamePiece> EnemyUnits => allUnits.Where(x => !x.friendly);

        #endregion

        #region MonoBehaviour Callbacks

        private void Start() {
            instance = this;
            switch (gameMode) {
                case GameMode.classic:
                    SetUpBoard(classicArmy);
                    break;
                case GameMode.modern:
                    SetUpBoard(ModernArmy());
                    break;
                default: throw new NotImplementedException();
            }
            turnManager.turnManagerListener = this;
            BeginTurn(0);
        }

        #endregion

        #region ITurnManager Callbacks
        /// <summary>
        /// Begin's the turn for the desired player.
        /// </summary>
        /// <param name="turn"> Number of turn. </param>
        public void BeginTurn(int turn) {
            turnDisplayerText.text = MyTurn ? "Your Turn!" : "Your Opponent's Turn!";
            suggestMoveButton.interactable = MyTurn;
            if (MyTurn) CalculateSuggestedMove();
            else aiController.BeginTurn();
        }

        #endregion

        #region Custom Methods

        /// <summary>
        /// Uses Mini-Max to calculate the best move.
        /// </summary>
        private void CalculateSuggestedMove() => suggestedMoves = miniMaxController.FindBestMove(true);
        /// <summary>
        /// Checks if opponent controls any generals.
        /// </summary>
        /// <param name = "player"> Was this called by local player?</param>
        /// <returns> Has local player won?</returns>
        private bool CheckForVictory(bool player) {
            bool victory = true;
            var list = player ? EnemyUnits : FriendlyUnits;
            foreach (var unit in list) {
                if (unit.unitType is UnitType.General) {
                    victory = false;
                    break;
                }
            }
            return victory;
        }        
        /// <summary>
        /// Ends the game.
        /// </summary>
        /// <param name="victory"> True if local player won. </param>
        private void EndGame(bool victory) {
            gameOver = true;
            SpaceManager.DisableSelection();
            menuButton.SetActive(false);
            returnToMainButton.SetActive(true);
            suggestMoveButton.gameObject.SetActive(false);
            if (victory) turnDisplayerText.text = "Congratualations, you've won!";
            else turnDisplayerText.text = "You lost. Better luck next time...";
        }
        /// <summary>
        /// Sets up the board for the game.
        /// </summary>
        /// <param name="army"> Which army to use.</param>
        private void SetUpBoard(int[,] army) {
            CreateBoard();
            LoadArmy(army);
            LoadArmy(MirroredArmy(army), false);
        }
        /// <summary>
        /// Generates the 8x8-Board.
        /// </summary>
        private void CreateBoard() {
            bool gray = false;
            for (int i = 0; i < 8; i++) {
                for (int foo = 0; foo < 8; foo++) {
                    SpaceManager clone = Instantiate(boardPrefab, boardParent).GetComponent<SpaceManager>();
                    clone.position = new Vector2Int(i, foo);
                    gameBoard[i, foo] = clone;
                    if (gray) clone.GetComponent<Image>().color = Color.gray;
                    clone.gameObject.name = $"Boardspace [{i}, {foo}]";
                    gray = !gray;
                }
                gray = !gray;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(boardParent);
        }
        /// <summary>
        /// Spawns a player's army.
        /// </summary>
        /// <param name="army"> Which army to spawn. </param>
        /// <param name="friendly"> Is the army controlled by local player? </param>
        private void LoadArmy(int[,] army, bool friendly = true) {
            int friendlyBonus = friendly ? 5 : 0;
            for (int x = 0; x < 3; x++) {
                for (int y = 0; y < 8; y++) {
                    var num = army[x, y];
                    if (num == 0) continue;
                    SpawnUnit(num - 1, new Vector2Int(x + friendlyBonus, y), friendly);
                }
            }
        }
        /// <summary>
        /// Randomly generates an army for Modern play.
        /// </summary>
        /// <returns> A randomly generated army, in the form of a 2d int-array.</returns>
        private int[,] ModernArmy () {
            var foo = new List<int>();
            int generalCount = Random.Range(2, 4);
            //How many Generals this game?
            int soldierCount = Random.Range(4, 9);
            //How many Soldiers this game?
            int restCount = 12 - soldierCount - generalCount;
            for (int i = 0; i < restCount; i++) {
                int newUnit = Random.Range(4, 8);
                foo.Add(newUnit);
            }
            for (int i = 0; i < soldierCount; i++) foo.Add(1);
            foo.Shuffle();
            for (int i = 0; i < generalCount; i++) foo.Add(3);
            foo.Reverse();
            var obj = foo.GetRange(0, 8).ToList();
            obj.Shuffle();
            foo = foo.GetRange(8, 4).Concat(obj).ToList();
            var res = new int[3, 8] {
                { foo[0], 0 , foo[1], 0, foo[2], 0, foo[3], 0 },
                { 0 , foo[4], 0, foo[5], 0, foo[6], 0, foo[7]  },
                { foo[8], 0, foo[9], 0, foo[10], 0, foo[11], 0  }
            };
            return res;
        }
        /// <summary>
        /// Mirrors an army (swaps rows 1 and 3).
        /// </summary>
        /// <param name="army"> Army to be mirrored. </param>
        /// <returns> The mirrored army. </returns>
        private int[,] MirroredArmy(int[,] army) {
            var row1 = army.GetRow(0);
            var row2 = army.GetRow(1);
            var row3 = army.GetRow(2);
            int[,] res = {
                { row3[0], row3[1], row3[2], row3[3], row3[4], row3[5], row3[6], row3[7] },
                { row2[0], row2[1], row2[2], row2[3], row2[4], row2[5], row2[6], row2[7] },
                { row1[0], row1[1], row1[2], row1[3], row1[4], row1[5], row1[6], row1[7] },
            };
            return res;
        }
        /// <summary>
        /// Spawns a unit.
        /// </summary>
        /// <param name="unitIndex">Which type of unit, indexed from unit Prefabs</param>
        /// <param name="pos">The spawn position</param>
        /// <param name="friendly">Is the unit friendly</param>
        private void SpawnUnit(int unitIndex, Vector2Int pos, bool friendly) {
            SpaceManager space = gameBoard[pos.x, pos.y];
            GamePiece clone = Instantiate(unitPrefabs[unitIndex], unitParent).GetComponent<GamePiece>();
            space.OccupiedPiece = clone;
            allUnits.Add(clone);
            clone.friendly = friendly;
            if (clone.friendly) clone.GetComponent<Image>().color = Color.blue;
        }
        /// <summary>
        /// Concedes the game.
        /// </summary>
        public void ConcedeGame() => EndGame(false);
        /// <summary>
        /// Calls Turn Manager's End Turn function.
        /// </summary>
        public void EndTurn(bool player = true) {
            if (!CheckForVictory(player)) turnManager.EndTurn(player);
            else EndGame(player);
        }
        /// <summary>
        /// Suggests a move for the player.
        /// </summary>
        public void ShowSuggestedMove() {
            if (!MyTurn || gameOver) return;
            if (suggestedMovesHighlighted) {
                SpaceManager.DisableSelection();
                return;
            }
            SpaceManager.DisableSelection();
            suggestedMovesHighlighted = true;
            SpaceManager unitSpace = gameBoard[suggestedMoves[0].x, suggestedMoves[0].y];
            SpaceManager moveSpace = gameBoard[suggestedMoves[1].x, suggestedMoves[1].y];
            SpaceManager.selectedSpace = unitSpace;
            unitSpace.Highlighted = true;
            moveSpace.Highlighted = true;
        }

        #endregion

    }
}