using ChessChallenge.API;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class MyBot : IChessBot
{
    class NodeComparer : IComparer<Node>
    {
        public int Compare(Node x, Node y)
        {
            if (x.Eval == y.Eval) return 0;
            int ordering = x.Eval > y.Eval ? 1 : -1;
            return x.Color > 0 ? ordering : -ordering;
        }
    }

    class BadNodeComparer : IComparer<Node>
    {
        public int Compare(Node x, Node y)
        {
            if (x.Eval == y.Eval) return 0;
            int ordering = x.Eval < y.Eval ? 1 : -1;
            return x.Color > 0 ? ordering : -ordering;
        }
    }


    struct Node
    {
        public Board ChessBoard;
        public int Color;
        public Move? Move;
        public double Eval;

        public Move? BestMove;

        public Node(Board board)
        {
            this.ChessBoard = board;
            this.Color = board.IsWhiteToMove ? 1 : -1;
            this.Move = null;
            this.BestMove = null;
            this.Eval = 0.0;
        }

        public Node(Node parent, Move move)
        {
            this.ChessBoard = parent.ChessBoard;
            this.Move = move;
            this.Color = -parent.Color;
            this.Eval = 0.0;
            this.BestMove = null;
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

                // System.Console.WriteLine($"Piece {piece} on {square} attacks {targets_count} squares.");
                // System.Console.WriteLine($"Piece {piece} on {square} is defended by {defenders[index]} pieces.");
                if (piece.IsWhite) white_safety += defenders[index];
                else black_safety += defenders[index];
            }

            eval = material
                + 0.01 * ((white_mobility - black_mobility)
                    + (white_attacks - black_attacks)
                    + (white_safety - black_safety)) / 64;
        }

        return eval;
    }

    PriorityQueue<Node, Node> Successors(Node node, int max_children)
    {
        Board board = node.ChessBoard;
        PriorityQueue<Node, Node> bad_queue = new(new BadNodeComparer());
        Span<Move> moves = stackalloc Move[1024];
        board.GetLegalMovesNonAlloc(ref moves);
        foreach (var move in moves)
        {
            Node child = new(node, move);
            board.MakeMove(move);
            child.Eval = evaluate(board);
            board.UndoMove(move);
            bad_queue.Enqueue(child, child);
            if (bad_queue.Count > max_children)
            {
                bad_queue.Dequeue();
            }
        }

        PriorityQueue<Node, Node> good_queue = new(new NodeComparer());
        while (bad_queue.Count > 0)
        {
            Node child = bad_queue.Dequeue();
            good_queue.Enqueue(child, child);
        }

        return good_queue;
    }

    Node Search(Node node,
                ref double alpha,
                ref double beta,
                int max_children = 20,
                int depth = 4)
    {
        var successors = Successors(node, max_children);
        NodeComparer comparer = new();
        Node best_child = successors.Peek();
        while (successors.Count > 0)
        {
            Node child = successors.Dequeue();
            if (depth > 0) child = Search(child, ref alpha, ref beta, max_children, depth - 1);
            int cmp = comparer.Compare(child, best_child);
            if (cmp > 0)
            {
                best_child = child;
            }

            if (node.Color > 0 && best_child.Eval >= beta)
            {
                if (node.Eval < best_child.Eval)
                {
                    node.Eval = best_child.Eval;
                    node.BestMove = best_child.Move;
                }
                return best_child;
            }
            else if (node.Color < 0 && best_child.Eval <= alpha)
            {
                if (node.Eval > best_child.Eval)
                {
                    node.Eval = best_child.Eval;
                    node.BestMove = best_child.Move;
                }
                return best_child;
            }

            if (node.Color > 0)
            {
                alpha = Math.Max(alpha, child.Eval);
            }
            else
            {
                beta = Math.Min(beta, child.Eval);
            }
        }

        if (node.Color > 0 && node.Eval < best_child.Eval)
        {
            node.Eval = best_child.Eval;
            node.BestMove = best_child.Move;
        }
        else if (node.Color < 0 && node.Eval > best_child.Eval)
        {
            node.Eval = best_child.Eval;
            node.BestMove = best_child.Move;
        }

        return best_child;
    }

    public Move Think(Board board, Timer timer)
    {
        Node root = new(board);
        int depth = 4;
        if (timer.MillisecondsRemaining / 1000 < 10) depth = 2;
        else if (timer.MillisecondsRemaining / 1000 < 30) depth = 3;

        double alpha = double.NegativeInfinity;
        double beta = double.PositiveInfinity;
        Node best_child = Search(root, ref alpha, ref beta, 20, depth);
        System.Console.WriteLine($"Eval: {best_child.Eval}, {best_child.Move}");
        return best_child.Move ?? Move.NullMove;
    }
}