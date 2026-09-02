using System.Collections.Generic;

namespace Tetris
{
    /// <summary>
    /// Standard "7-bag" randomizer: each of the 7 piece types appears exactly once
    /// per shuffled bag before the next bag is generated.
    /// </summary>
    public class SevenBag
    {
        static readonly PieceType[] AllPieces =
        {
            PieceType.I, PieceType.O, PieceType.T, PieceType.S, PieceType.Z, PieceType.J, PieceType.L
        };

        readonly System.Random rng = new System.Random();
        readonly Queue<PieceType> queue = new Queue<PieceType>();

        public PieceType Next()
        {
            if (queue.Count == 0) RefillBag();
            return queue.Dequeue();
        }

        void RefillBag()
        {
            var bag = new List<PieceType>(AllPieces);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            foreach (var piece in bag) queue.Enqueue(piece);
        }
    }
}
