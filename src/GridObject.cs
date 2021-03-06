using Godot;
using System;
using GodotOnReady.Attributes;

public partial class GridObject : MeshInstance
{
    [Export] private Color _color;
    [Export] private int _damage = 1;
    [Export] protected int _maxHealth = 1;
    [Export] private float _spawnDropTime = 0.66f;

    public int Damage => _damage;
    public Vector2 GridPos => _gridPos;
    
    public Action<GridObject> OnDestroyed;
    public Action<GridObject, int, ModManager.ModTypes> OnDamaged;

    protected SpatialMaterial _defaultMaterial;
    protected Material _beforeFlashMat;

    protected int _health;
    protected Vector2 _gridPos;
    protected Vector2 _moveStart;
    protected bool _moving;
    protected float _moveTimer;
    protected bool _queueDestroy;
    
    private bool _flashing;
    private float _hitFlashTimer;
    
    protected bool _justSpawned = true;
    private float _spawnTimer;
    private float _baseY;

    [OnReady]
    protected virtual void Ready()
    {
        _defaultMaterial = MaterialOverride as SpatialMaterial;
        _defaultMaterial = _defaultMaterial.Duplicate() as SpatialMaterial; // set unique material so we can change colour
        MaterialOverride = _defaultMaterial;
        _defaultMaterial.AlbedoColor = _color;
        
        _health = _maxHealth;

        Game.DoPreTurn += DoPreTurn;
        Game.DoTurn += DoTurn;
        Game.DoPostTurn += DoPostTurn;

        _spawnTimer = _spawnDropTime;
    }

    protected virtual void DoPreTurn()
    {
    }
    
    protected virtual void DoTurn()
    {
    }
    
    protected virtual void DoPostTurn()
    {
        if (_health <= 0)
        {
            Destroy();
        }
    }

    public void Init(Vector2 gridPos)
    {
        _gridPos = gridPos;
        _baseY = Game.Instance.GridToWorld(_gridPos).y;
        this.GlobalPosition(Game.Instance.GridToWorld(_gridPos) + Vector3.Up * 100.0f);
    }

    public override void _Process(float delta)
    {
        base._Process(delta);
        
        if (_flashing)
        {
            _hitFlashTimer -= delta;
            if (_hitFlashTimer < 0.0f)
            {
                _flashing = false;
                MaterialOverride = _beforeFlashMat;
            }
        }
        
        if (_queueDestroy)
        {
            if (!_flashing)
                QueueFree();
            
            return;
        }

        if (_justSpawned)
        {
            _spawnTimer -= delta;
            float y = _baseY + Mathf.Max(0.0f, Utils.EaseInCubic(_spawnTimer / _spawnDropTime)) * 10.0f;
            Vector3 pos = GlobalTransform.origin;
            pos.y = y;
            GlobalTransform = new Transform(GlobalTransform.basis, pos);

            if (_spawnTimer < 0.0f)
            {
                _justSpawned = false;
            }
        }
    }
    
    public virtual void OnHit(int damage, ModManager.ModTypes type)
    {
        _health -= damage;
        _flashing = true;
        _hitFlashTimer = 1.0f / 10.0f;
        _beforeFlashMat = MaterialOverride;
        MaterialOverride = Resources.Instance.FlashMaterial;
        
        OnDamaged?.Invoke(this, damage, type);
    }

    protected virtual void Destroy()
    {
        OnDestroyed?.Invoke(this);
        _queueDestroy = true;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        
        Game.DoPreTurn -= DoPreTurn;
        Game.DoTurn -= DoTurn;
        Game.DoPostTurn -= DoPostTurn;
    }
}
