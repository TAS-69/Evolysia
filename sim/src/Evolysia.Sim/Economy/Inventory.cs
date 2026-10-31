namespace Evolysia.Sim.Economy;

public sealed class Inventory
{
    readonly float[] _qty = new float[Goods.Count];

    int _version, _foodVersion = -1;
    float _food;

    public float this[ushort good] => _qty[good];
    public float Get(string good) => _qty[Goods.Id(good)];

    public void Add(ushort good, float amount) { if (amount > 0f) { _qty[good] += amount; _version++; } }
    public void Add(string good, float amount) => Add(Goods.Id(good), amount);

    public float Take(ushort good, float amount)
    {
        float t = MathF.Min(amount, _qty[good]);
        _qty[good] -= t;
        _version++;
        return t;
    }

    public float Take(string good, float amount) => Take(Goods.Id(good), amount);
    public bool Has(ushort good, float amount) => _qty[good] >= amount - 1e-4f;

    public float FoodValue
    {
        get
        {
            int v = _version;
            if (_foodVersion == v) return _food;
            float sum = 0f;
            foreach (ushort g in Goods.FoodsByPerishability) sum += _qty[g] * Goods.All[g].Food;
            _food = sum; _foodVersion = v;
            return sum;
        }
    }

    public float Bulk
    {
        get
        {
            float sum = 0f;
            for (int g = 0; g < _qty.Length; g++) sum += _qty[g] * Goods.All[g].Weight;
            return sum;
        }
    }

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
        _version++;
        return got;
    }

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
        _version++; to._version++;
        return moved;
    }

    public void Spoil(float rate)
    {
        foreach (ushort g in Goods.Perishable)
            if (_qty[g] > 0f)
            {
                _qty[g] *= 1f - MathF.Min(0.9f, Goods.All[g].Spoil * rate);
                if (_qty[g] < 1e-3f) _qty[g] = 0f;
            }
        _version++;
    }

    public IEnumerable<(GoodDef good, float qty)> Held()
    {
        for (int g = 0; g < _qty.Length; g++) if (_qty[g] >= 0.01f) yield return (Goods.All[g], _qty[g]);
    }

    public void Clear() { Array.Clear(_qty); _version++; }

    public void MoveNonFood(Inventory to)
    {
        foreach (ushort g in Goods.NonFood)
            if (_qty[g] >= 0.01f) { to._qty[g] += _qty[g]; _qty[g] = 0f; }
        _version++; to._version++;
    }

    public void MoveAllTo(Inventory to)
    {
        for (int g = 0; g < _qty.Length; g++) { to._qty[g] += _qty[g]; _qty[g] = 0f; }
        _version++; to._version++;
    }

    public void MoveFraction(Inventory to, float fraction)
    {
        for (int g = 0; g < _qty.Length; g++) { float m = _qty[g] * fraction; to._qty[g] += m; _qty[g] -= m; }
        _version++; to._version++;
    }

    public void Scale(float factor)
    {
        for (int g = 0; g < _qty.Length; g++) _qty[g] *= factor;
        _version++;
    }
}
