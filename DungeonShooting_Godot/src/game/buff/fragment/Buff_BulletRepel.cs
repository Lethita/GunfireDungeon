
[Buff("BulletRepel", "子弹击退 buff, 参数‘1’为击退增加类型: 1:具体击退值, 2:百分比击退值(小数), 参数‘2’为子弹增加的击退值")]
public class Buff_BulletRepel : BuffFragment
{
    private int _type;
    private float _value;
    public override void InitParam(float arg1, float arg2)
    {
        _type = (int)arg1;
        _value = arg2;
    }

    public override void OnPickUpItem()
    {
        if (_type == 1)
        {
            Role.RoleState.CalcBulletRepelEvent += CalcBulletRepelEvent1;
        }
        else
        {
            Role.RoleState.CalcBulletRepelEvent += CalcBulletRepelEvent2;
        }
    }
    
    public override void OnRemoveItem()
    {
        if (_type == 1)
        {
            Role.RoleState.CalcBulletRepelEvent -= CalcBulletRepelEvent1;
        }
        else
        {
            Role.RoleState.CalcBulletRepelEvent -= CalcBulletRepelEvent2;
        }
    }
    
    private void CalcBulletRepelEvent1(float originRepel, RefValue<float> repel)
    {
        if (Role.WeaponPack.ActiveItem != null && Role.WeaponPack.ActiveItem.Attribute.IsMelee)
        {
            return;
        }
        repel.Value += _value;
    }
    
    private void CalcBulletRepelEvent2(float originRepel, RefValue<float> repel)
    {
        if (Role.WeaponPack.ActiveItem != null && Role.WeaponPack.ActiveItem.Attribute.IsMelee)
        {
            return;
        }
        repel.Value += originRepel * _value;
    }
}