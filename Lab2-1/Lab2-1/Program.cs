using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Lab2
{
    internal static class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static uint program;

        // uniform helyek
        private static int modelLoc;
        private static int viewLoc;
        private static int projLoc;

        // A 27 kis kocka: mindegyiknek saját VAO-ja és saját modell mátrixa van.
        private static uint[] cubeVaos = new uint[27];
        private static Matrix4X4<float>[] cubeModels = new Matrix4X4<float>[27];
        private static uint[] cubeColorBuffers = new uint[27];

        // Közös (minden kockánál azonos) csúcs- és indexpuffer
        private static uint sharedVertexBuffer;
        private static uint sharedIndexBuffer;
        private static uint indexCount;

        private const float CubeSize = 1.0f;   // egy kis kocka éle
        private const float Spacing = 1.08f;   // kockák közepe közti távolság (a különbség a rés)

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;
 
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
 
        out vec4 outCol;
 
        void main()
        {
            outCol = vCol;
            gl_Position = uProjection * uView * uModel * vec4(vPos, 1.0);
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
            windowOptions.Title = "2. labor 1 - Rubik kocka";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(700, 700);
            windowOptions.PreferredDepthBufferBits = 24;

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static unsafe void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();

            Gl.ClearColor(System.Drawing.Color.White);
            Gl.Enable(EnableCap.DepthTest);

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
            viewLoc = Gl.GetUniformLocation(program, "uView");
            projLoc = Gl.GetUniformLocation(program, "uProjection");

            // --- kamera és projekció: fix értékek ---
            Matrix4X4<float> view = Matrix4X4.CreateLookAt(
                new Vector3D<float>(5.0f, 4.0f, 7.0f),   // kamera pozíciója (érdekes, ferde szög)
                new Vector3D<float>(0f, 0f, 0f),          // ide néz
                new Vector3D<float>(0f, 1f, 0f));         // "fel" irány

            Matrix4X4<float> projection = Matrix4X4.CreatePerspectiveFieldOfView(
                Scalar.DegreesToRadians(45f),
                (float)graphicWindow.Size.X / graphicWindow.Size.Y,
                0.1f,
                100f);

            Gl.UseProgram(program);
            Gl.UniformMatrix4(viewLoc, 1, false, (float*)&view);
            Gl.UniformMatrix4(projLoc, 1, false, (float*)&projection);

            CreateCubes();
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
                        n++;
                    }
                }
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            // NO GL
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(program);

            for (int i = 0; i < cubeVaos.Length; i++)
            {
                Matrix4X4<float> model = cubeModels[i];
                Gl.UniformMatrix4(modelLoc, 1, false, (float*)&model);

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
        }
    }
}