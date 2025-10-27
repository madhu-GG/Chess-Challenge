using ChessChallenge.API;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class MyBot : IChessBot
{
    class EvalComparer : IComparer<Node>
    {
        public int Compare(Node? x, Node? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x.Eval == y.Eval) return 0;
            int ordering = x.Eval > y.Eval ? 1 : -1;
            return x.Color > 0 ? ordering : -ordering;
        }
    }

    class ReverseEvalComparer : IComparer<Node>
    {
        public int Compare(Node? x, Node? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x.Eval == y.Eval) return 0;
            int ordering = x.Eval < y.Eval ? 1 : -1;
            return x.Color > 0 ? ordering : -ordering;
        }
    }

    class FitnessComparer : IComparer<Node>
    {
        public int Compare(Node? x, Node? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x.Fitness == y.Fitness) return 0;
            int ordering = x.Fitness > y.Fitness ? 1 : -1;
            return x.Color > 0 ? ordering : -ordering;
        }
    }

    class Node
    {
        public Board ChessBoard;
        public int Color;
        public Move? Move;
        public double Eval;
        public double Fitness;

        public Node? BestChild { get; set; }

        public Node(Board board)
        {
            this.ChessBoard = board;
            this.Color = board.IsWhiteToMove ? 1 : -1;
            this.Move = null;
            this.BestChild = null;
            this.Eval = 0.0;
            this.Fitness = 0.0;
        }

        public Node(Node parent, Move move)
        {
            this.ChessBoard = parent.ChessBoard;
            this.Move = move;
            this.Color = -parent.Color;
            this.Eval = 0.0;
            this.BestChild = null;
            this.Fitness = 0.0;
        }
    }

    static readonly double[] piece_values = {
        0.0,    // None
        1.0,    // Pawn
        3.0,    // Knight
        3.0,    // Bishop
        5.0,    // Rook
        9.0,    // Queen
        0.0     // King
    };
    static double evaluate(Board board)
    {
        double eval = 0.0;
        if (board.IsInCheckmate())
        {
            eval = board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;
        }
        else if (board.IsInStalemate())
        {
            eval = 0.0;
        }
        else
        {
            double material = 0.0,
                white_mobility = 0.0, black_mobility = 0.0,
                white_attacks = 0.0, black_attacks = 0.0,
                white_safety = 0.0, black_safety = 0.0;
            int index;
            Square square;
            ulong all_pieces = board.AllPiecesBitboard,
                white_pieces = board.WhitePiecesBitboard,
                black_pieces = board.BlackPiecesBitboard;

            int[] defenders = new int[64];

            while (all_pieces != 0)
            {
                index = BitboardHelper.ClearAndGetIndexOfLSB(ref all_pieces);
                square = new(index);
                var piece = board.GetPiece(square);

                if (piece.IsWhite) material += piece_values[(int)piece.PieceType];
                else material -= piece_values[(int)piece.PieceType];

                ulong piece_range = BitboardHelper.GetPieceAttacks(
                        piece.PieceType, square, board, piece.IsWhite);

                if (piece.IsWhite) white_mobility += BitboardHelper.GetNumberOfSetBits(piece_range);
                else black_mobility += BitboardHelper.GetNumberOfSetBits(piece_range);

                ulong piece_targets = piece_range & (piece.IsWhite ? black_pieces : white_pieces);
                int targets_count = BitboardHelper.GetNumberOfSetBits(piece_targets);
                if (piece.IsWhite) white_attacks += targets_count;
                else black_attacks += targets_count;

                int defend_index;
                ulong piece_defends = piece_range & (piece.IsWhite ? white_pieces : black_pieces);
                var defends = piece_defends;
                while (defends != 0)
                {
                    defend_index = BitboardHelper.ClearAndGetIndexOfLSB(ref defends);
                    defenders[defend_index]++;
                }

                if (piece.IsWhite) white_safety += defenders[index];
                else black_safety += defenders[index];
            }

            eval = 0.7 * material
                 + 0.1 * (white_attacks - black_attacks)
                 + 0.3 * (white_safety - black_safety)
                 + 0.05 * (white_mobility - black_mobility);
        }

        return eval;
    }

    Node Search(Node node, int depth, int explore_depth = 0)
    {
        if (depth == 0)
        {
            node.Fitness = node.Eval;
        }
        else
        {
            var board = node.ChessBoard;
            PriorityQueue<Node, Node> successors = new(new FitnessComparer());
            Span<Move> moves = stackalloc Move[1024];
            board.GetLegalMovesNonAlloc(ref moves);
            foreach (var move in moves)
            {
                Node child = new(node, move);
                board.MakeMove(move);
                child.Eval = evaluate(board);
                child = Search(child, depth - 1, explore_depth + 1);
                board.UndoMove(move);
                successors.Enqueue(child, child);
            }

            if (successors.Count > 0)
            {
                var best_child = successors.Peek();
                node.Fitness = best_child.Fitness;
                node.BestChild = best_child;
            } else
            {
                node.Fitness = node.Eval;
            }
        }

        return node;
    }

    public Move Think(Board board, Timer timer)
    {
        Node root = new(board);
        int depth = 4;
        root = Search(root, depth, 0);
        if (root.BestChild == null || root.BestChild.Move == null)
        {
            System.Console.WriteLine("No best move found, selecting first legal move.");
            Span<Move> moves = stackalloc Move[1024];
            board.GetLegalMovesNonAlloc(ref moves);
            return moves[0];
        }
        else
        {
            System.Console.WriteLine($"Found {root.BestChild.Move}, Eval: {root.BestChild.Eval}");
        }

        return (Move)root.BestChild.Move;
    }
}