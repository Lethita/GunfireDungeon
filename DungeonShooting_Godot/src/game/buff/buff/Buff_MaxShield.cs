
using System.Collections.Generic;
using System.Text.Json;

[BuffFragment("MaxShield", "护盾上限buff, 参数‘1’为护盾上限")]
public class Buff_MaxShield : BuffFragment
{
    private List<ulong> _cacheId = new List<ulong>();
    private int _maxShield;
    
    public override void InitParam(JsonElement[] args)
    {
        _maxShield = args[0].GetInt32();
    }
    
    public override void OnPickUpItem()
    {
        Role.MaxShield += _maxShield;
        var instanceId = Role.GetInstanceId();
        if (!_cacheId.Contains(instanceId))
        {
            _cacheId.Add(instanceId);
            Role.Shield += _maxShield;
        }
    }

    public override void OnRemoveItem()
    {
        Role.MaxShield -= _maxShield;
    }
}