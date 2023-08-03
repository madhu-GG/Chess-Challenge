using ChessChallenge.API;
using System.Collections.Generic;
using System;

public class MyBot : IChessBot
{
    class MoveTree
    {
        private Board ChessBoard { get; set; }
        private Dictionary<uint, MoveTree> Children { get; set; }
        private MoveTree? Parent { get; set; }
        private Move? Move { get; set; }

        private int BestChildIndex { get; set; }
        private double Color { get; set; }
        private double Eval { get; set; }

        private void UpdateBestChild(uint i)
        {
            if (BestChildIndex == -1) { BestChildIndex = (int)i; return; }

            MoveTree best = Children[(uint)BestChildIndex];
            MoveTree updated = Children[i];
            if (best.Color > 0)
            {
                if (best.Eval < updated.Eval) BestChildIndex = (int)i;
            }
            else if (best.Eval > updated.Eval) BestChildIndex = (int)i;
        }

        private int GetPiecesValue(bool isWhite)
        {
            int pieces_value = 0, index = 0;
            foreach (PieceType piece_type in Enum.GetValues(typeof(PieceType)))
            {
                ulong pieces_bitboard = ChessBoard.GetPieceBitboard(piece_type, isWhite);
                while(pieces_bitboard > 0)
                {
                    index = BitboardHelper.ClearAndGetIndexOfLSB(ref pieces_bitboard);
                    pieces_value += index > 0 ? (int)piece_type : 0;
                    Square square = new(index);
                }
            }

            return pieces_value;
        }

        private void Evaluate(Span<Move> moves)
        {
            if (ChessBoard.IsInCheckmate())
            {
                Eval = Color > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            } else if (ChessBoard.IsDraw())
            {
                Eval = 0.0;
            } else
            {
                /* Get evaluation for opponent as we already made move */
                int opp_pieces_value = GetPiecesValue(Color < 0);
                int my_pieces_value = GetPiecesValue(Color > 0);
                Eval = opp_pieces_value - my_pieces_value;
            }
        }

        public Move? BestMove()
        {
            if (Children.Count == 0) return null;
            if (BestChildIndex == -1) return null;
            return Children[(uint)BestChildIndex].Move;
        }

        public MoveTree(Board board, MoveTree? parent, Move? move, uint depth = 4)
        {
            Span<Move> moves = stackalloc Move[1024];
            ChessBoard = board;
            Children = new();
            Parent = parent;
            Move = move;
            Eval = 0.0;
            BestChildIndex = -1;
            Color = (parent != null)? -parent.Color : board.IsWhiteToMove? 1 : -1;

            if (move != null) ChessBoard.MakeMove((Move)move);

            if (depth > 0)
            {
                uint i = 0;
                ChessBoard.GetLegalMovesNonAlloc(ref moves);
                foreach (var next_move in moves)
                {
                    MoveTree child = new(board, this, next_move, depth - 1);
                    Children.Add(i, child);
                    UpdateBestChild(i);
                    i++;
                }
            }

            Evaluate(moves);

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