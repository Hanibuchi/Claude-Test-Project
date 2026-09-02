using UnityEngine;

namespace Tetris
{
    public enum PieceType { I, O, T, S, Z, J, L }

    /// <summary>
    /// Static piece shape and color tables. Cell offsets are (row, col) with row increasing
    /// downward and col increasing rightward, matching the board's storage layout.
    /// </summary>
    public static class TetrominoDefs
    {
        static readonly Vector2Int[][] I =
        {
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3) },
            new[] { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2) },
            new[] { new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) },
        };

        static readonly Vector2Int[][] O =
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) },
        };

        static readonly Vector2Int[][] T =
        {
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
        };

        static readonly Vector2Int[][] S =
        {
            new[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) },
            new[] { new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 0), new Vector2Int(2, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
        };

        static readonly Vector2Int[][] Z =
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2) },
            new[] { new Vector2Int(0, 2), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(2, 2) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 0) },
        };

        static readonly Vector2Int[][] J =
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) },
            new[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 0), new Vector2Int(2, 1) },
        };

        static readonly Vector2Int[][] L =
        {
            new[] { new Vector2Int(0, 2), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(2, 2) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
        };

        public static Vector2Int[] Cells(PieceType type, int rotation)
        {
            rotation = ((rotation % 4) + 4) % 4;
            return Table(type)[rotation];
        }

        static Vector2Int[][] Table(PieceType type) => type switch
        {
            PieceType.I => I,
            PieceType.O => O,
            PieceType.T => T,
            PieceType.S => S,
            PieceType.Z => Z,
            PieceType.J => J,
            PieceType.L => L,
            _ => O,
        };

        public static Color32 Color(PieceType type) => type switch
        {
            PieceType.I => new Color32(0, 240, 240, 255),
            PieceType.O => new Color32(240, 240, 0, 255),
            PieceType.T => new Color32(160, 0, 240, 255),
            PieceType.S => new Color32(0, 240, 0, 255),
            PieceType.Z => new Color32(240, 0, 0, 255),
            PieceType.J => new Color32(0, 0, 240, 255),
            PieceType.L => new Color32(240, 160, 0, 255),
            _ => new Color32(255, 255, 255, 255),
        };
    }
}
