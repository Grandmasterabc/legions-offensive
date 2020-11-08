using System.Collections;
using UnityEngine;
using static GamePlay.GameManager;

namespace GamePlay {

    public class AIController : MonoBehaviour {
        /// <summary>
        /// Begins the turn for the AI opponent.
        /// </summary>
        public void BeginTurn() {
            var move = instance.miniMaxController.FindBestMove();
            StartCoroutine(MovePiece(move));
        }
        /// <summary>
        /// Makes the AI's move.
        /// </summary>
        /// <param name="move"> The move made. </param>
        public IEnumerator MovePiece(Vector2Int[] move) {
            yield return new WaitForSeconds(1.0f);
            var unit = instance.gameBoard[move[0].x, move[0].y].OccupiedPiece;
            var newPos = instance.gameBoard[move[1].x, move[1].y];
            newPos.OccupiedPiece = unit;
            instance.EndTurn(false);
        }

    }
}