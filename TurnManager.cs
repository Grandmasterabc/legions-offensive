using UnityEngine;

namespace GamePlay {
    public class TurnManager : MonoBehaviour {

        #region Public Properties

        [ShowOnly] public int Turn = 0;

        [ShowOnly] public bool myTurn = true;

        public ITurnManagerCallbacks turnManagerListener;

        #endregion

        #region Custom Methods

        /// <summary>
        /// Ends the turn.
        /// </summary>
        /// <param name="player"> Was this called by local player? </param>
        public void EndTurn(bool player) {
            if (!myTurn == player) return;
            myTurn = !myTurn;
            Turn++;
            turnManagerListener.BeginTurn(Turn);
        }

        #endregion

    }

    public interface ITurnManagerCallbacks {
        /// <summary>
        /// Called the turn begins event.
        /// </summary>
        /// <param name="turn">Turn Index</param>
        void BeginTurn(int turn);

    }
}