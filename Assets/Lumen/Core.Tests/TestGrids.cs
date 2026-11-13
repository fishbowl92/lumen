using Lumen.Core;

namespace Lumen.Core.Tests
{
    /// <summary>테스트용 격자를 짧게 만드는 도우미.</summary>
    internal static class TestGrids
    {
        public static Int3 P(int x, int y, int z) => new Int3(x, y, z);

        public static Piece Plane(PieceState state, bool isFixed = false) =>
            new Piece(PieceType.PlaneMirror, state, isFixed);

        public static Piece Lift(PieceState state, bool isFixed = false) =>
            new Piece(PieceType.LiftMirror, state, isFixed);

        public static Piece Splitter(PieceState state, bool isFixed = false) =>
            new Piece(PieceType.Splitter, state, isFixed);

        /// <summary>
        /// 3×3×3 격자 한가운데 (1,1,1)에 거울을 두고, incoming 방향으로 들어오는 광원과
        /// expected 방향 출구에 크리스털을 둔다. 반사 표 검사용.
        /// </summary>
        public static Grid CenterMirror(Piece piece, Direction incoming, Direction expectedOut)
        {
            var grid = new Grid(P(3, 3, 3));
            var center = P(1, 1, 1);
            grid.PlacePiece(center, piece);
            grid.AddSource(center - incoming.ToOffset(), incoming);
            grid.AddCrystal(center + expectedOut.ToOffset());
            return grid;
        }
    }
}
