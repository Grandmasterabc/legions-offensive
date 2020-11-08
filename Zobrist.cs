using System;
using UnityEngine;

namespace GamePlay
{
    public class Zobrist {
        /// <summary>
        /// Zobrist Array: [friendly][unitType][xPos][yPos]
        /// </summary>
        private long[,,,] zArray = new long[2, 7, 8, 8];
        /// <summary>
        /// Is it my turn to make a Move?
        /// </summary>
        private long myMove;
        /// <summary>
        /// System.Random object to generate Random64.
        /// </summary>
        private readonly System.Random random = new System.Random();
        /// <summary>
        /// Object to synchronize Random64 function.
        /// </summary>
        private readonly object syncLock = new object();
        /// <summary>
        /// Generates a random int64.
        /// </summary>
        /// <returns>A randomly generated Int64.</returns>
        private long Random64() {
            var buffer = new byte[sizeof(long)];
            lock (syncLock) random.NextBytes(buffer);  // synchronize
            return BitConverter.ToInt64(buffer, 0);
        }
        /// <summary>
        /// Fills zArray.
        /// </summary>
        public void FillZobristArray() {
            for (int friendly = 0; friendly < 2; friendly++) {
                for (int unitType = 0; unitType < 7; unitType++) {
                    for (int xPos = 0; xPos < 8; xPos++) {
                        for (int yPos = 0; yPos < 8; yPos++) {
                            zArray[friendly, unitType, xPos, yPos] = Random64();
                        }
                    }
                }
            }
            myMove = Random64();
        }
        /// <summary>
        /// Creates a Zobrist Hash of the current board-state.
        /// </summary>
        /// <param name="gameBoard">Current board-state.</param>
        /// <param name="myGo">Is it the caller's turn?</param>
        /// <returns> A zobrist hash.</returns>
        public long GetZobristHash(SpaceDataHandler[,] gameBoard, bool myGo) {
            long zobristKey = 0;
            for (int xPos = 0; xPos < 8; xPos++) {
                for (int yPos = 0; yPos < 8; yPos++) {
                    var space = gameBoard[xPos, yPos];
                    if (!space.Occupied) continue;
                    long friendly = space.Friendly ? 0 : 1;
                    long unitType = (long)space.unitType - 1;
                    zobristKey ^= zArray[friendly, unitType, xPos, yPos];
                }
            }
            if (myGo) zobristKey ^= myMove;
            return zobristKey;
        }
        /// <summary>
        /// Updates the last Zobrist Hash.
        /// </summary>
        /// <param name="gameBoard"> The previous gameboard. </param>
        /// <param name="lastZobrist"> The last Zobrist Hash. </param>
        /// <param name="moves"> The move taken. Pos-a to Pos-b. </param>
        /// <returns> The updated Zobrist Hash. </returns>
        public long UpdateZobrist(SpaceDataHandler[,] gameBoard, long lastZobrist, Vector2Int[] moves) {
            var firstMove = moves[0];
            var secondMove = moves[1];
            var firstSpace = gameBoard[firstMove.x, firstMove.y];
            var secondSpace = gameBoard[secondMove.x, secondMove.y];
            if (firstSpace.unitType == UnitType.Archer && secondSpace.Occupied)
                return UpdateZobristArcher(gameBoard, lastZobrist, secondMove);
            lastZobrist ^= myMove;
            long firstFriendly = firstSpace.Friendly ? 0 : 1;
            var firstUnitType = (long)firstSpace.unitType - 1;
            lastZobrist ^= zArray[firstFriendly, firstUnitType, firstMove.x, firstMove.y];
            lastZobrist ^= zArray[firstFriendly, firstUnitType, secondMove.x, secondMove.y];
            if (secondSpace.Occupied) {
                long friendly = secondSpace.Friendly ? 0 : 1;
                long unitType = (long)secondSpace.unitType - 1;
                lastZobrist ^= zArray[friendly, unitType, secondMove.x, secondMove.y];
            }
            return lastZobrist;
        }
        /// <summary>
        /// Updates the last zobrist for when an archer snipes an enemy.
        /// </summary>
        /// <param name="gameBoard"> The previous gameboard. </param>
        /// <param name="lastZobrist"> The last Zobrist Hash. </param>
        /// <param name="dest"> The unit shot. </param>
        /// <returns> The updated Zobrist Hash. </returns>
        public long UpdateZobristArcher(SpaceDataHandler[,] gameBoard, long lastZobrist, Vector2Int dest) {
            lastZobrist ^= myMove;
            var space = gameBoard[dest.x, dest.y];
            long friendly = space.Friendly ? 0 : 1;
            long unitType = (long)space.unitType - 1;
            lastZobrist ^= zArray[friendly, unitType, dest.x, dest.y];
            return lastZobrist;
        }
        /// <summary>
        /// Updates the last zobrist forwhen a null move is done.
        /// </summary>
        /// <param name="lastZobrist"> The last Zobrist Hash. </param>
        /// <returns> The updated Zobrist Hash. </returns>
        public long DoNullMove(long lastZobrist) => lastZobrist ^= myMove;
    }
}