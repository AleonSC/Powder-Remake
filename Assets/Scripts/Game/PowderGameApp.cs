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
        // The layout is designed in virtual units (Dot per world dot) and scaled to fill
        // the window. The sandbox takes the window's shape, with the menu underneath.
        const float Dot = 2f;
        const float RowH = 24f, MenuPad = 8f, StatusH = 26f, MenuW = 800f;
        // Smallest sandbox (the original's) and largest (to keep the frame rate up), in dots.
        const int MinW = 400, MinH = 300, MaxW = 1024, MaxH = 640;
        // Wait for the window to stop changing size before rebuilding the world.
        const float ResizeDelay = 0.3f;
        const float StepTime = 1f / 60f;

        static readonly int[] PenSizes = { 0, 1, 2, 3, 4, 6, 8, 12, 16 };
        static readonly float[] Speeds = { 1f, 2f, 0.5f };

        // Element keys, tool names, or #actions. The first three columns keep the
        // original's elements in its order; the fourth holds the newer materials.
        static readonly string[][] Menu =
        {
            new[] { "DIRT", "FIREWORKS", "GAS", "SAND", "WIND", "SAVE" },
            new[] { "WATER", "OIL", "SOAPY", "GLASS", "AIR", "LOAD" },
            new[] { "FIRE", "C4", "THUNDER", "SALT", "DRAG", "#VIEW" },
            new[] { "SEED", "STONE", "METAL", "SNOW", "WALL", "#SIDE" },
            new[] { "GUNPOWDER", "MAGMA", "BOMB", "WOOD", "ERASE", "#PEN" },
            new[] { "FAN", "VIRUS", "LASER", "MERCURY", "PLAYER", "#SPEED" },
            new[] { "ICE", "NITRO", "ACID", "CLOUD", "FIGHTER", "#START" },
            new[] { "SUPERBALL", "ANT", "BUBBLE", "STEAM", "CLEAR", "#RESET" },
            new[] { "CLONE", "TORCH", "SMOKE", "CRYO", "VOID", "#SUN" },
        };

        static float MenuH { get { return MenuPad + Menu.Length * RowH + StatusH; } }

        static readonly uint[] Hues = { 0xff4848, 0x48ff48, 0x5090ff, 0xffff48, 0xff48ff, 0x48ffff, 0xffa030, 0xffffff };

        World world;
        int W, H;                 // world size in dots
        float canvasW, canvasH;   // world size in virtual units
        float resizePending = -1; // time the window size started differing from the world's
        Texture2D screen;
        Color32[] pixels, airBg;
        readonly Color32[] lut = new Color32[256 * 16];
        readonly Color32[] fireLut = new Color32[64];
        readonly Color32[] sparkLut = new Color32[8 * 16];

        string left = "DIRT", right = "ERASE";
        int pen = 3, speed;
        bool running = true, airView;
        float acc;

        // Cursor in world dots.
        Vector2 mouse = new Vector2(-100, -100), prevMouse;
        Vector2 dir = new Vector2(0, 1);
        int mouseButton = -1; // 0 = left, 1 = right
        bool mousePressed;    // a button went down since the last ApplyInput
        int spawnTimer;       // frames until the next fighter while the button is held
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
            int w, h;
            TargetSize(out w, out h);
            SetWorld(new World(w, h));
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

        void SetWorld(World w)
        {
            world = w;
            W = w.W;
            H = w.H;
            canvasW = W * Dot;
            canvasH = H * Dot;
            if (screen != null) Destroy(screen);
            screen = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[W * H];
            airBg = new Color32[w.AN];
        }

        /// <summary>The sandbox size, in dots, that fills the window with the menu underneath.</summary>
        static void TargetSize(out int w, out int h)
        {
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            // Scale so the original 400x300 layout just fits, then grow the sandbox into the rest.
            float s = Mathf.Min(sw / (MinW * Dot), sh / (MinH * Dot + MenuH));
            w = Mathf.Clamp(Mathf.FloorToInt(sw / s / Dot / World.AS) * World.AS, MinW, MaxW);
            h = Mathf.Clamp(Mathf.FloorToInt((sh / s - MenuH) / Dot / World.AS) * World.AS, MinH, MaxH);
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

            int tw, th;
            TargetSize(out tw, out th);
            if (tw == W && th == H) resizePending = -1;
            else if (resizePending < 0) resizePending = Time.unscaledTime;
            else if (Time.unscaledTime - resizePending > ResizeDelay)
            {
                SetWorld(world.Resized(tw, th));
                resizePending = -1;
            }

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
                else if (tool == "FIGHTER")
                {
                    // One per click, then a steady stream while held.
                    if (pressed) spawnTimer = 0;
                    if (--spawnTimer <= 0)
                    {
                        spawnTimer = 15;
                        if (world.AddFighter(mx, my + Player.Height / 2) == null && pressed)
                            Flash(world.Fighters.Count >= World.MaxFighters ? "MAX " + World.MaxFighters + " FIGHTERS" : "NO ROOM");
                    }
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
            // Sunlit sky is faintly blue, so clouds and roofs cast visible shadows.
            bool sun = world.Sun != World.SunLevel.Off;
            var sky = world.Sun == World.SunLevel.High ? new Color32(18, 22, 48, 255) : new Color32(9, 11, 26, 255);
            int[] sunTop = world.SunTop;
            var charged = new Color32(255, 255, 192, 255);
            var laser = new Color32(255, 64, 64, 255);
            var bolt = new Color32(255, 255, 192, 255);

            if (airView)
            {
                float[] ap = world.Pressure, avx = world.AirVx, avy = world.AirVy;
                for (int i = 0; i < world.AN; i++)
                {
                    float p = ap[i], v = Mathf.Sqrt(avx[i] * avx[i] + avy[i] * avy[i]);
                    airBg[i] = new Color32((byte)Mathf.Min(255, p > 0 ? p * 12 : 0), (byte)Mathf.Min(255, v * 25),
                        (byte)Mathf.Min(255, p < 0 ? -p * 12 : 0), 255);
                }
            }

            for (int y = 0; y < H; y++)
            {
                int row = y * W, arow = (y >> 2) * world.AW, dst = (H - 1 - y) * W; // textures are bottom-up
                for (int x = 0; x < W; x++)
                {
                    int i = row + x;
                    byte t = type[i];
                    Color32 c;
                    switch (t)
                    {
                        case E.Empty: c = airView ? airBg[arow + (x >> 2)] : sun && y < sunTop[x] ? sky : bg; break;
                        case E.Fire: c = fireLut[Mathf.Min(63, life[i] * 2)]; break;
                        case E.Magma: c = lut[t * 16 + (((shade[i] + frame) >> 2) & 15)]; break;
                        case E.Metal: case E.Mercury: c = life[i] > 0 ? charged : lut[t * 16 + (shade[i] >> 4)]; break;
                        case E.Wood: // glowing embers while burning
                            c = life[i] > 0 ? fireLut[24 + ((shade[i] + frame) & 31)] : lut[t * 16 + (shade[i] >> 4)];
                            break;
                        case E.Smoke: // fades out
                        {
                            Color32 s0 = lut[t * 16 + (shade[i] >> 4)];
                            float f = Mathf.Min(1, life[i] / 40f);
                            c = new Color32((byte)(s0.r * f), (byte)(s0.g * f), (byte)(s0.b * f), 255);
                            break;
                        }
                        case E.Void: c = lut[t * 16 + (((shade[i] + frame) >> 1) & 15)]; break;
                        case E.Cloud: // darker the more steam it holds
                        {
                            Color32 s0 = lut[t * 16 + (shade[i] >> 4)];
                            float f = 1 - 0.6f * Mathf.Min(1, (float)life[i] / World.CloudCapacity);
                            c = new Color32((byte)(s0.r * f), (byte)(s0.g * f), (byte)(s0.b * f), 255);
                            break;
                        }
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
        static readonly int[] TorsoY = { -6, -5, -4 };
        static readonly int[] LegsAX = { 0, -1, -1, -2, 1, 1, 2 }; // stride
        static readonly int[] LegsAY = { -3, -2, -1, 0, -2, -1, 0 };
        static readonly int[] LegsBX = { 0, 0, -1, -1, 1, 1 };      // passing
        static readonly int[] LegsBY = { -3, -2, -1, 0, -1, 0 };

        void DrawPlayers()
        {
            var playerBody = new Color32(232, 232, 232, 255);
            var fighterBody = new Color32(255, 128, 128, 255);
            var hurt = new Color32(255, 240, 96, 255);
            for (int s = 0; s < World.MaxPlayers; s++)
            {
                Player p = world.Players[s];
                if (p == null) continue;
                Color32 body = p.Hurt > 0 ? hurt : playerBody;
                DrawFigure(p, body, p.Held != 0 ? lut[p.Held * 16 + 12] : body);
            }
            foreach (Player f in world.Fighters)
            {
                Color32 body = f.Hurt > 0 ? hurt : fighterBody;
                DrawFigure(f, body, body);
            }
        }

        void DrawFigure(Player p, Color32 body, Color32 head)
        {
            int fx = Mathf.FloorToInt(p.X), fy = Mathf.FloorToInt(p.Y);
            for (int y = -9; y <= -7; y++)
                for (int x = -1; x <= 1; x++) Plot(fx + x, fy + y, head);
            foreach (int y in TorsoY) Plot(fx, fy + y, body);
            // Arms hang down, except the front one shoots straight out while punching.
            for (int side = -1; side <= 1; side += 2)
            {
                if (p.Punch > 0 && side == p.Facing)
                    for (int k = 1; k <= 3; k++) Plot(fx + side * k, fy - 5, body);
                else
                {
                    Plot(fx + side, fy - 5, body);
                    Plot(fx + side * 2, fy - 4, body);
                }
            }
            bool stride = !p.Grounded || ((p.Walk >> 3) & 1) == 0;
            int[] lx = stride ? LegsAX : LegsBX, ly = stride ? LegsAY : LegsBY;
            for (int k = 0; k < lx.Length; k++) Plot(fx + lx[k], fy + ly[k], body);
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

            float vw = canvasW, vh = canvasH + MenuH;
            float s = Mathf.Min(Screen.width / vw, Screen.height / vh);
            var offset = new Vector3((Screen.width - vw * s) / 2, (Screen.height - vh * s) / 2, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1));

            Event e = Event.current;
            HandleCanvasEvents(e);

            if (e.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(0, 0, canvasW, canvasH), screen);
                FillRect(new Rect(0, canvasH, vw, vh - canvasH), new Color32(64, 64, 64, 255));
            }
            DrawMenu(e);
            DrawStatus(vh);
        }

        /// <summary>Screen rectangle of the menu entry at Menu[row][col]; the menu is centred under the sandbox.</summary>
        Rect MenuCell(int row, int col)
        {
            float colW = MenuW / 6, x0 = (canvasW - MenuW) / 2;
            return new Rect(x0 + col * colW, canvasH + MenuPad + row * RowH, colW, RowH);
        }

        void HandleCanvasEvents(Event e)
        {
            Vector2 mp = e.mousePosition;
            hover = mp.x >= 0 && mp.x < canvasW && mp.y >= 0 && mp.y < canvasH;
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
                case "FIGHTER": text = key; color = new Color32(255, 140, 140, 255); return;
                case "#VIEW": text = airView ? "VIEW AIR" : "VIEW DOT"; return;
                case "#SIDE": text = world.SidesOpen ? "SIDE-ON" : "SIDE-OFF"; return;
                case "#PEN": text = "PEN-S " + PenSizes[pen]; return;
                case "#SPEED": text = Speeds[speed] < 1 ? "SPEED/2" : "SPEEDx" + Speeds[speed]; return;
                case "#START": text = running ? "STOP" : "START"; return;
                case "#RESET": text = "RESET"; return;
                case "#SUN":
                    text = world.Sun == World.SunLevel.Off ? "SUN OFF" : world.Sun == World.SunLevel.Low ? "SUN LOW" : "SUN HIGH";
                    color = new Color32(255, 220, 96, 255);
                    return;
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
            float sy = bottom - StatusH;
            Shadowed(new Rect(8, sy, 220, StatusH), stats, white, status);
            status.alignment = TextAnchor.MiddleCenter;
            Shadowed(new Rect(0, sy, canvasW, StatusH), msg, white, status);
            status.alignment = TextAnchor.MiddleRight;
            Shadowed(new Rect(0, sy, canvasW - 8, StatusH), hint, grey, status);
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
                case "#RESET": world.Clear(); world.ClearAir(); world.Sun = World.SunLevel.Off; speed = 0; running = true; break;
                case "#SUN": world.Sun = (World.SunLevel)(((int)world.Sun + 1) % 3); break;
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
