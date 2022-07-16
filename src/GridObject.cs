using Godot;
using System;
using GodotOnReady.Attributes;

public partial class GridObject : MeshInstance
{
    [Export] private Color _color;
    [Export] private int _damage = 1;
    [Export] private int _maxHealth = 1;

    public int Damage => _damage;
    public Vector2 GridPos => _gridPos;
    
    public Action<GridObject> OnDestroyed;
    
    private SpatialMaterial _defaultMaterial;

    private int _health;
    protected Vector2 _gridPos;
    protected Vector2 _moveStart;
    protected bool _moving;
    protected float _moveTimer;
    protected bool _queueDestroy;
    
    private bool _flashing;
    private float _hitFlashTimer;

    [OnReady]
    protected virtual void Ready()
    {
        _defaultMaterial = MaterialOverride as SpatialMaterial;
        _defaultMaterial = _defaultMaterial.Duplicate() as SpatialMaterial; // set unique material so we can change colour
        MaterialOverride = _defaultMaterial;
        _defaultMaterial.AlbedoColor = _color;
        
        _health = _maxHealth;
        
        this.GlobalPosition(Game.Instance.GridToWorld(_gridPos));
        
        Game.DoPreTurn += DoPreTurn;
        Game.DoTurn += DoTurn;
        Game.DoPostTurn += DoPostTurn;
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
        GlobalTransform = new Transform(GlobalTransform.basis, _gridPos.To3D());
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
                MaterialOverride = _defaultMaterial;
            }
        }
        
        if (_queueDestroy)
        {
            if (!_flashing)
                QueueFree();
            
            return;
        }
    }
    
    public virtual void OnHit(int damage)
    {
        _health -= damage;
        _flashing = true;
        _hitFlashTimer = 1.0f / 10.0f;
        MaterialOverride = Resources.Instance.FlashMaterial;
        _health -= damage;
    }

    protected virtual void Destroy()
    {
        OnDestroyed?.Invoke(this);
        _queueDestroy = true;
        Game.DoTurn -= DoTurn;
    }
}