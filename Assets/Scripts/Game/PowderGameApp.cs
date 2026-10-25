// Menu, input handling, rendering and the main loop.
// Drawn with IMGUI so it needs no extra packages and works with either input system.
using System;
using System.IO;
using PowderGame.Simulation;
using UnityEngine;

namespace PowderGame
{
    public sealed class PowderGameApp : MonoBehaviour
    {
        const int W = World.W, H = World.H;
        // The layout is designed at a fixed virtual size and scaled to fill the window.
        // Narrow windows put the menu under the play area like the original; wide
        // (e.g. 16:9 fullscreen) windows put it in a panel to the right.
        const float Dot = 2f;
        const float CanvasW = W * Dot, CanvasH = H * Dot;
        const float RowH = 24f, MenuPad = 8f, StatusH = 26f;
        const float SidePanelW = 400f, SideStatusH = 48f;
        const float SideRowH = (CanvasH - MenuPad - SideStatusH) / 18;
        const float StepTime = 1f / 60f;

        static readonly int[] PenSizes = { 0, 1, 2, 3, 4, 6, 8, 12, 16 };
        static readonly float[] Speeds = { 1f, 2f, 0.5f };

        // Mirrors the original layout. Element keys, tool names, or #actions.
        static readonly string[][] Menu =
        {
            new[] { "POWDER", "FIREWORKS", "GAS", "WIND", "WALL", "SAVE" },
            new[] { "WATER", "OIL", "SOAPY", "AIR", "ERASE", "LOAD" },
            new[] { "FIRE", "C4", "THUNDER", "DRAG", "CLEAR", "" },
            new[] { "SEED", "STONE", "METAL", "BUBBLE", "", "" },
            new[] { "GUNPOWDER", "MAGMA", "BOMB", "", "#VIEW", "#SIDE" },
            new[] { "FAN", "VIRUS", "LASER", "", "#PEN", "" },
            new[] { "ICE", "NITRO", "ACID", "", "#SPEED", "" },
            new[] { "SUPERBALL", "ANT", "", "PLAYER", "#START", "" },
            new[] { "CLONE", "TORCH", "", "", "", "#RESET" },
        };

        static readonly uint[] Hues = { 0xff4848, 0x48ff48, 0x5090ff, 0xffff48, 0xff48ff, 0x48ffff, 0xffa030, 0xffffff };

        World world;
        Texture2D screen;
        Color32[] pixels;
        readonly Color32[] lut = new Color32[256 * 16];
        readonly Color32[] fireLut = new Color32[64];
        readonly Color32[] sparkLut = new Color32[8 * 16];
        readonly Color32[] airBg = new Color32[World.AN];

        string left = "POWDER", right = "ERASE";
        int pen = 3, speed;
        bool running = true, airView;
        float acc;

        // Cursor in world dots.
        Vector2 mouse = new Vector2(-100, -100), prevMouse;
        Vector2 dir = new Vector2(0, 1);
        int mouseButton = -1; // 0 = left, 1 = right
        bool mousePressed;    // a button went down since the last ApplyInput
        bool hover;

        // Held keys: player 1 uses the arrows, player 2 uses WASD.
        static readonly KeyCode[] PlayerKeys =
        {
            KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
            KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S,
        };
        readonly bool[] keyDown = new bool[8];

        string flashText = "";
        float flashUntil, fpsTimer;
        int fpsSteps, fps;

