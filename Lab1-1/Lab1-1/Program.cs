
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Lab1
{
    internal static class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static uint program;

        // Uj: Itt valaszthato ki, melyik hibat szeretnenk kiprobalni.
        // 0 = nincs hiba
        // 1 = hibas vertex shader
        // 2 = hibas fragment shader
        // 3 = AttachShader kihagyasa
        // 4 = LinkProgram kihagyasa
        // 5 = BindBuffer kihagyasa
        // 6 = EnableVertexAttribArray(0) kihagyasa
        // 7 = hibas attribútumindex
        // 8 = hibas vertexindex
        // 9 = BufferData elott nincs buffer kotve
        // 10 = LinkProgram a CompileShader elott
        private static int TestError = 9;

        // Uj: OpenGL globalis hibakezeles
        private static void CheckGlError(string location)
        {
            GLEnum error;

            while ((error = Gl.GetError()) != GLEnum.NoError)
            {
                Console.WriteLine($"OpenGL hiba itt: {location} - {error}");
            }
        }

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;

        out vec4 outCol;

        void main()
        {
            outCol = vCol;
            gl_Position = vec4(vPos.x, vPos.y, vPos.z, 1.0);
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

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "1. szeminárium - háromszög";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(500, 500);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Load()
        {
            // egszeri beallitasokat
            //Console.WriteLine("Loaded");

            Gl = graphicWindow.CreateOpenGL();
            CheckGlError("OpenGL inicializalas");

            Gl.ClearColor(System.Drawing.Color.White);
            CheckGlError("ClearColor");

            string vertexSource = VertexShaderSource;
            string fragmentSource = FragmentShaderSource;

            // Uj: Shader stringek szandekos elrontasa
            if (TestError == 1)
                vertexSource = vertexSource.Replace(
                    "vec4(vPos.x, vPos.y, vPos.z, 1.0)",
                    "vec4(vPos.x, vPos.y, )");

            if (TestError == 2)
                fragmentSource = fragmentSource.Replace(
                    "FragColor = outCol;",
                    "FragColor = ;");

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);
            CheckGlError("Shader objektumok letrehozasa");

            Gl.ShaderSource(vshader, vertexSource);

            // Uj: 10-es tesztben a linkeles a forditas elott tortenik.
            if (TestError == 10)
            {
                program = Gl.CreateProgram();
                Gl.AttachShader(program, vshader);
                Gl.AttachShader(program, fshader);
                Gl.LinkProgram(program);
                CheckGlError("Linkeles CompileShader elott");
            }

            Gl.CompileShader(vshader);
            CheckGlError("Vertex shader forditasa");

            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                Console.WriteLine("Vertex shader hiba: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, fragmentSource);
            Gl.CompileShader(fshader);
            CheckGlError("Fragment shader forditasa");

            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                Console.WriteLine("Fragment shader hiba: " + Gl.GetShaderInfoLog(fshader));

            program = Gl.CreateProgram();
            CheckGlError("Program letrehozasa");

            Gl.AttachShader(program, vshader);

            // Uj: 3-as tesztben kihagyjuk a fragment shader csatolasat.
            if (TestError != 3)
                Gl.AttachShader(program, fshader);

            CheckGlError("Shaderek csatolasa");

            // Uj: 4-es tesztben kihagyjuk a linkelest.
            if (TestError != 4 && TestError != 10)
                Gl.LinkProgram(program);

            CheckGlError("Program linkelese");

            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);

            if (status == 0)
            {
                Console.WriteLine(
                    $"Error linking shader: {Gl.GetProgramInfoLog(program)}");

                CheckGlError("Program linkeles ellenorzese");

                // A program nincs megfeleloen linkelve, ne folytassuk.
                throw new Exception("A shaderprogram linkelese sikertelen.");
            }

            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);
            CheckGlError("Shaderek levalasztasa es torlese");
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            // NO GL
            // make it threadsave
            //Console.WriteLine($"Update after {deltaTime} [s]");
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s]");

            Gl.Clear(ClearBufferMask.ColorBufferBit);
            CheckGlError("Kepernyo torlese");

            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);
            CheckGlError("VAO letrehozasa es kotese");

            float[] vertexArray = new float[] {
                -0.5f, -0.5f, 0.0f,
                +0.5f, -0.5f, 0.0f,
                 0.0f, +0.5f, 0.0f,
                 1f, 1f, 0f
            };

            float[] colorArray = new float[] {
                1.0f, 0.0f, 0.0f, 1.0f,
                0.0f, 1.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f, 1.0f,
                1.0f, 0.0f, 0.0f, 1.0f,
            };

            uint[] indexArray = new uint[] {
                0, 1, 2,
                2, 1, 3
            };

            // Uj: 8-as tesztben ervenytelen vertexindexet hasznalunk.
            if (TestError == 8)
                indexArray[2] = 20;

            uint vertices = Gl.GenBuffer();

            // Uj: 5-os tesztben kihagyjuk a buffer kotest.
            if (TestError != 5 && TestError != 9)
                Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);

            CheckGlError("Vertex buffer kotese");

            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw);
            CheckGlError("Vertex adatok feltoltese");

            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, null);

            // Uj: 6-os tesztben a 0-s attribútum marad kikapcsolva.
            if (TestError != 6)
            {
                Gl.EnableVertexAttribArray(0);
            }
            else
            {
                Console.WriteLine("Teszt 6: a 0-as vertexattribútum engedelyezese kimaradt.");
            }

            CheckGlError("Vertex attribútum beallitasa");

            // Uj: 7-es tesztben rossz attribútumindexet engedelyezunk.
            if (TestError == 7)
            {
                Gl.EnableVertexAttribArray(5);
                Console.WriteLine("Teszt 7: az 5-os attribútum engedelyezve, a 0-s letiltva.");
            }

            CheckGlError("Vertex attribútum beallitasa");

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            CheckGlError("Color buffer kotese");

            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
            CheckGlError("Color adatok feltoltese");

            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);
            CheckGlError("Szin attribútum beallitasa");

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            CheckGlError("Index buffer kotese");

            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);
            CheckGlError("Index adatok feltoltese");

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            CheckGlError("Array buffer levalasztasa");

            Gl.UseProgram(program);
            CheckGlError("Shader program aktivalasa");

            Gl.DrawElements(GLEnum.Triangles, (uint)indexArray.Length, GLEnum.UnsignedInt, null); // we used element buffer
            CheckGlError("DrawElements");

            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            Gl.BindVertexArray(vao);

            // always unbound the vertex buffer first, so no halfway results are displayed by accident
            Gl.DeleteBuffer(vertices);
            Gl.DeleteBuffer(colors);
            Gl.DeleteBuffer(indices);
            Gl.DeleteVertexArray(vao);
            CheckGlError("Bufferek es VAO torlese");
        }
    }
}
