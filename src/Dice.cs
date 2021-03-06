using Godot;
using System;
using System.Collections.Generic;
using GodotOnReady.Attributes;

public partial class Dice : GridObject
{
    public static Dice Instance => _instance;
    private static Dice _instance;
    
    [Export] private float _moveTime = 0.25f;
    [Export] private Vector2 _start = new Vector2(5, 5);
    [Export] private NodePath _indicatorPath;
    
    [Export] private List<Mesh> _numberMeshes;
    [Export] private List<Mesh> _bulletMeshes;
    [Export] private List<Mesh> _healMeshes;
    [Export] private List<Mesh> _lightningMeshes;
    [Export] private List<Mesh> _coinMeshes;
    [Export] private List<Mesh> _freezeMeshes;

    [OnReadyGet] public AudioStreamPlayer2D _bulletSfx;
    [OnReadyGet] public AudioStreamPlayer2D _lightningSfx;
    [OnReadyGet] public AudioStreamPlayer2D _healSfx;
    [OnReadyGet] public AudioStreamPlayer2D _coinSfx;
    [OnReadyGet] public AudioStreamPlayer2D _freezeSfx;
    
    [OnReadyGet] private Game _game;
    [OnReadyGet] private Camera _camera;
    
    public float MoveTime => _moveTime;
    public float MaxHealth => _maxHealth;
    public float Health => _health;

    public Action<Dice, int> OnHealed;
    public Action OnCollectPickup;
    
    private DiceIndicator _indicator;
    private Queue<Vector2> _moves = new Queue<Vector2>();
    private Queue<Vector2> _rotations = new Queue<Vector2>();
    private Quat _rotFrom;
    private Quat _rot;
    private Vector2 _forward;
    
    public int Coins;
    
    private int _topFace;
    private int _bottomFace;
    private int _rightFace;
    private int _leftFace;
    private int _frontFace;
    private int _backFace;

    private int[] _faceValues = new[] {1, 2, 3, 4, 5, 6};
    
    private Dictionary<int, ModDice> _mods = new();
    private Dictionary<ModManager.ModTypes, List<Mesh>> _meshes = new();
    private List<MeshInstance> _faces = new List<MeshInstance>();
    
