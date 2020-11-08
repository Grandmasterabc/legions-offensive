using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;


namespace GamePlay {

    public enum UnitType { 
        None, Soldier, Sergeant, General, Sorcerer, Archer, Pikeman, Militia
    }

    public class GamePiece : MonoBehaviour {

        #region Public Fields
        [Tooltip("Does this unit belong to the player?")]
        [ShowOnly] public bool friendly;
        [Tooltip("What kind of Unit is this piece?")]
        [ShowOnly] public UnitType unitType;
        // The board space this unit is on.
        [HideInInspector] public SpaceManager occupiedSpace;

        #endregion

        #region Private Fields

        // The game board, which is a 2d-array that saves the position of each 
        private SpaceManager[,] GameBoard => GameManager.instance.gameBoard;
        // The position of this unit (x is row, y is column)
        private Vector2Int Pos => occupiedSpace.position;
        // Each entry represents a direction the unit could move (horizontal, vertical or diagonal)
        private readonly static Vector2Int[] adjacentSpaces = new Vector2Int[8] {
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(-1, 0),
                new Vector2Int(1, -1),
                new Vector2Int(0, 1),
                new Vector2Int(-1, 1),
                new Vector2Int(0, -1),
                new Vector2Int(-1, -1)
        };


        private const int outerValue = 8;
        private const int outerCenter = 9;
        private const int innerCenter = 11;
        private const int innerValue = 14;
        // The value of a piece based on position.
        public static readonly int[,] boardValue = new int[8, 8] {
            { outerValue, outerValue, outerValue, outerValue, outerValue, outerValue, outerValue, outerValue },
            { outerValue, outerCenter, outerCenter, outerCenter, outerCenter, outerCenter, outerCenter, outerValue },
            { outerValue, outerCenter, innerCenter, innerCenter, innerCenter, innerCenter, outerCenter, outerValue },
            { outerValue, outerCenter, innerCenter, innerValue, innerValue, innerCenter, outerCenter, outerValue },
            { outerValue, outerCenter, innerCenter, innerValue, innerValue, innerCenter, outerCenter, outerValue },
            { outerValue, outerCenter, innerCenter, innerCenter, innerCenter, innerCenter, outerCenter, outerValue },
            { outerValue, outerCenter, outerCenter, outerCenter, outerCenter, outerCenter, outerCenter, outerValue },
            { outerValue, outerValue, outerValue, outerValue, outerValue, outerValue, outerValue, outerValue }
        };

        #endregion

        #region Custom Methods
        // Returns true if the unit is a captured general.
        public static bool Captured(SpaceDataHandler[,] gameBoard, Vector2Int pos) {
            if (gameBoard[pos.x, pos.y].unitType == UnitType.General) {
                foreach (var adjSpace in adjacentSpaces) {
                    Vector2Int newPos = pos + adjSpace;
                    if (!InsideBounds(newPos)) continue;
                    var space = gameBoard[newPos.x, newPos.y];
                    if (space.Occupied && !space.Friendly) return true;
                }
            }
            return false;
        }
        // Handles promotions.
        public void OnEnemyKilled() {
            switch (unitType) {
                case UnitType.Soldier:
                    unitType = UnitType.Sergeant;
                    transform.GetChild(0).gameObject.SetActive(true);
                    break;
                case UnitType.Sergeant:
                    unitType = UnitType.General;
                    Destroy(transform.GetChild(0).gameObject);
                    transform.GetChild(1).gameObject.SetActive(true);
                    break;
            }
        }

