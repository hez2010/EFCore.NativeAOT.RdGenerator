using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

if (args.Length < 2)
{
    var versionString = Assembly.GetEntryAssembly()?
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                            .InformationalVersion
                            .ToString();

    Console.WriteLine($"ef-rdgen v{versionString}");
    Console.WriteLine("-------------");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet ef-rdgen [assembly] [dbcontext...]");
    return;
}

var assembly = Assembly.LoadFile(args[0]);
var types = new List<Type>();
var filter = new Regex(@", Version=.*?, Culture=.*?, PublicKeyToken=[a-z0-9]+", RegexOptions.Compiled);
foreach (var ctx in args[1..])
{
    if (assembly.GetType(ctx) is Type t)
        types.Add(t);
}

await WriteRdXmlFileAsync(types.SelectMany(t => ProcessContextType(t)).ToList());

IEnumerable<Entity> ProcessContextType(Type type)
{
    foreach (var prop in type.GetProperties())
    {
        if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        {
            var entityType = prop.PropertyType.GenericTypeArguments[0];
            yield return new(prop.PropertyType.Assembly, prop.Name, entityType, ProcessEntityType(entityType));
        }
    }
}

IEnumerable<Property> ProcessEntityType(Type type)
{
    var properties = type.GetProperties();
    foreach (var prop in properties)
    {
        yield return new(prop.PropertyType.Assembly, prop.Name, prop.PropertyType, prop.Name.ToLowerInvariant() is "id" || prop.CustomAttributes.Any(ca => ca.AttributeType == typeof(KeyAttribute)));
    }
}

Type GetSnapshotType(Type[] types)
{
    return (types.Length switch
    {
        1 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<>),
        2 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,>),
        3 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,>),
        4 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,>),
        5 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,>),
        6 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,>),
        7 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,>),
        8 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,>),
        9 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,>),
        10 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,>),
        11 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,>),
        12 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,>),
        13 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,>),
        14 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,>),
        15 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,>),
        16 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,>),
        17 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,>),
        18 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,>),
        19 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,>),
        20 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,>),
        21 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,>),
        22 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,>),
        23 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,>),
        24 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,>),
        25 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,,>),
        26 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,,,>),
        27 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,,,,>),
        28 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,,,,,>),
        29 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,,,,,,>),
        30 => typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>),
        _ => throw new ArgumentOutOfRangeException(nameof(types))
    }).MakeGenericType(types);
}

async Task WriteRdXmlFileAsync(List<Entity> entities)
{
    var types = new HashSet<Type>();
    var methods = new HashSet<MethodInfo>();

    Console.WriteLine("Generating necessary runtime directives for snapshots...");
    foreach (var e in entities)
    {
        types.Add(GetSnapshotType(e.Properties.Select(i => i.Type.IsValueType ? i.Type : typeof(object)).ToArray()));
    }

    Console.WriteLine("Generating necessary runtime directives for keys...");
    var idMapFacFacCreateFacMethod = typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.IdentityMapFactoryFactory).GetMethod("CreateFactory", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new NotSupportedException();
    var efPropMethod = typeof(Microsoft.EntityFrameworkCore.EF).GetMethod("Property") ?? throw new NotSupportedException();
    foreach (var e in entities.SelectMany(i => i.Properties).Where(i => i.IsKey).Select(i => i.Type.IsValueType ? i.Type : typeof(object)).Distinct())
    {
        methods.Add(efPropMethod.MakeGenericMethod(new[] { e }));
        methods.Add(idMapFacFacCreateFacMethod.MakeGenericMethod(e));
    }

    Console.WriteLine("Generating necessary runtime directives for value comparers...");
    var valueComparerType = typeof(Microsoft.EntityFrameworkCore.ChangeTracking.EntryCurrentValueComparer<>);
    var valueDefaultComparerType = typeof(Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer).GetNestedType("DefaultValueComparer`1", BindingFlags.Default | BindingFlags.NonPublic) ?? throw new NotSupportedException();
    foreach (var e in entities.SelectMany(i => i.Properties).Where(i => !i.IsKey).Select(i => i.Type.IsValueType ? i.Type : typeof(object)).Distinct())
    {
        types.Add(valueComparerType.MakeGenericType(e));
        types.Add(valueDefaultComparerType.MakeGenericType(e));
    }

    var clrPropGetterFacType = typeof(Microsoft.EntityFrameworkCore.Metadata.Internal.ClrPropertyGetterFactory);
    var clrPropSetterFacType = typeof(Microsoft.EntityFrameworkCore.Metadata.Internal.ClrPropertySetterFactory);
    var clrAccFacType = typeof(Microsoft.EntityFrameworkCore.Metadata.Internal.ClrAccessorFactory<>);
    var clrPropGetterType = typeof(Microsoft.EntityFrameworkCore.Metadata.Internal.ClrPropertyGetter<,>);
    var clrPropGetterCreateGenericMethod = clrPropGetterFacType.GetMethod("CreateGeneric", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new NotSupportedException();
    foreach (var e in entities)
    {
        foreach (var p in e.Properties)
        {
            methods.Add(clrAccFacType.MakeGenericType(clrPropGetterType.MakeGenericType(e.Type, p.Type))
                .GetMethod("CreateGeneric", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(new[] { e.Type, p.Type, p.Type }) ?? throw new NotSupportedException());
            methods.Add(clrPropGetterCreateGenericMethod.MakeGenericMethod(new[] { e.Type, p.Type, p.Type }));
        }
    }

    // TODO
    // await using var fs = new FileStream("rd.efcore.gen.xml", FileMode.Create);
    // fs.Seek(0, SeekOrigin.Begin);

    foreach (var i in types)
    {
        Console.WriteLine(i);
    }

    foreach (var i in methods)
    {
        Console.WriteLine(i);
    }
}

record Entity(Assembly Assembly, string Name, Type Type, IEnumerable<Property> Properties);
record Property(Assembly Assembly, string Name, Type Type, bool IsKey);