    protected override void Ready()
    {
        _gridPos = _start;
        this.GlobalPosition(Game.Instance.GridToWorld(_gridPos) + Vector3.Up * 100.0f);
        
        base.Ready();

        _indicator = GetNode<DiceIndicator>(_indicatorPath);
        
        _rot = GlobalTransform.basis.RotationQuat();
        _mods[0] = new ModDice(ModManager.ModTypes.Number);
        _mods[1] = new ModDice(ModManager.ModTypes.Number);
        _mods[2] = new ModDice(ModManager.ModTypes.Number);
        _mods[3] = new ModDice(ModManager.ModTypes.Number);
        _mods[4] = new ModDice(ModManager.ModTypes.Number);
        _mods[5] = new ModDice(ModManager.ModTypes.Number);
        
        _instance = this;
        
        for (int i = 0; i < (int)ModManager.ModTypes.COUNT; i++)
        {
            switch ((ModManager.ModTypes) i)
            {
                case ModManager.ModTypes.Number:
                    _meshes[(ModManager.ModTypes) i] = _numberMeshes;
                    break;
                case ModManager.ModTypes.Bullet:
                    _meshes[(ModManager.ModTypes) i] = _bulletMeshes;
                    break;
                case ModManager.ModTypes.Heal:
                    _meshes[(ModManager.ModTypes) i] = _healMeshes;
                    break;
                case ModManager.ModTypes.Lightning:
                    _meshes[(ModManager.ModTypes) i] = _lightningMeshes;
                    break;
                case ModManager.ModTypes.Coin:
                    _meshes[(ModManager.ModTypes) i] = _coinMeshes;
                    break;
                case ModManager.ModTypes.Freeze:
                    _meshes[(ModManager.ModTypes) i] = _freezeMeshes;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            for (int j = 0; j < 6; j++)
            {
                ResourceLoader.Load(_meshes[(ModManager.ModTypes) i][j].ResourcePath); // Preload
            }
        }

        for (int i = 0; i < 6; i++)
        {
            _faces.Add(GetNode<MeshInstance>($"{i}"));
            
            ShaderMaterial mat = _faces[i].MaterialOverride as ShaderMaterial;
            mat = mat.Duplicate() as ShaderMaterial; // set unique material so we can change colour
            _faces[i].MaterialOverride = mat;
            SetFace(i, ModManager.ModTypes.Number, _faceValues[i]);
        }
    }

    private void SetFace(int face, ModManager.ModTypes type, int value)
    {
        ShaderMaterial mat = _faces[face].MaterialOverride as ShaderMaterial;
        mat.SetShaderParam("u_col_1", ModManager.Instance.ModColours[type]);
        mat.SetShaderParam("u_col_2", ModManager.Instance.ModColoursSecondary[type]);

        _faces[face].Mesh = _meshes[type][value - 1];
    }

    public override void _Process(float delta)
    {
        base._Process(delta);

        if (Input.IsActionJustPressed("w"))
        {
            _moves.Enqueue(new Vector2(0, -1));
            _rotations.Enqueue(new Vector2(-1, 0));
        }
        if (Input.IsActionJustPressed("a"))
        {
            _moves.Enqueue(new Vector2(-1, 0));
            _rotations.Enqueue(new Vector2(0, 1));
        }
        if (Input.IsActionJustPressed("s"))
        {
            _moves.Enqueue(new Vector2(0, 1));
            _rotations.Enqueue(new Vector2(1, 0));
        }
        if (Input.IsActionJustPressed("d"))
        {
            _moves.Enqueue(new Vector2(1, 0));
            _rotations.Enqueue(new Vector2(0, -1));
        }

        if (_moves.Count > 0)
        {
            Vector2 move = _moves.Dequeue();
            Vector2 rot = _rotations.Dequeue();
            
            if (_game.InBounds(_gridPos + move))
            {
                // apply any incomplete rotation.
                Basis basis = new Basis(_rot);
                basis.Scale = GlobalTransform.basis.Scale;
                GlobalTransform = new Transform(basis, GlobalTransform.origin);
                
                _moveStart = _gridPos;
                _gridPos += move;
                
                _moveTimer = _moveTime;
                _moving = true;

                _rotFrom = GlobalTransform.basis.RotationQuat();
                Basis rotBasis = GlobalTransform.basis.Rotated(new Vector3(rot.x, 0.0f, rot.y), Mathf.Pi * 0.5f);
                _rot = rotBasis.RotationQuat();

                _forward = (_gridPos - _moveStart).Normalized();
                
                for (int i = 0; i < 6; i++)
                {
                    if (FaceDirection(i, rotBasis).y > 0.5f) _topFace = i;
                    if (FaceDirection(i, rotBasis).y < -0.5f) _bottomFace = i;
                    if (FaceDirection(i, rotBasis).x > 0.5f) _rightFace = i;
                    if (FaceDirection(i, rotBasis).x < -0.5f) _leftFace = i;
                    if (FaceDirection(i, rotBasis).z > 0.5f) _frontFace = i;
                    if (FaceDirection(i, rotBasis).z < -0.5f) _backFace = i;
                }

                _game.TriggerTurn();
            }
        }

        if (_moving)
        {
            _moveTimer = Mathf.Clamp(_moveTimer - delta, 0.0f, 1.0f);
            float t = Utils.EaseOutCubic(1.0f - (_moveTimer / _moveTime));
            
            Basis rot = new Basis(_rotFrom.Slerp(_rot, t));
            rot.Scale = GlobalTransform.basis.Scale;
            Vector3 pos = _game.GridToWorld(_moveStart).LinearInterpolate(_game.GridToWorld(_gridPos), t);
            GlobalTransform = new Transform(rot, pos);
            
            if (_moveTimer == 0.0f)
            {
                _moving = false;
            }
        }

        Vector3 camPos = GlobalTransform.origin;
        camPos.y = 0.0f;
        _camera.GlobalPosition(camPos + _camera.GlobalTransform.basis.z.Normalized() * 10.0f);
        _indicator.GlobalPosition(GlobalTransform.origin);
    }

    protected override void DoPreTurn()
    {
        base.DoPreTurn();
        
        _mods[_bottomFace].Activate(_gridPos, _forward, _faceValues[_bottomFace]);
    }

    protected override void DoTurn()
    {
        base.DoTurn();

        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        ModManager.ModTypes[] types = new ModManager.ModTypes[4]
        {
            _mods[_backFace].Type,
            _mods[_rightFace].Type,
            _mods[_frontFace].Type,
            _mods[_leftFace].Type,
        };
        int[] faceValues = new int[4]
        {
            _faceValues[_backFace],
            _faceValues[_rightFace],
            _faceValues[_frontFace],
            _faceValues[_leftFace],
        };
        _indicator.UpdateSides(types, faceValues);
    }

    public void Heal(int amount)
    {
        _healSfx.Play();
        
        _health = Mathf.Min(_health + amount, _maxHealth);
        OnHealed?.Invoke(this, amount);
    }
    
    public void OnPickup(ModPickup pickup)
    {
        int face = _bottomFace;
        ModDice mod = new ModDice(pickup.Type);
        mod.Activate(_gridPos, _forward, _faceValues[_bottomFace]);
        _mods[face] = mod;
        SetFace(face, pickup.Type, _faceValues[face]);
        OnCollectPickup?.Invoke();
    }
    
    public bool OnOneUp(OneUp pickup)
    {
        int face = _bottomFace;
        if (_faceValues[face] == 6)
            return false;

        if (Coins < pickup.Cost)
            return false;
        
        _faceValues[face] += 1;
        SetFace(face, _mods[face].Type, _faceValues[face]);
        UpdateIndicator();
        Coins -= pickup.Cost;
        
        return true;
    }
    
    private Vector3 FaceDirection(int face, Basis basis)
    {
        switch (face)
        {
            case 0:
                return -basis.y.Normalized();
            case 1:
                return basis.x.Normalized();
            case 2:
                return -basis.z.Normalized();
            case 3:
                return basis.z.Normalized();
            case 4:
                return -basis.x.Normalized();
            case 5:
                return basis.y.Normalized();
        }

        return Vector3.Zero;
    }

    public void SetMaxHealth(int value)
    {
        _maxHealth = value;
    }
}
