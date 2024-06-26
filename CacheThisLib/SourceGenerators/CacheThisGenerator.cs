﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;

namespace CacheThisLib.SourceGenerators
{
    [Generator]
    public class CacheThisGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var methodProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                transform: static (ctx, _) => ctx.Node as MethodDeclarationSyntax
            ).Where(m => m != null && HasCacheItAttribute(m));

            var compilationProvider = context.CompilationProvider.Combine(methodProvider.Collect());

            context.RegisterSourceOutput(compilationProvider, (spc, source) => Execute(spc, source.Left, source.Right));
        }

        private static bool HasCacheItAttribute(MethodDeclarationSyntax method)
        {
            return method.AttributeLists.Any(al => al.Attributes.Any(attr => attr.Name.ToString().Contains("CacheThis")));
        }

        private void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods)
        {
            var groupedByClass = methods
                .Select(m => new
                {
                    Method = m,
                    Model = compilation.GetSemanticModel(m.SyntaxTree),
                    Symbol = compilation.GetSemanticModel(m.SyntaxTree).GetDeclaredSymbol(m) as IMethodSymbol
                })
                .Where(x => x.Symbol != null)
                .GroupBy(x => x.Symbol.ContainingType);

            foreach (var group in groupedByClass)
            {

                var className = $"{group.Key.Name}_Cached";
                var cachedMethods = string.Join("\n", group.Select(x =>
                {
                    if (x.Symbol.IsVirtual || x.Symbol.IsAbstract || x.Symbol.IsOverride)
                    {
                        return GenerateMethodInMemoryCachingCode(x.Symbol);
                    }
                    else
                    {
                        return GenerateNonOverridingMethodCode(x.Symbol);
                    }
                }));

                string hashFunctionsCode = @"/// <summary>
/// Generates a secure cache key based on the method and its arguments using SHA-256, optimized for performance and memory usage.
/// </summary>
/// <param name=""method"">The method for which the cache key is being generated.</param>
/// <param name=""args"">The arguments to the method, if any.</param>
/// <returns>A string representing the unique and secure cache key.</returns>
private string GenerateCacheKey(string methodName, object?[]? args)
{
    using var sha256 = SHA256.Create();
    using var stream = new MemoryStream();

    // Use StreamWriter for efficient byte conversions with buffered writing
    using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
    {
        writer.Write(methodName);

        foreach (object? arg in args ?? Array.Empty<object?>())
        {
            writer.Write(';');
            writer.Write(arg?.ToString() ?? string.Empty);
        }
    }

    stream.Position = 0; // Reset position to read the stream from the beginning
    var hashBytes = sha256.ComputeHash(stream);

    // Convert byte array to a hexadecimal string efficiently
    return ByteArrayToHexViaStringBuilder(hashBytes);
}

    /// <summary>
    /// Generates a secure cache key based on the method and its arguments using a specified hash algorithm,
    /// optimized for performance and memory usage.
    /// </summary>
    /// <param name=""methodName"">The method for which the cache key is being generated.</param>
    /// <param name=""args"">The arguments to the method, if any.</param>
    /// <param name=""hashAlgorithmName"">The name of the hash algorithm to use (e.g., ""SHA256"", ""SHA1"", ""SHA384"", ""SHA512"", ""SHA3-256"").</param>
    /// <returns>A string representing the unique and secure cache key.</returns>
    public string GenerateCacheKey(string methodName, object?[]? args, string hashAlgorithmName=""SHA256"")
    {
        using var hashAlgorithm = SelectHashAlgorithm(hashAlgorithmName);
        if (hashAlgorithm == null)
            throw new ArgumentException(""Invalid or unsupported hash algorithm."");

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
        {
            writer.Write(methodName);
            foreach (object? arg in args ?? Array.Empty<object?>())
            {
                writer.Write(';');
                writer.Write(arg?.ToString() ?? string.Empty);
            }
        }

        stream.Position = 0; // Reset position to read the stream from the beginning
        var hashBytes = hashAlgorithm.ComputeHash(stream);

        // Convert byte array to a hexadecimal string efficiently
        return ByteArrayToHexViaStringBuilder(hashBytes);
    }

    /// <summary>
    /// Selects a hash algorithm based on the provided name.
    /// </summary>
    /// <param name=""name"">The name of the hash algorithm.</param>
    /// <returns>A new instance of the specified hash algorithm or SHA256 if the name is unrecognized.</returns>
    private HashAlgorithm? SelectHashAlgorithm(string name)
    {
        return name switch
        {
            ""SHA256"" => SHA256.Create(),
            ""SHA1"" => SHA1.Create(),
            ""SHA384"" => SHA384.Create(),
            ""SHA512"" => SHA512.Create(),
            ""MD5"" => MD5.Create(),
            _ => SHA256.Create()
        };
    }

    /// <summary>
    /// Converts a byte array to a hexadecimal string using a StringBuilder for efficiency.
    /// </summary>
    /// <param name=""bytes"">Byte array to convert.</param>
    /// <returns>Hexadecimal string representation of the byte array.</returns>
    private string ByteArrayToHexViaStringBuilder(byte[] bytes)
    {
        var stringBuilder = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            stringBuilder.Append(b.ToString(""x2""));
        }
        return stringBuilder.ToString();
    }";

                var fullClass = $@"
                    using System;
                    using System.Collections.Concurrent;
                    using System.Reflection;
                    using System.Linq;
                    using Microsoft.Extensions.Caching.Memory;
                    using System.Security.Cryptography;
                    using System.Text;

                    namespace {group.Key.ToString().Split('.').FirstOrDefault()};

                    public class {className} : {group.Key}
                    {{
                        private readonly {group.Key} _instance;
                        
                        private static readonly IMemoryCache InMemoryCacheStore = new MemoryCache(new MemoryCacheOptions());

                        public {className}({group.Key} instance) : base()
                        {{
                            _instance = instance;
                        }}

                        {hashFunctionsCode}

                        {cachedMethods}
                    }}
                ";

                context.AddSource($"{className}.g.cs", fullClass);
            }
        }


        private string GenerateMethodInMemoryCachingCode(IMethodSymbol methodSymbol)
        {
            var returnType = methodSymbol.ReturnType.ToString();
            var parameters = string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type} {p.Name}"));
            var parameterNames = string.Join(", ", methodSymbol.Parameters.Select(p => p.Name));

            string attributeName = "CacheThisLib.CacheThisAttribute"; // Directly using the fully qualified name as a string
                                                                   // Retrieve CacheItAttribute applied on the method, if any
            var cacheAttribute = methodSymbol.GetAttributes()
                                             .FirstOrDefault(a => a.AttributeClass.ToString().Contains(attributeName));

            // Prepare MemoryCacheEntryOptions code snippet based on the attribute
            var optionsCode = "new MemoryCacheEntryOptions()";

            if (cacheAttribute != null)
            {
                var optionsList = ExtractCacheEntryOptions(cacheAttribute);
                if (optionsList.Count > 0)
                    optionsCode = $"new MemoryCacheEntryOptions {{ {string.Join(", ", optionsList)} }}";
            }

            return $@"
