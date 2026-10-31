namespace Evolysia.Sim.Economy;

/// <summary>
/// Goods held by one owner (a person's pack, a band's stores): a quantity per kind of good. Food
/// comes out most-perishable first, and everything spoils at its own rate.
/// </summary>
public sealed class Inventory
{
    readonly float[] _qty = new float[Goods.Count];

    public float this[ushort good] => _qty[good];
    public float Get(string good) => _qty[Goods.Id(good)];

    public void Add(ushort good, float amount) { if (amount > 0f) _qty[good] += amount; }
    public void Add(string good, float amount) => Add(Goods.Id(good), amount);

    /// <summary>Removes up to <paramref name="amount"/>; returns how much was there to take.</summary>
    public float Take(ushort good, float amount)
    {
        float t = MathF.Min(amount, _qty[good]);
        _qty[good] -= t;
        return t;
    }

    public float Take(string good, float amount) => Take(Goods.Id(good), amount);
    public bool Has(ushort good, float amount) => _qty[good] >= amount - 1e-4f;

    /// <summary>Person-days of food held.</summary>
    public float FoodValue
    {
        get
        {
            float sum = 0f;
            foreach (ushort g in Goods.FoodsByPerishability) sum += _qty[g] * Goods.All[g].Food;
            return sum;
        }
    }

    /// <summary>Total bulk held (sum of weights).</summary>
    public float Bulk
    {
        get
        {
            float sum = 0f;
            for (int g = 0; g < _qty.Length; g++) sum += _qty[g] * Goods.All[g].Weight;
            return sum;
        }
    }

    /// <summary>Eats or hands over up to <paramref name="value"/> person-days of food, most perishable first. Returns what it gave.</summary>
    public float TakeFood(float value)
    {
        float got = 0f;
        foreach (ushort g in Goods.FoodsByPerishability)
        {
            if (got >= value) break;
            float have = _qty[g];
            if (have <= 0f) continue;
            float per = Goods.All[g].Food;
            float units = MathF.Min(have, (value - got) / per);
            _qty[g] -= units;
            got += units * per;
        }
        return got;
    }

    /// <summary>Moves up to <paramref name="value"/> person-days of food into another inventory, most perishable first.</summary>
    public float GiveFood(Inventory to, float value)
    {
        float moved = 0f;
        foreach (ushort g in Goods.FoodsByPerishability)
        {
            if (moved >= value) break;
            float have = _qty[g];
            if (have <= 0f) continue;
            float per = Goods.All[g].Food;
            float units = MathF.Min(have, (value - moved) / per);
            _qty[g] -= units;
            to._qty[g] += units;
            moved += units * per;
        }
        return moved;
    }

    /// <summary>One day of spoiling. <paramref name="rate"/> scales every good's own rate (heat, storage, preservation).</summary>
    public void Spoil(float rate)
    {
        for (int g = 0; g < _qty.Length; g++)
            if (_qty[g] > 0f && Goods.All[g].Spoil > 0f)
            {
                _qty[g] *= 1f - MathF.Min(0.9f, Goods.All[g].Spoil * rate);
                if (_qty[g] < 1e-3f) _qty[g] = 0f;
            }
    }

    /// <summary>Every good held, with its quantity (for display and trade).</summary>
    public IEnumerable<(GoodDef good, float qty)> Held()
    {
        for (int g = 0; g < _qty.Length; g++) if (_qty[g] >= 0.01f) yield return (Goods.All[g], _qty[g]);
    }

    public void Clear() => Array.Clear(_qty);

    /// <summary>Moves everything into <paramref name="to"/>.</summary>
    public void MoveAllTo(Inventory to)
    {
        for (int g = 0; g < _qty.Length; g++) { to._qty[g] += _qty[g]; _qty[g] = 0f; }
    }

    /// <summary>Moves a fraction of everything into <paramref name="to"/> (a band splitting its stores).</summary>
    public void MoveFraction(Inventory to, float fraction)
    {
        for (int g = 0; g < _qty.Length; g++) { float m = _qty[g] * fraction; to._qty[g] += m; _qty[g] -= m; }
    }

    /// <summary>Scales everything (a share lost, left behind or split off).</summary>
    public void Scale(float factor)
    {
        for (int g = 0; g < _qty.Length; g++) _qty[g] *= factor;
    }
}
