using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// Presentation only. PongLogic remains responsible for movement and collisions.
public partial class OceanPolish : Node3D
{
    public bool LayerEnabled { get; private set; } = true;
    public float Shake { get; private set; }
    public float Staging { get; private set; }
    private bool initialized;
    private readonly Node3D ocean = new();
    private readonly List<Node3D> fish = new();
    private readonly List<Node3D> originalPaddleVisuals = new();
    private readonly List<Node3D> weeds = new();
    private readonly MeshInstance3D pearl = new();
    private readonly WorldEnvironment environmentNode = new();
    private readonly Godot.Environment oceanEnvironment = new();
    private readonly Dictionary<string, AudioStreamPlayer> sounds = new();
    private readonly Dictionary<string, ArrayMesh> meshCache = new();
    private readonly RandomNumberGenerator random = new();
    private readonly float[] lastZ = new float[2];
    private readonly float[] lastSign = new float[2];
    private readonly float[] pulse = new float[2];
    private readonly int[] scores = new int[2];
    private Node3D[] paddles;
    private PongLogic controller;
    private MeshInstance3D ball;
    private Camera3D camera;
    private Transform3D originalCamera;
    private float originalFov;
    private Camera3D.ProjectionType originalProjection;
    private float originalCameraSize;
    private uint originalBallLayers;
    private float ballPulse;
    private Vector3 ballDirection = Vector3.Right;
    private float elapsed;
    private Label scoreLabel;
    private CheckButton toggle;