//{optionsCode}
public override {returnType} {methodSymbol.Name}({parameters})
{{
    var key = GenerateCacheKey(""{methodSymbol.Name}"", new object[] {{ {parameterNames} }});

    if (!InMemoryCacheStore.TryGetValue(key, out object cachedResult))
    {{
        cachedResult = _instance.{methodSymbol.Name}({parameterNames});
        InMemoryCacheStore.Set(key, cachedResult, {optionsCode});
    }}

    return ({returnType})cachedResult;
}}
";
        }

        private List<string> ExtractCacheEntryOptions(AttributeData cacheAttribute)
        {
            var optionsList = new List<string>();

            foreach (var arg in cacheAttribute.NamedArguments)
            {
                if (arg.Key == "AbsoluteExpirationRelativeToNow" && TryGetDoubleFromTypedConstant(arg.Value, out double absoluteSeconds) && absoluteSeconds >= 0)
                {
                    optionsList.Add($"AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds({absoluteSeconds})");
                }
                if (arg.Key == "SlidingExpirationInSeconds" && TryGetDoubleFromTypedConstant(arg.Value, out double slidingSeconds) && slidingSeconds >= 0)
                {
                    optionsList.Add($"SlidingExpiration = TimeSpan.FromSeconds({slidingSeconds})");
                }
            }

            return optionsList;
        }

        private bool TryGetDoubleFromTypedConstant(TypedConstant typedConstant, out double result)
        {
            result = 0;
            if (typedConstant.Value is double doubleValue)
            {
                result = doubleValue;
                return true;
            }
            else if (typedConstant.Value is int intValue)
            {
                result = intValue;
                return true;
            }
            else if (double.TryParse(typedConstant.Value?.ToString(), out double parsedValue))
            {
                result = parsedValue;
                return true;
            }
            return false;
        }
        private bool TryGetPriorityFromTypedConstant(TypedConstant typedConstant, out CacheItemPriority priority)
        {
            priority = CacheItemPriority.Normal;  // Default value

            if (typedConstant.Value is CacheItemPriority priorityValue)
            {
                priority = priorityValue;
                return true;
            }

            if (typedConstant.Value is int intValue && Enum.IsDefined(typeof(CacheItemPriority), intValue))
            {
                priority = (CacheItemPriority)intValue;
                return true;
            }

            if (typedConstant.Value is string stringValue && Enum.TryParse(stringValue, true, out CacheItemPriority parsedPriority))
            {
                priority = parsedPriority;
                return true;
            }

            return false;
        }
        private string GenerateNonOverridingMethodCode(IMethodSymbol methodSymbol)
        {
            var returnType = methodSymbol.ReturnType.ToString();
            var parameters = string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type} {p.Name}"));
            var parameterNames = string.Join(", ", methodSymbol.Parameters.Select(p => p.Name));

            return $@"
        public {returnType} {methodSymbol.Name}({parameters})
        {{
            return _instance.{methodSymbol.Name}({parameterNames});
        }}
    ";
        }
    }
}
