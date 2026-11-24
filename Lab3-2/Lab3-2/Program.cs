using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace Labor3_2
{
    internal class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static ImGuiController imGuiController;

        // Két külön kocka objektum (külön színbufferrel), hogy a központi kocka
        // színének módosítása ne hasson a "gyémánt" kockára.
        private static ModelObjectDescriptor centerCube;
        private static ModelObjectDescriptor diamondCube;

        private static CameraDescriptor camera = new CameraDescriptor();

        private static CubeArrangementModel cubeArrangementModel = new CubeArrangementModel();

        private const string ModelMatrixVariableName = "uModel";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private const string LightColorVariableName = "uLightColor";
        private const string AmbientLightColorVariableName = "uAmbientLightColor";
        private const string LightPositionVariableName = "uLightPos";
        private const string ViewPositionVariableName = "uViewPos";

        private const string AmbientStrengthVariableName = "uAmbientStrength";
        private const string DiffuseStrengthVariableName = "uDiffuseStrength";
        private const string SpecularStrengthVariableName = "uSpecularStrength";

        private const string ShinenessVariableName = "uShininess";

        // UI-ról állítható értékek
        private static float shininess = 50;

        // az illuminációs együtthatók színkomponensenként (R, G, B)
        private static Vector3 ambientStrength = new Vector3(0.1f, 0.1f, 0.1f);
        private static Vector3 diffuseStrength = new Vector3(0.3f, 0.3f, 0.3f);
        private static Vector3 specularStrength = new Vector3(0.6f, 0.6f, 0.6f);

        // a háttérfény (ambient fény) színe
        private static Vector3 ambientLightColor = new Vector3(1f, 1f, 1f);

        // a központi kocka oldalainak aktuális színe (a ModelObjectDescriptor eredeti színeivel egyezik)
        private static readonly string[] faceNames = { "Felso", "Elulso", "Bal", "Also", "Hatso", "Jobb" };
        private static int selectedFace = 0;
        private static Vector3[] faceColors = new Vector3[]
        {
            new Vector3(1f, 0f, 0f), // felső
            new Vector3(0f, 1f, 0f), // elülső
            new Vector3(0f, 0f, 1f), // bal
            new Vector3(1f, 0f, 1f), // alsó
            new Vector3(0f, 1f, 1f), // hátsó
            new Vector3(1f, 1f, 0f), // jobb
        };

        private static uint program;

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Grafika szeminárium";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(500, 500);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            centerCube.Dispose();
            diamondCube.Dispose();
            Gl.DeleteProgram(program);
        }

        private static void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();

            var inputContext = graphicWindow.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            // Handle resizes
            graphicWindow.FramebufferResize += s =>
            {
                // Adjust the viewport to the new window size
                Gl.Viewport(s);
            };

            imGuiController = new ImGuiController(Gl, graphicWindow, inputContext);

            centerCube = ModelObjectDescriptor.CreateCube(Gl);
            diamondCube = ModelObjectDescriptor.CreateCube(Gl);

            Gl.ClearColor(System.Drawing.Color.White);

            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, GetEmbeddedResourceAsString("Shaders.VertexShader.vert"));
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, GetEmbeddedResourceAsString("Shaders.FragmentShader.frag"));
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
        }

        private static string GetEmbeddedResourceAsString(string resourceRelativePath)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // az erőforrás teljes nevét a végződése alapján keressük meg,
            // így mindegy, mi a projekt neve vagy a gyökér névtere
            string resourceFullPath = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceRelativePath, StringComparison.OrdinalIgnoreCase));

            if (resourceFullPath == null)
            {
                throw new Exception("Embedded resource not found: " + resourceRelativePath
                    + ". Available: " + string.Join(", ", assembly.GetManifestResourceNames()));
            }

            using (var resStream = assembly.GetManifestResourceStream(resourceFullPath))
            using (var resStreamReader = new StreamReader(resStream))
            {
                return resStreamReader.ReadToEnd();
            }
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.Left:
                    camera.DecreaseZYAngle();
                    break;
                case Key.Right:
                    camera.IncreaseZYAngle();
                    break;
                case Key.Down:
                    camera.IncreaseDistance();
                    break;
                case Key.Up:
                    camera.DecreaseDistance();
                    break;
                case Key.U:
                    camera.IncreaseZXAngle();
                    break;
                case Key.D:
                    camera.DecreaseZXAngle();
                    break;
                case Key.Space:
                    cubeArrangementModel.AnimationEnabled = !cubeArrangementModel.AnimationEnabled;
                    break;
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            // NO OpenGL
            // make it threadsafe
            cubeArrangementModel.AdvanceTime(deltaTime);

            imGuiController.Update((float)deltaTime);
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(program);

            SetUniform3(LightColorVariableName, new Vector3(1f, 1f, 1f));
            SetUniform3(AmbientLightColorVariableName, ambientLightColor);
            SetUniform3(LightPositionVariableName, new Vector3(0f, 1.2f, 0f));
            SetUniform3(ViewPositionVariableName, new Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z));

            SetUniform3(AmbientStrengthVariableName, ambientStrength);
            SetUniform3(DiffuseStrengthVariableName, diffuseStrength);
            SetUniform3(SpecularStrengthVariableName, specularStrength);
            SetUniform1(ShinenessVariableName, shininess);

            var viewMatrix = Matrix4X4.CreateLookAt(camera.Position, camera.Target, camera.UpVector);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 2), 1024f / 768f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);

            var modelMatrixCenterCube = Matrix4X4.CreateScale((float)cubeArrangementModel.CenterCubeScale);
            SetModelMatrix(modelMatrixCenterCube);
            DrawModelObject(centerCube);

            Matrix4X4<float> diamondScale = Matrix4X4.CreateScale(0.25f);
            Matrix4X4<float> rotx = Matrix4X4.CreateRotationX((float)Math.PI / 4f);
            Matrix4X4<float> rotz = Matrix4X4.CreateRotationZ((float)Math.PI / 4f);
            Matrix4X4<float> roty = Matrix4X4.CreateRotationY((float)cubeArrangementModel.DiamondCubeLocalAngle);
            Matrix4X4<float> trans = Matrix4X4.CreateTranslation(1f, 1f, 0f);
            Matrix4X4<float> rotGlobalY = Matrix4X4.CreateRotationY((float)cubeArrangementModel.DiamondCubeGlobalYAngle);
            Matrix4X4<float> dimondCubeModelMatrix = diamondScale * rotx * rotz * roty * trans * rotGlobalY;
            SetModelMatrix(dimondCubeModelMatrix);
            DrawModelObject(diamondCube);

            DrawUi();

            imGuiController.Render();
        }

        private static void DrawUi()
        {
            ImGuiNET.ImGui.Begin("Lighting", ImGuiNET.ImGuiWindowFlags.AlwaysAutoResize | ImGuiNET.ImGuiWindowFlags.NoCollapse);

            // Mindegyik vektornak 3 csúszkája van: R, G, B
            ImGuiNET.ImGui.SliderFloat3("Ambient strength", ref ambientStrength, 0f, 1f);
            ImGuiNET.ImGui.SliderFloat3("Diffuse strength", ref diffuseStrength, 0f, 1f);
            ImGuiNET.ImGui.SliderFloat3("Specular strength", ref specularStrength, 0f, 1f);
            ImGuiNET.ImGui.SliderFloat("Shininess", ref shininess, 5, 100);

            ImGuiNET.ImGui.Separator();

            // háttérfény színe: R, G, B csúszkák
            ImGuiNET.ImGui.SliderFloat3("Ambient light color", ref ambientLightColor, 0f, 1f);

            ImGuiNET.ImGui.Separator();

            // a központi kocka kiválasztott oldalának színe színválasztóval
            ImGuiNET.ImGui.Combo("Center cube face", ref selectedFace, faceNames, faceNames.Length);
            if (ImGuiNET.ImGui.ColorEdit3("Face color", ref faceColors[selectedFace]))
            {
                Vector3 c = faceColors[selectedFace];
                centerCube.SetFaceColor(selectedFace, c.X, c.Y, c.Z);
            }

            ImGuiNET.ImGui.End();
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            SetMatrix(modelMatrix, ModelMatrixVariableName);

            // set also the normal matrix
            int location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }

            // G = (M^-1)^T
            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));

            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetUniform1(string uniformName, float uniformValue)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.Uniform1(location, uniformValue);
            CheckError();
        }

        private static unsafe void SetUniform3(string uniformName, Vector3 uniformValue)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.Uniform3(location, uniformValue);
            CheckError();
        }

        private static unsafe void DrawModelObject(ModelObjectDescriptor modelObject)
        {
            Gl.BindVertexArray(modelObject.Vao);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, modelObject.Indices);
            Gl.DrawElements(PrimitiveType.Triangles, modelObject.IndexArrayLength, DrawElementsType.UnsignedInt, null);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }
    }
}
