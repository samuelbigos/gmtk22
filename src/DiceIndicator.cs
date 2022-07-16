using Godot;
using System;
using System.Collections.Generic;
using GodotOnReady.Attributes;

public partial class DiceIndicator : Spatial
{
    [OnReadyGet] private MeshInstance _top;
    [OnReadyGet] private MeshInstance _right;
    [OnReadyGet] private MeshInstance _bottom;
    [OnReadyGet] private MeshInstance _left;

    private ShaderMaterial[] _shaders = new ShaderMaterial[4];
    
    [Export] private List<Mesh> _numberMeshes;
    [Export] private List<Mesh> _bulletMeshes;

    private Dictionary<ModManager.ModTypes, List<Mesh>> _meshes = new();
    
    [OnReady]
    private void Ready()
    {
        base._Ready();

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
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
        }

        _bottom.Visible = false;
        _right.Visible = false;
        
        _shaders[0] = (_top.MaterialOverride as ShaderMaterial).Duplicate() as ShaderMaterial;
        _top.MaterialOverride = _shaders[0];
        _shaders[1] = (_right.MaterialOverride as ShaderMaterial).Duplicate() as ShaderMaterial;
        _right.MaterialOverride = _shaders[1];
        _shaders[2] = (_bottom.MaterialOverride as ShaderMaterial).Duplicate() as ShaderMaterial;
        _bottom.MaterialOverride = _shaders[2];
        _shaders[3] = (_left.MaterialOverride as ShaderMaterial).Duplicate() as ShaderMaterial;
        _left.MaterialOverride = _shaders[3];
    }

    public void UpdateSides(ModManager.ModTypes[] types, int[] values)
    {
        Visible = true;
        _top.Mesh = _meshes[types[0]][values[0]];
        _right.Mesh = _meshes[types[1]][values[1]];
        _bottom.Mesh = _meshes[types[2]][values[2]];
        _left.Mesh = _meshes[types[3]][values[3]];

        for (int i = 0; i < 4; i++)
        {
            _shaders[i].SetShaderParam("u_col", ModManager.Instance.ModColours[types[i]]);
        }
    }
}