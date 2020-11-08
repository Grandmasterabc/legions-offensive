using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GamePlay
{
    public class GameStateReader {
        public bool GameOver {
            get {
                var generals = gameBoard.Cast<SpaceDataHandler>().Where(x => x.unitType == UnitType.General);
                var friendly = true;
                for (int i = 0; i < 2; i++) {
                    var results = from general in generals
                                           where general.Friendly == friendly
                                           select general;
                    if (results.Count() <= 0) return true;
                    friendly = false;
                }
                return false;
            }
        }

        public SpaceDataHandler[,] gameBoard = new SpaceDataHandler[8, 8];

        public Vector2Int[] lastMoves => lastMoveEntries.Last().move;

        public bool CanDoNullMove() {
            var count = 0;
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    if (gameBoard[i, j].Occupied) count++;
            return count >= 10;
        }

        public long zobristHash;
        public Zobrist zobrist;
        public MiniMaxController miniMax;

        /// <summary>
        /// True if the move doesn't result in a capture.
        /// </summary>
        /// <param name="dest"> Destination to move to. </param>
        public bool IsntCapture(Vector2Int dest) => gameBoard[dest.x, dest.y].unitType == UnitType.None;

        #region Quick Gamestate Generation
        /// <summary>
        /// Quiet Moves are non captures and non vanquishes.
        /// </summary>
        /// <returns></returns>
        public bool Quiet() {
            for (int i = 0; i < 8; i++) {
                for (int j = 0; j < 8; j++) {
                    if (GamePiece.Captured(gameBoard, new Vector2Int(i, j))) return false;
                }
            }
            return lastMoveEntries.Last().destUnitType == UnitType.None;
        }

        private struct LastMoveEntry {
            public Vector2Int[] move;
            public UnitType destUnitType;
            public long zobrist;
            public bool promotion;

            public LastMoveEntry(Vector2Int[] move, UnitType destUnitType, long zobrist, bool promotion = false) {
                this.move = move;
                this.destUnitType = destUnitType;
                this.zobrist = zobrist;
                this.promotion = promotion;
            }
        }
        // Last in first out.
        private readonly List<LastMoveEntry> lastMoveEntries = new List<LastMoveEntry>();

        /// <summary>
        /// Returns just the moves which can be made, rather than the game state.
        /// </summary>
        /// <param name="friendly"> true if maximizing player is calling this</param>
        /// <returns> Each possible move that can be made from the current game state. </returns>
        public List<Vector2Int[]> PossibleMovesQuick(bool friendly = true) {
            var res = new List<Vector2Int[]>();
            bool PieceToMove(SpaceDataHandler obj) => obj.Occupied && friendly == obj.friendly;
            //Is this a unit the calling player could move? 
            for (int i = 0; i < 8; i++) {
                for (int j = 0; j < 8; j++) {
                    var space = gameBoard[i, j];
                    if (PieceToMove(space)) {
                        var pos = new Vector2Int(i, j);
                        var moves = GamePiece.PossibleMoves(gameBoard, pos);
                        Array.ForEach(moves, x => res.Add(new Vector2Int[] { pos, x }));
                    }
                }
            }
            return res;
        }
        /// <summary>
        /// Returns just the captures which can be made.
        /// </summary>
        /// <param name="friendly"> true if maximizing player is calling this</param>
        /// <returns> Each possible move that can be made from the current game state. </returns>
        public List<Vector2Int[]> CaptureMoves(bool friendly = true) {
            var res = new List<Vector2Int[]>();
            bool PieceToMove(SpaceDataHandler obj) => obj.Occupied && friendly == obj.friendly;
            //Is this a unit the calling player could move? 
            for (int i = 0; i < 8; i++) {
                for (int j = 0; j < 8; j++) {
                    var space = gameBoard[i, j];
                    if (PieceToMove(space)) {
                        var pos = new Vector2Int(i, j);
                        var moves = GamePiece.CaptureMoves(gameBoard, pos);
                        Array.ForEach(moves, x => res.Add(new Vector2Int[] { pos, x }));
                    }
                }
            }
            return res;
        }
        /// <summary>
        /// Makes a move on the gameboard rather than copying the gameboard.
        /// </summary>
        /// <param name="move"> Move to make. </param>
        public void MakeMove(Vector2Int[] move) {
            var startPos = gameBoard[move[0].x, move[0].y]; // Unit to move.
            var endPos = gameBoard[move[1].x, move[1].y]; // Destination to move to.
            if (endPos.Occupied) {
                switch (startPos.unitType) {
                    case UnitType.Soldier:
                        lastMoveEntries.Add(new LastMoveEntry(move, endPos.unitType, zobristHash, true));
                        zobristHash = zobrist.UpdateZobrist(gameBoard, zobristHash, move);
                        endPos.unitType = UnitType.Sergeant;
                        endPos.friendly = startPos.friendly;
                        startPos.unitType = UnitType.None;
                        return;
                    case UnitType.Sergeant:
                        lastMoveEntries.Add(new LastMoveEntry(move, endPos.unitType, zobristHash, true));
                        zobristHash = zobrist.UpdateZobrist(gameBoard, zobristHash, move);
                        var foo = lastMoveEntries.Last();
                        foo.promotion = true;
                        endPos.unitType = UnitType.General;
                        endPos.friendly = startPos.friendly;
                        startPos.unitType = UnitType.None;
                        return;
                    case UnitType.Archer:
                        lastMoveEntries.Add(new LastMoveEntry(move, endPos.unitType, zobristHash));
                        zobristHash = zobrist.UpdateZobristArcher(gameBoard, zobristHash, move[1]);
                        endPos.unitType = UnitType.None;
                        return;
                }
            }
            lastMoveEntries.Add(new LastMoveEntry(move, endPos.unitType, zobristHash));
            zobristHash = zobrist.UpdateZobrist(gameBoard, zobristHash, move);
            endPos.unitType = startPos.unitType;
            endPos.friendly = startPos.friendly;
            startPos.unitType = UnitType.None;
        }
        /// <summary>
        /// Undo the last move made.
        /// </summary>
        public void UndoMove() {
            var lastMove = lastMoveEntries.Last();
            var move = lastMove.move;
            var origStartPos = gameBoard[move[0].x, move[0].y];
            var origEndPos = gameBoard[move[1].x, move[1].y];

            if (origStartPos.unitType != UnitType.Archer) {
                if (lastMove.promotion) {
                    switch (origEndPos.unitType) {
                        case UnitType.General:
                            origStartPos.unitType = UnitType.Sergeant;
                            break;
                        case UnitType.Sergeant:
                            origStartPos.unitType = UnitType.Soldier;
                            break;
                        default: throw new NotSupportedException();
                    }
                }
                else origStartPos.unitType = origEndPos.unitType;
                origStartPos.friendly = origEndPos.friendly;
            }
                        
            origEndPos.unitType = lastMove.destUnitType;
            origEndPos.friendly = !origStartPos.friendly;

            zobristHash = lastMove.zobrist;
            lastMoveEntries.RemoveAt(lastMoveEntries.Count - 1);
        }
        /// <summary>
        /// Updates the zobrist Hash for when a null move is done.
        /// </summary>
        public void DoNullMove() => zobristHash = zobrist.DoNullMove(zobristHash);

        #endregion

        public GameStateReader() { }
    }
}