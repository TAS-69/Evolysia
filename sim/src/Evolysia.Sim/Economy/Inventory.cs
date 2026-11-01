namespace Evolysia.Sim.Economy;

public sealed class StockStack
{
    public string Id = "", ParentId = "";
    public ushort Good;
    public float Quantity;
}

public sealed record StackMerge(string RetiredId, string IntoId, float Quantity);
public sealed record StockReservation(ushort Good, float Quantity);

public sealed class Inventory
{
    readonly float[] _qty = new float[Goods.Count];
    readonly float[] _localQty = new float[Goods.Count];
    readonly float[] _reservedQty = new float[Goods.Count];
    readonly List<StockStack> _stacks = new();
    readonly List<Inventory> _containers = new();
    readonly Dictionary<int, StockReservation> _reservations = new();
    Inventory? _parent;
    long _nextStack;
    public string Owner;
    public float Capacity;
    public string Filter = "";
    public readonly List<StackMerge> Merges = new();
    int _version, _foodVersion = -1;
    float _food;

    public Inventory(string? owner = null, float capacity = float.PositiveInfinity)
    {
        Owner = owner ?? "temporary/" + Guid.NewGuid().ToString("N");
        Capacity = capacity;
    }

    public IReadOnlyList<StockStack> Stacks => _stacks;
    public IReadOnlyList<Inventory> Containers => _containers;
    public Inventory? Parent => _parent;
    public float Local(ushort good) => _localQty[good];
    public float Reserved(ushort good) => _reservedQty[good];
    float OwnAvailable(ushort good) => MathF.Max(0f, _localQty[good] - _reservations.Values.Where(r => r.Good == good).Sum(r => r.Quantity));
    public float AvailableLocal(ushort good) => OwnAvailable(good);
    public bool Reserve(int id, ushort good, float quantity)
    {
        if (!float.IsFinite(quantity) || quantity <= 0f || _reservations.ContainsKey(id) || OwnAvailable(good) < quantity - 1e-5f) return false;
        _reservations[id] = new StockReservation(good, quantity); ReserveDelta(good, quantity); return true;
    }
    public void Release(int id) { if (_reservations.Remove(id, out var reservation)) ReserveDelta(reservation.Good, -reservation.Quantity); }
    public void ReleaseAll() { foreach (int id in _reservations.Keys.ToArray()) Release(id); }
    public float this[ushort good] => _qty[good];
    public float Get(string good) => _qty[Goods.Id(good)];
    public bool Has(ushort good, float amount) => float.IsFinite(amount) && amount >= 0f && _qty[good] - _reservedQty[good] >= amount - 1e-4f;

    public void Attach(Inventory container)
    {
        if (container == this || container._parent != null) throw new InvalidOperationException("A container has one location.");
        for (var p = this; p != null; p = p._parent) if (p == container) throw new InvalidOperationException("Container cycle.");
        if (container.Bulk > MathF.Max(0f, Capacity - Bulk)) throw new InvalidOperationException("Not enough container capacity.");
        _containers.Add(container); container._parent = this;
        for (ushort g = 0; g < _qty.Length; g++) { Change(g, container._qty[g], false); ReserveDelta(g, container._reservedQty[g]); }
    }

    public void Detach()
    {
        if (_parent == null) return;
        var parent = _parent; _parent = null; parent._containers.Remove(this);
        for (ushort g = 0; g < _qty.Length; g++) { parent.Change(g, -_qty[g], false); parent.ReserveDelta(g, -_reservedQty[g]); }
    }

    void Change(ushort good, float delta, bool local = true)
    {
        if (local) _localQty[good] = MathF.Max(0f, _localQty[good] + delta);
        _qty[good] = MathF.Max(0f, _qty[good] + delta); _version++;
        _parent?.Change(good, delta, false);
    }
    void ReserveDelta(ushort good, float delta) { _reservedQty[good] = MathF.Max(0f, _reservedQty[good] + delta); _parent?.ReserveDelta(good, delta); }

    public bool Accepts(ushort good) => Filter.Length == 0 || Filter.Split(',').Contains(Goods.All[good].Id);
    float Room(ushort good, Inventory? source = null)
    {
        if (!Accepts(good)) return 0f;
        float room = float.PositiveInfinity;
        for (var p = this; p != null; p = p._parent)
        {
            bool internalMove = false;
            for (var from = source; from != null; from = from._parent) if (from == p) { internalMove = true; break; }
            if (!internalMove) room = MathF.Min(room, MathF.Max(0f, p.Capacity - p.Bulk));
        }
        return room / MathF.Max(0.01f, Goods.All[good].Weight);
    }
    string NewId() => Owner + ":" + ++_nextStack;

    void Receive(StockStack stack)
    {
        var existing = _stacks.Count >= 64 ? _stacks.FirstOrDefault(s => s.Good == stack.Good) : null;
        if (existing == null) _stacks.Add(stack);
        else
        {
            existing.Quantity += stack.Quantity;
            Merges.Add(new StackMerge(stack.Id, existing.Id, stack.Quantity));
            if (Merges.Count > 64) Merges.RemoveAt(0);
        }
    }

    public void Add(ushort good, float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;
        foreach (var container in _containers)
        {
            float put = MathF.Min(amount, container.Room(good));
            if (put <= 0f) continue;
            container.Add(good, put); amount -= put;
            if (amount <= 0f) return;
        }
        float add = MathF.Min(amount, Room(good));
        if (add <= 0f) return;
        var stack = _stacks.FirstOrDefault(s => s.Good == good && s.ParentId.Length == 0 && s.Id.StartsWith(Owner + ":", StringComparison.Ordinal));
        if (stack == null) { stack = new StockStack { Id = NewId(), Good = good }; _stacks.Add(stack); }
        stack.Quantity += add; Change(good, add);
    }
    public void Add(string good, float amount) => Add(Goods.Id(good), amount);