    private StandardMaterial3D Material(Color color, float glow = 0)
    {
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.65f };
        if (glow > 0)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = glow;
        }
        return material;
    }

    private ArrayMesh Model(string name)
    {
        if (meshCache.TryGetValue(name, out var cached)) return cached;
        using var data = JsonDocument.Parse(FileAccess.GetFileAsString($"res://Ocean/Models/{name}.json"));
        var vertices = data.RootElement.GetProperty("vertices");
        var colors = data.RootElement.GetProperty("colors");
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < vertices.GetArrayLength() / 3; i++)
        {
            surface.SetColor(new Color(colors[i * 4].GetSingle(), colors[i * 4 + 1].GetSingle(), colors[i * 4 + 2].GetSingle()));
            surface.AddVertex(new Vector3(vertices[i * 3].GetSingle(), vertices[i * 3 + 1].GetSingle(), vertices[i * 3 + 2].GetSingle()));
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        var material = Material(Colors.White);
        material.VertexColorUseAsAlbedo = true;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mesh.SurfaceSetMaterial(0, material);
        meshCache[name] = mesh;
        return mesh;
    }

    private MeshInstance3D PlaceModel(string name, Vector3 position, Vector3 size)
    {
        var model = new MeshInstance3D { Mesh = Model(name), Position = position, Scale = size };
        ocean.AddChild(model);
        return model;
    }

    public override void _Ready()
    {
        random.Randomize();
        controller = GetParent().GetNode<PongLogic>("PaddleController");
        ball = GetParent().GetNode<MeshInstance3D>("Ball");
        paddles = new[] { controller.GetNode<Node3D>("LeftPaddle"), controller.GetNode<Node3D>("RightPaddle") };
        camera = GetParent().GetNode<Camera3D>("Camera3D");
        originalCamera = camera.Transform;
        originalFov = camera.Fov;
        originalProjection = camera.Projection;
        originalCameraSize = camera.Size;
        originalBallLayers = ball.Layers;
        AddChild(ocean);
        // Rotate only the visual world into an upright aquarium screen.
        // Original Pong continues to calculate movement in its X/Z plane.
        ocean.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
        // Separate visuals leave the paddle scales used by the rules unchanged.
        for (int i = 0; i < 2; i++)
        {
            var visual = new Node3D();
            ocean.AddChild(visual);
            fish.Add(visual);
            var mesh = new MeshInstance3D
            {
                Mesh = Model(i == 0 ? "fish_orange" : "fish_blue"),
                Rotation = new Vector3(0, i == 0 ? 0 : Mathf.Pi, 0),
                Scale = new Vector3(0.22f, 1.1f, 0.5f)
            };
            visual.AddChild(mesh);
            AddFishDetails(visual, i);
            originalPaddleVisuals.Add(paddles[i].GetChild<Node3D>(0));
            lastZ[i] = paddles[i].Position.Z;
        }
        pearl.Mesh = new SphereMesh { Radius = 0.052f, Height = 0.104f };
        pearl.MaterialOverride = Material(new Color("#fff4cc"), 0.6f);
        ocean.AddChild(pearl);
        BuildArena();
        var keyLight = new OmniLight3D { Position = new Vector3(-1.2f, 1.8f, -0.7f), LightColor = new Color("#fff0cf"), LightEnergy = 0.9f, OmniRange = 6 };
        ocean.AddChild(keyLight);
        var fillLight = new OmniLight3D { Position = new Vector3(1.6f, 1.0f, 0.3f), LightColor = new Color("#7be7ff"), LightEnergy = 0.5f, OmniRange = 5 };
        ocean.AddChild(fillLight);
        oceanEnvironment.BackgroundMode = Godot.Environment.BGMode.Color;
        oceanEnvironment.BackgroundColor = new Color("#062c46");
        oceanEnvironment.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        oceanEnvironment.AmbientLightColor = new Color("#82d8e2");
        oceanEnvironment.AmbientLightEnergy = 0.65f;
        environmentNode.Environment = oceanEnvironment;
        AddChild(environmentNode);
        foreach (string key in new[] { "impact", "smash", "start", "stop" })
        {
            var audio = new AudioStreamPlayer
            {
                Stream = GD.Load<AudioStream>($"res://Ocean/Audio/{key}.wav"),
                VolumeDb = key == "impact" ? -10 : -6
            };
            AddChild(audio);
            sounds[key] = audio;
        }
        BuildUi();
        initialized = true;
        SetEnabled(true);
        RestartShow();
    }

    private MeshInstance3D Sphere(Node3D parent, Vector3 position, Vector3 size, Color color)
    {
        var shape = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1, RadialSegments = 32, Rings = 16 },
            Position = position, Scale = size, MaterialOverride = Material(color)
        };
        parent.AddChild(shape);
        return shape;
    }

    private void AddFishDetails(Node3D parent, int index)
    {
        float facing = index == 0 ? 1 : -1;
        // Rounded body under the Kenney-derived fins and coloured silhouette.
        Sphere(parent, new Vector3(0.025f * facing, -0.025f, 0), new Vector3(0.19f, 0.20f, 0.37f),
            new Color(index == 0 ? "#f9a328" : "#38b7ec"));
        var finColor = new Color(index == 0 ? "#f6b82c" : "#33b8de");
        AddFin(parent, new Vector3(-0.065f*facing,0.04f,0),
            new Vector3(-0.22f*facing,0.04f,-0.09f), new Vector3(-0.22f*facing,0.04f,0.09f),finColor);
        AddFin(parent, new Vector3(-0.04f*facing,0.02f,-0.12f),
            new Vector3(0.025f*facing,0.02f,-0.23f),new Vector3(0.08f*facing,0.02f,-0.14f),finColor);
        // Two protruding eyes, pupils and small catchlights, all real 3D spheres.
        foreach (float x in new[] { 0.035f, 0.092f })
        {
            var eye = new Vector3(x * facing, 0.105f, -0.065f);
            Sphere(parent, eye, new Vector3(0.075f, 0.065f, 0.085f), new Color("#fff9e7"));
            Sphere(parent, eye + new Vector3(0.008f * facing, 0.035f, 0.002f),
                new Vector3(0.035f, 0.022f, 0.045f), new Color("#122535"));
            Sphere(parent, eye + new Vector3(-0.001f, 0.047f, -0.009f),
                Vector3.One * 0.012f, Colors.White);
        }
    }

    private void AddFin(Node3D parent, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var points = new[] { a,b,c,a+Vector3.Up*0.025f,b+Vector3.Up*0.025f,c+Vector3.Up*0.025f };
        int[] triangles = { 0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5 };
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (int index in triangles) surface.AddVertex(points[index]);
        surface.GenerateNormals();
        var material = Material(color);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        parent.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = material });
    }

    private void CoralBranch(Vector3 start, Vector3 end, Color color, float radius)
    {
        Vector3 direction = end - start;
        var branch = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius * 0.7f, BottomRadius = radius, Height = direction.Length(), RadialSegments = 12 },
            Position = (start + end) / 2,
            Quaternion = new Quaternion(Vector3.Up, direction.Normalized()),
            MaterialOverride = Material(color)
        };
        ocean.AddChild(branch);
        Sphere(ocean, end, Vector3.One * radius * 1.5f, color.Lightened(0.12f));
    }

    private void BuildArena()
    {
        // Upright underwater backdrop: UV-driven light rays and soft caustics.
        var waterShader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded;
void fragment() {
    vec2 p = UV;
    float ray = pow(max(0.0, sin(p.x*33.0 + p.y*4.0 + TIME*0.12)), 14.0);
    float ripples = sin(p.x*45.0 + sin(p.y*28.0 + TIME*0.25)*2.0);
    vec3 water = mix(vec3(0.018,0.16,0.24),vec3(0.07,0.46,0.53),p.y);
    ALBEDO = water + vec3(0.10,0.18,0.15)*ray*0.75 + vec3(0.022)*ripples;
}" };
        var backdrop = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(5, 0.08f, 3.5f) },
            Position = new Vector3(0, -0.30f, 0),
            MaterialOverride = new ShaderMaterial { Shader = waterShader }
        };
        ocean.AddChild(backdrop);
        var sandMaterial = Material(Colors.White);
        var noise = new FastNoiseLite { Frequency = 0.07f, FractalOctaves = 3 };
        var ramp = new Gradient();
        ramp.SetColor(0,new Color("#bca576"));
        ramp.SetColor(1,new Color("#f6dfad"));
        var texture = new NoiseTexture2D { Width = 256, Height = 256, Noise = noise, Seamless = true, ColorRamp = ramp };
        sandMaterial.AlbedoTexture = texture;
        sandMaterial.Roughness = 1;
        var sand = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(4.8f, 0.07f, 0.85f) },
            Position = new Vector3(0, -0.18f, 1.38f), MaterialOverride = sandMaterial
        };
        ocean.AddChild(sand);
        // Decorations stay outside the active play rectangle, with no collisions.
        string[] plantColors = { "#63c985", "#18aa99", "#ed6aa2", "#edaa4b", "#a08bdd" };
        for (int i = 0; i < 22; i++)
        {
            float x = -2.1f + i * 0.2f;
            float height = 0.32f + (i % 4) * 0.07f;
            var weed = PlaceModel(i % 3 == 0 ? "seaweed_pink_a" : "seaweed_green_a",
                new Vector3(x, -0.04f, 1.27f), new Vector3(0.23f, 0.6f, height));
            var tint = Material(new Color(plantColors[i % plantColors.Length]));
            weed.MaterialOverride = tint;
            weeds.Add(weed);
        }
        for (int i = 0; i < 8; i++)
        {
            float x = i < 4 ? -1.95f : 1.95f;
            var weed = PlaceModel(i % 2 == 0 ? "seaweed_green_a" : "seaweed_pink_a",
                new Vector3(x, -0.05f, -0.75f + (i % 4)*0.5f), new Vector3(0.22f,0.6f,0.4f));
            weed.MaterialOverride = Material(new Color(plantColors[i % plantColors.Length]));
            weeds.Add(weed);
        }
        string[] stoneColors = { "#748a9c", "#bd8b9c", "#a8b6a2", "#a58dbe", "#c6aa80" };
        for (int i = 0; i < 17; i++)
        {
            float x = -2.0f + i * 0.25f;
            Sphere(ocean, new Vector3(x, -0.03f, 1.2f + (i%2)*0.1f),
                new Vector3(0.22f + (i%3)*0.05f, 0.18f, 0.15f + (i%2)*0.07f), new Color(stoneColors[i%5]));
        }
        for (int i = 0; i < 6; i++)
        {
            float x = -1.9f + i * 0.75f;
            var origin = new Vector3(x, 0.01f, 1.25f);
            var tip = origin + new Vector3(0,0,-0.27f);
            var color = new Color(i%2 == 0 ? "#ef7c85" : "#a67cda");
            CoralBranch(origin,tip,color,0.025f);
            CoralBranch(origin + new Vector3(0,0,-0.12f),tip + new Vector3(0.11f,0,0.06f),color,0.017f);
            CoralBranch(origin + new Vector3(0,0,-0.15f),tip + new Vector3(-0.08f,0,-0.04f),color,0.017f);
        }
        foreach (float z in new[] { -1f, 1f })
        {
            var rail = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(3.1f, 0.015f, 0.012f) },
                Position = new Vector3(0, -0.08f, z),
                MaterialOverride = Material(new Color("#70e0d4"), 0.25f)
            };
            ocean.AddChild(rail);
        }
        for (int i = 0; i < 13; i++)
            Sphere(ocean, new Vector3(0, -0.1f, -0.9f+i*0.15f), Vector3.One*0.013f, new Color("#a1d8cf"));
    }

    private void BuildUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);
        var overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer();
        overlay.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft = 18; panel.OffsetRight = -18; panel.OffsetTop = 12;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.015f, 0.09f, 0.14f, 0.92f),
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            CornerRadiusTopLeft = 12, CornerRadiusBottomRight = 12
        };
        panel.AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer();
        panel.AddChild(row);
        scoreLabel = new Label();
        scoreLabel.AddThemeFontSizeOverride("font_size", 24);
        row.AddChild(scoreLabel);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        toggle = new CheckButton { Text = "Ocean theme  [P]", ButtonPressed = true };
        toggle.Toggled += SetEnabled;
        row.AddChild(toggle);
        var help = new Label
        {
            Text = "Controller: left / right stick    •    Move on impact to smash    •    R: restart",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        overlay.AddChild(help);
        help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        help.OffsetTop = 74; help.OffsetBottom = 94;
        help.AddThemeFontSizeOverride("font_size", 14);
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (scoreLabel != null)
            scoreLabel.Text = $"{(LayerEnabled ? "REEF RALLY" : "ORIGINAL PONG")}    {scores[0]} : {scores[1]}";
    }

    public void SetEnabled(bool value)
    {
        LayerEnabled = value;
        if (!initialized) return;
        ocean.Visible = value;
        GetParent().GetNode<Node3D>("Level/Background").Visible = !value;
        GetParent().GetNode<Node3D>("Level/Centerline").Visible = !value;
        foreach (var visual in originalPaddleVisuals) visual.Visible = !value;
        ball.Layers = value ? 0u : originalBallLayers;
        environmentNode.Environment = value ? oceanEnvironment : null;
        camera.Transform = originalCamera;
        camera.Fov = originalFov;
        camera.Projection = value ? Camera3D.ProjectionType.Orthogonal : originalProjection;
        camera.Size = value ? 2.8f : originalCameraSize;
        if (value)
        {
            camera.Position = new Vector3(0, 0, 4);
            camera.LookAt(Vector3.Zero, Vector3.Up);
        }
        Shake = 0;
        Staging = 0;
        ballPulse = 0;
        Array.Clear(pulse);
        foreach (var child in ocean.GetChildren())
            if (child is CpuParticles3D) child.QueueFree();
        foreach (var audio in sounds.Values) audio.Stop();
        toggle.SetPressedNoSignal(value);
        UpdateLabel();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.P) SetEnabled(!LayerEnabled);
        if (key.Keycode == Key.R) controller.InitMatch();
    }

    public override void _Process(double delta)
    {
        if (!initialized) return;
        float dt = (float)delta;
        elapsed += dt;
        for (int i = 0; i < 2; i++)
        {
            float z = paddles[i].Position.Z;
            float direction = Mathf.Sign(z - lastZ[i]);
            if (LayerEnabled && direction != 0 && direction != lastSign[i])
                Bubbles(paddles[i].GlobalPosition, new Vector3(0, 0, -direction), false, 8);
            if (direction != 0) lastSign[i] = direction;
            lastZ[i] = z;
            fish[i].Position = paddles[i].GlobalPosition;
            pulse[i] = Mathf.MoveToward(pulse[i], 0, dt * 5);
            fish[i].Scale = new Vector3(1 + pulse[i] * 0.4f, 1 + pulse[i] * 0.25f, 1 - pulse[i] * 0.25f);
            fish[i].Rotation = new Vector3(0, LayerEnabled ? Mathf.Sin(elapsed * 7 + i * Mathf.Pi) * 0.035f : 0, 0);
        }
        if (!LayerEnabled) return;
        pearl.Position = ball.GlobalPosition;
        ballPulse = Mathf.MoveToward(ballPulse, 0, dt * 6);
        pearl.Scale = new Vector3(1 - ballPulse * 0.32f, 1 + ballPulse * 0.15f, 1 + ballPulse * 0.35f);
        pearl.Rotation = new Vector3(0, -Mathf.Atan2(ballDirection.Z, ballDirection.X), 0);
        for (int i = 0; i < weeds.Count; i++)
            weeds[i].Rotation = new Vector3(0, Mathf.Sin(elapsed * 1.6f + i) * 0.06f, 0);
        Staging = Mathf.MoveToward(Staging, 0, dt);
        Shake = Mathf.MoveToward(Shake, 0, dt * 0.55f);
        // Front-on aquarium view. A tiny follow offset keeps the entire field visible.
        var target = new Vector3(ball.Position.X * 0.025f, -ball.Position.Z * 0.025f, 4);
        camera.Position = camera.Position.Lerp(target, 1 - Mathf.Exp(-dt * 7));
        camera.Rotation = Vector3.Zero;
        camera.Size = 2.8f + Staging * 0.12f;
        camera.Position += new Vector3(random.RandfRange(-Shake, Shake), random.RandfRange(-Shake, Shake), 0);
    }

    // Notifications from PongLogic: these methods never modify gameplay state.
    public void Impact(Vector3 position, Vector3 velocity, int paddleIndex, bool smash)
    {
        if (!initialized || !LayerEnabled) return;
        ballDirection = velocity.Normalized();
        ballPulse = smash ? 1.5f : 1;
        if (paddleIndex >= 0) pulse[paddleIndex] = smash ? 1.45f : 1;
        Bubbles(position, velocity.Normalized(), smash, smash ? 30 : 12);
        sounds[smash ? "smash" : "impact"].Play();
        if (smash) Shake = 0.045f;
    }

    public void RestartShow()
    {
        if (!initialized || !LayerEnabled) return;
        Staging = 1;
        sounds["start"].Play();
    }

    public void PointScored(bool leftLost)
    {
        if (!initialized) return;
        scores[leftLost ? 1 : 0]++;
        UpdateLabel();
        if (LayerEnabled) sounds["stop"].Play();
    }

    private void Bubbles(Vector3 position, Vector3 direction, bool big, int count)
    {
        var particles = new CpuParticles3D
        {
            OneShot = true, Amount = count,
            Lifetime = big ? 0.8 : 0.55, Explosiveness = 1,
            Direction = direction, Spread = 100,
            Gravity = new Vector3(0, 0.05f, -0.20f),
            InitialVelocityMin = 0.12f, InitialVelocityMax = big ? 1.1f : 0.65f,
            ScaleAmountMin = 0.008f, ScaleAmountMax = big ? 0.035f : 0.023f,
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1 },
            MaterialOverride = Material(new Color("#b4f5ef"), 0.3f)
        };
        ocean.AddChild(particles);
        particles.Position = position;
        particles.Emitting = true;
        GetTree().CreateTimer(1.1).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(particles) && !particles.IsQueuedForDeletion())
                particles.QueueFree();
        };
    }
}
