using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tetris
{
    /// <summary>
    /// Self-contained Tetris game. Bootstraps its own camera, board, and UI at runtime,
    /// so no manual scene setup is required — just press Play.
    /// Controls: Left/Right (or A/D) move, Down/S soft drop, Up/X rotate CW, Z rotate CCW,
    /// Space hard drop, C/Shift hold, P/Esc pause, R restart.
    /// </summary>
    public class TetrisGame : MonoBehaviour
    {
        const int Cols = 10;
        const int VisibleRows = 20;
        const int HiddenRows = 2;
        const int Rows = VisibleRows + HiddenRows;
        const float CellSize = 0.5f;
        const int NextPreviewCount = 3;

        const float DasDelay = 0.15f;
        const float ArrRate = 0.04f;
        const float SoftDropInterval = 0.035f;
        const float LockDelay = 0.5f;

        static readonly Color EmptyCellColor = new Color32(40, 40, 55, 255);

        enum State { Playing, Paused, GameOver }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<TetrisGame>() != null) return;
            var go = new GameObject("TetrisGame");
            go.AddComponent<TetrisGame>();
        }

        // Board state
        Color32?[,] settled = new Color32?[Rows, Cols];
        SpriteRenderer[,] boardCells;

        // Active piece
        PieceType activeType;
        int rotation;
        int anchorRow;
        int anchorCol;

        // Hold / next
        SevenBag bag = new SevenBag();
        readonly List<PieceType> nextQueue = new List<PieceType>();
        PieceType? holdType;
        bool holdUsedThisTurn;

        // Timing
        float dropTimer;
        float lockTimer;
        float dropInterval = 1.0f;
        int dasDir;
        float dasTimer;
        float arrTimer;

        // Score
        int score;
        int lines;
        int level = 1;

        State state = State.Playing;

        // UI references
        Font uiFont;
        Text scoreText;
        Text levelText;
        Text linesText;
        GameObject pausePanel;
        GameObject gameOverPanel;
        Text gameOverText;
        PreviewGrid holdGrid;
        readonly PreviewGrid[] nextGrids = new PreviewGrid[NextPreviewCount];

        static Sprite pixelSprite;

        class PreviewGrid
        {
            public readonly Image[] cells = new Image[8]; // 4 cols x 2 rows

            public void SetPiece(PieceType? type)
            {
                foreach (var img in cells) img.color = new Color(0, 0, 0, 0f);
                if (!type.HasValue) return;
                Color c = TetrominoDefs.Color(type.Value);
                foreach (var cell in TetrominoDefs.Cells(type.Value, 0))
                {
                    int idx = cell.x * 4 + cell.y;
                    if (idx >= 0 && idx < cells.Length) cells[idx].color = c;
                }
            }
        }

        void Start()
        {
            uiFont = GetUIFont();
            SetupCamera();
            SetupBoardRenderers();
            SetupUI();
            Restart();
        }

        void Update()
        {
            var kb = Keyboard.current;

            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                Restart();
                return;
            }

            if (state == State.GameOver) return;

            if (kb != null && (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                TogglePause();

            if (state == State.Paused) return;

            HandleHorizontalInput(Time.deltaTime);

            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) TryRotate(1);
                if (kb.zKey.wasPressedThisFrame) TryRotate(-1);
                if (kb.cKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame) HoldPiece();
                if (kb.spaceKey.wasPressedThisFrame) HardDrop();
            }

            UpdateGravity(Time.deltaTime);
            Draw();
        }

        // ---------- Input ----------

        void HandleHorizontalInput(float dt)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool leftHeld = kb.leftArrowKey.isPressed || kb.aKey.isPressed;
            bool rightHeld = kb.rightArrowKey.isPressed || kb.dKey.isPressed;

            if (leftHeld && !rightHeld) HandleDirHeld(-1, dt);
            else if (rightHeld && !leftHeld) HandleDirHeld(1, dt);
            else { dasDir = 0; dasTimer = 0f; arrTimer = 0f; }
        }

        void HandleDirHeld(int dir, float dt)
        {
            if (dasDir != dir)
            {
                dasDir = dir;
                dasTimer = 0f;
                arrTimer = 0f;
                TryMove(0, dir);
            }
            else
            {
                dasTimer += dt;
                if (dasTimer >= DasDelay)
                {
                    arrTimer += dt;
                    if (arrTimer >= ArrRate)
                    {
                        arrTimer = 0f;
                        TryMove(0, dir);
                    }
                }
            }
        }

        // ---------- Game logic ----------

        void UpdateGravity(float dt)
        {
            var kb = Keyboard.current;
            bool softDrop = kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed);
            float interval = softDrop ? Mathf.Min(dropInterval, SoftDropInterval) : dropInterval;

            dropTimer += dt;
            if (dropTimer >= interval)
            {
                dropTimer = 0f;
                TryMove(1, 0);
            }

            if (!CanPlace(activeType, rotation, anchorRow + 1, anchorCol))
            {
                lockTimer += dt;
                if (lockTimer >= LockDelay) LockPiece();
            }
            else
            {
                lockTimer = 0f;
            }
        }

        bool TryMove(int dRow, int dCol)
        {
            if (!CanPlace(activeType, rotation, anchorRow + dRow, anchorCol + dCol)) return false;
            anchorRow += dRow;
            anchorCol += dCol;
            if (dCol != 0) lockTimer = 0f;
            return true;
        }

        void TryRotate(int dir)
        {
            int newRotation = (rotation + dir + 4) % 4;
            int[] rowKicks = { 0, -1 };
            int[] colKicks = { 0, -1, 1, -2, 2 };

            foreach (var rk in rowKicks)
            {
                foreach (var ck in colKicks)
                {
                    if (CanPlace(activeType, newRotation, anchorRow + rk, anchorCol + ck))
                    {
                        rotation = newRotation;
                        anchorRow += rk;
                        anchorCol += ck;
                        lockTimer = 0f;
                        return;
                    }
                }
            }
        }

        void HardDrop()
        {
            int ghostRow = ComputeGhostRow();
            score += Mathf.Max(0, ghostRow - anchorRow) * 2;
            anchorRow = ghostRow;
            LockPiece();
        }

        void HoldPiece()
        {
            if (holdUsedThisTurn) return;
            holdUsedThisTurn = true;

            if (holdType.HasValue)
            {
                PieceType swapped = holdType.Value;
                holdType = activeType;
                activeType = swapped;
                PositionAtSpawn(activeType);
            }
            else
            {
                holdType = activeType;
                SpawnPiece();
            }
        }

        bool CanPlace(PieceType type, int rot, int row, int col)
        {
            foreach (var cell in TetrominoDefs.Cells(type, rot))
            {
                int br = row + cell.x;
                int bc = col + cell.y;
                if (bc < 0 || bc >= Cols) return false;
                if (br >= Rows) return false;
                if (br < 0) continue;
                if (settled[br, bc] != null) return false;
            }
            return true;
        }

        void LockPiece()
        {
            foreach (var cell in TetrominoDefs.Cells(activeType, rotation))
            {
                int br = anchorRow + cell.x;
                int bc = anchorCol + cell.y;
                if (br >= 0 && br < Rows && bc >= 0 && bc < Cols)
                    settled[br, bc] = TetrominoDefs.Color(activeType);
            }

            int cleared = ClearLines();
            ApplyScore(cleared);
            holdUsedThisTurn = false;
            SpawnPiece();
        }

        int ClearLines()
        {
            int cleared = 0;
            for (int r = Rows - 1; r >= 0; r--)
            {
                bool full = true;
                for (int c = 0; c < Cols; c++)
                {
                    if (settled[r, c] == null) { full = false; break; }
                }
                if (!full) continue;

                cleared++;
                for (int rr = r; rr > 0; rr--)
                    for (int c = 0; c < Cols; c++)
                        settled[rr, c] = settled[rr - 1, c];
                for (int c = 0; c < Cols; c++) settled[0, c] = null;
                r++; // re-check this row index after the shift
            }
            return cleared;
        }

        void ApplyScore(int cleared)
        {
            if (cleared <= 0) return;
            int[] table = { 0, 100, 300, 500, 800 };
            score += table[Mathf.Min(cleared, 4)] * level;
            lines += cleared;

            int newLevel = lines / 10 + 1;
            if (newLevel != level)
            {
                level = newLevel;
                dropInterval = Mathf.Max(0.08f, 1.0f - (level - 1) * 0.07f);
            }
        }

        void SpawnPiece()
        {
            activeType = nextQueue[0];
            nextQueue.RemoveAt(0);
            RefillNextQueue();
            PositionAtSpawn(activeType);
        }

        void PositionAtSpawn(PieceType type)
        {
            rotation = 0;
            var cells = TetrominoDefs.Cells(type, 0);
            int minRow = int.MaxValue, minCol = int.MaxValue, maxCol = int.MinValue;
            foreach (var c in cells)
            {
                minRow = Mathf.Min(minRow, c.x);
                minCol = Mathf.Min(minCol, c.y);
                maxCol = Mathf.Max(maxCol, c.y);
            }

            anchorRow = -minRow;
            anchorCol = (Cols - (maxCol - minCol + 1)) / 2 - minCol;
            dropTimer = 0f;
            lockTimer = 0f;

            if (!CanPlace(type, rotation, anchorRow, anchorCol)) TriggerGameOver();
        }

        void RefillNextQueue()
        {
            while (nextQueue.Count < NextPreviewCount) nextQueue.Add(bag.Next());
        }

        int ComputeGhostRow()
        {
            int r = anchorRow;
            while (CanPlace(activeType, rotation, r + 1, anchorCol)) r++;
            return r;
        }

        void TriggerGameOver()
        {
            state = State.GameOver;
            gameOverPanel.SetActive(true);
            gameOverText.text = $"GAME OVER\nScore {score}\nPress R to Restart";
        }

        void TogglePause()
        {
            if (state == State.Playing) { state = State.Paused; pausePanel.SetActive(true); }
            else if (state == State.Paused) { state = State.Playing; pausePanel.SetActive(false); }
        }

        void Restart()
        {
            settled = new Color32?[Rows, Cols];
            bag = new SevenBag();
            nextQueue.Clear();
            RefillNextQueue();
            holdType = null;
            holdUsedThisTurn = false;
            score = 0;
            lines = 0;
            level = 1;
            dropInterval = 1.0f;
            dasDir = 0;
            dasTimer = 0f;
            arrTimer = 0f;
            state = State.Playing;
            gameOverPanel.SetActive(false);
            pausePanel.SetActive(false);
            SpawnPiece();
            Draw();
        }

        // ---------- Rendering ----------

        void Draw()
        {
            for (int r = HiddenRows; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var color = settled[r, c];
                    boardCells[r - HiddenRows, c].color = color.HasValue ? (Color)color.Value : EmptyCellColor;
                }
            }

            int ghostRow = ComputeGhostRow();
            Color pieceColor = TetrominoDefs.Color(activeType);
            foreach (var cell in TetrominoDefs.Cells(activeType, rotation))
            {
                int br = ghostRow + cell.x;
                int bc = anchorCol + cell.y;
                if (br >= HiddenRows && br < Rows)
                    boardCells[br - HiddenRows, bc].color = new Color(pieceColor.r, pieceColor.g, pieceColor.b, 0.25f);
            }

            foreach (var cell in TetrominoDefs.Cells(activeType, rotation))
            {
                int br = anchorRow + cell.x;
                int bc = anchorCol + cell.y;
                if (br >= HiddenRows && br < Rows)
                    boardCells[br - HiddenRows, bc].color = pieceColor;
            }

            scoreText.text = $"SCORE\n{score}";
            levelText.text = $"LEVEL\n{level}";
            linesText.text = $"LINES\n{lines}";

            holdGrid.SetPiece(holdType);
            for (int i = 0; i < NextPreviewCount; i++)
                nextGrids[i].SetPiece(i < nextQueue.Count ? nextQueue[i] : (PieceType?)null);
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("TetrisCamera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(15, 15, 26, 255);
            cam.orthographicSize = VisibleRows * CellSize / 2f + 0.5f;
            cam.transform.position = new Vector3(Cols * CellSize / 2f, VisibleRows * CellSize / 2f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        void SetupBoardRenderers()
        {
            var boardParent = new GameObject("Board").transform;
            boardParent.SetParent(transform, false);

            var bg = CreateCell(boardParent, "Background", 0);
            bg.transform.localPosition = new Vector3(Cols * CellSize / 2f, VisibleRows * CellSize / 2f, 0f);
            bg.transform.localScale = new Vector3(Cols * CellSize + 0.1f, VisibleRows * CellSize + 0.1f, 1f);
            bg.color = new Color32(10, 10, 20, 255);

            boardCells = new SpriteRenderer[VisibleRows, Cols];
            for (int i = 0; i < VisibleRows; i++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var sr = CreateCell(boardParent, $"cell_{i}_{c}", 1);
                    float x = c * CellSize + CellSize / 2f;
                    float y = (VisibleRows - 1 - i) * CellSize + CellSize / 2f;
                    sr.transform.localPosition = new Vector3(x, y, 0f);
                    sr.transform.localScale = new Vector3(CellSize * 0.92f, CellSize * 0.92f, 1f);
                    sr.color = EmptyCellColor;
                    boardCells[i, c] = sr;
                }
            }
        }

        SpriteRenderer CreateCell(Transform parent, string name, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetPixelSprite();
            sr.sortingOrder = sortOrder;
            return sr;
        }

        static Sprite GetPixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return pixelSprite;
        }

        // ---------- UI ----------

        void SetupUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRoot = canvasGo.transform;

            CreateText(canvasRoot, "TETRIS", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -16), new Vector2(300, 36), 26, TextAnchor.UpperCenter, Color.white);

            // Top-left: score / level / lines
            scoreText = CreateText(canvasRoot, "SCORE\n0", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(150, 44), 18, TextAnchor.UpperLeft, Color.white);
            levelText = CreateText(canvasRoot, "LEVEL\n1", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -74), new Vector2(150, 44), 18, TextAnchor.UpperLeft, Color.white);
            linesText = CreateText(canvasRoot, "LINES\n0", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -128), new Vector2(150, 44), 18, TextAnchor.UpperLeft, Color.white);

            CreateText(canvasRoot, "HOLD", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -186), new Vector2(150, 24), 16, TextAnchor.UpperLeft, Color.white);
            holdGrid = BuildPreviewGrid(canvasRoot, new Vector2(0, 1), new Vector2(20, -214));

            // Top-right: next queue
            CreateText(canvasRoot, "NEXT", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-20, -20), new Vector2(150, 24), 16, TextAnchor.UpperRight, Color.white);
            for (int i = 0; i < NextPreviewCount; i++)
                nextGrids[i] = BuildPreviewGrid(canvasRoot, new Vector2(1, 1), new Vector2(-100, -48 - i * 62));

            CreateText(canvasRoot,
                "←→ Move  ↓ Soft Drop  ↑/X Rotate CW  Z Rotate CCW\nSpace Hard Drop  C Hold  P Pause  R Restart",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 20), new Vector2(700, 44), 14, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.7f));

            pausePanel = BuildOverlayPanel(canvasRoot, "PAUSED\nPress P to Resume", out _);
            pausePanel.SetActive(false);

            gameOverPanel = BuildOverlayPanel(canvasRoot, "GAME OVER", out gameOverText);
            gameOverPanel.SetActive(false);
        }

        GameObject BuildOverlayPanel(Transform parent, string message, out Text text)
        {
            var panelGo = new GameObject("OverlayPanel", typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bgImage = panelGo.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.6f);

            text = CreateText(panelGo.transform, message, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 200), 32, TextAnchor.MiddleCenter, Color.white);

            return panelGo;
        }

        PreviewGrid BuildPreviewGrid(Transform parent, Vector2 anchor, Vector2 anchoredPos)
        {
            const float cellPx = 20f;
            var grid = new PreviewGrid();
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    var img = CreateImage(parent, anchor, anchor, anchor,
                        anchoredPos + new Vector2(col * (cellPx + 2), -row * (cellPx + 2)),
                        new Vector2(cellPx, cellPx), new Color(0, 0, 0, 0));
                    grid.cells[row * 4 + col] = img;
                }
            }
            return grid;
        }

        Text CreateText(Transform parent, string content, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta, int fontSize, TextAnchor align, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var txt = go.AddComponent<Text>();
            txt.font = uiFont;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.text = content;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        Image CreateImage(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Cell", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Font GetUIFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return f;
        }
    }
}