        GUIStyle label, status;
        bool sideLayout;
        bool toggleFullscreen; // requested from OnGUI, applied in Update

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (FindFirstObjectByType<PowderGameApp>() == null) new GameObject("Powder Game").AddComponent<PowderGameApp>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (!Application.isEditor) SetFullscreen(true);
            world = new World();
            screen = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[W * H];
            BuildColors();

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(21, 21, 21, 255);
                cam.orthographic = true;
            }
        }

        void OnDestroy()
        {
            if (screen != null) Destroy(screen);
        }

        // -------------------------------------------------------------- loop

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Array.Clear(keyDown, 0, keyDown.Length);
        }

        static void SetFullscreen(bool on)
        {
            int sw = Display.main.systemWidth, sh = Display.main.systemHeight;
            if (on) Screen.SetResolution(sw, sh, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(Mathf.Min(1280, sw * 4 / 5), Mathf.Min(720, sh * 4 / 5), FullScreenMode.Windowed);
        }

        void ToggleFullscreen()
        {
#if UNITY_EDITOR
            // Screen modes don't apply in the editor; maximize the Game view instead.
            var view = UnityEditor.EditorWindow.focusedWindow;
            if (view != null) view.maximized = !view.maximized;
#else
            SetFullscreen(Screen.fullScreenMode == FullScreenMode.Windowed);
#endif
        }

        void Update()
        {
            if (toggleFullscreen) { toggleFullscreen = false; ToggleFullscreen(); }
            ApplyInput();
            for (int s = 0; s < World.MaxPlayers; s++)
            {
                Player p = world.Players[s];
                if (p == null) continue;
                p.Left = keyDown[s * 4];
                p.Right = keyDown[s * 4 + 1];
                p.Jump = keyDown[s * 4 + 2];
                p.Shoot = keyDown[s * 4 + 3];
            }
            if (running)
            {
                acc += Time.unscaledDeltaTime * Speeds[speed];
                int n = 0;
                while (acc >= StepTime && n < 4) { world.Step(); acc -= StepTime; n++; fpsSteps++; }
                if (n == 4) acc = 0;
            }
            else acc = 0;

            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fps = Mathf.RoundToInt(fpsSteps / fpsTimer);
                fpsSteps = 0;
                fpsTimer = 0;
            }
            Render();
        }

        /// <summary>Applies the current tool between the last and current cursor positions.</summary>
        void ApplyInput()
        {
            Vector2 d = mouse - prevMouse;
            float len = d.magnitude;
            if (len > 0.5f) dir = d / len;
            bool pressed = mousePressed;
            mousePressed = false;
            if (mouseButton >= 0)
            {
                string tool = mouseButton == 1 ? right : left;
                int r = PenSizes[pen], mx = (int)mouse.x, my = (int)mouse.y;
                if (tool == "PLAYER")
                {
                    // One per click, centred on the cursor.
                    if (pressed && world.AddPlayer(mx, my + Player.Height / 2) == null)
                        Flash(world.Players[World.MaxPlayers - 1] != null ? "MAX 2 PLAYERS" : "NO ROOM");
                }
                else if (tool == "WIND") world.Wind(mx, my, r, d.x, d.y);
                else if (tool == "AIR") world.AirBlast(mx, my, r);
                else if (tool == "DRAG") world.Drag((int)prevMouse.x, (int)prevMouse.y, r, d.x, d.y);
                else
                {
                    bool erase = tool == "ERASE";
                    byte t = erase ? E.Empty : (byte)Elements.IdOf(tool);
                    bool lone = !erase && Elements.Sparse[t] == 1;
                    int steps = lone ? 0 : Mathf.CeilToInt(len / Mathf.Max(1, r * 0.5f));
                    for (int k = 0; k <= steps; k++)
                    {
                        float f = steps > 0 ? (float)k / steps : 1;
                        int x = Mathf.RoundToInt(prevMouse.x + d.x * f), y = Mathf.RoundToInt(prevMouse.y + d.y * f);
                        if (erase) world.Erase(x, y, r);
                        else world.Brush(x, y, r, t, dir.x, dir.y);
                    }
                }
            }
            prevMouse = mouse;
        }

        // ------------------------------------------------------------ render

        static Color32 ToColor(uint rgb, float k)
        {
            return new Color32(Channel(rgb, 16, k), Channel(rgb, 8, k), Channel(rgb, 0, k), 255);
        }

        static byte Channel(uint rgb, int shift, float k)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(((rgb >> shift) & 255) * k), 0, 255);
        }

        static uint Lerp(uint a, uint b, float f)
        {
            uint r = 0;
            for (int s = 0; s <= 16; s += 8)
                r |= (uint)Mathf.RoundToInt(((a >> s) & 255) * (1 - f) + ((b >> s) & 255) * f) << s;
            return r;
        }

        void BuildColors()
        {
            for (int t = 0; t < Elements.Defs.Length; t++)
                for (int s = 0; s < 16; s++) lut[t * 16 + s] = ToColor(Elements.Color[t], 0.86f + s / 15f * 0.28f);
            for (int l = 0; l < 64; l++)
            {
                float f = l / 63f;
                fireLut[l] = ToColor(f < 0.4f ? Lerp(0x601000, 0xff4010, f / 0.4f) : Lerp(0xff4010, 0xffe070, (f - 0.4f) / 0.6f), 1);
            }
            for (int h = 0; h < 8; h++)
                for (int l = 0; l < 16; l++) sparkLut[h * 16 + l] = ToColor(Hues[h], 0.3f + l / 15f * 0.7f);
        }

        void Render()
        {
            byte[] type = world.Type, shade = world.Shade;
            short[] life = world.Life;
            int frame = world.Frame;
            var bg = new Color32(0, 0, 0, 255);
            var charged = new Color32(255, 255, 192, 255);
            var laser = new Color32(255, 64, 64, 255);
            var bolt = new Color32(255, 255, 192, 255);

            if (airView)
            {
                float[] ap = world.Pressure, avx = world.AirVx, avy = world.AirVy;
                for (int i = 0; i < World.AN; i++)
                {
                    float p = ap[i], v = Mathf.Sqrt(avx[i] * avx[i] + avy[i] * avy[i]);
                    airBg[i] = new Color32((byte)Mathf.Min(255, p > 0 ? p * 12 : 0), (byte)Mathf.Min(255, v * 25),
                        (byte)Mathf.Min(255, p < 0 ? -p * 12 : 0), 255);
                }
            }

            for (int y = 0; y < H; y++)
            {
                int row = y * W, arow = (y >> 2) * World.AW, dst = (H - 1 - y) * W; // textures are bottom-up
                for (int x = 0; x < W; x++)
                {
                    int i = row + x;
                    byte t = type[i];
                    Color32 c;
                    switch (t)
                    {
                        case E.Empty: c = airView ? airBg[arow + (x >> 2)] : bg; break;
                        case E.Fire: c = fireLut[Mathf.Min(63, life[i] * 2)]; break;
                        case E.Magma: c = lut[t * 16 + (((shade[i] + frame) >> 2) & 15)]; break;
                        case E.Metal: c = life[i] > 0 ? charged : lut[t * 16 + (shade[i] >> 4)]; break;
                        case E.Spark: c = sparkLut[(shade[i] & 7) * 16 + Mathf.Clamp(life[i], 0, 15)]; break;
                        case E.Bolt: c = world.Vx[i] != 0 ? laser : bolt; break;
                        default: c = lut[t * 16 + (shade[i] >> 4)]; break;
                    }
                    pixels[dst + x] = c;
                }
            }
            DrawPlayers();
            DrawCursor();
            screen.SetPixels32(pixels);
            screen.Apply(false);
        }

        // Stick figure, relative to the feet. Head is 3x3 at rows -9..-7.
        static readonly int[] BodyX = { 0, 0, 0, -1, -2, 1, 2 };
        static readonly int[] BodyY = { -6, -5, -4, -5, -4, -5, -4 };
        static readonly int[] LegsAX = { 0, -1, -1, -2, 1, 1, 2 }; // stride
        static readonly int[] LegsAY = { -3, -2, -1, 0, -2, -1, 0 };
        static readonly int[] LegsBX = { 0, 0, -1, -1, 1, 1 };      // passing
        static readonly int[] LegsBY = { -3, -2, -1, 0, -1, 0 };

        void DrawPlayers()
        {
            var body = new Color32(232, 232, 232, 255);
            for (int s = 0; s < World.MaxPlayers; s++)
            {
                Player p = world.Players[s];
                if (p == null) continue;
                int fx = Mathf.FloorToInt(p.X), fy = Mathf.FloorToInt(p.Y);
                Color32 head = p.Held != 0 ? lut[p.Held * 16 + 12] : body;
                for (int y = -9; y <= -7; y++)
                    for (int x = -1; x <= 1; x++) Plot(fx + x, fy + y, head);
                for (int k = 0; k < BodyX.Length; k++) Plot(fx + BodyX[k], fy + BodyY[k], body);
                bool stride = !p.Grounded || ((p.Walk >> 3) & 1) == 0;
                int[] lx = stride ? LegsAX : LegsBX, ly = stride ? LegsAY : LegsBY;
                for (int k = 0; k < lx.Length; k++) Plot(fx + lx[k], fy + ly[k], body);
            }
        }

        void Plot(int x, int y, Color32 c)
        {
            if (x >= 0 && x < W && y >= 0 && y < H) pixels[(H - 1 - y) * W + x] = c;
        }

        void DrawCursor()
        {
            if (!hover) return;
            int r = PenSizes[pen], mx = (int)mouse.x, my = (int)mouse.y;
            float ro = (r + 0.5f) * (r + 0.5f), ri = r > 0 ? (r - 0.5f) * (r - 0.5f) : -1;
            var c = new Color32(144, 144, 144, 255);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int d2 = dx * dx + dy * dy;
                    if (d2 > ro || d2 <= ri) continue;
                    int x = mx + dx, y = my + dy;
                    if (x >= 0 && x < W && y >= 0 && y < H && world.Type[y * W + x] == 0) pixels[(H - 1 - y) * W + x] = c;
                }
            }
        }

        // --------------------------------------------------------------- GUI

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                status = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Normal };
            }

            // Use whichever layout makes the play area bigger.
            float bottomW = CanvasW, bottomH = CanvasH + MenuPad + Menu.Length * RowH + StatusH;
            float sideW = CanvasW + SidePanelW, sideH = CanvasH;
            float bottomScale = Mathf.Min(Screen.width / bottomW, Screen.height / bottomH);
            float sideScale = Mathf.Min(Screen.width / sideW, Screen.height / sideH);
            sideLayout = sideScale > bottomScale;
            float vw = sideLayout ? sideW : bottomW, vh = sideLayout ? sideH : bottomH;
            float s = sideLayout ? sideScale : bottomScale;
            var offset = new Vector3((Screen.width - vw * s) / 2, (Screen.height - vh * s) / 2, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1));

            Event e = Event.current;
            HandleCanvasEvents(e);

            if (e.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(0, 0, CanvasW, CanvasH), screen);
                if (sideLayout) FillRect(new Rect(CanvasW, 0, SidePanelW, vh), new Color32(64, 64, 64, 255));
                else FillRect(new Rect(0, CanvasH, vw, vh - CanvasH), new Color32(64, 64, 64, 255));
            }
            DrawMenu(e);
            DrawStatus(vh);
        }

        /// <summary>Screen rectangle of the menu entry at Menu[row][col] in the current layout.</summary>
        Rect MenuCell(int row, int col)
        {
            if (!sideLayout)
            {
                float colW = CanvasW / 6;
                return new Rect(col * colW, CanvasH + MenuPad + row * RowH, colW, RowH);
            }
            // Side panel: three columns, with the right half of the menu stacked below the left half.
            float w = SidePanelW / 3;
            int r = col < 3 ? row : row + Menu.Length;
            return new Rect(CanvasW + (col % 3) * w, MenuPad + r * SideRowH, w, SideRowH);
        }

        void HandleCanvasEvents(Event e)
        {
            Vector2 mp = e.mousePosition;
            hover = mp.x >= 0 && mp.x < CanvasW && mp.y >= 0 && mp.y < CanvasH;
            if (hover || mouseButton >= 0) mouse = new Vector2(Mathf.Floor(mp.x / Dot), Mathf.Floor(mp.y / Dot));

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (hover && (e.button == 0 || e.button == 1))
                    {
                        mouseButton = e.button;
                        mousePressed = true;
                        prevMouse = mouse;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (mouseButton >= 0) { mouseButton = -1; e.Use(); }
                    break;
                case EventType.ScrollWheel:
                    if (hover)
                    {
                        pen = Mathf.Clamp(pen + (e.delta.y < 0 ? 1 : -1), 0, PenSizes.Length - 1);
                        e.Use();
                    }
                    break;
                case EventType.KeyDown:
                case EventType.KeyUp:
                {
                    bool down = e.type == EventType.KeyDown;
                    if (down && e.keyCode == KeyCode.Space) { running = !running; e.Use(); break; }
                    int k = Array.IndexOf(PlayerKeys, e.keyCode);
                    if (k >= 0) { keyDown[k] = down; e.Use(); }
                    else if (down && e.keyCode == KeyCode.F11) { toggleFullscreen = true; e.Use(); }
                    break;
                }
            }
        }

        void DrawMenu(Event e)
        {
            for (int row = 0; row < Menu.Length; row++)
            {
                for (int col = 0; col < 6; col++)
                {
                    string key = Menu[row][col];
                    if (key.Length == 0) continue;
                    Rect rect = MenuCell(row, col);
                    bool isAction = key[0] == '#' || key == "SAVE" || key == "LOAD" || key == "CLEAR";

                    if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                    {
                        if (isAction) { if (e.button == 0) DoAction(key); }
                        else if (e.button == 0) left = key;
                        else if (e.button == 1) right = key;
                        e.Use();
                    }
                    if (e.type != EventType.Repaint) continue;

                    if (rect.Contains(e.mousePosition)) FillRect(rect, new Color32(80, 80, 80, 255));
                    if (left == key) FillRect(new Rect(rect.x + 5, rect.y + 4, 7, 7), new Color32(255, 48, 48, 255));
                    if (right == key) FillRect(new Rect(rect.x + 5, rect.yMax - 11, 7, 7), new Color32(48, 96, 255, 255));
                    string text;
                    Color color;
                    Describe(key, out text, out color);
                    var textRect = new Rect(rect.x + 16, rect.y, rect.width - 16, rect.height);
                    Shadowed(textRect, text, color, label);
                }
            }
        }

        void Describe(string key, out string text, out Color color)
        {
            color = new Color32(255, 144, 144, 255);
            switch (key)
            {
                case "WIND": text = key; color = new Color32(102, 136, 255, 255); return;
                case "AIR": text = key; color = new Color32(192, 192, 255, 255); return;
                case "DRAG": text = key; color = new Color32(224, 224, 224, 255); return;
                case "ERASE": text = key; color = new Color32(160, 160, 160, 255); return;
                case "PLAYER": text = key; color = new Color32(240, 220, 220, 255); return;
                case "#VIEW": text = airView ? "VIEW AIR" : "VIEW DOT"; return;
                case "#SIDE": text = world.SidesOpen ? "SIDE-ON" : "SIDE-OFF"; return;
                case "#PEN": text = "PEN-S " + PenSizes[pen]; return;
                case "#SPEED": text = Speeds[speed] < 1 ? "SPEED/2" : "SPEEDx" + Speeds[speed]; return;
                case "#START": text = running ? "STOP" : "START"; return;
                case "#RESET": text = "RESET"; return;
            }
            int id = Elements.IdOf(key);
            if (id < 0) { text = key; return; }
            text = Elements.Defs[id].Label;
            uint c = Elements.Color[id];
            // Brighten so dark materials stay readable on the grey panel.
            color = new Color32(Bright(c, 16), Bright(c, 8), Bright(c, 0), 255);
        }

        static byte Bright(uint rgb, int shift)
        {
            return (byte)Mathf.Min(255, Mathf.RoundToInt(((rgb >> shift) & 255) * 0.7f + 90));
        }

        void DrawStatus(float bottom)
        {
            if (Event.current.type != EventType.Repaint) return;
            var white = new Color32(224, 224, 224, 255);
            var grey = new Color32(160, 160, 160, 255);
            string msg = Time.unscaledTime < flashUntil ? flashText : running ? "POWDER REMAKE" : "PAUSED";
            string stats = fps + "fps dot" + world.Count;
            const string hint = "arrows/WASD: player · F11: full screen";
            if (sideLayout)
            {
                // Two lines at the bottom of the side panel.
                float x = CanvasW + 8, w = SidePanelW - 16, y = bottom - SideStatusH;
                Shadowed(new Rect(x, y, w, SideStatusH / 2), stats, white, status);
                status.alignment = TextAnchor.MiddleRight;
                Shadowed(new Rect(x, y, w, SideStatusH / 2), msg, white, status);
                status.alignment = TextAnchor.MiddleLeft;
                Shadowed(new Rect(x, y + SideStatusH / 2, w, SideStatusH / 2), hint, grey, status);
                return;
            }
            float sy = bottom - StatusH;
            Shadowed(new Rect(8, sy, 220, StatusH), stats, white, status);
            status.alignment = TextAnchor.MiddleCenter;
            Shadowed(new Rect(0, sy, CanvasW, StatusH), msg, white, status);
            status.alignment = TextAnchor.MiddleRight;
            Shadowed(new Rect(0, sy, CanvasW - 8, StatusH), hint, grey, status);
            status.alignment = TextAnchor.MiddleLeft;
        }

        static void Shadowed(Rect r, string text, Color color, GUIStyle style)
        {
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        static void FillRect(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ----------------------------------------------------------- actions

        static string SavePath { get { return Path.Combine(Application.persistentDataPath, "powder-save.bin"); } }

        void DoAction(string key)
        {
            switch (key)
            {
                case "CLEAR": world.Clear(); break;
                case "#RESET": world.Clear(); world.ClearAir(); speed = 0; running = true; break;
                case "#VIEW": airView = !airView; break;
                case "#SIDE": world.SidesOpen = !world.SidesOpen; break;
                case "#PEN": pen = (pen + 1) % PenSizes.Length; break;
                case "#SPEED": speed = (speed + 1) % Speeds.Length; break;
                case "#START": running = !running; break;
                case "SAVE":
                    try
                    {
                        using (var f = File.Create(SavePath)) world.Save(f);
                        Flash("SAVED");
                    }
                    catch (Exception ex) { Debug.LogException(ex); Flash("SAVE FAILED"); }
                    break;
                case "LOAD":
                    try
                    {
                        if (!File.Exists(SavePath)) { Flash("NO SAVE"); break; }
                        using (var f = File.OpenRead(SavePath)) world.Load(f);
                        Flash("LOADED");
                    }
                    catch (Exception ex) { Debug.LogException(ex); Flash("LOAD FAILED"); }
                    break;
            }
        }

        void Flash(string text)
        {
            flashText = text;
            flashUntil = Time.unscaledTime + 1.2f;
        }
    }
}
