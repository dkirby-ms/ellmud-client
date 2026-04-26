using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom VisualElement that draws an explored-room minimap using Painter2D.
/// Rooms are laid out on an inferred grid via BFS over exit connections.
/// </summary>
public class MinimapElement : VisualElement
{
    private Dictionary<string, ExploredRoomData> rooms = new();
    private string currentRoomId;
    private readonly Dictionary<string, Vector2Int> gridPositions = new();

    private const int CellSize = 11;      // pixel size of each room square
    private const int Gap = 4;            // pixel gap between adjacent cells

    public MinimapElement()
    {
        generateVisualContent += OnGenerateVisualContent;
        style.flexGrow = 1;
    }

    public void UpdateMap(Dictionary<string, ExploredRoomData> rooms, string currentRoomId)
    {
        this.rooms = rooms ?? new Dictionary<string, ExploredRoomData>();
        this.currentRoomId = currentRoomId;
        ComputeGridPositions();
        MarkDirtyRepaint();
    }

    // ── BFS grid layout ───────────────────────────────────

    private void ComputeGridPositions()
    {
        gridPositions.Clear();
        if (rooms.Count == 0)
            return;

        var startId = !string.IsNullOrEmpty(currentRoomId) && rooms.ContainsKey(currentRoomId)
            ? currentRoomId
            : rooms.Keys.First();

        var queue = new Queue<(string id, Vector2Int pos)>();
        gridPositions[startId] = Vector2Int.zero;
        queue.Enqueue((startId, Vector2Int.zero));

        while (queue.Count > 0)
        {
            var (id, pos) = queue.Dequeue();
            if (!rooms.TryGetValue(id, out var room) || room.exits == null)
                continue;

            Enqueue(queue, room.exits.north, pos + new Vector2Int(0, -1));
            Enqueue(queue, room.exits.south, pos + new Vector2Int(0, 1));
            Enqueue(queue, room.exits.east,  pos + new Vector2Int(1, 0));
            Enqueue(queue, room.exits.west,  pos + new Vector2Int(-1, 0));
        }
    }

    private void Enqueue(Queue<(string, Vector2Int)> queue, string targetId, Vector2Int nextPos)
    {
        if (string.IsNullOrEmpty(targetId))
            return;
        if (gridPositions.ContainsKey(targetId))
            return;
        if (!rooms.ContainsKey(targetId))
            return;

        gridPositions[targetId] = nextPos;
        queue.Enqueue((targetId, nextPos));
    }

    // ── Drawing ───────────────────────────────────────────

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (gridPositions.Count == 0)
            return;

        var bounds = contentRect;
        if (bounds.width < 1f || bounds.height < 1f)
            return;

        var painter = ctx.painter2D;
        var step = CellSize + Gap;

        var minX = gridPositions.Values.Min(p => p.x);
        var maxX = gridPositions.Values.Max(p => p.x);
        var minY = gridPositions.Values.Min(p => p.y);
        var maxY = gridPositions.Values.Max(p => p.y);

        var mapW = (maxX - minX + 1) * step;
        var mapH = (maxY - minY + 1) * step;
        var ox = (bounds.width  - mapW) * 0.5f - minX * step + CellSize * 0.5f;
        var oy = (bounds.height - mapH) * 0.5f - minY * step + CellSize * 0.5f;

        // Draw connections
        painter.strokeColor = new Color(0.22f, 0.32f, 0.52f, 1f);
        painter.lineWidth = 1.5f;

        foreach (var (id, pos) in gridPositions)
        {
            if (!rooms.TryGetValue(id, out var room) || room.exits == null)
                continue;

            var from = GridToPixel(pos, ox, oy, step);

            DrawEdge(painter, from, room.exits.north, pos + new Vector2Int(0, -1), ox, oy, step);
            DrawEdge(painter, from, room.exits.south, pos + new Vector2Int(0,  1), ox, oy, step);
            DrawEdge(painter, from, room.exits.east,  pos + new Vector2Int(1,  0), ox, oy, step);
            DrawEdge(painter, from, room.exits.west,  pos + new Vector2Int(-1, 0), ox, oy, step);
        }

        // Draw room squares
        var half = CellSize * 0.5f;

        foreach (var (id, pos) in gridPositions)
        {
            var center = GridToPixel(pos, ox, oy, step);
            var isCurrent = id == currentRoomId;

            Color fill;
            if (isCurrent)
            {
                fill = new Color(1f, 0.82f, 0.22f, 1f);
            }
            else if (rooms.TryGetValue(id, out var r) &&
                     !string.IsNullOrEmpty(r.roomType) &&
                     r.roomType.Contains("refuge"))
            {
                fill = new Color(0.25f, 0.65f, 0.35f, 1f);
            }
            else
            {
                fill = new Color(0.2f, 0.38f, 0.62f, 1f);
            }

            painter.fillColor = fill;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x - half, center.y - half));
            painter.LineTo(new Vector2(center.x + half, center.y - half));
            painter.LineTo(new Vector2(center.x + half, center.y + half));
            painter.LineTo(new Vector2(center.x - half, center.y + half));
            painter.ClosePath();
            painter.Fill();
        }
    }

    private void DrawEdge(Painter2D painter, Vector2 from, string targetId,
        Vector2Int targetGridPos, float ox, float oy, int step)
    {
        if (string.IsNullOrEmpty(targetId) || !gridPositions.ContainsKey(targetId))
            return;

        var to = GridToPixel(targetGridPos, ox, oy, step);
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(to);
        painter.Stroke();
    }

    private static Vector2 GridToPixel(Vector2Int grid, float ox, float oy, int step)
        => new Vector2(ox + grid.x * step, oy + grid.y * step);
}
