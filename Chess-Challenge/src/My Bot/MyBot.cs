using ChessChallenge.API;
using System.Collections.Generic;
using System;
using System.ComponentModel;

public class MyBot : IChessBot
{
    class MoveTreeComparer : IComparer<MoveTree>
    {
        public int Compare(MoveTree? x, MoveTree? y)
        {
            if (x == null) return y == null? 0 : -1;
            if (y == null) return 1;
            if (x.Eval == y.Eval) return 0;
            int cmp = x.Eval > y.Eval ? 1 : -1;
            return x.Color > 0 ? cmp : -cmp; 
        }
    }

    class MoveTree
    {
        private Board ChessBoard { get; set; }
        private PriorityQueue<MoveTree, MoveTree> Children { get; set; }
        private Move? Move { get; set; }

        private MoveTree? BestChild { get; set; }
        public int Color { get; set; }
        public double Eval { get; set; }


        private void UpdateBestChild(bool reshuffle = true)
        {
            if (Children.Count > 0)
            {
                if (reshuffle)
                {
                    PriorityQueue<MoveTree, MoveTree> refresh = new(new MoveTreeComparer());
                    refresh.EnqueueRange(Children.UnorderedItems);
                    Children = refresh;
                }

                BestChild = Children.Peek();
                Evaluate();
            }
        }

        private static double[] PieceValue = { 0.0, 1.0, 3.0, 3.0, 5.0, 8.0, 0.0};
        private double GetPiecesValue(bool isWhite)
        {
            double pieces_value = 0.0;
            ulong pieces_bitboard = isWhite? ChessBoard.WhitePiecesBitboard : ChessBoard.BlackPiecesBitboard;
            ulong opponent_pieces_bitboard = isWhite? ChessBoard.BlackPiecesBitboard : ChessBoard.WhitePiecesBitboard;

            while (pieces_bitboard > 0)
            {
                int i = BitboardHelper.ClearAndGetIndexOfLSB(ref pieces_bitboard);
                Square square = new(i);
                Piece piece = ChessBoard.GetPiece(square);
                pieces_value += PieceValue[(int)piece.PieceType];

                ulong piece_attack_bitmap = BitboardHelper.GetPieceAttacks(piece.PieceType, square, ChessBoard, isWhite);
                ulong opponent_attacked_pieces_bitmap = piece_attack_bitmap & opponent_pieces_bitboard;
                while (opponent_attacked_pieces_bitmap > 0)
                {
                    int j = BitboardHelper.ClearAndGetIndexOfLSB(ref opponent_attacked_pieces_bitmap);
                    Square opp_square = new(j);
                    Piece opp_piece = ChessBoard.GetPiece(opp_square);
                    pieces_value +=  (PieceValue[(int)opp_piece.PieceType] / 100);
                }
            }
    
            return isWhite? pieces_value : -pieces_value;
        }

        private void Evaluate()
        {
            if (ChessBoard.IsInCheckmate())
            {
                Eval = Color > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            } else if (ChessBoard.IsDraw())
            {
                Eval = 0.0;
            } else if (BestChild != null)
            {
                Eval = BestChild.Eval;
            } else
            {
                double opp_pieces_value = GetPiecesValue((-Color) > 0);
                double my_pieces_value = GetPiecesValue(Color > 0);
                Eval = my_pieces_value + opp_pieces_value;
            }
        }

        public Move? BestMove()
        {
            if (BestChild != null) Console.WriteLine($"BestMove eval is {BestChild.Eval}");
            return BestChild?.Move;
        }

        public void Search(uint depth = 4, uint width = 30)
        {
            if (Move != null) ChessBoard.MakeMove((Move)Move);
            if (depth > 0)
            {
                Span<Move> moves = stackalloc Move[1024];
                ChessBoard.GetLegalMovesNonAlloc(ref moves);
                foreach (var next_move in moves)
                {
                    MoveTree child = new(ChessBoard, this, next_move);
                    child.Search(depth - 1, width);
                    if (Children.Count < width) Children.Enqueue(child, child);
                }

                UpdateBestChild();
            }
            if (Move != null) ChessBoard.UndoMove((Move)Move);
        }

        public MoveTree(Board board, MoveTree? parent, Move? move)
        {
            ChessBoard = board;
            Children = new(new MoveTreeComparer());
            Move = move;
            Eval = 0.0;
            BestChild = null;
            Color = (parent != null)? -parent.Color : board.IsWhiteToMove? 1 : -1;

            if (move != null) ChessBoard.MakeMove((Move)move);
            Evaluate();
            if (move != null) ChessBoard.UndoMove((Move)move);
        }

        public static Move? BestMove(Board board, uint depth = 4, uint width = 30)
        {
            MoveTree root = new(board, null, null);
            root.Search(depth, width);
            return root.BestChild?.Move;
        }
    }

    public Move Think(Board board, Timer timer)
    {
        /* 1. From current position, evaluate the strength of the next position from the possible legal moves */
        /* 2. Recursively consider each next position, and update evaluation of current position */
        /* 2a. Need to remember the current position for updating it's evaluation */
        /* 2b. Evaluation of a position is how good that position looks, and influenced by child evaluations */
        /* 3. After a while, select the move with best evaluation in the current position */
        Move[] moves = board.GetLegalMoves();
        int randIndex = new Random(timer.MillisecondsRemaining).Next(0, moves.Length);

        uint depth = 4;
        float secs_remaining = timer.MillisecondsRemaining / 1000;
        uint width = (uint)secs_remaining;

        Move? botMove = MoveTree.BestMove(board, depth, width + 1);
        if (botMove == null)
        {
            Console.WriteLine("Bot made a NULL move!");
        }
        Move bestMove = botMove ?? moves[randIndex];
        return bestMove;
    }
}