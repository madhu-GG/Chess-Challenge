using ChessChallenge.API;
using System.Collections.Generic;
using System;

public class MyBot : IChessBot
{
    class MoveTreeComparer : IComparer<MoveTree>
    {
        public int Compare(MoveTree? x, MoveTree? y)
        {
            if (x == null)
            {
                if (y == null) return 0;
                else return -1;
            } else if (y == null) return 1;
            else
            {
                MoveTree a = (MoveTree)x, b = (MoveTree)y;
                if (a.Eval == b.Eval) return 0;
                else {
                    if (a.Color > 0) return a.Eval > b.Eval ? 1 : -1;
                    else return a.Eval < b.Eval ? 1 : -1;
                }
            }
        }
    }

    class MoveTree
    {
        private Board ChessBoard { get; set; }
        private PriorityQueue<MoveTree, MoveTree> Children { get; set; }
        private MoveTree? Parent { get; set; }
        private Move? Move { get; set; }

        private MoveTree? BestChild { get; set; }
        public int Color { get; set; }
        public double Eval { get; set; }

        private void UpdateBestChild()
        {
            PriorityQueue<MoveTree, MoveTree> refresh = new(new MoveTreeComparer());
            while(Children.Count > 0)
            {
                MoveTree child = Children.Dequeue();
                refresh.Enqueue(child, child);
            }

            if (refresh.Count > 0)
            {
                BestChild = refresh.Peek();
            }
        }

        private double GetPiecesValue(bool isWhite)
        {
            int index = 0;
            double pieces_value = 0.0;
            foreach (PieceType piece_type in Enum.GetValues(typeof(PieceType)))
            {
                ulong pieces_bitboard = ChessBoard.GetPieceBitboard(piece_type, isWhite);
                while(pieces_bitboard > 0)
                {
                    index = BitboardHelper.ClearAndGetIndexOfLSB(ref pieces_bitboard);
                    if (index > 0)
                    {
                        Square square = new(index);
                        ulong piece_scope_bitmap = BitboardHelper.GetPieceAttacks(piece_type, square, ChessBoard, isWhite);
                        int piece_scope = BitboardHelper.GetNumberOfSetBits(piece_scope_bitmap);
                        pieces_value += (int)piece_type + 0.1 * pieces_value;
                    }
                }
            }

            return isWhite? pieces_value : -pieces_value;
        }

        private void Evaluate(bool after, Span<Move> moves)
        {
            int color_to_eval = after? -Color : Color;
            if (ChessBoard.IsInCheckmate())
            {
                Eval = color_to_eval > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            } else if (ChessBoard.IsDraw())
            {
                Eval = 0.0;
            } else if (BestChild != null)
            {
                Eval = BestChild.Eval;
            } else
            {
                /* Get evaluation for opponent as we already made move */
                double opp_pieces_value = GetPiecesValue(color_to_eval > 0);
                double my_pieces_value = GetPiecesValue(Color > 0);
                Eval = my_pieces_value + opp_pieces_value;
                // Console.WriteLine($"color = {Color}, eval = {Eval}, opp_pieces_value = {opp_pieces_value}, my_pieces_val = {my_pieces_value}");
            }
        }

        public Move? BestMove()
        {
            if (BestChild == null) return null;
            Console.WriteLine($"BestMove eval is {BestChild.Eval}");
            return BestChild.Move;
        }

        public MoveTree(Board board, MoveTree? parent, Move? move, uint depth = 4)
        {
            Span<Move> moves = stackalloc Move[1024];
            ChessBoard = board;
            Children = new(new MoveTreeComparer());
            Parent = parent;
            Move = move;
            Eval = 0.0;
            BestChild = null;
            Color = (parent != null)? -parent.Color : board.IsWhiteToMove? 1 : -1;
            bool hasMove = false;

            if (move != null) {
                hasMove = true;
                ChessBoard.MakeMove((Move)move);
            }

            if (depth > 0)
            {
                ChessBoard.GetLegalMovesNonAlloc(ref moves);
                foreach (var next_move in moves)
                {
                    MoveTree child = new(board, this, next_move, depth - 1);
                    Children.Enqueue(child, child);
                }

                UpdateBestChild();
            } else {           
                Evaluate(hasMove, moves);
            }

            if (move != null) ChessBoard.UndoMove((Move)move);
        }

        public static Move? BestMove(Board board, uint depth = 4)
        {
            MoveTree root = new(board, null, null, depth);
            return root.BestMove();
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
        Move? botMove = MoveTree.BestMove(board);
        if (botMove == null)
        {
            Console.WriteLine("Bot made a NULL move!");
        }
        Move bestMove = botMove ?? moves[randIndex];
        return bestMove;
    }
}