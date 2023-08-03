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
            }
            if (y == null)
            {
                return 1;
            }

            MoveTree a = (MoveTree)x;
            MoveTree b = (MoveTree)y;
            if (a.Multiplier == b.Multiplier)
            {
                if (a.Multiplier > 0) return (a.Eval > b.Eval) ? 1 : ((a.Eval < b.Eval) ? -1 : 0);
                else return (a.Eval < b.Eval) ? 1 : ((a.Eval > b.Eval) ? -1 : 0);
            }
            else
            {
                return 0;
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
        public double Multiplier { get; set; }
        public double Eval { get; set; }

        public static double[] PieceValue =
        {
            0.0,
            1.0,
            3.0,
            3.1,
            5.0,
            8.0,
            0.0
        };

        private double PiecePoints(PieceType pieceType, bool isWhite)
        {
            return MoveTree.PieceValue[(int)pieceType] * this.ChessBoard.GetPieceList(pieceType, isWhite).Count;
        }

        private void Evaluate()
        {
            if (this.ChessBoard.IsInCheckmate())
            {
                this.Eval = this.ChessBoard.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;
            }
            else if (this.ChessBoard.IsDraw())
            {
                this.Eval = 0.0;
            }
            else
            {
                double white_count = 0, black_count = 0;
                white_count += PiecePoints(PieceType.Queen, true);
                black_count += PiecePoints(PieceType.Queen, false);
                white_count += PiecePoints(PieceType.Rook, true);
                black_count += PiecePoints(PieceType.Rook, false);
                white_count += PiecePoints(PieceType.Bishop, true);
                black_count += PiecePoints(PieceType.Bishop, false);
                white_count += PiecePoints(PieceType.Knight, true);
                black_count += PiecePoints(PieceType.Knight, false);
                white_count += PiecePoints(PieceType.Pawn, true);
                black_count += PiecePoints(PieceType.Pawn, false);
                double pieceAdvantage = white_count - black_count;

                RefreshChildrenPiority();
                Move[] captures = this.ChessBoard.GetLegalMoves(true);
                int NumCaptures = captures.Length;
                int NumMoves = this.Children.Count;

                this.Eval = pieceAdvantage + Multiplier * ((NumCaptures / 1000) + (NumMoves / 10000));
                if (this.BestChild != null)
                {
                    /* black's best eval is white's eval */
                    if (Multiplier > 0)
                    {
                        if (this.Eval < this.BestChild.Eval)
                        {
                            this.Eval = this.BestChild.Eval;
                        }
                    }
                    /* white's best eval is black's eval */
                    else
                    {
                        if (this.Eval > this.BestChild.Eval)
                        {
                            this.Eval = this.BestChild.Eval;
                        }
                    }
                }
            }
        }

        private void RefreshChildrenPiority()
        {
            if (this.Children.Count > 0)
            {
                PriorityQueue<MoveTree, MoveTree> refresh = new(this.Children.Comparer);

                while (this.Children.Count > 0)
                {
                    MoveTree child = this.Children.Dequeue();
                    refresh.Enqueue(child, child);
                }

                this.Children = refresh;
                this.BestChild = this.Children.Peek();
            }
        }

        public MoveTree(Board chessBoard, uint depth = 1)
        {
            ChessBoard = chessBoard;
            Children = new(new MoveTreeComparer());
            Parent = null;
            Move = null;

            Multiplier = chessBoard.IsWhiteToMove ? 1 : -1;

            foreach (var next_move in this.ChessBoard.GetLegalMoves())
            {
                MoveTree child = new(this, next_move, depth - 1);
                this.Children.Enqueue(child, child);
            }

            Evaluate();
        }

        public MoveTree(MoveTree? parent, Move? move, uint depth = 1)
        {
            this.Parent = parent;
            this.ChessBoard = parent.ChessBoard;
            this.Move = move;
            this.Children = new(new MoveTreeComparer());

            Multiplier = -parent.Multiplier;

            if (move != null && depth > 0)
            {
                this.ChessBoard.MakeMove((Move)move);

                foreach (var next_move in this.ChessBoard.GetLegalMoves())
                {
                    MoveTree child = new(this, next_move, depth - 1);
                    this.Children.Enqueue(child, child);
                }

                Evaluate();
                this.ChessBoard.UndoMove((Move)move);
            }

            if (depth == 0)
            {
                RefreshChildrenPiority();
            }
        }

        public static Move? BestMove(Board board, uint depth = 2)
        {
            MoveTree root = new(board, depth);

            return root.BestChild.Move;
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
        Move? botMove = MoveTree.BestMove(board, 4);
        if (botMove == null)
        {
            Console.WriteLine("Bot made a NULL move!");
        }
        Move bestMove = botMove ?? moves[randIndex];
        return bestMove;
    }
}