        public SpaceManager[] PossibleMoves() => PossibleMoves(unitType);
        // Returns the possible spaces this unit can move to.
        public SpaceManager[] PossibleMoves(UnitType unitType) {
            List<SpaceManager> res = new List<SpaceManager>();
            switch (unitType) {
                case UnitType.Soldier:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = Pos + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = GameBoard[space.x, space.y];
                            if (foo.Friendly) continue;
                            res.Add(foo);
                        }
                    }
                    break;
                case UnitType.Sergeant:
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = Pos;
                        for (int i = 0; i < 2; i++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = GameBoard[pos.x, pos.y];
                                if (space.Friendly) break;
                                res.Add(space);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                case UnitType.General:
                    bool captured = false;
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = Pos + adjSpace;
                        if (!InsideBounds(pos)) continue;
                        var space = GameBoard[pos.x, pos.y];
                        if (space.Occupied && !space.Friendly) {
                            captured = true;
                            res.Add(space);
                        }
                    }
                    if (captured) break;
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = Pos;
                        for (int i = 0; i < 3; i++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = GameBoard[pos.x, pos.y];
                                if (space.Friendly) break;
                                res.Add(space);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                case UnitType.Sorcerer:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = Pos + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = GameBoard[space.x, space.y];
                            if (foo.Friendly || !foo.Occupied) continue;
                            res.Add(foo);
                        }
                    }
                    foreach (var space in GameBoard) {
                        if (res.Contains(space) || space.Occupied) continue;
                        res.Add(space);
                    }
                    break;
                case UnitType.Archer:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = Pos + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = GameBoard[space.x, space.y];
                            if (!foo.Friendly) res.Add(foo);
                        }
                    }
                    for (var i = 0; i < adjacentSpaces.Length; i += 2) {
                        var space = Pos + adjacentSpaces[i] * 2;
                        if (InsideBounds(space)) {
                            var foo = GameBoard[space.x, space.y];
                            if (!foo.Friendly && foo.Occupied) res.Add(foo);
                        }
                    }
                    break;
                case UnitType.Pikeman:
                    for (int i = 1; i < 8; i += 2) {
                        var adjSpace = adjacentSpaces[i];
                        Vector2Int pos = Pos;
                        for (int j = 0; j < 3; j++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = GameBoard[pos.x, pos.y];
                                if (space.Friendly) break;
                                res.Add(space);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                case UnitType.Militia:
                    for (int i = 0; i < 8; i += 2) {
                        var adjSpace = adjacentSpaces[i];
                        Vector2Int pos = Pos;
                        for (int j = 0; j < 3; j++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = GameBoard[pos.x, pos.y];
                                if (space.Friendly) break;
                                res.Add(space);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                default: throw new NotImplementedException();
            }
            return res.ToArray();
        }
        /// <summary>
        /// Find all possible locations a unit may move to (Called from GameStateReader.)
        /// </summary>
        /// <param name="gameboard"> The gameboard of the current gamestate.</param>
        /// <param name="unit"> The position of the unit whose moves are being searched.</param>
        /// <returns> All the places the unit may move to. </returns>
        public static Vector2Int[] PossibleMoves(SpaceDataHandler[,] gameboard, Vector2Int unit) {
            var res = new List<Vector2Int>();
            var piece = gameboard[unit.x, unit.y];
            bool Allied(SpaceDataHandler obj) => obj.Occupied && piece.friendly == obj.friendly;
            bool Opposed(SpaceDataHandler obj) => obj.Occupied && piece.friendly != obj.friendly;
            switch (piece.unitType) {
                case UnitType.Soldier:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = unit + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Allied(foo)) continue;
                            res.Add(space);
                        }
                    }
                    break;
                case UnitType.Sergeant:
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = unit;
                        for (int i = 0; i < 2; i++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Allied(space)) break;
                                res.Add(pos);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                case UnitType.General:
                    bool captured = false;
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = unit + adjSpace;
                        if (!InsideBounds(pos)) continue;
                        var space = gameboard[pos.x, pos.y];
                        if (Opposed(space)) {
                            captured = true;
                            res.Add(pos);
                        }
                    }
                    if (captured) break;
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = unit;
                        for (int i = 0; i < 3; i++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Allied(space)) break;
                                res.Add(pos);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                case UnitType.Sorcerer: 
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = unit + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Opposed(foo)) res.Add(space);
                        }
                    }
                    for (int i = 0; i < 8; i++) {
                        for (int j = 0; j < 8; j++) {
                            var space = gameboard[i, j];
                            if (space.Occupied) continue;
                            res.Add(new Vector2Int(i, j));
                        }
                    }
                    break;
                case UnitType.Archer:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = unit + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (!Allied(foo)) res.Add(space);
                        }
                    }
                    for (var i = 0; i < adjacentSpaces.Length; i += 2) {
                        var space = unit + adjacentSpaces[i] * 2;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Opposed(foo)) res.Add(space);
                        }
                    }
                    break;
                case UnitType.Pikeman:
                    for (int i = 1; i < 8; i += 2) {
                        var adjSpace = adjacentSpaces[i];
                        Vector2Int pos = unit;
                        for (int j = 0; j < 3; j++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Allied(space)) break;
                                res.Add(pos);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                case UnitType.Militia:
                    for (int i = 0; i < 8; i += 2) {
                        var adjSpace = adjacentSpaces[i];
                        Vector2Int pos = unit;
                        for (int j = 0; j < 3; j++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Allied(space)) break;
                                res.Add(pos);
                                if (space.Occupied) break;
                            }
                        }
                    }
                    break;
                default: throw new NotImplementedException();
            }
            return res.ToArray();
        }
        /// <summary>
        /// Returns all moves a unit can do that would result in a vanquish. 
        /// </summary>
        /// <param name="gameboard"> The gameboard of the current gamestate.</param>
        /// <param name="unit"> The position of the unit whose moves are being searched.</param>
        /// <returns> All the places with an enemy the unit can vanquish. </returns>
        public static Vector2Int[] CaptureMoves(SpaceDataHandler[,] gameboard, Vector2Int unit) {
            List<Vector2Int> res = new List<Vector2Int>();
            var piece = gameboard[unit.x, unit.y];
            bool Opposed(SpaceDataHandler obj) => obj.Occupied && piece.friendly != obj.friendly;
            switch (piece.unitType) {
                case UnitType.Soldier:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = unit + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Opposed(foo)) res.Add(space);
                        }
                    }
                    break;
                case UnitType.Sergeant:
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = unit;
                        for (int i = 0; i < 2; i++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Opposed(space)) {
                                    res.Add(pos);
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case UnitType.General:
                    bool captured = false;
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = unit + adjSpace;
                        if (!InsideBounds(pos)) continue;
                        var space = gameboard[pos.x, pos.y];
                        if (Opposed(space)) {
                            captured = true;
                            res.Add(pos);
                        }
                    }
                    if (captured) break;
                    foreach (var adjSpace in adjacentSpaces) {
                        Vector2Int pos = unit;
                        for (int i = 0; i < 3; i++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Opposed(space)) {
                                    res.Add(pos);
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case UnitType.Sorcerer: 
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = unit + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Opposed(foo)) res.Add(space);
                        }
                    }
                    break;
                case UnitType.Archer:
                    foreach (var adjSpace in adjacentSpaces) {
                        var space = unit + adjSpace;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Opposed(foo)) res.Add(space);
                        }
                    }
                    for (var i = 0; i < adjacentSpaces.Length; i += 2) {
                        var space = unit + adjacentSpaces[i] * 2;
                        if (InsideBounds(space)) {
                            var foo = gameboard[space.x, space.y];
                            if (Opposed(foo)) res.Add(space);
                        }
                    }
                    break;
                case UnitType.Pikeman:
                    for (int i = 1; i < 8; i += 2) {
                        var adjSpace = adjacentSpaces[i];
                        Vector2Int pos = unit;
                        for (int j = 0; j < 3; j++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Opposed(space)) {
                                    res.Add(pos);
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case UnitType.Militia:
                    for (int i = 0; i < 8; i += 2) {
                        var adjSpace = adjacentSpaces[i];
                        Vector2Int pos = unit;
                        for (int j = 0; j < 3; j++) {
                            pos += adjSpace;
                            if (InsideBounds(pos)) {
                                var space = gameboard[pos.x, pos.y];
                                if (Opposed(space)) {
                                    res.Add(pos);
                                    break;
                                }
                            }
                        }
                    }
                    break;
                default: throw new NotImplementedException();
            }
            return res.ToArray();
        }
        /// <summary>
        /// The static value of a unit.
        /// </summary>
        /// <param name="unitType"> The type of unit. </param>
        /// <returns> The static value of a unit. </returns>
        public static int PieceValue(UnitType unitType) {
            switch (unitType) {
                case UnitType.Soldier: return 1;
                case UnitType.Sergeant: return 2;
                case UnitType.Sorcerer: return 2;
                case UnitType.Archer: return 2;
                case UnitType.Pikeman: return 2;
                case UnitType.Militia: return 2;
                case UnitType.General: return 3;
                default: throw new NotImplementedException();
            }
        }
        /// <summary>
        /// The static value of a unit based on it's position.
        /// </summary>
        /// <param name="unitType"> The type of unit. </param>
        /// <returns> The static value of a unit. </returns>
        public static int PieceValue(Vector2Int pos, UnitType unitType) {
            int num = boardValue[pos.x, pos.y];
            return num * PieceValue(unitType);
        }
        /// <summary>
        /// The score of a vanquish (defender value - attacker value) to prioritze good vanquishes. 
        /// </summary>
        /// <param name="attacker"> The attacking unit. </param>
        /// <param name="defender"> The unit being attacked. </param>
        /// <returns> The evaluation of the vanquish. </returns>
        public static float CaptureScore(UnitType attacker, UnitType defender) {
            if (attacker == UnitType.None || defender == UnitType.None)
                return float.NegativeInfinity;
            else if (attacker == UnitType.Soldier || attacker == UnitType.Sergeant) return PieceValue(defender);
            else return PieceValue(defender) - PieceValue(attacker);
        }
        /// <summary>
        /// Is a space inside the board? 
        /// </summary>
        /// <param name="pos"> The position to evaluate. </param>
        /// <returns> False if out of bounds. </returns>
        private static bool InsideBounds(Vector2Int pos) => pos.x >= 0 && pos.x <= 7 && pos.y >= 0 && pos.y <= 7;

        #endregion

    }
}
