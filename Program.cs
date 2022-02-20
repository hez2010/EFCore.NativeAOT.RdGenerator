using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
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

var (types, methods) = GenerateTypesAndMethods(args[1..].SelectMany(ctx => assembly.GetType(ctx) is Type t ? new[] { t } : Array.Empty<Type>()).SelectMany(t => ProcessContextType(t)).ToList());
await WriteRdXmlFileAsync(types, methods);
Console.WriteLine("Done");

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

(HashSet<Type> Types, HashSet<MethodInfo> Methods) GenerateTypesAndMethods(List<Entity> entities)
{
    var types = new HashSet<Type>();
    var methods = new HashSet<MethodInfo>();

    Console.WriteLine("Generating necessary runtime directives for primitive types...");
    foreach (var m in typeof(DateTime).GetRuntimeMethods())
    {
        if (m.Name.StartsWith("Add")) methods.Add(m);
    }

    foreach (var m in typeof(DateTimeOffset).GetRuntimeMethods())
    {
        if (m.Name.StartsWith("Add")) methods.Add(m);
    }

    foreach (var m in typeof(DateOnly).GetRuntimeMethods())
    {
        if (m.Name.StartsWith("Add")) methods.Add(m);
    }

    foreach (var m in typeof(TimeOnly).GetRuntimeMethods())
    {
        if (m.Name.StartsWith("Add")) methods.Add(m);
    }

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

    return (types, methods);
}

async Task WriteRdXmlFileAsync(HashSet<Type> types, HashSet<MethodInfo> methods)
{
    Console.WriteLine("Emitting runtime directive entries...");
    var filter = new Regex(@", Version=.*?, Culture=.*?, PublicKeyToken=[a-z0-9]+", RegexOptions.Compiled);
    var rdEntries = new Dictionary</* Assembly */ string, Dictionary</* Type */ string, List<(string Name, List<string> GenericArguments, List<string> Parameters)>>>();

    foreach (var type in types)
    {
        var assemblyName = filter.Replace(type.Assembly.FullName!, "");
        if (!rdEntries.ContainsKey(assemblyName)) rdEntries[assemblyName] = new();
        var typeName = filter.Replace(type.FullName!, "");
        if (!rdEntries[assemblyName].ContainsKey(typeName)) rdEntries[assemblyName][typeName] = new();
    }

    foreach (var method in methods)
    {
        var type = method.DeclaringType!;
        var assemblyName = filter.Replace(type.Assembly!.FullName!, "");
        if (!rdEntries.ContainsKey(assemblyName)) rdEntries[assemblyName] = new();
        var typeName = filter.Replace(type.FullName!, "");
        if (!rdEntries[assemblyName].ContainsKey(typeName)) rdEntries[assemblyName][typeName] = new();

        (string Name, List<string> GenericArguments, List<string> Parameters) mSig = new(method.Name, new(), new());
        if (method.IsGenericMethod)
        {
            foreach (var genericArgument in method.GetGenericArguments())
            {
                mSig.GenericArguments.Add(filter.Replace(genericArgument.AssemblyQualifiedName!, ""));
            }
        }
        foreach (var parameter in method.GetParameters())
        {
            mSig.Parameters.Add(filter.Replace(parameter.ParameterType.AssemblyQualifiedName!, ""));
        }
        rdEntries[assemblyName][typeName].Add(mSig);
    }

    Console.WriteLine("Writting rd.efcore.gen.xml...");
    await using var fs = new FileStream("rd.efcore.gen.xml", FileMode.Create);
    await using var sw = new StreamWriter(fs);
    await sw.WriteLineAsync("<Directives>");
    await sw.WriteLineAsync("  <Application>");
    foreach (var assembly in rdEntries)
    {
        await sw.WriteLineAsync($"    <Assembly Name=\"{assembly.Key}\">");
        foreach (var type in assembly.Value)
        {
            await sw.WriteLineAsync($"      <Type Name=\"{type.Key}\">");
            foreach (var method in type.Value)
            {
                await sw.WriteLineAsync($"        <Method Name=\"{method.Name}\">");
                foreach (var genericArgument in method.GenericArguments)
                {
                    await sw.WriteLineAsync($"          <GenericArgument Name=\"{genericArgument}\" />");
                }
                foreach (var parameter in method.Parameters)
                {
                    await sw.WriteLineAsync($"          <Parameter Name=\"{parameter}\" />");
                }
                await sw.WriteLineAsync($"        </Method>");
            }
            await sw.WriteLineAsync($"      </Type>");
        }
        await sw.WriteLineAsync($"    </Assembly>");
    }
    await sw.WriteLineAsync("  </Application>");
    await sw.WriteLineAsync("</Directives>");
}

record Entity(Assembly Assembly, string Name, Type Type, IEnumerable<Property> Properties);
record Property(Assembly Assembly, string Name, Type Type, bool IsKey);
