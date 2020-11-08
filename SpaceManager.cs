using Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using static GamePlay.GameManager;

namespace GamePlay {

    public class SpaceManager : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler {

        #region Custom Fields & Properties

        #region Public Fields

        [ShowOnly] public Vector2Int position;

        public static SpaceManager selectedSpace;

        #endregion

        #region Private Fields

        [SerializeField] private GamePiece occupiedPiece;
        private Vector3 offset;
        private Coroutine moveBack = null;

        #endregion

        #region Public Properties

        public static SpaceManager[,] GameBoard => instance.gameBoard;

        public bool Occupied => OccupiedPiece != null;

        public bool Friendly => Occupied && OccupiedPiece.friendly;

        public bool Highlighted {
            get => transform.GetChild(0).gameObject.activeSelf;
            set => transform.GetChild(0).gameObject.SetActive(value);
        }

        public Vector2 LocalPosition => transform.localPosition;

        public GamePiece OccupiedPiece {
            get => occupiedPiece;
            set {
                if (occupiedPiece != null) {
                    instance.allUnits.Remove(occupiedPiece);
                    Destroy(occupiedPiece.gameObject);
                    value.OnEnemyKilled();
                    if (value.unitType == UnitType.Archer) {
                        occupiedPiece = null;
                        instance.EndTurn(value.friendly);
                        return;
                    }
                }
                bool foo = value.occupiedSpace; 
                if (foo) value.occupiedSpace.occupiedPiece = null;
                occupiedPiece = value;
                value.transform.localPosition = LocalPosition;
                value.occupiedSpace = this;
                if (foo) instance.EndTurn(OccupiedPiece.friendly);
            }
        }

        #endregion

        #endregion

        #region Interface Callbacks

        public void OnPointerDown(PointerEventData eventData) {
            OnClick();
            offset = Input.mousePosition - transform.position;
            if (moveBack != null) StopCoroutine(moveBack);
        }

        public void OnBeginDrag(PointerEventData eventData) {
            if (!Occupied) return;
            if (selectedSpace != this) OnClick();
            occupiedPiece.transform.SetAsLastSibling();
            occupiedPiece.GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData) {
            if (!Occupied) return;
            occupiedPiece.transform.position = Input.mousePosition - offset;
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!Occupied) return;
            occupiedPiece.GetComponent<CanvasGroup>().blocksRaycasts = true;
            moveBack = StartCoroutine(MoveBack());
        }

        public void OnDrop(PointerEventData eventData) => OnClick();

        #endregion

        #region Custom Methods

        public void OnClick () {
            if (instance.gameOver) return;
            if (Friendly) {
                if (selectedSpace == null || (selectedSpace.Friendly && selectedSpace != this)) {
                    DisableSelection();
                    selectedSpace = this;
                    Highlighted = true;
                    instance.unitExplainer.EnableDisplayer(occupiedPiece);
                    if (MyTurn) foreach (var possMove in occupiedPiece.PossibleMoves())
                        possMove.Highlighted = true;
                }
                else DisableSelection();
            }
            else {
                if (Highlighted) {
                    if (selectedSpace != this) {
                        OccupiedPiece = selectedSpace.OccupiedPiece;
                    }
                    DisableSelection();
                }
                else if (Occupied) {
                    DisableSelection();
                    selectedSpace = this;
                    instance.unitExplainer.EnableDisplayer(occupiedPiece);
                    Highlighted = true;
                }
                else DisableSelection();
            }
        }
        /// <summary>
        /// Returns a piece to it's original position when dragging fails.
        /// </summary>
        private IEnumerator MoveBack() {
            if (!Occupied) yield break;
            float timeToMove = 0.1f;
            var currentPos = occupiedPiece.transform.localPosition;
            var t = 0f;
            while (t < 1) {
                t += Time.deltaTime / timeToMove;
                occupiedPiece.transform.localPosition = Vector3.Lerp(currentPos, transform.localPosition, t);
                yield return null;
            }
        }

        public static void DisableSelection() {
            selectedSpace = null;
            instance.unitExplainer.DisableDisplayer();
            instance.suggestedMovesHighlighted = false;
            foreach (SpaceManager space in GameBoard) 
                space.Highlighted = false;
        }

        #endregion

    }
}
