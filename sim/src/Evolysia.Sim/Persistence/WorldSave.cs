using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Evolysia.Sim.Persistence;

public static class WorldSave
{
    const string Magic = "EVOLYSIA";
    const int Version = 2;
    static readonly Dictionary<Type, FieldInfo[]> Fields = new();
    static readonly Dictionary<string, Type> Types = BuildTypes();

    static bool Generic(Type t, Type definition) => t.IsGenericType && t.GetGenericTypeDefinition() == definition;
    static bool PackedCollection(Type t) => Generic(t, typeof(Dictionary<,>)) || Generic(t, typeof(HashSet<>));
    static bool Sequence(Type t) => Generic(t, typeof(List<>)) || Generic(t, typeof(SortedDictionary<,>));
    static bool Scalar(Type t) => t.IsPrimitive || t == typeof(string) || t == typeof(decimal);
    static string TypeKey(Type t) => t.IsArray ? TypeKey(t.GetElementType()!) + "[]" : t.IsGenericType
        ? t.GetGenericTypeDefinition().FullName + "[" + string.Join(";", t.GetGenericArguments().Select(TypeKey)) + "]" : t.FullName!;

    static FieldInfo[] Members(Type t)
    {
        lock (Fields)
        {
            if (Fields.TryGetValue(t, out var fields)) return fields;
            fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsStatic && (!PackedCollection(t) || f.Name is not ("_comparer" or "_keys" or "_values" or "_syncRoot")))
                .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            return Fields[t] = fields;
        }
    }

    static Dictionary<string, Type> BuildTypes()
    {
        var types = new Dictionary<string, Type>();
        void Visit(Type t)
        {
            if (!types.TryAdd(TypeKey(t), t)) return;
            if (Scalar(t) || t.IsEnum || t == typeof(System.Diagnostics.Stopwatch)) return;
            if (Nullable.GetUnderlyingType(t) is { } underlying) { Visit(underlying); return; }
            if (t.IsArray) { Visit(t.GetElementType()!); return; }
            if (Sequence(t)) { foreach (var a in t.GetGenericArguments()) Visit(a); return; }
            bool entry = t.DeclaringType is { } parent && parent.IsGenericType &&
                (parent.GetGenericTypeDefinition() == typeof(Dictionary<,>) || parent.GetGenericTypeDefinition() == typeof(HashSet<>));
            if (t.Assembly != typeof(Simulation).Assembly && !PackedCollection(t) && !entry && !t.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
                throw new NotSupportedException($"Save field type is not supported: {t.FullName}");
            foreach (var f in Members(t)) Visit(f.FieldType);
        }
        Visit(typeof(Simulation));
        return types;
    }

    public static void Save(Simulation sim, string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = File.Create(temp))
            {
                using var header = new BinaryWriter(file, Encoding.UTF8, true);
                header.Write(Encoding.ASCII.GetBytes(Magic)); header.Write(Version); header.Write(Environment.Version.Major);
                using var compressed = new BrotliStream(file, CompressionLevel.Fastest, true);
                using var writer = new BinaryWriter(compressed, Encoding.UTF8, true);
                new Encoder(writer).Write(sim, typeof(Simulation));
            }
            byte[] checksum;
            using (var file = File.OpenRead(temp)) checksum = SHA256.HashData(file);
            using (var file = new FileStream(temp, FileMode.Append)) file.Write(checksum);
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static Simulation Load(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is < 49 or > 2_147_483_648L) throw new InvalidDataException("Invalid world save size.");
        long payload = file.Length - 32;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        for (long left = payload; left > 0;)
        {
            int read = file.Read(buffer, 0, (int)Math.Min(left, buffer.Length));
            if (read == 0) throw new InvalidDataException("Truncated world save.");
            hash.AppendData(buffer, 0, read); left -= read;
        }
        var checksum = new byte[32]; file.ReadExactly(checksum);
        if (!CryptographicOperations.FixedTimeEquals(checksum, hash.GetHashAndReset())) throw new InvalidDataException("The world save checksum does not match.");
        file.Position = 0;
        using var header = new BinaryReader(file, Encoding.UTF8, true);
        if (Encoding.ASCII.GetString(header.ReadBytes(8)) != Magic || header.ReadInt32() != Version)
            throw new InvalidDataException("This world save uses a different format or runtime version.");
        header.ReadInt32();
        using var compressed = new BrotliStream(file, CompressionMode.Decompress, true);
        using var reader = new BinaryReader(compressed, Encoding.UTF8, true);
        var sim = (Simulation)new Decoder(reader).Read(typeof(Simulation))!;
        foreach (var person in sim.Living.Values.Concat(sim.Dead.Values))
            if (person.ActivityCounts.Length < Enum.GetValues<Core.Activity>().Length) Array.Resize(ref person.ActivityCounts, Enum.GetValues<Core.Activity>().Length);
        sim.RebuildWorkIndexes();
        if (sim.World.W is < 16 or > 4096 || sim.World.H is < 8 or > 4096 || sim.Day < 0 || sim.Living.Any(p => p.Key != p.Value.Id))
            throw new InvalidDataException("Invalid saved world state.");
        return sim;
    }

    sealed class Encoder(BinaryWriter writer)
    {
        readonly Dictionary<object, int> _references = new(ReferenceEqualityComparer.Instance);
        public void Write(object? value, Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } nullable) { writer.Write(value != null); if (value != null) Write(value, nullable); return; }
            if (type == typeof(string)) { writer.Write(value != null); if (value != null) writer.Write((string)value); return; }
            if (type.IsEnum) { Write(Convert.ChangeType(value, Enum.GetUnderlyingType(type)), Enum.GetUnderlyingType(type)); return; }
            if (type.IsPrimitive || type == typeof(decimal)) { WriteScalar(value!, type); return; }
            if (!type.IsValueType)
            {
                if (value == null) { writer.Write(0); return; }
                if (_references.TryGetValue(value, out int existing)) { writer.Write(-existing); return; }
                int id = _references.Count + 1; _references[value] = id; writer.Write(id);
                type = value.GetType();
                if (!Types.ContainsKey(TypeKey(type))) throw new InvalidDataException("Unknown save type.");
                writer.Write(TypeKey(type));
            }
            if (type == typeof(System.Diagnostics.Stopwatch)) return;
            if (type.IsArray)
            {
                var array = (Array)value!; writer.Write(array.Length);
                var element = type.GetElementType()!;
                if (element.IsPrimitive && element != typeof(bool) && element != typeof(char))
                {
                    var bytes = new byte[Buffer.ByteLength(array)]; Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length); writer.Write(bytes);
                }
                else foreach (var item in array) Write(item, element);
                return;
            }
            if (Sequence(type))
            {
                var args = type.GetGenericArguments();
                if (value is IDictionary dictionary)
                {
                    writer.Write(dictionary.Count);
                    foreach (DictionaryEntry entry in dictionary) { Write(entry.Key, args[0]); Write(entry.Value, args[1]); }
                }
                else { var list = (IList)value!; writer.Write(list.Count); foreach (var item in list) Write(item, args[0]); }
                return;
            }
            var fields = Members(type); writer.Write(fields.Length);
            foreach (var f in fields) { writer.Write(f.Name); Write(f.GetValue(value), f.FieldType); }
        }

        void WriteScalar(object value, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: writer.Write((bool)value); break;
                case TypeCode.Byte: writer.Write((byte)value); break;
                case TypeCode.SByte: writer.Write((sbyte)value); break;
                case TypeCode.Int16: writer.Write((short)value); break;
                case TypeCode.UInt16: writer.Write((ushort)value); break;
                case TypeCode.Int32: writer.Write((int)value); break;
                case TypeCode.UInt32: writer.Write((uint)value); break;
                case TypeCode.Int64: writer.Write((long)value); break;
                case TypeCode.UInt64: writer.Write((ulong)value); break;
                case TypeCode.Single: writer.Write((float)value); break;
                case TypeCode.Double: writer.Write((double)value); break;
                case TypeCode.Char: writer.Write((char)value); break;
                case TypeCode.Decimal: writer.Write((decimal)value); break;
                default: throw new InvalidDataException("Unsupported scalar.");
            }
        }
    }

    sealed class Decoder(BinaryReader reader)
    {
        readonly Dictionary<int, object> _references = new();
        long _allocated;
        int _depth;
        string Text(int maximum = 262144)
        {
            int count = reader.Read7BitEncodedInt();
            if (count < 0 || count > maximum || (_allocated += count) > 200_000_000) throw new InvalidDataException("Saved text exceeds allocation limits.");
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new InvalidDataException("Truncated saved text.");
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        int Size(int maximum)
        {
            int value = reader.ReadInt32();
            if (value < 0 || value > maximum || (_allocated += value) > 200_000_000) throw new InvalidDataException("World save exceeds allocation limits.");
            return value;
        }
        public object? Read(Type type)
        {
            if (++_depth > 512) throw new InvalidDataException("Saved object graph is too deep.");
            try { return ReadValue(type); }
            finally { _depth--; }
        }

        object? ReadValue(Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } nullable) return reader.ReadBoolean() ? Read(nullable) : null;
            if (type == typeof(string)) { if (!reader.ReadBoolean()) return null; string text = Text(); if (text.Length > 65536) throw new InvalidDataException("Saved text is too long."); return text; }
            if (type.IsEnum) return Enum.ToObject(type, Read(Enum.GetUnderlyingType(type))!);
            if (type.IsPrimitive || type == typeof(decimal)) return ReadScalar(type);
            int id = 0;
            if (!type.IsValueType)
            {
                id = reader.ReadInt32();
                if (id == 0) return null;
                if (id < 0) return _references.TryGetValue(-id, out var existing) && type.IsInstanceOfType(existing) ? existing : throw new InvalidDataException("Invalid saved reference.");
                if (_references.ContainsKey(id) || _references.Count > 10_000_000) throw new InvalidDataException("Invalid object graph.");
                if (!Types.TryGetValue(Text(8192), out var actual) || !type.IsAssignableFrom(actual)) throw new InvalidDataException("Unknown or incompatible save type.");
                type = actual;
            }
            object value;
            if (type == typeof(System.Diagnostics.Stopwatch)) value = new System.Diagnostics.Stopwatch();
            else if (type.IsArray)
            {
                int count = Size(20_000_000); var element = type.GetElementType()!;
                var array = Array.CreateInstance(element, count); value = array; _references.Add(id, value);
                if (element.IsPrimitive && element != typeof(bool) && element != typeof(char))
                {
                    byte[] bytes = reader.ReadBytes(Buffer.ByteLength(array));
                    if (bytes.Length != Buffer.ByteLength(array)) throw new InvalidDataException("Truncated saved array.");
                    Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
                }
                else for (int i = 0; i < count; i++) array.SetValue(Read(element), i);
                return value;
            }
            else if (Sequence(type))
            {
                value = Activator.CreateInstance(type)!; _references.Add(id, value); int count = Size(1_000_000);
                var args = type.GetGenericArguments();
                if (value is IDictionary dictionary) for (int i = 0; i < count; i++) dictionary.Add(Read(args[0])!, Read(args[1]));
                else { var list = (IList)value; for (int i = 0; i < count; i++) list.Add(Read(args[0])); }
                return value;
            }
            else value = PackedCollection(type) ? Activator.CreateInstance(type)! : RuntimeHelpers.GetUninitializedObject(type);
            if (id > 0) _references.Add(id, value);
            if (type == typeof(System.Diagnostics.Stopwatch)) return value;
            var fields = Members(type);
            int fieldCount = Size(256);
            var seen = new HashSet<string>();
            for (int i = 0; i < fieldCount; i++)
            {
                string name = Text(1024);
                var f = fields.FirstOrDefault(f => f.Name == name);
                if (f == null || !seen.Add(name)) throw new InvalidDataException("Saved fields do not match this build.");
                f.SetValue(value, Read(f.FieldType));
            }
            foreach (var f in fields.Where(f => !seen.Contains(f.Name)))
            {
                if (!f.IsDefined(typeof(SaveOptionalAttribute))) throw new InvalidDataException("This save is missing required world state.");
                object? initial = f.FieldType == typeof(string) ? "" : f.FieldType.IsArray ? Array.CreateInstance(f.FieldType.GetElementType()!, 0) :
                    f.FieldType == typeof(int) ? f.GetCustomAttribute<SaveOptionalAttribute>()!.IntegerDefault :
                    f.FieldType.IsValueType || f.FieldType.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(f.FieldType) : null;
                f.SetValue(value, initial);
            }
            return value;
        }

        object ReadScalar(Type type) => Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => reader.ReadBoolean(), TypeCode.Byte => reader.ReadByte(), TypeCode.SByte => reader.ReadSByte(),
            TypeCode.Int16 => reader.ReadInt16(), TypeCode.UInt16 => reader.ReadUInt16(), TypeCode.Int32 => reader.ReadInt32(), TypeCode.UInt32 => reader.ReadUInt32(),
            TypeCode.Int64 => reader.ReadInt64(), TypeCode.UInt64 => reader.ReadUInt64(), TypeCode.Single => reader.ReadSingle(), TypeCode.Double => reader.ReadDouble(),
            TypeCode.Char => reader.ReadChar(), TypeCode.Decimal => reader.ReadDecimal(), _ => throw new InvalidDataException("Unsupported scalar."),
        };
    }
}
