using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Lab2
{
    internal static class Program
    {
        private static IWindow graphicWindow;
        private static IInputContext inputContext;
        private static IKeyboard keyboard;

        private static GL Gl;

        private static uint program;

        // uniform helyek
        private static int modelLoc;
        private static int rotationLoc;
        private static int viewLoc;
        private static int projLoc;

        // A 27 kis kocka: mindegyiknek saját VAO-ja és saját modell mátrixa van.
        private static uint[] cubeVaos = new uint[27];
        private static Matrix4X4<float>[] cubeModels = new Matrix4X4<float>[27];
        private static bool[] cubeInSlice = new bool[27];   // a forgatott szeleten van-e
        private static uint[] cubeColorBuffers = new uint[27];

        // Közös (minden kockánál azonos) csúcs- és indexpuffer
        private static uint sharedVertexBuffer;
        private static uint sharedIndexBuffer;
        private static uint indexCount;

        private const float CubeSize = 1.0f;   // egy kis kocka éle
        private const float Spacing = 1.08f;   // kockák közepe közti távolság (a különbség a rés)

        // --- kamera (szabad kamera: pozíció + yaw + pitch) ---
        private static Vector3D<float> camPos = new Vector3D<float>(5.0f, 4.0f, 7.0f);
        private static float camYaw;     // Y tengely körüli forgás (radián)
        private static float camPitch;   // fel-le billenés (radián)
        private const float MoveSpeed = 4.0f;   // egység / másodperc
        private const float TurnSpeed = 1.6f;   // radián / másodperc

        private static Matrix4X4<float> projection;

        // --- szelet-forgatás animáció ---
        // A felső réteget (y == 1) forgatjuk az Y tengely körül.
        private static float sliceAngle = 0f;                 // aktuális szög
        private static float sliceTargetAngle = 0f;           // cél szög (90 fok többszöröse)
        private const float SliceSpeed = MathF.PI;            // radián / másodperc (90 fok = 0.5 s)

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;

        uniform mat4 uModel;       // a kocka helye a Rubik kockában
        uniform mat4 uRotation;    // a szelet forgatása (a nem forgó kockáknál egységmátrix)
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec4 outCol;

        void main()
        {
            outCol = vCol;
            gl_Position = uProjection * uView * uRotation * uModel * vec4(vPos, 1.0);
        }
        ";

        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;

        in vec4 outCol;

        void main()
        {
            FragColor = outCol;
        }
        ";

        // Rubik-kocka színek (lapsorrend: +X, -X, +Y, -Y, +Z, -Z)
        private static readonly float[][] FaceColors = new float[][]
        {
            new float[] { 0.85f, 0.10f, 0.10f, 1f }, // +X piros
            new float[] { 1.00f, 0.55f, 0.00f, 1f }, // -X narancs
            new float[] { 1.00f, 1.00f, 1.00f, 1f }, // +Y fehér
            new float[] { 1.00f, 0.85f, 0.00f, 1f }, // -Y sárga
            new float[] { 0.00f, 0.60f, 0.20f, 1f }, // +Z zöld
            new float[] { 0.00f, 0.25f, 0.85f, 1f }, // -Z kék
        };

        private static readonly float[] InnerColor = new float[] { 0.12f, 0.12f, 0.12f, 1f }; // belső lapok: sötétszürke

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "2. Labor 2 - Rubik kocka (nyilak/QE/WASD: kamera, szóköz/backspace: forgatás)";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(700, 700);
            windowOptions.PreferredDepthBufferBits = 24;

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;
            graphicWindow.FramebufferResize += GraphicWindow_FramebufferResize;

            graphicWindow.Run();
        }

        private static unsafe void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();

            Gl.ClearColor(System.Drawing.Color.White);
            Gl.Enable(EnableCap.DepthTest);

            // --- bemenet ---
            inputContext = graphicWindow.CreateInput();
            keyboard = inputContext.Keyboards[0];
            keyboard.KeyDown += Keyboard_KeyDown;

            // --- shaderek ---
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception("Fragment shader failed to compile: " + Gl.GetShaderInfoLog(fshader));

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }

            modelLoc = Gl.GetUniformLocation(program, "uModel");
            rotationLoc = Gl.GetUniformLocation(program, "uRotation");
            viewLoc = Gl.GetUniformLocation(program, "uView");
            projLoc = Gl.GetUniformLocation(program, "uProjection");

            // --- kamera kezdőállapot: a (5,4,7) pontból az origó felé néz ---
            Vector3D<float> toOrigin = Vector3D.Normalize(-camPos);
            camYaw = MathF.Atan2(toOrigin.X, -toOrigin.Z);
            camPitch = MathF.Asin(toOrigin.Y);

            UpdateProjection();

            CreateCubes();

            Console.WriteLine("Iranyitas:");
            Console.WriteLine("  Fel/Le nyil: elore/hatra, Bal/Jobb nyil: oldalra lepes");
            Console.WriteLine("  PageUp/E: fel, PageDown/Q: le");
            Console.WriteLine("  A/D: balra/jobbra fordulas, W/S: felfele/lefele billentes");
            Console.WriteLine("  Szokoz: szelet forgatasa egyik iranyba, Backspace: masik iranyba");
        }

        private static unsafe void UpdateProjection()
        {
            float aspect = graphicWindow.Size.Y == 0 ? 1f : (float)graphicWindow.Size.X / graphicWindow.Size.Y;
            projection = Matrix4X4.CreatePerspectiveFieldOfView(
                Scalar.DegreesToRadians(45f),
                aspect,
                0.1f,
                100f);

            Gl.UseProgram(program);
            Matrix4X4<float> p = projection;
            Gl.UniformMatrix4(projLoc, 1, false, (float*)&p);
        }

        private static void GraphicWindow_FramebufferResize(Vector2D<int> size)
        {
            if (size.X <= 0 || size.Y <= 0)
                return;

            Gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
            UpdateProjection();
        }

        private static unsafe void CreateCubes()
        {
            float h = CubeSize / 2f;

            // A kis kocka modellje: 6 lap x 4 csúcs = 24 csúcs.
            // Minden kockánál ugyanez, ezért csak egyszer hozzuk létre és osztjuk meg.
            float[] vertexArray = new float[] {
                // +X
                +h, -h, -h,   +h, +h, -h,   +h, +h, +h,   +h, -h, +h,
                // -X
                -h, -h, -h,   -h, -h, +h,   -h, +h, +h,   -h, +h, -h,
                // +Y
                -h, +h, -h,   -h, +h, +h,   +h, +h, +h,   +h, +h, -h,
                // -Y
                -h, -h, -h,   +h, -h, -h,   +h, -h, +h,   -h, -h, +h,
                // +Z
                -h, -h, +h,   +h, -h, +h,   +h, +h, +h,   -h, +h, +h,
                // -Z
                -h, -h, -h,   -h, +h, -h,   +h, +h, -h,   +h, -h, -h,
            };

            // laponként 2 háromszög
            uint[] indexArray = new uint[36];
            for (uint f = 0; f < 6; f++)
            {
                uint b = f * 4;
                uint i = f * 6;
                indexArray[i + 0] = b + 0;
                indexArray[i + 1] = b + 1;
                indexArray[i + 2] = b + 2;
                indexArray[i + 3] = b + 0;
                indexArray[i + 4] = b + 2;
                indexArray[i + 5] = b + 3;
            }
            indexCount = (uint)indexArray.Length;

            sharedVertexBuffer = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, sharedVertexBuffer);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw);

            sharedIndexBuffer = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, sharedIndexBuffer);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

            int n = 0;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        // Ennek a kockának a szín tömbje: a külső lapok színesek, a belsők szürkék.
                        float[] colorArray = new float[6 * 4 * 4];
                        for (int f = 0; f < 6; f++)
                        {
                            bool outer = f switch
                            {
                                0 => x == 1,
                                1 => x == -1,
                                2 => y == 1,
                                3 => y == -1,
                                4 => z == 1,
                                _ => z == -1,
                            };

                            float[] c = outer ? FaceColors[f] : InnerColor;
                            for (int v = 0; v < 4; v++)
                            {
                                int o = (f * 4 + v) * 4;
                                colorArray[o + 0] = c[0];
                                colorArray[o + 1] = c[1];
                                colorArray[o + 2] = c[2];
                                colorArray[o + 3] = c[3];
                            }
                        }

                        // saját VAO
                        uint vao = Gl.GenVertexArray();
                        Gl.BindVertexArray(vao);

                        // 0. attribútum: a közös csúcspuffer
                        Gl.BindBuffer(GLEnum.ArrayBuffer, sharedVertexBuffer);
                        Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, null);
                        Gl.EnableVertexAttribArray(0);

                        // 1. attribútum: ennek a kockának a saját szín puffere
                        uint colors = Gl.GenBuffer();
                        Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
                        Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
                        Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
                        Gl.EnableVertexAttribArray(1);

                        // a közös indexpuffer (a VAO-ban tárolódik)
                        Gl.BindBuffer(GLEnum.ElementArrayBuffer, sharedIndexBuffer);

                        Gl.BindVertexArray(0);
                        Gl.BindBuffer(GLEnum.ArrayBuffer, 0);

                        cubeVaos[n] = vao;
                        cubeColorBuffers[n] = colors;
                        cubeModels[n] = Matrix4X4.CreateTranslation(
                            new Vector3D<float>(x * Spacing, y * Spacing, z * Spacing));
                        cubeInSlice[n] = (y == 1);   // a felső réteg forog
                        n++;
                    }
                }
            }
        }

        private static void Keyboard_KeyDown(IKeyboard kb, Key key, int scancode)
        {
            // Az animáció közben is lehet nyomni: a cél szög egyszerűen tovább nő/csökken,
            // az animáció pedig utolérni igyekszik.
            switch (key)
            {
                case Key.Space:
                    sliceTargetAngle += MathF.PI / 2f;
                    break;
                case Key.Backspace:
                    sliceTargetAngle -= MathF.PI / 2f;
                    break;
                case Key.Escape:
                    graphicWindow.Close();
                    break;
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            // NO GL
            float dt = (float)deltaTime;

            // --- kamera forgatás ---
            if (keyboard.IsKeyPressed(Key.A)) camYaw -= TurnSpeed * dt;
            if (keyboard.IsKeyPressed(Key.D)) camYaw += TurnSpeed * dt;
            if (keyboard.IsKeyPressed(Key.W)) camPitch += TurnSpeed * dt;
            if (keyboard.IsKeyPressed(Key.S)) camPitch -= TurnSpeed * dt;

            float maxPitch = Scalar.DegreesToRadians(89f);
            camPitch = Math.Clamp(camPitch, -maxPitch, maxPitch);

            // --- kamera mozgatás ---
            float cosP = MathF.Cos(camPitch);
            Vector3D<float> forward = new Vector3D<float>(
                MathF.Sin(camYaw) * cosP,
                MathF.Sin(camPitch),
                -MathF.Cos(camYaw) * cosP);
            Vector3D<float> right = new Vector3D<float>(MathF.Cos(camYaw), 0f, MathF.Sin(camYaw));
            Vector3D<float> up = new Vector3D<float>(0f, 1f, 0f);

            float step = MoveSpeed * dt;
            if (keyboard.IsKeyPressed(Key.Up)) camPos += forward * step;
            if (keyboard.IsKeyPressed(Key.Down)) camPos -= forward * step;
            if (keyboard.IsKeyPressed(Key.Right)) camPos += right * step;
            if (keyboard.IsKeyPressed(Key.Left)) camPos -= right * step;
            if (keyboard.IsKeyPressed(Key.PageUp) || keyboard.IsKeyPressed(Key.E)) camPos += up * step;
            if (keyboard.IsKeyPressed(Key.PageDown) || keyboard.IsKeyPressed(Key.Q)) camPos -= up * step;

            // --- szelet animáció: az aktuális szög közelít a célszöghöz ---
            float diff = sliceTargetAngle - sliceAngle;
            float maxDelta = SliceSpeed * dt;
            if (MathF.Abs(diff) <= maxDelta)
                sliceAngle = sliceTargetAngle;
            else
                sliceAngle += MathF.Sign(diff) * maxDelta;
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(program);

            // nézeti mátrix: a kamera pozíciójából a nézési irányba
            float cosP = MathF.Cos(camPitch);
            Vector3D<float> forward = new Vector3D<float>(
                MathF.Sin(camYaw) * cosP,
                MathF.Sin(camPitch),
                -MathF.Cos(camYaw) * cosP);

            Matrix4X4<float> view = Matrix4X4.CreateLookAt(
                camPos,
                camPos + forward,
                new Vector3D<float>(0f, 1f, 0f));
            Gl.UniformMatrix4(viewLoc, 1, false, (float*)&view);

            Matrix4X4<float> sliceRotation = Matrix4X4.CreateRotationY(sliceAngle);
            Matrix4X4<float> identity = Matrix4X4<float>.Identity;

            for (int i = 0; i < cubeVaos.Length; i++)
            {
                Matrix4X4<float> model = cubeModels[i];
                Gl.UniformMatrix4(modelLoc, 1, false, (float*)&model);

                // a forgó szelet kockái kapják a forgatást, a többi egységmátrixot
                Matrix4X4<float> rot = cubeInSlice[i] ? sliceRotation : identity;
                Gl.UniformMatrix4(rotationLoc, 1, false, (float*)&rot);

                Gl.BindVertexArray(cubeVaos[i]);
                Gl.DrawElements(GLEnum.Triangles, indexCount, GLEnum.UnsignedInt, null);
            }

            Gl.BindVertexArray(0);
        }

        private static void GraphicWindow_Closing()
        {
            for (int i = 0; i < cubeVaos.Length; i++)
            {
                Gl.DeleteBuffer(cubeColorBuffers[i]);
                Gl.DeleteVertexArray(cubeVaos[i]);
            }
            Gl.DeleteBuffer(sharedVertexBuffer);
            Gl.DeleteBuffer(sharedIndexBuffer);
            Gl.DeleteProgram(program);

            inputContext?.Dispose();
        }
    }
}