
[BuffFragment("WeaponCapacity", "武器背包容量 buff, 参数‘1’为武器背包增加的容量")]
public class Buff_WeaponCapacity : BuffFragment
{
    private int _value;

    public override void InitParam(float arg1)
    {
        _value = (int)arg1;
    }

    public override void OnPickUpItem()
    {
        Role.WeaponPack.SetCapacity(Role.WeaponPack.Capacity + _value);
    }

    public override void OnRemoveItem()
    {
        Role.WeaponPack.SetCapacity(Role.WeaponPack.Capacity - _value);
    }
}