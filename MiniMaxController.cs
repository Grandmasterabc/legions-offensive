using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace GamePlay {
    public class MiniMaxController : MonoBehaviour {

        #region Public Fields

        [SerializeField] private int maxDepth = 3;
        [SerializeField] private int quiesceDepth = 3;
        [SerializeField] private long timeLimit = 1000;
        public static Zobrist zobrist = new Zobrist();
        private int depth = 3;

        public Dictionary<long, TranspositionTableEntry> transpositionTable = new Dictionary<long, TranspositionTableEntry>();
        // Killer Moves[ply, slot]
        public Vector2Int[,][] killerMoves;
        // History Moves [FromPosX, FromPosY, ToPosX, ToPosY]
        public int[,,,] historyMoves;

        public class TranspositionTableEntry {
            /// <summary>
            /// The depth of this search.
            /// </summary>
            public int depth;
            /// <summary>
            /// Flag of node.
            /// </summary>
            public Flag flag;
            /// <summary>
            /// Value of node.
            /// </summary>
            public float value;
            /// <summary>
            /// Should this be overridden?
            /// </summary>
            public bool ancient = true;
            /// <summary>
            /// PV Move is saved here.
            /// </summary>
            public Vector2Int[] pvMove = null;
            /// <summary>
            /// Enum for the different flags.
            /// </summary>
            public enum Flag { 
                EXACT, UPPERBOUND, LOWERBOUND
            }
        }

        #endregion

        #region MonoBehaviour Callbacks

        public void Awake() {
            zobrist = new Zobrist();
            zobrist.FillZobristArray();
            historyMoves = new int[8, 8, 8, 8];
            killerMoves = new Vector2Int[maxDepth + 1, 2][];
        }

        #endregion

        #region Custom Methods
        /// <summary>
        /// Recreates the current gamestate in SpaceDataHandlers
        /// </summary>
        /// <param name="player"> Determines who's view: Player's or AI's.</param>
        /// <returns>A reenactment of the current gameboard.</returns>
        public GameStateReader CurrentGameState(bool player) {
            var res = new GameStateReader();
            var gameBoard = GameManager.instance.gameBoard;
            for (int i = 0; i < 8; i++) {
                for (int j = 0; j < 8; j++) {
                    var foo = gameBoard[i, j];
                    if (foo.Occupied) {
                        bool friendly = player ? foo.Friendly : !foo.Friendly;
                        res.gameBoard[i, j] = new SpaceDataHandler(foo.OccupiedPiece.unitType, friendly);
                    }
                    else res.gameBoard[i, j] = new SpaceDataHandler();
                }
            }
            res.miniMax = this;
            res.zobrist = zobrist;
            res.zobristHash = zobrist.GetZobristHash(res.gameBoard, true);
            return res;
        }
        /// <summary>
        /// Uses NegaMax to find best move.
        /// </summary>
        public Vector2Int[] FindBestMove(bool player = false) => IterativeDeepening(CurrentGameState(player));

        /// <summary>
        /// Use iterative deepening for a depth first search.
        /// </summary>
        /// <param name="currentGameState"> Current gamestate to search.</param>
        /// <returns> The best move. </returns>
        private Vector2Int[] IterativeDeepening(GameStateReader currentGameState) {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            depth = 1;
            var bestMove = RootNegaMax(currentGameState, depth);
            bool OutOfTime() => timer.ElapsedMilliseconds >= timeLimit;
            for (depth = 2; depth <= maxDepth && !OutOfTime(); depth++)
                bestMove = RootNegaMax(currentGameState, depth);
            timer.Stop();
            ClearTable();
            return bestMove;
        }

        /// <summary>
        /// Using (Root) NegaMax with Alpha-Beta Pruning and Transposition Table to determine the best possible move.
        /// </summary>
        /// <param name="gameState"> Current GameState. </param>
        /// <param name="depth"> How many layers of branches to foresee. </param>
        /// <param name="alpha">  Maximum to prune out. </param>
        /// <param name="beta">  Minimum to prune out. </param>
        /// <param name="maximizingPlayer">  Current Player. True = caller. </param>
        /// <returns>  The best move. </returns>
        private Vector2Int[] RootNegaMax(GameStateReader gameState, int depth, float alpha = float.NegativeInfinity, float beta = float.PositiveInfinity, bool maximizingPlayer = true/*, bool allowNullMove = true*/) {

            #region Inititalize Variables

            var origAlpha = alpha;
            long zobristHash = gameState.zobristHash;

            var bestEval = float.NegativeInfinity;
            var orderedMoves = gameState.PossibleMovesQuick(maximizingPlayer);
            orderedMoves = OrderMoves(orderedMoves, gameState, depth);
            var bestMove = orderedMoves[0];

            #endregion

            #region Search Tree

            foreach (var child in orderedMoves) {
                gameState.MakeMove(child);
                var eval = -NegaMax(gameState, depth - 1, -beta, -alpha, !maximizingPlayer);
                gameState.UndoMove();
                if (eval > bestEval) {
                    bestEval = eval;
                    bestMove = child;
                    alpha = Mathf.Max(alpha, bestEval);
                    if (alpha >= beta) {
                        if (gameState.IsntCapture(child[1])) {
                            int ply = this.depth - depth;
                            killerMoves[ply, 1] = killerMoves[ply, 0];
                            killerMoves[ply, 0] = child;
                            historyMoves[child[0].x, child[0].y, child[1].x, child[1].y] += depth * depth;
                        }
                        break;
                    }
                }
            }

            #endregion

            #region Add to Transposition Table.

            var newEntry = new TranspositionTableEntry {
                depth = depth,
                value = bestEval,
                pvMove = bestMove
            };
            switch (bestEval) {
                case float value when value <= origAlpha:
                    newEntry.flag = TranspositionTableEntry.Flag.UPPERBOUND;
                    break;
                case float value when value >= beta:
                    newEntry.flag = TranspositionTableEntry.Flag.LOWERBOUND;
                    break;
                default:
                    newEntry.flag = TranspositionTableEntry.Flag.EXACT;
                    break;
            }
            transpositionTable[zobristHash] = newEntry;

            #endregion

            #region Return result

            return bestMove;

            #endregion
        }
        /// <summary>
        /// Using NegaMax with Alpha-Beta Pruning and Transposition Table to determine the best possible move.
        /// </summary>
        /// <param name="gameState"> Current GameState. </param>
        /// <param name="depth"> How many layers of branches to foresee. </param>
        /// <param name="alpha">  Maximum to prune out. </param>
        /// <param name="beta">  Minimum to prune out. </param>
        /// <param name="maximizingPlayer">  Current Player. True = caller. </param>
        /// <returns>  The evaluated score. </returns>
        private float NegaMax(GameStateReader gameState, int depth, float alpha = float.NegativeInfinity, float beta = float.PositiveInfinity, bool maximizingPlayer = true/*, bool allowNullMove = true*/) {

            #region Transposition Table retrieval

            var origAlpha = alpha;
            long zobristHash = gameState.zobristHash;
            if (transpositionTable.ContainsKey(zobristHash)) {
                var ttEntry = transpositionTable[zobristHash];
                var value = ttEntry.value;
                if (!maximizingPlayer) value *= -1;
                if (ttEntry.depth >= depth) {
                    switch (ttEntry.flag) {
                        case TranspositionTableEntry.Flag.EXACT:
                            return value;
                        case TranspositionTableEntry.Flag.UPPERBOUND:
                            alpha = Mathf.Max(alpha, value);
                            break;
                        case TranspositionTableEntry.Flag.LOWERBOUND:
                            beta = Mathf.Min(beta, value);
                            break;
                    }
                    if (alpha >= beta) return value;
                }
            }

            #endregion

            #region End Node

            if (gameState.GameOver) return EvaluateBoardState(gameState) * (maximizingPlayer ? 1 : -1);
            
            if (depth == 0) {
                if (gameState.Quiet()) return EvaluateBoardState(gameState) * (maximizingPlayer ? 1 : -1);
                else return Quiesce(gameState, quiesceDepth, !maximizingPlayer, alpha, beta);
            }
            
            #endregion

            #region Search Tree

            var bestEval = float.NegativeInfinity;
            var bestMove = new Vector2Int[2];
            var orderedMoves = gameState.PossibleMovesQuick(maximizingPlayer);
            orderedMoves = OrderMoves(orderedMoves, gameState, depth);

            foreach (var child in orderedMoves) {
                gameState.MakeMove(child);
                var eval = -NegaMax(gameState, depth - 1, -beta, -alpha, !maximizingPlayer);
                gameState.UndoMove();
                if (eval > bestEval) {
                    bestEval = eval;
                    bestMove = child;
                    alpha = Mathf.Max(alpha, bestEval);
                    if (alpha >= beta) {
                        if (gameState.IsntCapture(child[1])) {
                            int ply = this.depth - depth;
                            killerMoves[ply, 1] = killerMoves[ply, 0];
                            killerMoves[ply, 0] = child;
                            historyMoves[child[0].x, child[0].y, child[1].x, child[1].y] += depth * depth;
                        }
                        break;
                    }
                }
            }

            #endregion

            #region Add to Transposition Table.

            var newEntry = new TranspositionTableEntry {
                depth = depth,
                value = bestEval * (maximizingPlayer ? 1 : -1),
                pvMove = bestMove
            };
            switch (bestEval) {
                case float value when value <= origAlpha:
                    newEntry.flag = TranspositionTableEntry.Flag.UPPERBOUND;
                    break;
                case float value when value >= beta:
                    newEntry.flag = TranspositionTableEntry.Flag.LOWERBOUND;
                    break;
                default:
                    newEntry.flag = TranspositionTableEntry.Flag.EXACT;
                    break;
            }
            transpositionTable[zobristHash] = newEntry;

            #endregion

            #region Return result

            return bestEval;

            #endregion
        }
        /// <summary>
        /// Quiescence Search to avoid Horizon Effect.
        /// </summary>
        /// <param name="gameState"> Gamestate to evaluate. </param>
        /// <param name="depth"> Depth to search. </param>
        /// <param name="maximizingPlayer"> Player to move. </param>
        /// <param name="alpha"> Alpha value. </param>
        /// <param name="beta"> Beta value. </param>
        /// <returns> Quiesce Eval. </returns>
        private float Quiesce(GameStateReader gameState, int depth, bool maximizingPlayer, float alpha, float beta) {
            float stand_pat = EvaluateBoardState(gameState) * (maximizingPlayer ? -1 : 1);

            if (depth == 0 || gameState.Quiet() || gameState.GameOver) return stand_pat;

            if (stand_pat >= beta)
                return beta;
            if (alpha < stand_pat)
                alpha = stand_pat;

            var captureMoves = gameState.CaptureMoves(maximizingPlayer);
            captureMoves = OrderMoves(captureMoves, gameState, depth);

            foreach(var child in captureMoves)  {
                gameState.MakeMove(child);
                var score = -Quiesce(gameState, depth - 1, !maximizingPlayer, -beta, -alpha);
                gameState.UndoMove();

                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }
            return alpha;
        }
        /// <summary>
        /// Evaluates the current boardstate, 
        /// </summary>
        /// <param name="gameState">The boardstate to evaluate.</param>
        /// <returns> inf or -inf if the game would be over.</returns>
        private float EvaluateBoardState(GameStateReader gameState) {
            var friendlyUnits = new Dictionary<Vector2Int, UnitType>();
            var enemyUnits = new Dictionary<Vector2Int, UnitType>();
            for (int i = 0; i < 8; i++) {
                for (int j = 0; j < 8; j++) {
                    var foo = gameState.gameBoard[i, j];
                    if (foo.Friendly) friendlyUnits.Add(new Vector2Int(i, j), foo.unitType);
                    else if (foo.Enemy) enemyUnits.Add(new Vector2Int(i, j), foo.unitType);
                }
            }
            
            if (!friendlyUnits.ContainsValue(UnitType.General)) return float.NegativeInfinity;
            //If this move would result in opponent winning, return neg infinity.
            if (!enemyUnits.ContainsValue(UnitType.General)) return float.PositiveInfinity;
            //If this move would result in victory, give it highest priority.

            float res = 0.0f;
            foreach (var foo in friendlyUnits) res += GamePiece.PieceValue(foo.Key, foo.Value);
            foreach (var foo in enemyUnits) res -= GamePiece.PieceValue(foo.Key, foo.Value);
            return res;
        }
        /// <summary>
        /// Clears all ancient Transposition Table entries.
        /// </summary>
        public void ClearTable() {
            transpositionTable = transpositionTable.Where(x => !x.Value.ancient).ToDictionary(i => i.Key, i => i.Value);
            foreach (var ttEntry in transpositionTable) ttEntry.Value.ancient = true;
        }
        /// <summary>
        /// Handles Move Ordering.
        /// </summary>
        /// <param name="list"> Moves to order. </param>
        /// <param name="depth"> Depth of search. </param>
        /// <returns> Sorted List. </returns>
        public List<Vector2Int[]> OrderMoves(List<Vector2Int[]> moves, GameStateReader gameState, int depth) {
            var pvMove = transpositionTable.ContainsKey(gameState.zobristHash) ? transpositionTable[gameState.zobristHash].pvMove : null;
            var ply = this.depth - depth;
            var score = new Dictionary<Vector2Int[], float>(moves.ToDictionary(x => x,
                                                                               x => x == pvMove ? 2000000 : QuickEval(gameState, x, ply)));
            return score.OrderBy(x => x.Value).Reverse().ToDictionary(x => x.Key, x => x.Value).Keys.ToList();
        }
        /// <summary>
        /// Quickly evaluates a move for ordering.
        /// </summary>
        /// <param name="gameState"> The move to evaluate. </param>
        /// <param name="move"> Move being made. </param>
        /// <param name="depth"> The depth of the search. </param>
        /// <returns> An evaluation. </returns>
        private float QuickEval(GameStateReader gameState, Vector2Int[] move, int ply) {
            var oldPos = move[0];
            var newPos = move[1];
            var enemy = gameState.gameBoard[newPos.x, newPos.y].unitType;
            if (enemy != UnitType.None) {
                var res = GamePiece.CaptureScore(gameState.gameBoard[oldPos.x, oldPos.y].unitType, enemy);
                switch (res) {
                    case float value when value > 0:
                        return res + 1100000;
                    case 0:
                        return res + 1000000;
                    default:
                        return res;
                }

            }
            //A unit was captured...
            else if (killerMoves[ply, 0] == move) return 900000;
            else if (killerMoves[ply, 1] == move) return 800000;
            // Killer moves
            return historyMoves[oldPos.x, oldPos.y, newPos.x, newPos.y];
            // Sorts units by history.
        }

        #endregion
    }
}