    IEnumerable<Inventory> Leaves()
    {
        yield return this;
        foreach (var child in _containers) foreach (var leaf in child.Leaves()) yield return leaf;
    }

    public float Take(ushort good, float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return 0f;
        float taken = 0f;
        foreach (var leaf in Leaves())
            foreach (var stack in leaf._stacks.Where(s => s.Good == good).ToArray())
            {
                float count = MathF.Min(MathF.Min(stack.Quantity, amount - taken), leaf.OwnAvailable(good));
                stack.Quantity -= count; leaf.Change(good, -count); taken += count;
                if (stack.Quantity <= 0f) leaf._stacks.Remove(stack);
                if (taken >= amount) return taken;
            }
        return taken;
    }
    public float Take(string good, float amount) => Take(Goods.Id(good), amount);

    public float TransferTo(Inventory to, ushort good, float amount)
        => Transfer(to, good, amount, true, true);
    public float TransferLocalTo(Inventory to, ushort good, float amount)
        => Transfer(to, good, amount, false, false);
    float Transfer(Inventory to, ushort good, float amount, bool recursive, bool distribute)
    {
        if (to == this || !float.IsFinite(amount) || amount <= 0f) return 0f;
        float moved = 0f;
        foreach (var leaf in recursive ? Leaves().ToArray() : new[] { this })
        {
            if (leaf == to) continue;
            foreach (var stack in leaf._stacks.Where(s => s.Good == good).ToArray())
            {
                foreach (var target in distribute ? to._containers.Concat(new[] { to }) : new[] { to })
                {
                    if (target == leaf) continue;
                    float count = MathF.Min(MathF.Min(MathF.Min(stack.Quantity, amount - moved), target.Room(good, leaf)), leaf.OwnAvailable(good));
                    if (count <= 0f) continue;
                    StockStack cargo;
                    if (count == stack.Quantity) { cargo = stack; leaf._stacks.Remove(stack); }
                    else cargo = new StockStack { Id = leaf.NewId(), ParentId = stack.Id, Good = good, Quantity = count };
                    if (!ReferenceEquals(cargo, stack)) stack.Quantity -= count;
                    target.Receive(cargo); leaf.Change(good, -count); target.Change(good, count); moved += count;
                    if (moved >= amount) return moved;
                    if (ReferenceEquals(cargo, stack)) break;
                }
            }
        }
        return moved;
    }

    public float FoodValue
    {
        get
        {
            if (_foodVersion == _version) return _food;
            float value = 0f; foreach (ushort g in Goods.FoodsByPerishability) value += _qty[g] * Goods.All[g].Food;
            _food = value; _foodVersion = _version; return value;
        }
    }
    public float Bulk { get { float value = 0f; for (int g = 0; g < _qty.Length; g++) value += _qty[g] * Goods.All[g].Weight; return value; } }

    public float TakeFood(float value)
    {
        if (!float.IsFinite(value) || value <= 0f) return 0f;
        float got = 0f;
        foreach (ushort g in Goods.FoodsByPerishability)
        {
            got += Take(g, MathF.Min(_qty[g], (value - got) / Goods.All[g].Food)) * Goods.All[g].Food;
            if (got >= value) break;
        }
        return got;
    }
    public float GiveFood(Inventory to, float value)
    {
        if (to == this || !float.IsFinite(value) || value <= 0f) return 0f;
        float moved = 0f;
        foreach (ushort g in Goods.FoodsByPerishability)
        {
            moved += TransferTo(to, g, MathF.Min(_qty[g], (value - moved) / Goods.All[g].Food)) * Goods.All[g].Food;
            if (moved >= value) break;
        }
        return moved;
    }
    public void Spoil(float rate)
    {
        if (!float.IsFinite(rate) || rate < 0f) return;
        foreach (var leaf in Leaves()) foreach (var stack in leaf._stacks.ToArray())
        {
            float loss = stack.Quantity * MathF.Min(0.9f, Goods.All[stack.Good].Spoil * rate);
            stack.Quantity -= loss; leaf.Change(stack.Good, -loss);
            if (stack.Quantity <= 0f) leaf._stacks.Remove(stack);
        }
    }
    public IEnumerable<(GoodDef good, float qty)> Held()
    {
        for (int g = 0; g < _qty.Length; g++) if (_qty[g] >= 0.01f) yield return (Goods.All[g], _qty[g]);
    }
    public void Clear() => Scale(0f);
    public void MoveNonFood(Inventory to) { foreach (ushort g in Goods.NonFood) TransferTo(to, g, _qty[g]); }
    public void MoveAllTo(Inventory to) { for (ushort g = 0; g < _qty.Length; g++) TransferTo(to, g, _qty[g]); }
    public void MoveFraction(Inventory to, float fraction)
    {
        if (!float.IsFinite(fraction)) return;
        fraction = Math.Clamp(fraction, 0f, 1f);
        for (ushort g = 0; g < _qty.Length; g++) TransferTo(to, g, _qty[g] * fraction);
    }
    public void Scale(float factor)
    {
        if (!float.IsFinite(factor)) return;
        factor = Math.Clamp(factor, 0f, 1f);
        foreach (var leaf in Leaves()) foreach (var stack in leaf._stacks.ToArray())
        {
            float loss = stack.Quantity * (1f - factor); stack.Quantity -= loss; leaf.Change(stack.Good, -loss);
            if (stack.Quantity <= 0f) leaf._stacks.Remove(stack);
        }
    }
}
